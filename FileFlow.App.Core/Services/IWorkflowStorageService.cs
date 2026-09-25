using System.IO;
using System.Text.Json;
using FileFlow.Core.Engine;

namespace FileFlow.App.Services;

/// <summary>
/// Contrato para el servicio de persistencia y almacenamiento de flujos de trabajo.
/// </summary>
public interface IWorkflowStorageService
{
    ValueTask SaveWorkflowAsync(string filePath, WorkflowGraph graph, CancellationToken ct = default);
    ValueTask<WorkflowGraph> LoadWorkflowAsync(string filePath, CancellationToken ct = default);
    string SerializeGraph(WorkflowGraph graph);
    WorkflowGraph DeserializeGraph(string json);
}
