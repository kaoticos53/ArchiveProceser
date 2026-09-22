using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Storage;
using SixLabors.ImageSharp;
using Tesseract;

namespace FileFlow.Plugin.AI;

[NodeDefinition("LocalOcrNode_Name", "Documents", "LocalOcrNode_Desc", PipelineRole.Analyze,
    "ocr", "texto", "imagen a texto", "escaner", "paddle", "leer", "text", "reconocimiento")]
public sealed class LocalOcrNode : FlowNodeBase
{
    public override string Name => LocalizationManager.Instance.GetString("LocalOcrNode_Name", "Reconocimiento Óptico (OCR Local)");
    public override string Category => "Documents";
    public override string Description => LocalizationManager.Instance.GetString("LocalOcrNode_Desc", "Extrae texto desde imágenes y documentos escaneados usando Tesseract OCR 5 de forma local y privada.");

    public LocalOcrNode()
    {
        Inputs =
        [
            new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
        ];

        Outputs =
        [
            new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
            new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
        ];

        Parameters["Language"] = "Auto";
        Parameters["EngineMode"] = "Neural";
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("Language", ParameterEditorType.Dropdown, DefaultValue: "Auto", Options: ["Auto", "spa", "eng", "fra", "deu", "ita"], DisplayOrder: 1),
        new("EngineMode", ParameterEditorType.Dropdown, DefaultValue: "Neural", Options: ["Neural", "Legacy", "Both"], DisplayOrder: 2)
    ];

    private static readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif", ".webp"
    };

    public override async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        var storage = context.GetStorage();
        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !await storage.FileExistsAsync(item.CurrentPath, cancellationToken).ConfigureAwait(false))
        {
            Log(context, $"[LocalOcr] Archivo no encontrado: '{item.CurrentPath}'", LogLevel.Error, item);
            await EmitAsync(context, item, "Error").ConfigureAwait(false);
            return;
        }

        string ext = Path.GetExtension(item.CurrentPath).ToLowerInvariant();
        if (!_supportedExtensions.Contains(ext))
        {
            Log(context, $"[LocalOcr] Formato no compatible para OCR ({ext}): {item.FileName}", LogLevel.Warning, item);
            await EmitAsync(context, item).ConfigureAwait(false);
            return;
        }

        try
        {
            string languageParam = GetParameter("Language", "Auto");
            string ocrLang = languageParam.Equals("Auto", StringComparison.OrdinalIgnoreCase) ? "spa" : languageParam;
            string engineModeParam = GetParameter("EngineMode", "Neural");
            EngineMode mode = engineModeParam switch
            {
                "Legacy" => EngineMode.TesseractOnly,
                "Both" => EngineMode.TesseractAndLstm,
                _ => EngineMode.LstmOnly
            };

            Log(context, $"[LocalOcr] Analizando texto en {item.FileName} (idioma: {ocrLang})...", LogLevel.Information, item);

            // Descargar tessdata para el idioma seleccionado
            string tessdataModelId = $"tessdata-{ocrLang}";
            string? tessdataPath = await AiModelManager.EnsureModelAsync(tessdataModelId, context, item, cancellationToken).ConfigureAwait(false);

            if (tessdataPath == null)
            {
                // Intentar con inglés como fallback
                Log(context, $"[LocalOcr] ⚠️ Tessdata para '{ocrLang}' no disponible. Intentando inglés como fallback...", LogLevel.Warning, item);
                tessdataPath = await AiModelManager.EnsureModelAsync("tessdata-eng", context, item, cancellationToken).ConfigureAwait(false);
                ocrLang = "eng";
            }

            if (tessdataPath == null)
            {
                Log(context, "[LocalOcr] ⚠️ Tessdata OCR no disponible. El nodo pasa el archivo sin OCR.", LogLevel.Warning, item);
                await EmitAsync(context, item).ConfigureAwait(false);
                return;
            }

            // Directorio padre de tessdata (ej: %AppData%/FileFlow/Models/tessdata → %AppData%/FileFlow/Models)
            string tessdataDir = Path.GetDirectoryName(Path.GetDirectoryName(tessdataPath)) ?? AiModelManager.ModelsDirectory;

            byte[] imageBytes;
            await using (var inStream = await storage.OpenReadAsync(item.CurrentPath, cancellationToken).ConfigureAwait(false))
            using (var ms = new MemoryStream())
            {
                await inStream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
                imageBytes = ms.ToArray();
            }

            string fullText = await Task.Run(() =>
            {
                using var engine = new TesseractEngine(Path.Combine(tessdataDir, "tessdata"), ocrLang, mode);
                Pix? pix = null;
                try
                {
                    if (ext is ".webp" or ".tga" or ".pbm")
                    {
                        using var img = Image.Load(imageBytes);
                        using var pngMs = new MemoryStream();
                        img.SaveAsPng(pngMs);
                        pix = Pix.LoadFromMemory(pngMs.ToArray());
                    }
                    else
                    {
                        try
                        {
                            pix = Pix.LoadFromMemory(imageBytes);
                        }
                        catch
                        {
                            using var img = Image.Load(imageBytes);
                            using var pngMs = new MemoryStream();
                            img.SaveAsPng(pngMs);
                            pix = Pix.LoadFromMemory(pngMs.ToArray());
                        }
                    }

                    using var page = engine.Process(pix);
                    return page.GetText();
                }
                finally
                {
                    pix?.Dispose();
                }
            }, cancellationToken).ConfigureAwait(false);

            fullText = fullText.Trim();
            int wordCount = fullText.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;
            int lineCount = fullText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Length;

            item.Metadata["Ocr:Text"] = fullText;
            item.Metadata["Ocr:WordCount"] = wordCount;
            item.Metadata["Ocr:LineCount"] = lineCount;
            item.Metadata["Ocr:Language"] = ocrLang;
            item.Metadata["Ocr:Engine"] = "Tesseract-5";

            Log(context, $"[LocalOcr] ✅ OCR completado: {wordCount} palabras, {lineCount} líneas ({ocrLang}).", LogLevel.Information, item);

            await EmitAsync(context, item).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log(context, $"[LocalOcr] Error ejecutando OCR en {item.FileName}: {ex.Message}", LogLevel.Error, item);
            await EmitAsync(context, item, "Error").ConfigureAwait(false);
        }
    }
}
