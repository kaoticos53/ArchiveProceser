using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using Tesseract;

namespace FileFlow.Plugin.AI;

[NodeDefinition("LocalOcrNode_Name", "Documents", "LocalOcrNode_Desc", PipelineRole.Analyze,
    "ocr", "texto", "imagen a texto", "escaner", "paddle", "leer", "text", "reconocimiento")]
public class LocalOcrNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("LocalOcrNode_Name", "Reconocimiento Óptico (OCR Local)");
    public string Category => "Documents";
    public string Description => LocalizationManager.Instance.GetString("LocalOcrNode_Desc", "Extrae texto desde imágenes y documentos escaneados usando Tesseract OCR 5 de forma local y privada.");

    public IReadOnlyList<NodePort> Inputs { get; } =
    [
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    ];

    public IReadOnlyList<NodePort> Outputs { get; } =
    [
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
        new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
    ];

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Language"] = "Auto",
        ["EngineMode"] = "Neural"
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("Language", ParameterEditorType.Dropdown, DefaultValue: "Auto", Options: ["Auto", "spa", "eng", "fra", "deu", "ita"], DisplayOrder: 1),
        new("EngineMode", ParameterEditorType.Dropdown, DefaultValue: "Neural", Options: ["Neural", "Legacy", "Both"], DisplayOrder: 2)
    ];

    private static readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif", ".webp"
    };

    public async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !File.Exists(item.CurrentPath))
        {
            context.Log($"[LocalOcr] Archivo no encontrado: '{item.CurrentPath}'", LogLevel.Error, item);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
            return;
        }

        string ext = Path.GetExtension(item.CurrentPath).ToLowerInvariant();
        if (!_supportedExtensions.Contains(ext))
        {
            context.Log($"[LocalOcr] Formato no compatible para OCR ({ext}): {item.FileName}", LogLevel.Warning, item);
            await context.EmitAsync("Out", item).ConfigureAwait(false);
            return;
        }

        try
        {
            string languageParam = Parameters.TryGetValue("Language", out var lp) ? lp?.ToString() ?? "Auto" : "Auto";
            string ocrLang = languageParam.Equals("Auto", StringComparison.OrdinalIgnoreCase) ? "spa" : languageParam;
            string engineModeParam = Parameters.TryGetValue("EngineMode", out var em) ? em?.ToString() ?? "Neural" : "Neural";
            EngineMode mode = engineModeParam switch
            {
                "Legacy" => EngineMode.TesseractOnly,
                "Both" => EngineMode.TesseractAndLstm,
                _ => EngineMode.LstmOnly
            };

            context.Log($"[LocalOcr] Analizando texto en {item.FileName} (idioma: {ocrLang})...", LogLevel.Information, item);

            // Descargar tessdata para el idioma seleccionado
            string tessdataModelId = $"tessdata-{ocrLang}";
            string? tessdataPath = await AiModelManager.EnsureModelAsync(tessdataModelId, context, item, cancellationToken).ConfigureAwait(false);

            if (tessdataPath == null)
            {
                // Intentar con inglés como fallback
                context.Log($"[LocalOcr] ⚠️ Tessdata para '{ocrLang}' no disponible. Intentando inglés como fallback...", LogLevel.Warning, item);
                tessdataPath = await AiModelManager.EnsureModelAsync("tessdata-eng", context, item, cancellationToken).ConfigureAwait(false);
                ocrLang = "eng";
            }

            if (tessdataPath == null)
            {
                context.Log($"[LocalOcr] ⚠️ Tessdata OCR no disponible. El nodo pasa el archivo sin OCR.", LogLevel.Warning, item);
                await context.EmitAsync("Out", item).ConfigureAwait(false);
                return;
            }

            // Directorio padre de tessdata (ej: %AppData%/FileFlow/Models/tessdata → %AppData%/FileFlow/Models)
            string tessdataDir = Path.GetDirectoryName(Path.GetDirectoryName(tessdataPath)) ?? AiModelManager.ModelsDirectory;

            string fullText = await Task.Run(() =>
            {
                using var engine = new TesseractEngine(Path.Combine(tessdataDir, "tessdata"), ocrLang, mode);
                using var pix = Pix.LoadFromFile(item.CurrentPath);
                using var page = engine.Process(pix);
                return page.GetText();
            }, cancellationToken).ConfigureAwait(false);

            fullText = fullText.Trim();
            int wordCount = fullText.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;
            int lineCount = fullText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Length;

            item.Metadata["Ocr:Text"] = fullText;
            item.Metadata["Ocr:WordCount"] = wordCount;
            item.Metadata["Ocr:LineCount"] = lineCount;
            item.Metadata["Ocr:Language"] = ocrLang;
            item.Metadata["Ocr:Engine"] = "Tesseract-5";

            context.Log($"[LocalOcr] ✅ OCR completado: {wordCount} palabras, {lineCount} líneas ({ocrLang}).", LogLevel.Information, item);

            await context.EmitAsync("Out", item).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            context.Log($"[LocalOcr] Error ejecutando OCR en {item.FileName}: {ex.Message}", LogLevel.Error, item);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
        }
    }
}
