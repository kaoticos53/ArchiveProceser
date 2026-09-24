using System.IO;
using System.Text.Json;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Sdk;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Core;

/// <summary>
/// Los ejemplos que se entregan son documentación ejecutable: tienen que ser archivos del <b>formato</b> que
/// el producto escribe hoy y tienen que abrirse en el editor <b>sin perder nada</b>. Un ejemplo sin versión se
/// lee como anterior al versionado y arrastra reparaciones pensadas para archivos que ya no se producen, y uno
/// que pierda cables al abrirse enseña un flujo incompleto que parece completo.
/// </summary>
public class WorkflowExamplesValidationTests
{
    [Fact]
    public void AllExampleFlows_ShouldLoadAndHaveValidNodesAndPorts()
    {
        var loader = CreateLoader();

        Assert.True(Directory.Exists(ExamplesDirectory()), $"Examples dir not found: {ExamplesDirectory()}");

        var jsonFiles = ExampleFiles();
        Assert.NotEmpty(jsonFiles);

        var errors = new List<string>();

        foreach (var file in jsonFiles)
        {
            string fileName = Path.GetFileName(file);
            string json = File.ReadAllText(file);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("nodes", out var nodesElement))
            {
                errors.Add($"[{fileName}] Missing 'nodes' property.");
                continue;
            }

            var nodeMap = new Dictionary<string, (IFlowNode Node, List<string> InPorts, List<string> OutPorts)>();

            foreach (var nodeElem in nodesElement.EnumerateArray())
            {
                string id = nodeElem.GetProperty("id").GetString()!;
                string nodeTypeName = nodeElem.GetProperty("nodeTypeName").GetString()!;

                // Lookup node type in loader
                if (!loader.DiscoveredNodeTypes.TryGetValue(nodeTypeName, out var nodeType))
                {
                    // Try without namespace
                    string simpleName = nodeTypeName.Contains('.') ? nodeTypeName.Split('.').Last() : nodeTypeName;
                    if (!loader.DiscoveredNodeTypes.TryGetValue(simpleName, out nodeType))
                    {
                        errors.Add($"[{fileName}] Node type '{nodeTypeName}' (id: {id}) not found in discovered plugins.");
                        continue;
                    }
                }

                var instance = (IFlowNode)Activator.CreateInstance(nodeType)!;
                var inPorts = instance.Inputs.Select(p => p.Name).ToList();
                var outPorts = instance.Outputs.Select(p => p.Name).ToList();

                if (instance.GetType().Name.Contains("SwitchCaseNode", StringComparison.OrdinalIgnoreCase))
                {
                    // Dynamic ports for switch
                    if (nodeElem.TryGetProperty("parameters", out var pElem) && pElem.TryGetProperty("Cases", out var casesElem))
                    {
                        // Cases are handled
                    }
                }

                nodeMap[id] = (instance, inPorts, outPorts);

                // Validate parameters
                if (nodeElem.TryGetProperty("parameters", out var paramsElem))
                {
                    var validKeys = new HashSet<string>(instance.ParameterDescriptors.Select(d => d.Key), StringComparer.OrdinalIgnoreCase);
                    foreach (var prop in paramsElem.EnumerateObject())
                    {
                        // Check if key is known or a dynamic variable
                        if (instance.GetType().Name.Contains("VariableInjectorNode") || instance.GetType().Name.Contains("SwitchCaseNode"))
                        {
                            continue;
                        }
                        if (instance.GetType().Name.Contains("AdvancedRenamerNode") && (prop.Name.Equals("PipelineName", StringComparison.OrdinalIgnoreCase) || prop.Name.Equals("CollisionStrategy", StringComparison.OrdinalIgnoreCase) || prop.Name.Equals("MethodSteps", StringComparison.OrdinalIgnoreCase)))
                        {
                            continue;
                        }

                        if (!validKeys.Contains(prop.Name) && !instance.Parameters.ContainsKey(prop.Name))
                        {
                            errors.Add($"[{fileName}] Node '{nodeTypeName}' (id: {id}) has unknown parameter '{prop.Name}'. Valid keys: {string.Join(", ", validKeys)}");
                        }
                    }
                }
            }

            // Validate edges
            if (root.TryGetProperty("edges", out var edgesElement))
            {
                foreach (var edgeElem in edgesElement.EnumerateArray())
                {
                    string srcId = edgeElem.GetProperty("sourceNodeId").GetString()!;
                    string srcPort = edgeElem.GetProperty("sourcePortName").GetString()!;
                    string tgtId = edgeElem.GetProperty("targetNodeId").GetString()!;
                    string tgtPort = edgeElem.GetProperty("targetPortName").GetString()!;

                    if (!nodeMap.TryGetValue(srcId, out var srcInfo))
                    {
                        errors.Add($"[{fileName}] Edge source node '{srcId}' not found.");
                        continue;
                    }

                    if (!nodeMap.TryGetValue(tgtId, out var tgtInfo))
                    {
                        errors.Add($"[{fileName}] Edge target node '{tgtId}' not found.");
                        continue;
                    }

                    if (!srcInfo.OutPorts.Contains(srcPort) && !srcInfo.Node.GetType().Name.Contains("SwitchCaseNode"))
                    {
                        errors.Add($"[{fileName}] Edge source port '{srcPort}' not found on node '{srcInfo.Node.GetType().Name}' (id: {srcId}). Valid: {string.Join(", ", srcInfo.OutPorts)}");
                    }

                    if (!tgtInfo.InPorts.Contains(tgtPort))
                    {
                        errors.Add($"[{fileName}] Edge target port '{tgtPort}' not found on node '{tgtInfo.Node.GetType().Name}' (id: {tgtId}). Valid: {string.Join(", ", tgtInfo.InPorts)}");
                    }
                }
            }
        }

