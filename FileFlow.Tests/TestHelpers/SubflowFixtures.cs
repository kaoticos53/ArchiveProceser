using FileFlow.Core.Engine;
using FileFlow.Plugin.Subflows;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// Material de partida para las pruebas que resuelven la frontera de un subflujo: un subgrafo con puertos
/// declarados, nodos frontera para armarlo y archivos temporales con la fecha de escritura bajo control
/// (varias pruebas necesitan que un archivo cambie de contenido sin cambiar de marca, o al revés).
/// </summary>
public static class SubflowFixtures
{
    /// <summary>Subgrafo con una frontera propia: los puertos que declaran sus nodos de entrada y salida.</summary>
    public static string DefinitionJson(string inputPorts, string outputPorts) => new WorkflowGraph
    {
        Name = "Subflujo con frontera propia",
        Nodes =
        {
            Boundary("in", "SubflowInputNode", inputPorts),
            Boundary("out", "SubflowOutputNode", outputPorts)
        }
    }.ToJson();

    /// <summary>Ruta de un archivo temporal con nombre único, sin crearlo.</summary>
    public static string TempFile() =>
        Path.Combine(Path.GetTempPath(), $"fileflow-subflow-{Guid.NewGuid():N}.flow");

    /// <summary>Escribe el archivo y le fija la fecha de última escritura indicada.</summary>
    public static void WriteFile(string path, string content, DateTime lastWriteUtc)
    {
        File.WriteAllText(path, content);
        File.SetLastWriteTimeUtc(path, lastWriteUtc);
    }

    /// <summary>
    /// Nodo frontera de un subflujo, con los nombres de puerto que expone hacia el exterior.
    /// </summary>
    private static WorkflowNode Boundary(string id, string nodeTypeName, string portNames) => new()
    {
        Id = id,
        NodeTypeName = $"{typeof(SubflowNode).Namespace}.{nodeTypeName}",
        Parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["PortNames"] = portNames
        }
    };
}
