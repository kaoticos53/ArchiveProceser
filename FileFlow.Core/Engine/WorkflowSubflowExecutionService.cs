using System.Collections.Concurrent;
using System.Text.Json;
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
    private readonly ConcurrentDictionary<string, WorkflowGraph> _graphCache = new(StringComparer.OrdinalIgnoreCase);

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

    public (List<string> Inputs, List<string> Outputs) DiscoverSubflowPorts(ISubflowNode subflowNode)
    {
        try
        {
            var graph = ResolveGraph(subflowNode, null, null);
            if (graph == null || graph.Nodes.Count == 0)
            {
                return (["In"], ["Out"]);
            }

            var inputs = new List<string>();
            var outputs = new List<string>();

            foreach (var node in graph.Nodes)
            {
                if (node.NodeTypeName.Contains("SubflowInputNode", StringComparison.OrdinalIgnoreCase))
                {
                    if (node.Parameters.TryGetValue("PortNames", out var val) && val != null)
                    {
                        var parts = val.ToString()!.Split([';', ',', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        inputs.AddRange(parts);
                    }
                    else
                    {
                        inputs.Add("In");
                    }
                }
                else if (node.NodeTypeName.Contains("SubflowOutputNode", StringComparison.OrdinalIgnoreCase))
                {
                    if (node.Parameters.TryGetValue("PortNames", out var val) && val != null)
                    {
                        var parts = val.ToString()!.Split([';', ',', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        outputs.AddRange(parts);
                    }
                    else
                    {
                        outputs.Add("Out");
                    }
                }
            }

            if (inputs.Count == 0) inputs.Add("In");
            if (outputs.Count == 0) outputs.Add("Out");

            return (inputs.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    outputs.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        }
        catch
        {
            return (["In"], ["Out"]);
        }
    }

    private WorkflowGraph? ResolveGraph(ISubflowNode subflowNode, IFlowExecutionContext? context, FileItemContext? item)
    {
        if (subflowNode.EmbedDefinition && !string.IsNullOrWhiteSpace(subflowNode.SubflowDefinitionJson))
        {
            return WorkflowGraph.FromJson(subflowNode.SubflowDefinitionJson);
        }

        if (!string.IsNullOrWhiteSpace(subflowNode.SubflowDefinitionJson))
        {
            try
            {
                return WorkflowGraph.FromJson(subflowNode.SubflowDefinitionJson);
            }
            catch { }
        }

        if (!string.IsNullOrWhiteSpace(subflowNode.SubflowPath))
        {
            string resolvedPath = subflowNode.SubflowPath;

            if (!File.Exists(resolvedPath))
            {
                // Buscar relativo al directorio base o AppData
                string candidate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, subflowNode.SubflowPath);
                if (File.Exists(candidate))
                {
                    resolvedPath = candidate;
                }
                else
                {
                    string subflowDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Subflows", subflowNode.SubflowPath);
                    if (File.Exists(subflowDir))
                    {
                        resolvedPath = subflowDir;
                    }
                }
            }

            if (File.Exists(resolvedPath))
            {
                string json = File.ReadAllText(resolvedPath);
                return WorkflowGraph.FromJson(json);
            }
            else if (context != null)
            {
                context.Log($"[Subflow] Archivo de subflujo no encontrado: '{subflowNode.SubflowPath}'", LogLevel.Warning, item);
            }
        }

        return null;
    }
}
