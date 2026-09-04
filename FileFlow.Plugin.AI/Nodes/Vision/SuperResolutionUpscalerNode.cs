using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Plugin.AI.Inference;
using FileFlow.Sdk;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Nodo de super-resolución y restauración neural de imágenes y documentos antiguos con Real-ESRGAN Compact.
/// Escala y reconstruye detalles visuales a 2x o 4x previniendo pixelado y artefactos de compresión.
/// </summary>
[NodeDefinition("SuperResolutionUpscalerNode_Name", "ImageVision", "SuperResolutionUpscalerNode_Desc", PipelineRole.Transform,
    "super resolucion", "escalar", "aumentar", "upscale", "4x", "realesrgan", "calidad", "nitidez")]
public class SuperResolutionUpscalerNode : IFlowNode, IModelLifecycleNode
{
    public event Action? ModelStatusChanged;

    public SuperResolutionUpscalerNode()
    {
        OnnxSessionManager.SessionStateChanged += () => ModelStatusChanged?.Invoke();
    }

    public bool IsModelLoaded
    {
        get
        {
            string modelChoice = Parameters.TryGetValue("Model", out var mVal) ? mVal?.ToString() ?? "Auto" : "Auto";
            string? modelPath = AiModelManager.ResolveModelPathSync(modelChoice, AiTaskType.SuperResolution);
            return modelPath != null && OnnxSessionManager.IsSessionLoaded(modelPath);
        }
    }

    public string? ModelIdentifier
    {
        get
        {
            string modelChoice = Parameters.TryGetValue("Model", out var mVal) ? mVal?.ToString() ?? "Auto" : "Auto";
            return AiModelManager.GetModelDisplayName(modelChoice, AiTaskType.SuperResolution);
        }
    }

    public bool IsGpuAccelerated
    {
        get
        {
            string modelChoice = Parameters.TryGetValue("Model", out var mVal) ? mVal?.ToString() ?? "Auto" : "Auto";
            string? modelPath = AiModelManager.ResolveModelPathSync(modelChoice, AiTaskType.SuperResolution);
            return modelPath != null && OnnxSessionManager.ShouldUseDirectMl(modelPath);
        }
    }

    public async Task PreloadModelAsync(CancellationToken cancellationToken = default)
    {
        string modelChoice = Parameters.TryGetValue("Model", out var mVal) ? mVal?.ToString() ?? "Auto" : "Auto";
        string? modelPath = await AiModelManager.ResolveModelPathAsync(modelChoice, AiTaskType.SuperResolution, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(modelPath) && File.Exists(modelPath))
        {
            OnnxSessionManager.GetOrCreateSession(modelPath);
        }
        ModelStatusChanged?.Invoke();
    }

    public void UnloadModel()
    {
        string modelChoice = Parameters.TryGetValue("Model", out var mVal) ? mVal?.ToString() ?? "Auto" : "Auto";
        string? modelPath = AiModelManager.ResolveModelPathSync(modelChoice, AiTaskType.SuperResolution);
        if (!string.IsNullOrWhiteSpace(modelPath))
        {
            OnnxSessionManager.UnloadSession(modelPath);
        }
        ModelStatusChanged?.Invoke();
    }

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("SuperResolutionUpscalerNode_Name", "Super-Resolución IA");
    public string Description => LocalizationManager.Instance.GetString("SuperResolutionUpscalerNode_Desc", "Escala y restaura imágenes o documentos de baja resolución con modelos Real-ESRGAN.");
    public string Category => "ImageVision";

    public IReadOnlyList<NodePort> Inputs { get; } =
    [
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    ];

