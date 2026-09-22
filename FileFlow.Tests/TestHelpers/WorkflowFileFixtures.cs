using System.Text.Json.Nodes;
using FileFlow.App.Services;
using FileFlow.Core.Engine;
using FluentAssertions;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// Archivos de flujo para las pruebas. La ida y vuelta pasa por el servicio real de guardado, en memoria,
/// para que el grafo llegue como llega desde un archivo —con el convertidor de tipos inferidos del
/// producto— y no por una serialización de conveniencia.
/// </summary>
public static class WorkflowFileFixtures
{
    /// <summary>El grafo tal y como vuelve de un archivo guardado, leído con el lector del producto.</summary>
    public static WorkflowGraph SavedFile(WorkflowGraph graph)
    {
        var storage = new WorkflowStorageService();
        return storage.DeserializeGraph(storage.SerializeGraph(graph));
    }

    /// <summary>
    /// El mismo archivo, sin el campo que declara la versión del formato: exactamente en eso se diferencia un
    /// archivo guardado antes de que el formato se versionara de uno actual. Se construye con el escritor
    /// real para que no se diferencie en nada más.
    /// </summary>
    public static WorkflowGraph FileFromOlderFormat(WorkflowGraph graph)
    {
        var storage = new WorkflowStorageService();
        var json = JsonNode.Parse(storage.SerializeGraph(graph))!.AsObject();
        json.Remove("Schema").Should().BeTrue("un archivo anterior al formato versionado no declara su versión");

        return storage.DeserializeGraph(json.ToJsonString());
    }
}
