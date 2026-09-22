using FileFlow.App.Services;
using FileFlow.Core.Engine;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Un flujo guardado se lee por <b>dos</b> caminos: el de la app —el servicio de guardado, que es quien lo
/// escribe— y el de Core —<see cref="WorkflowGraph.FromJson"/>, que es el que usan el CLI al ejecutar un
/// archivo y el resolutor al abrir la definición de un subflujo—. Los dos tienen que entender el mismo
/// archivo, y esto no estaba pasando: la app escribe los nombres tal cual y el lector de Core esperaba
/// camelCase, así que no enlazaba <b>ninguna</b> propiedad. No fallaba: devolvía un grafo vacío, y el CLI
/// ejecutaba un flujo que no tenía nada, con su resumen en verde.
///
/// Por eso la prueba no se conforma con que los dos lectores coincidan —dos grafos vacíos coinciden—: exige
/// primero que hayan leído algo, y después que lean lo mismo.
///
/// <para>
/// La fase 3E estrecha eso con la mitad que faltaba: que los dos <b>escriban</b> lo mismo. Coincidir al leer
/// no era coincidir en el formato —la app escribía los nombres tal cual y Core en camelCase, los dos
/// declarando <c>v2</c>—, así que el mismo grafo guardado por los dos caminos daba dos archivos distintos y
/// «el formato» era en realidad la tolerancia del lector. Ahora la definición es una sola y de ahí sale el
/// texto; lo que se comprueba es el texto, no el grafo que sale de él.
/// </para>
///
/// <para>
/// Y la otra mitad de la misma moneda: cambiar lo que se escribe sólo es seguro si lo que <b>ya</b> está
/// escrito se sigue leyendo, así que hay un archivo de verdad, con el dialecto anterior —los nombres tal
/// cual, con sus nulos—, que tiene que llegar entero.
/// </para>
/// </summary>
public class WorkflowFileInteropTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Los dos escritores escriben lo mismo
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// El archivo que se escribe en disco, byte a byte, es el que escribe Core. Se compara el <b>texto del
    /// archivo</b> y no el resultado de serializar en memoria: es el archivo el que viaja, y guardar pasa por
    /// un temporal y un movimiento que podrían escribir otra cosa.
    /// </summary>
    [Fact]
    public async Task TheSameGraph_ShouldBeWrittenAsTheSameTextByBothWriters()
    {
        string file = SubflowFixtures.TempFile();

        try
        {
            var byTheApp = SampleGraph();
            await new WorkflowStorageService().SaveWorkflowAsync(file, byTheApp);

            // Dos grafos equivalentes y **sin** versión declarada, que es como están en memoria: la declara el
            // escritor. Así la comparación cubre también esa mitad —una versión que se olvide en uno de los
            // caminos convierte un archivo actual en uno al que se le aplican reparaciones de un formato que ya
            // no se produce— y no sólo la política de nombres.
            string writtenByTheCore = SampleGraph().ToJson();

            File.ReadAllText(file).Should().Be(writtenByTheCore,
                "el mismo grafo tiene que dar el mismo texto por los dos caminos: mientras sean dos, «el formato» "
                + "es lo que cada escritor decida, y el que lo lea tiene que adivinar cuál le tocó");
        }
        finally
        {
            File.Delete(file);
        }
    }

    /// <summary>
    /// Cuál es ese texto único, escrito aquí para que se lea: la versión va primero, las claves son las del
    /// formato y no las del modelo, y los nombres de los parámetros se quedan como los nombra el nodo —la
    /// política de nombres es de propiedades y no toca las claves de un diccionario—.
    ///
    /// Sin esta prueba, la de arriba seguiría pasando si los dos escritores se pusieran de acuerdo en
    /// cualquier cosa, incluido volver al dialecto viejo.
    /// </summary>
    [Fact]
    public void WhatGetsWritten_ShouldBeTheFormatAndNotTheModel()
    {
        string json = new WorkflowStorageService().SerializeGraph(SampleGraph());

        json.Should().Contain($"\"schema\": \"{WorkflowFormat.CurrentSchema}\"",
            "un archivo sin versión se lee como anterior al versionado, y el que escribe dice la suya");
        json.Should().Contain("\"nodes\":", "las claves del archivo son las suyas, no las del modelo");
        json.Should().NotContain("\"Nodes\":", "los dos dialectos no pueden seguir vivos");
        json.Should().Contain("\"ExtensionFilter\": \"*.jpg\"",
            "los parámetros se guardan como los nombra el nodo: la política de nombres es de propiedades");
    }

    /// <summary>
    /// Un parámetro con valor nulo se escribe y vuelve. Es el límite que se midió al unificar: de una
    /// propiedad nula no se escribe nada —el lector la lee igual como ausente—, pero la condición es de
    /// propiedades y no toca los valores de un diccionario, así que ningún parámetro se pierde por esto.
    /// </summary>
    [Fact]
    public void AParameterWhoseValueIsNull_ShouldSurviveBothWriters()
    {
        var graph = SampleGraph();
        graph.Nodes[0].Parameters["SinValor"] = null;

        var byApp = new WorkflowStorageService().DeserializeGraph(new WorkflowStorageService().SerializeGraph(graph));
        var byCore = WorkflowGraph.FromJson(graph.ToJson());

        byApp.Nodes[0].Parameters.Should().ContainKey("SinValor", "el parámetro está, aunque no tenga valor");
        byApp.Nodes[0].Parameters["SinValor"].Should().BeNull();
        byCore.Nodes[0].Parameters.Should().ContainKey("SinValor");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Y lo que se escribió antes se sigue leyendo
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Un archivo del dialecto anterior —los nombres tal cual, con los nulos escritos, que es como guardaba
    /// la app hasta esta fase— llega entero por los dos lectores. Es lo que hace seguro cambiar lo que se
    /// escribe: el formato nuevo no puede exigir que lo viejo desaparezca.
    /// </summary>
    [Fact]
    public void AFileWrittenBeforeThisPhase_ShouldStillBeReadWhole()
    {
        const string oldFile = """
            {
              "Schema": "FileFlow.Workflow.v2",
              "Name": "Flujo antiguo",
              "GlobalOutputDir": "C:\\Salida",
              "TemporaryDirectory": "",
              "Nodes": [
                {
                  "Id": "src",
                  "NodeTypeName": "FileFlow.Plugin.FileSystem.FolderSourceNode",
                  "CustomTitle": "Origen",
                  "X": 10,
                  "Y": 20,
                  "HasBreakpoint": true,
                  "IsLoggingEnabled": true,
                  "Parameters": {
                    "ExtensionFilter": "*.jpg",
                    "MaxDepth": 3,
                    "Recursive": true,
                    "SinValor": null
                  }
                },
                {
                  "Id": "sink",
                  "NodeTypeName": "FileFlow.Plugin.FileSystem.DestinationSinkNode",
                  "X": 200,
                  "Y": 20,
                  "HasBreakpoint": false,
                  "IsLoggingEnabled": true,
                  "Parameters": null
                }
              ],
              "Edges": [
                {
                  "Id": "e1",
                  "SourceNodeId": "src",
                  "SourcePortName": "Out",
                  "TargetNodeId": "sink",
                  "TargetPortName": "In"
                }
              ],
              "Annotations": [
                {
                  "Id": "note",
                  "Title": "Nota",
                  "Content": "Texto de la nota",
                  "X": 100,
                  "Y": 200,
                  "Width": 240,
                  "Height": 180,
                  "Color": "#FEF08A"
                }
              ],
              "Groups": [],
              "BreakpointNodeIds": [ "src" ],
              "DisabledLoggingNodeIds": []
            }
            """;

        var (byApp, byCore) = ReadBothWays(oldFile);

        AssertReadWholeWorkflow(byApp, "el dialecto anterior tiene que llegar entero", name: "Flujo antiguo");
        AssertReadWholeWorkflow(byCore, "y por el mismo camino con el que el CLI ejecuta ese archivo", name: "Flujo antiguo");
        byCore.Should().BeEquivalentTo(byApp, "el mismo archivo no puede dar dos grafos distintos");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Los dos lectores coinciden
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AFileSavedByTheApp_ShouldBeReadTheSameByBothReaders()
    {
        string json = new WorkflowStorageService().SerializeGraph(SampleGraph());

        var (byApp, byCore) = ReadBothWays(json);

        AssertReadWholeWorkflow(byApp);
        AssertReadWholeWorkflow(byCore, "el CLI y el resolutor de subflujos leen con este");
        byCore.Should().BeEquivalentTo(byApp, "el mismo archivo no puede dar dos grafos distintos");
    }

    [Fact]
    public void AFileWrittenByTheCoreWriter_ShouldBeReadTheSameByBothReaders()
    {
        string json = SampleGraph().ToJson();

        var (byApp, byCore) = ReadBothWays(json);

        AssertReadWholeWorkflow(byApp);
        AssertReadWholeWorkflow(byCore);
        byCore.Should().BeEquivalentTo(byApp, "el mismo archivo no puede dar dos grafos distintos");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Utilidades
    // ─────────────────────────────────────────────────────────────────────────────

    private static (WorkflowGraph ByApp, WorkflowGraph ByCore) ReadBothWays(string json) =>
        (new WorkflowStorageService().DeserializeGraph(json), WorkflowGraph.FromJson(json));

    /// <summary>
    /// Los datos del flujo de ejemplo, comprobados uno a uno en el grafo que devolvió un lector. Se comprueban
    /// en los dos porque es la única forma de que «coinciden» no sea «los dos leyeron lo mismo, aunque sea
    /// nada»: mientras algo esté en juego, la comparación es una comprobación.
    /// </summary>
    private static void AssertReadWholeWorkflow(
        WorkflowGraph graph,
        string? because = null,
        string name = "Flujo de ejemplo")
    {
        var reader = because ?? "el servicio de guardado de la app";

        graph.Name.Should().Be(name, "lo lee {0}", reader);
        graph.GlobalOutputDir.Should().Be(@"C:\Salida");
        graph.Schema.Should().Be(WorkflowFormat.CurrentSchema);
        graph.Nodes.Should().HaveCount(2, "lo lee {0}", reader);

        var source = graph.Nodes.Single(node => node.Id == "src");
        source.NodeTypeName.Should().Be("FileFlow.Plugin.FileSystem.FolderSourceNode");
        source.CustomTitle.Should().Be("Origen");
        source.X.Should().Be(10);
        source.Parameters["ExtensionFilter"].Should().Be("*.jpg",
            "un parámetro de texto es de texto, no un elemento JSON");
        source.Parameters["MaxDepth"].Should().Be(3L, "un número entero vuelve como entero");
        source.Parameters["Recursive"].Should().Be(true, "un booleano vuelve como booleano");
        source.Parameters["SinValor"].Should().BeNull("un parámetro sin valor vuelve sin valor, y no desaparece");

        graph.Edges.Should().ContainSingle().Which.TargetPortName.Should().Be("In");
        graph.BreakpointNodeIds.Should().Contain("src");
        graph.Annotations.Should().ContainSingle().Which.Content.Should().Be("Texto de la nota");
    }

    /// <summary>Un flujo con lo que un archivo lleva de verdad: nodos, aristas, desplazamientos, parámetros de
    /// varios tipos —uno de ellos sin valor—, un punto de interrupción y una nota. La versión no la trae: la
    /// declara quien escribe, que es justo lo que se está comparando.</summary>
    private static WorkflowGraph SampleGraph()
    {
        var graph = new WorkflowGraph
        {
            Name = "Flujo de ejemplo",
            GlobalOutputDir = @"C:\Salida"
        };

        graph.Nodes.Add(new WorkflowNode
        {
            Id = "src",
            NodeTypeName = "FileFlow.Plugin.FileSystem.FolderSourceNode",
            CustomTitle = "Origen",
            X = 10,
            Y = 20,
            Parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["ExtensionFilter"] = "*.jpg",
                ["MaxDepth"] = 3,
                ["Recursive"] = true,
                ["SinValor"] = null
            }
        });

        graph.Nodes.Add(new WorkflowNode
        {
            Id = "sink",
            NodeTypeName = "FileFlow.Plugin.FileSystem.DestinationSinkNode"
        });

        graph.Edges.Add(new WorkflowEdge
        {
            // Con identificador: dos grafos equivalentes tienen que ser <b>el mismo</b> grafo para que
            // comparar sus textos hable de los escritores y no de los identificadores que reparte cada uno.
            Id = "e1",
            SourceNodeId = "src",
            SourcePortName = "Out",
            TargetNodeId = "sink",
            TargetPortName = "In"
        });

        graph.BreakpointNodeIds.Add("src");

        graph.Annotations.Add(new WorkflowAnnotation
        {
            Id = "note",
            Title = "Nota",
            Content = "Texto de la nota",
            X = 100,
            Y = 200
        });

        return graph;
    }
}