    public IReadOnlyList<NodePort> Outputs { get; } =
    [
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
        new NodePort("Skipped", typeof(FileItemContext), PortDirection.Output, "Skipped"),
        new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
    ];

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Model"] = "Auto",
        ["ScaleFactor"] = "4x",
        ["MaxInputDimension"] = 2048,
        ["OutputDirectory"] = "{GlobalOutputDir}",
        ["SkipIfExists"] = false
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("Model", ParameterEditorType.Dropdown, DefaultValue: "Auto",
            Options: ["Auto", "realesrgan-compact"],
            HelpText: "Modelo neural de super-resolución ('Auto' selecciona según hardware).", DisplayOrder: 1),
        new("ScaleFactor", ParameterEditorType.Dropdown, DefaultValue: "4x",
            Options: ["2x", "4x"],
            HelpText: "Factor de aumento de resolución.", DisplayOrder: 2),
        new("MaxInputDimension", ParameterEditorType.Number, DefaultValue: 2048, Min: 256, Max: 8192,
            HelpText: "Límite máximo de ancho/alto original para prevenir consumo excesivo de RAM.", DisplayOrder: 3),
        new("OutputDirectory", ParameterEditorType.FolderPath, DefaultValue: "{GlobalOutputDir}",
            HelpText: "Carpeta de destino donde se guardarán las imágenes escaladas.", DisplayOrder: 4),
        new("SkipIfExists", ParameterEditorType.Toggle, DefaultValue: false,
            HelpText: "Si el archivo resultante ya existe en destino, omite la inferencia neural y reutiliza el archivo.", DisplayOrder: 5)
    ];

    private static readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".tiff"
    };

    public async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !File.Exists(item.CurrentPath))
        {
            context.Log($"[SuperResolution] Archivo no encontrado: '{item.CurrentPath}'", LogLevel.Error, item);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
            return;
        }

        string ext = Path.GetExtension(item.CurrentPath).ToLowerInvariant();
        if (!_supportedExtensions.Contains(ext))
        {
            context.Log($"[SuperResolution] Formato no compatible ({ext}): {item.FileName}", LogLevel.Warning, item);
            await context.EmitAsync("Skipped", item).ConfigureAwait(false);
            return;
        }

        try
        {
            string modelChoice = Parameters.TryGetValue("Model", out var mVal) ? mVal?.ToString() ?? "Auto" : "Auto";
            string scaleStr = Parameters.TryGetValue("ScaleFactor", out var sfVal) ? sfVal?.ToString() ?? "4x" : "4x";
            int maxDim = Parameters.TryGetValue("MaxInputDimension", out var mdVal) ? ParameterHelper.GetInt32(mdVal, 2048) : 2048;
            string outputDirRaw = Parameters.TryGetValue("OutputDirectory", out var odVal) ? odVal?.ToString() ?? "{GlobalOutputDir}" : "{GlobalOutputDir}";
            bool skipIfExists = Parameters.TryGetValue("SkipIfExists", out var skVal) && (skVal is true || string.Equals(skVal?.ToString(), "True", StringComparison.OrdinalIgnoreCase));

            string targetDir;
            if (string.IsNullOrWhiteSpace(outputDirRaw) || string.Equals(outputDirRaw, "{GlobalOutputDir}", StringComparison.OrdinalIgnoreCase))
            {
                if (item.Metadata.TryGetValue("GlobalOutputDir", out var godVal) && !string.IsNullOrWhiteSpace(godVal?.ToString()))
                {
                    targetDir = godVal.ToString()!;
                }
                else
                {
                    targetDir = Path.GetDirectoryName(item.CurrentPath) ?? Directory.GetCurrentDirectory();
                }
            }
            else
            {
                targetDir = ParameterHelper.ResolveOutputPath(outputDirRaw, item);
            }

            Directory.CreateDirectory(targetDir);

            string targetFileName = $"{Path.GetFileNameWithoutExtension(item.CurrentPath)}_upscaled{ext}";
            string targetPath = Path.Combine(targetDir, targetFileName);

            if (skipIfExists && File.Exists(targetPath))
            {
                context.Log($"[SuperResolution] ⏭️ El archivo de salida ya existe ('{targetFileName}'). Omitiendo inferencia.", LogLevel.Information, item);
                var existingItem = item.DeepClone();
                existingItem.CurrentPath = targetPath;
                existingItem.PhysicalPath = targetPath;
                existingItem.FileSizeBytes = new FileInfo(targetPath).Length;
                existingItem.Metadata["AI:SuperResolution"] = true;
                await context.EmitAsync("Out", existingItem).ConfigureAwait(false);
                return;
            }

            int requestedScale = scaleStr.Contains("2") ? 2 : 4;

            using var image = await Image.LoadAsync<Rgb24>(item.CurrentPath, cancellationToken).ConfigureAwait(false);
            int origW = image.Width;
            int origH = image.Height;

            // Verificar si excede el tamaño máximo
            if (origW > maxDim || origH > maxDim)
            {
                context.Log($"[SuperResolution] ⏭️ Imagen omitida ({origW}x{origH} > límite {maxDim}px) para evitar saturación de memoria.", LogLevel.Information, item);
                await context.EmitAsync("Skipped", item).ConfigureAwait(false);
                return;
            }

            string? modelPath = await AiModelManager.ResolveModelPathAsync(
                modelChoice,
                AiTaskType.SuperResolution,
                context,
                item,
                cancellationToken).ConfigureAwait(false);

            if (modelPath == null)
            {
                context.Log($"[SuperResolution] ⚠️ Modelo de super-resolución no disponible. Se emite sin procesar.", LogLevel.Warning, item);
                await context.EmitAsync("Skipped", item).ConfigureAwait(false);
                return;
            }

            context.Log($"[SuperResolution] 🔍 Escalando {scaleStr} ({origW}x{origH}) para '{item.FileName}'...", LogLevel.Information, item);

            using var upscaledImage = await Task.Run(
                () => OnnxInferenceEngine.UpscaleImage(modelPath, image, requestedScale),
                cancellationToken).ConfigureAwait(false);

            int newW = upscaledImage.Width;
            int newH = upscaledImage.Height;

            await upscaledImage.SaveAsync(targetPath, cancellationToken).ConfigureAwait(false);

            var newItem = item.DeepClone();
            newItem.CurrentPath = targetPath;
            newItem.PhysicalPath = targetPath;
            newItem.FileSizeBytes = new FileInfo(targetPath).Length;
            newItem.Metadata["AI:Upscaled"] = true;
            newItem.Metadata["AI:OriginalResolution"] = $"{origW}x{origH}";
            newItem.Metadata["AI:NewResolution"] = $"{newW}x{newH}";
            newItem.Metadata["AI:ScaleFactor"] = scaleStr;
            newItem.Metadata["AI:UpscalerModel"] = Path.GetFileNameWithoutExtension(modelPath);

            context.Log($"[SuperResolution] ✅ Escalado a {newW}x{newH} completado: '{targetFileName}'", LogLevel.Information, newItem);
            await context.EmitAsync("Out", newItem).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            context.Log($"[SuperResolution] ❌ Error escalando {item.FileName}: {ex.Message}", LogLevel.Error, item);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
        }
    }
}
