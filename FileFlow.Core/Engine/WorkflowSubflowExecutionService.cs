using FileFlow.Core.Plugins;
using FileFlow.Sdk;
using FileFlow.Sdk.Services;

namespace FileFlow.Core.Engine;

/// <summary>
/// Implementación Core del servicio de ejecución y descubrimiento de subflujos jerárquicos DAG.
/// </summary>
public sealed class WorkflowSubflowExecutionService : ISubflowExecutionService
{
    private readonly PluginLoader _loader;

    public WorkflowSubflowExecutionService(PluginLoader? loader = null)
    {
        _loader = loader ?? new PluginLoader();
        if (_loader.DiscoveredNodeTypes.Count == 0)
        {
            _loader.ScanCurrentAppDomain();
        }
    }

    public async Task ExecuteSubflowAsync(
        ISubflowNode subflowNode,
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext parentContext,
        CancellationToken cancellationToken)
    {
        var graph = ResolveGraph(subflowNode, parentContext, item);
        if (graph == null || graph.Nodes.Count == 0)
        {
            parentContext.Log($"[Subflow] Definición de subgrafo vacía o no disponible para '{subflowNode.Name}'", LogLevel.Warning, item);
            return;
        }

        string subflowIdentifier = !string.IsNullOrWhiteSpace(subflowNode.SubflowPath)
            ? subflowNode.SubflowPath
            : (!string.IsNullOrWhiteSpace(graph.Name) ? graph.Name : subflowNode.Id);

        // Control de recursión cíclica
        HashSet<string> callStack;
        if (item.Metadata.TryGetValue("__SubflowCallStack__", out var stackObj) && stackObj is HashSet<string> existingStack)
        {
            callStack = new HashSet<string>(existingStack, StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            callStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        if (callStack.Contains(subflowIdentifier))
        {
            string errMsg = $"Detección de recursión infinita: El subflujo '{subflowIdentifier}' ya se encuentra en la pila de ejecución.";
            parentContext.Log($"[Subflow Error] {errMsg}", LogLevel.Error, item);
            throw new InvalidOperationException(errMsg);
        }

        callStack.Add(subflowIdentifier);

        var subflowItem = item.DeepClone();
        subflowItem.Metadata["__SubflowCallStack__"] = callStack;
        subflowItem.Metadata["__SubflowExecutionService__"] = this;

        // Callback para capturar las emisiones de los SubflowOutputNode y enviarlas al parentContext
        subflowItem.Metadata[ISubflowNode.SubflowSinkKey] = new Func<string, FileItemContext, Task>(async (outPort, outItem) =>
        {
            var cleanClone = outItem.DeepClone();
            cleanClone.Metadata.Remove(ISubflowNode.SubflowSinkKey);
            await parentContext.EmitAsync(outPort, cleanClone).ConfigureAwait(false);
        });

        var childExecutor = new WorkflowExecutor
        {
            MaxDegreeOfParallelism = parentContext.IsDryRun ? 1 : Environment.ProcessorCount
        };

        childExecutor.LogEmitted += (msg, lvl) =>
        {
            parentContext.Log($"[{subflowNode.Name}] {msg}", lvl, item);
        };

        childExecutor.ProgressChanged += (pct, status) =>
        {
            parentContext.ReportProgress(pct, $"[{subflowNode.Name}] {status}");
        };

        await childExecutor.ExecuteAsync(
            graph,
            _loader,
            cancellationToken,
            initialItem: subflowItem,
            entryInputPortName: inputPortName).ConfigureAwait(false);
    }

    /// <summary>
    /// Puertos frontera del subflujo del nodo. La regla vive en <see cref="SubflowPortResolver"/>, que es
    /// también la que usa el editor en tiempo de diseño: el motor y el lienzo no pueden discrepar sobre
    /// qué puertos expone un subflujo.
    /// </summary>
    public (List<string> Inputs, List<string> Outputs) DiscoverSubflowPorts(ISubflowNode subflowNode) =>
        SubflowPortResolver.Discover(subflowNode);

    private WorkflowGraph? ResolveGraph(ISubflowNode subflowNode, IFlowExecutionContext? context, FileItemContext? item)
    {
        var graph = SubflowPortResolver.ResolveGraph(subflowNode);

        // El servicio sí avisa en el registro de ejecución cuando la definición no se pudo resolver; el
        // resolutor devuelve null en silencio porque el editor pregunta sin contexto de ejecución.
        if (graph == null && context != null && !string.IsNullOrWhiteSpace(subflowNode.SubflowPath))
        {
            context.Log($"[Subflow] Archivo de subflujo no encontrado: '{subflowNode.SubflowPath}'", LogLevel.Warning, item);
        }

        return graph;
    }
}