        Assert.True(errors.Count == 0, $"Validation errors found in example flows:\n" + string.Join("\n", errors));
    }

    /// <summary>
    /// El ejemplo es lo que el producto escribe y declara el formato que escribe: se lee por el camino de
    /// lectura de la aplicación, se vuelve a escribir con su escritor y el texto tiene que salir idéntico. Un
    /// ejemplo escrito a mano con el dialecto de otra época enseña la forma que no es.
    /// </summary>
    [Fact]
    public void AllExampleFlows_ShouldBeWrittenByTheProductWriter()
    {
        var storage = new WorkflowStorageService();
        var problems = new List<string>();

        foreach (string file in ExampleFiles())
        {
            string name = Path.GetFileName(file);
            string text = File.ReadAllText(file);
            var graph = storage.DeserializeGraph(text);

            if (WorkflowFormat.VersionOf(graph) != WorkflowFormat.CurrentVersion)
            {
                problems.Add($"[{name}] declara '{graph.Schema}' y no '{WorkflowFormat.CurrentSchema}': " +
                    "se leería como anterior al versionado y llevaría encima sus reparaciones");
                continue;
            }

            string rewritten = storage.SerializeGraph(storage.DeserializeGraph(text));
            if (rewritten != text)
            {
                problems.Add($"[{name}] no es lo que el escritor del producto produce: vuelve a guardarlo desde la app");
            }
        }

        problems.Should().BeEmpty(string.Join("\n", problems));
    }

    /// <summary>
    /// Se abren como los abre la aplicación —materializando puertos dinámicos y emparejando cables por nombre—
    /// y vuelven enteros: ni un nodo menos, ni un cable descartado. Es la misma propiedad que persigue la fase
    /// 2E-P4, fijada sobre el material que se entrega y no sobre un flujo de prueba.
    /// </summary>
    [Fact]
    public void AllExampleFlows_ShouldOpenInTheEditorWithoutLosingAnything()
    {
        var storage = new WorkflowStorageService();
        var problems = new List<string>();

        foreach (string file in ExampleFiles())
        {
            string name = Path.GetFileName(file);
            var graph = storage.DeserializeGraph(File.ReadAllText(file));
            var editor = new EditorViewModel(CreateLoader());

            var report = editor.LoadFromGraphModel(graph);

            if (editor.Nodes.Count != graph.Nodes.Count)
            {
                problems.Add($"[{name}] el lienzo quedó con {editor.Nodes.Count} nodos de {graph.Nodes.Count}");
            }

            if (!report.IsComplete)
            {
                problems.Add($"[{name}] cables descartados al abrir: " +
                    string.Join(", ", report.DroppedConnections.Select(d => $"{d.Source.PortName}->{d.Target.PortName}")));
            }

            if (editor.Connections.Count != graph.Edges.Count)
            {
                problems.Add($"[{name}] el lienzo quedó con {editor.Connections.Count} cables de {graph.Edges.Count}");
            }
        }

        problems.Should().BeEmpty(string.Join("\n", problems));
    }

    /// <summary>
    /// <b>Todo compresor del catálogo dice dónde escribe.</b> El nodo tiene una salida por omisión —sin carpeta de
    /// destino declarada, el comprimido sale en la <i>salida del flujo</i>: la que el flujo declara como suya y, si
    /// no declara ninguna, la salida por defecto de los ajustes— y esa regla está escrita en el nodo, en su log y en
    /// la ayuda del parámetro; lo que no puede es ser la <b>única</b> respuesta para quien lee un ejemplo: el catálogo
    /// es documentación, y «dónde acaba mi archivo» no se deduce de un diagrama. De los cinco ejemplos que usan el
    /// compresor, cuatro no lo decían (08, 12, 21 y 38) y el quinto tampoco: el 34 lo callaba. La salida por omisión
    /// sigue siendo la del nodo para los flujos del usuario que no declaren carpeta; el catálogo, que enseña, lo
    /// declara (hitos 208 y 209).
    /// </summary>
    [Fact]
    public void EveryCompressorInTheCatalog_ShouldSayWhereTheArchiveGoes()
    {
        var storage = new WorkflowStorageService();
        var mute = new List<string>();
        int compressors = 0;

        foreach (string file in ExampleFiles())
        {
            var graph = storage.DeserializeGraph(File.ReadAllText(file));

            foreach (var node in graph.Nodes.Where(n => n.NodeTypeName == "ArchiveCompressorNode"))
            {
                compressors++;

                if (!DeclaresWhereToWrite(node.Parameters))
                {
                    mute.Add($"[{Path.GetFileName(file)}] el compresor '{node.Id}' no declara 'DestinationFolder': " +
                        "el comprimido acabaría en la salida por omisión —la del flujo, o la de los ajustes— y el ejemplo no lo dice");
                }
            }
        }

        compressors.Should().BeGreaterThan(0, "la guardia sólo vale mientras el catálogo use el compresor");
        mute.Should().BeEmpty(string.Join("\n", mute));
    }

    /// <summary>Carpeta de destino declarada, con el nombre vigente o con el heredado.</summary>
    private static bool DeclaresWhereToWrite(IReadOnlyDictionary<string, object?> parameters) =>
        Declares(parameters, "DestinationFolder") || Declares(parameters, "DestinationDirectory");

    private static bool Declares(IReadOnlyDictionary<string, object?> parameters, string key) =>
        parameters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value?.ToString());

    /// <summary>Directorio de los ejemplos que se entregan, buscado desde la raíz del repositorio.</summary>
    private static string ExamplesDirectory() =>
        Path.Combine(TestRepositoryLocator.RepositoryRoot(), "docs", "examples");

    /// <summary>
    /// Los flujos de ejemplo que se entregan, tal y como los enumera el catálogo. El `docs/flujo_test.json` de
    /// la raíz de la documentación no entra: es un archivo de prueba con parámetros que ningún nodo declara hoy
    /// (`DestinationFolder`, `CleanWrapper`) y una ruta absoluta de otra máquina, así que validarlo obligaría a
    /// inventarle la traducción a los nombres vigentes.
    /// </summary>
    private static List<string> ExampleFiles() =>
        [.. Directory.GetFiles(ExamplesDirectory(), "*.json", SearchOption.AllDirectories).OrderBy(path => path)];

    private static PluginLoader CreateLoader()
    {
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.FileSystem.FolderSourceNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Archives.SmartUnpackNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Images.ImageOptimizerNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Logic.SwitchCaseNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Hashing.HashCalculatorNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Integrations.MediaTranscoderNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Scripting.CustomScriptNode).Assembly);
        return loader;
    }
}
