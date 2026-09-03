using System.Threading;
using System.Threading.Tasks;
using FileFlow.Sdk;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Clase base abstracta para nodos de Inteligencia Artificial que consumen modelos locales con resolución por hardware o catálogo.
/// </summary>
public abstract class AiFlowNodeBase : FlowNodeBase
{
    public abstract AiTaskType TaskType { get; }

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

    public string CustomModelPath
    {
        get => GetParameter("CustomModelPath", string.Empty);
        set => SetParameter("CustomModelPath", value);
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
            CustomModelPath,
            TaskType,
            context,
            item,
            cancellationToken).ConfigureAwait(false);
    }
}
