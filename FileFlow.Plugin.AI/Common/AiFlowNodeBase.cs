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
    /// Construye el nodo observando el evento único de estado de sesiones del plugin
    /// (<see cref="OnnxSessionRegistry.SessionStateChanged"/>), que agrega los cambios de los almacenes
    /// de visión, audio y embeddings. El nodo no necesita saber qué motor materializó la sesión: escucha
    /// siempre el mismo evento y consulta su propio almacén.
    /// </summary>
    protected AiFlowNodeBase()
    {
        // Relay débil: el nodo es alcanzable desde el evento estático sólo vía WeakReference, así que
        // desaparece con el editor sin dejar el delegado anclado para siempre (ver WeakModelStatusRelay).
        _ = WeakModelStatusRelay.Subscribe(
            h => OnnxSessionRegistry.SessionStateChanged += h,
            h => OnnxSessionRegistry.SessionStateChanged -= h,
            this,
            static self => self.RaiseModelStatusChanged());
    }

    #region Almacén de sesiones (punto de extensión único por motor)

    /// <summary>
    /// Almacén que respalda el ciclo de vida de modelos de este nodo. Por defecto es el de visión y
    /// texto; los motores con política de sesión propia (audio, embeddings) lo sobrescriben apuntando a
    /// su almacén registrado. Es el único punto de extensión: la observación del estado y las consultas
    /// de carga, aceleración y descarga se derivan de él.
    /// </summary>
    protected virtual OnnxSessionStore SessionStore => OnnxSessionManager.Default;

    #endregion

    public virtual bool IsModelLoaded
    {
        get
        {
            string? modelPath = AiModelManager.ResolveModelPathSync(ModelSelection, TaskType);
            return modelPath != null && SessionStore.IsSessionLoaded(modelPath);
        }
    }

    public virtual string? ModelIdentifier => AiModelManager.GetModelDisplayName(ModelSelection, TaskType);

    public virtual bool IsGpuAccelerated
    {
        get
        {
            string? modelPath = AiModelManager.ResolveModelPathSync(ModelSelection, TaskType);
            return modelPath != null && SessionStore.IsHardwareAccelerated(modelPath);
        }
    }

    public virtual async Task PreloadModelAsync(CancellationToken cancellationToken = default)
    {
        string? modelPath = await AiModelManager.ResolveModelPathAsync(ModelSelection, TaskType, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(modelPath) && File.Exists(modelPath))
        {
            SessionStore.GetOrCreateSession(modelPath);
        }
        ModelStatusChanged?.Invoke();
    }

    public virtual void UnloadModel()
    {
        string? modelPath = AiModelManager.ResolveModelPathSync(ModelSelection, TaskType);
        if (!string.IsNullOrWhiteSpace(modelPath))
        {
            SessionStore.UnloadSession(modelPath);
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
            // "Model" es el nombre heredado del parámetro y, si está informado, tiene prioridad sobre
            // "ModelSelection". Un valor vacío o ausente se trata igual y cae al modelo por defecto.
            string model = GetParameter("Model", string.Empty);
            return string.IsNullOrWhiteSpace(model) ? GetParameter("ModelSelection", DefaultModelSelection) : model;
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
