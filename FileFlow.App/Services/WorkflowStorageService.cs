using System.IO;
using System.Text.Json;
using FileFlow.Core.Engine;

namespace FileFlow.App.Services;

/// <summary>
/// Servicio de persistencia y serialización asíncrona de grafos de flujo en formato JSON.
/// </summary>
public class WorkflowStorageService : IWorkflowStorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new FileFlow.Sdk.Serialization.ObjectToInferredTypesConverter() }
    };

    public async ValueTask SaveWorkflowAsync(string filePath, WorkflowGraph graph, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(graph);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempPath = filePath + ".tmp_" + Guid.NewGuid().ToString("N");
        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, graph, JsonOptions, ct).ConfigureAwait(false);
            }
            File.Move(tempPath, filePath, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // Silencioso para no enmascarar excepciones previas
            }
        }
    }

    public async ValueTask<WorkflowGraph> LoadWorkflowAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"El archivo de flujo no existe: {filePath}", filePath);
        }

        await using var stream = File.OpenRead(filePath);
        var graph = await JsonSerializer.DeserializeAsync<WorkflowGraph>(stream, JsonOptions, ct).ConfigureAwait(false);

        return graph ?? throw new InvalidDataException("El archivo de flujo no contiene un grafo válido.");
    }

    public string SerializeGraph(WorkflowGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return JsonSerializer.Serialize(graph, JsonOptions);
    }

    public WorkflowGraph DeserializeGraph(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var graph = JsonSerializer.Deserialize<WorkflowGraph>(json, JsonOptions);
        return graph ?? throw new InvalidDataException("El contenido JSON no contiene un grafo válido.");
    }
}
