using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Plugin.AI.Inference;
using FileFlow.Sdk;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Clase base abstracta para nodos de Inteligencia Artificial que consumen modelos locales con resolución por hardware o catálogo.
/// </summary>
public abstract class AiFlowNodeBase : FlowNodeBase, IModelLifecycleNode
{
    public abstract AiTaskType TaskType { get; }

    public event Action? ModelStatusChanged;

    protected AiFlowNodeBase()
    {
        OnnxSessionManager.SessionStateChanged += () => ModelStatusChanged?.Invoke();
    }

    public virtual bool IsModelLoaded
    {
        get
        {
            string? modelPath = AiModelManager.ResolveModelPathSync(ModelSelection, TaskType);
            return modelPath != null && OnnxSessionManager.IsSessionLoaded(modelPath);
        }
    }

    public virtual string? ModelIdentifier => AiModelManager.GetModelDisplayName(ModelSelection, TaskType);

    public virtual bool IsGpuAccelerated
    {
        get
        {
            string? modelPath = AiModelManager.ResolveModelPathSync(ModelSelection, TaskType);
            return modelPath != null && OnnxSessionManager.ShouldUseDirectMl(modelPath);
        }
    }

    public virtual async Task PreloadModelAsync(CancellationToken cancellationToken = default)
    {
        string? modelPath = await AiModelManager.ResolveModelPathAsync(ModelSelection, TaskType, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(modelPath) && File.Exists(modelPath))
        {
            OnnxSessionManager.GetOrCreateSession(modelPath);
        }
        ModelStatusChanged?.Invoke();
    }

    public virtual void UnloadModel()
    {
        string? modelPath = AiModelManager.ResolveModelPathSync(ModelSelection, TaskType);
        if (!string.IsNullOrWhiteSpace(modelPath))
        {
            OnnxSessionManager.UnloadSession(modelPath);
        }
        ModelStatusChanged?.Invoke();
    }

    public string ModelSelection
    {
        get
        {
            if (Parameters.TryGetValue("Model", out var mVal) && mVal is not null)
                return mVal.ToString() ?? "Auto";
            return GetParameter("ModelSelection", "Auto");
        }
        set
        {
            SetParameter("Model", value);
            SetParameter("ModelSelection", value);
        }
    }

    /// <summary>
    /// Resuelve la ruta física del modelo para la tarea actual, descargándolo si es necesario o resolviendo la ruta local.
    /// </summary>
    protected async Task<string?> ResolveModelPathAsync(
        IFlowExecutionContext context,
        FileItemContext item,
        CancellationToken cancellationToken)
    {
        return await AiModelManager.ResolveModelPathAsync(
            ModelSelection,
            TaskType,
            context,
            item,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Carga una imagen RGB24 de forma desacoplada usando la abstracción de almacenamiento (IStorageService).
    /// </summary>
    protected static async Task<SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgb24>?> LoadInputRgb24ImageAsync(
        FileItemContext item,
        FileFlow.Sdk.Storage.IStorageService storage,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !await storage.FileExistsAsync(item.CurrentPath, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        await using var stream = await storage.OpenReadAsync(item.CurrentPath, cancellationToken).ConfigureAwait(false);
        return await SixLabors.ImageSharp.Image.LoadAsync<SixLabors.ImageSharp.PixelFormats.Rgb24>(stream, cancellationToken).ConfigureAwait(false);
    }
}
