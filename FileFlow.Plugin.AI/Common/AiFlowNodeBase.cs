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

    /// <summary>Puente para el relay débil: los eventos sólo se pueden invocar desde la clase que los declara.</summary>

    public void RaiseModelStatusChanged() => ModelStatusChanged?.Invoke();

    /// <summary>
    /// Construye el nodo observando el gestor ONNX estándar, que es el almacén de sesiones de los nodos
    /// de visión y texto.
    /// </summary>
    protected AiFlowNodeBase()
        : this(
            h => OnnxSessionManager.SessionStateChanged += h,
            h => OnnxSessionManager.SessionStateChanged -= h)
    {
    }

    /// <summary>
    /// Construye el nodo observando un evento de sesión distinto del gestor ONNX estándar (los motores
    /// especializados, como el de audio, mantienen su propia caché). Uno de los dos pares debe venir del
    /// evento estático del motor; pasar una lambda que capture al nodo reintroduciría la fuga que
    /// <see cref="WeakModelStatusRelay"/> elimina.
    /// </summary>
    protected AiFlowNodeBase(Action<Action> sessionStateSubscribe, Action<Action> sessionStateUnsubscribe)
    {
        // Relay débil: el nodo es alcanzable desde el evento estático sólo vía WeakReference, así que

        // desaparece con el editor sin dejar el delegado anclado para siempre (ver WeakModelStatusRelay).

        _ = WeakModelStatusRelay.Subscribe(

            sessionStateSubscribe,

            sessionStateUnsubscribe,

            this,

            static self => self.RaiseModelStatusChanged());
    }

    #region Almacén de sesiones (punto de extensión para motores especializados)

    /// <summary>Indica si la sesión del modelo ya está materializada en memoria.</summary>
    protected virtual bool IsSessionLoadedForModel(string modelPath)
        => OnnxSessionManager.IsSessionLoaded(modelPath);

    /// <summary>Libera deterministamente la sesión del modelo.</summary>
    protected virtual bool UnloadSessionForModel(string modelPath)
        => OnnxSessionManager.UnloadSession(modelPath);

    /// <summary>Indica si el modelo aprovecha aceleración por hardware.</summary>
    protected virtual bool IsGpuAcceleratedForModel(string modelPath)
        => OnnxSessionManager.ShouldUseDirectMl(modelPath);

    /// <summary>Materializa la sesión del modelo en memoria.</summary>
    protected virtual void EnsureSessionLoadedForModel(string modelPath)
        => OnnxSessionManager.GetOrCreateSession(modelPath);

    #endregion

    public virtual bool IsModelLoaded
    {
        get
        {
            string? modelPath = AiModelManager.ResolveModelPathSync(ModelSelection, TaskType);
            return modelPath != null && IsSessionLoadedForModel(modelPath);
        }
    }

    public virtual string? ModelIdentifier => AiModelManager.GetModelDisplayName(ModelSelection, TaskType);

    public virtual bool IsGpuAccelerated
    {
        get
        {
            string? modelPath = AiModelManager.ResolveModelPathSync(ModelSelection, TaskType);
            return modelPath != null && IsGpuAcceleratedForModel(modelPath);
        }
    }

    public virtual async Task PreloadModelAsync(CancellationToken cancellationToken = default)
    {
        string? modelPath = await AiModelManager.ResolveModelPathAsync(ModelSelection, TaskType, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(modelPath) && File.Exists(modelPath))
        {
            EnsureSessionLoadedForModel(modelPath);
        }
        ModelStatusChanged?.Invoke();
    }

    public virtual void UnloadModel()
    {
        string? modelPath = AiModelManager.ResolveModelPathSync(ModelSelection, TaskType);
        if (!string.IsNullOrWhiteSpace(modelPath))
        {
            UnloadSessionForModel(modelPath);
        }
        ModelStatusChanged?.Invoke();
    }

    /// <summary>
    /// Modelo usado cuando la configuración no trae ninguno. Los nodos cuyo modelo está fijado por diseño
    /// (por ejemplo el transformador de prompts, siempre ligado al par MarianMT que esperan los motores de
    /// prompts visuales) lo sobrescriben en lugar de exponer un parámetro 'Model' que el usuario podría
    /// desviar hacia un modelo que el nodo no sabe usar.
    /// </summary>
    protected virtual string DefaultModelSelection => "Auto";

    public string ModelSelection
    {
        get
        {
            if (Parameters.TryGetValue("Model", out var mVal) && mVal is not null)
                return mVal.ToString() ?? "Auto";
            return GetParameter("ModelSelection", DefaultModelSelection);
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
