using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.Archives;
using FileFlow.Plugin.Data;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Hashing;
using FileFlow.Plugin.Images;
using FileFlow.Plugin.Integrations;
using FileFlow.Plugin.Network;
using FileFlow.Sdk;
using FileFlow.Sdk.Services;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// El andamiaje del <b>contrato de ramas</b>: un origen de prueba que emite rutas concretas, un espía que
/// registra por qué puerto le llegó cada ítem, y el motor real con el cableado real en medio.
///
/// <para>La pregunta que responde es la que un contexto simulado no puede responder: <i>¿el motor entrega este
/// ítem a alguien cuando el nodo emite por esta rama?</i> El motor empareja los cables por nombre exacto
/// (<c>{nodo}:{puerto}</c>) y, si no hay arista para el nombre emitido, da el ítem por terminado como si fuera
/// una hoja del grafo —sin error, sin aviso y sin nodo descendente—. Llamar al nodo con un contexto que acepta
/// cualquier nombre no lo ve; por eso la arista de la rama va a una <b>segunda entrada</b> del espía
/// (<c>Branch</c>) y el nombre de esa entrada es la afirmación.</para>
///
/// <para>Vive en TestHelpers porque lo comparten varias clases de prueba —los nodos de archivos, los de puertos
/// calculados y los de visión, que van aparte por el clúster ONNX— y el espía es estático, así que las clases que
/// lo usan tienen que declarar la colección <c>BranchPortHarness</c> para no pisarse la cola; obtener el espía por
/// clase saldría más caro que serializar sólo a quien lo comparte. Las de visión son la excepción: viven en la
/// colección exclusiva <c>OnnxInference</c>, que ya no corre junto a ninguna otra.</para>
/// </summary>
public sealed class BranchPortHarness : IAsyncDisposable
{
    /// <summary>
    /// Origen de prueba: emite una ruta por cada elemento de <c>Paths</c> (separadas por <c>;</c>).
    ///
    /// <para><c>Metadata</c> añade metadatos a cada ítem (pares <c>clave=valor</c> separados por <c>;</c>), que
    /// es cómo se simula un ítem que viene de otro nodo —por ejemplo la sesión que el Fan-In espera—. Las claves
    /// llevan <c>:</c> a diario (<c>Archive:TotalEntries</c>), así que el separador es <c>;</c> y la clave
    /// termina en el primer <c>=</c>.</para>
    /// </summary>
    public sealed class ProbeSourceNode : FlowNodeBase
    {
        /// <summary>
        /// Servicio de subflujos del arranque en curso. Va en el <b>ítem</b> y no en el estático global
        /// (<c>ISubflowExecutionService.Instance</c>) porque ese estático lo reescribe cualquier otra ejecución
        /// del proceso al empezar: un contenedor de subflujo que dependiera de él podría acabar usando el
        /// cargador de la prueba vecina. Es el camino que el propio producto documenta para desacoplar el nodo del
        /// servicio, y deja la prueba sin estado compartido del que dependa su resultado.
        /// </summary>
        public static ISubflowExecutionService? SubflowService;

        public override string Name => "Origen de prueba";
        public override string Category => "Testing";
        public override string Description => "Emite las rutas indicadas en el parámetro Paths.";

        public ProbeSourceNode() =>
            Outputs = [new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out")];

        public override async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
        {
            foreach (string path in GetParameter("Paths", string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var emitted = new FileItemContext(path.Trim(), isDirectory: false);

                foreach (string pair in GetParameter("Metadata", string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    int separator = pair.IndexOf('=');
                    if (separator > 0) emitted.Metadata[pair[..separator]] = pair[(separator + 1)..];
                }

                if (SubflowService is not null) emitted.Metadata["__SubflowExecutionService__"] = SubflowService;

                await context.EmitAsync("Out", emitted);
            }
        }
    }

    /// <summary>
    /// Espía: registra por qué entrada le llegó cada ítem. Dos entradas declaradas (<c>In</c> y <c>Branch</c>)
    /// permiten tender el camino feliz por una y la rama por la otra, y leer la respuesta del motor.
    /// </summary>
    public sealed class ProbeSinkNode : FlowNodeBase
    {
        public static readonly ConcurrentQueue<(string Branch, FileItemContext Item)> Received = new();

        public override string Name => "Espía de ramas";
        public override string Category => "Testing";
        public override string Description => "Registra por qué puerto de entrada le llega cada ítem.";

        public ProbeSinkNode()
        {
            Inputs =
            [
                new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In"),
                new NodePort("Branch", typeof(FileItemContext), PortDirection.Input, "Branch")
            ];
        }

        public override Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
        {
            Received.Enqueue((inputPortName, item));
            return Task.CompletedTask;
        }
    }

    private BranchPortHarness(string root) => Root = root;

    /// <summary>Carpeta temporal de la ejecución, que es también el directorio temporal del motor.</summary>
    public string Root { get; }

    /// <summary>Carpeta de trabajo del Fan-Out, dentro de la raíz.</summary>
    public string WorkingRoot => Path.Combine(Root, "temp");

    /// <summary>
    /// Crea una ejecución aislada con su carpeta temporal y el espía vacío. Vaciar el espía es lo que ata la
    /// prueba a lo que reciba ella: si dos clases lo compartieran a la vez, la segunda limpieza se llevaría por
    /// delante los ítems de la primera (las colecciones están justo para que eso no pase).
    /// </summary>
    public static Task<BranchPortHarness> CreateAsync()
    {
        ProbeSinkNode.Received.Clear();

        string root = Path.Combine(Path.GetTempPath(), "FF_BranchPort_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return Task.FromResult(new BranchPortHarness(root));
    }

    /// <summary>Escribe un archivo en la raíz de la ejecución y devuelve su ruta.</summary>
    public string WriteFile(string name, string content)
    {
        string path = Path.Combine(Root, name);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>
    /// Ejecuta <c>origen → nodo</c> con el camino feliz (<c>happyPort → In</c>) y la rama
    /// (<c>branchPort → Branch</c>) tendidos, y devuelve lo que llegó al espía.
    /// </summary>
    /// <param name="happyPort">
    /// Puerto del camino feliz que también se tiende hacia el espía. <c>null</c> para no tenderlo: un nodo cuyos
    /// únicos puertos son calculados no tiene por qué declarar el genérico <c>Out</c>, y una arista desde un
    /// puerto que no existe es un cable inventado por la prueba.
    /// </param>
    /// <param name="extraAssemblies">
    /// Ensamblados de plugin que hay que registrar además de los habituales: la rama vive en el plugin del nodo,
    /// y registrarlos todos por defecto ataría esta prueba a que sigan existiendo.
    /// </param>
    public async Task<IReadOnlyList<(string Branch, FileItemContext Item)>> RunAsync(
        IReadOnlyList<string> sourcePaths,
        string nodeTypeName,
        Dictionary<string, object?> nodeParameters,
        string branchPort,
        string? happyPort = "Out",
        string? itemMetadata = null,
        IReadOnlyList<Assembly>? extraAssemblies = null)
    {
        Directory.CreateDirectory(WorkingRoot);

        var loader = new PluginLoader();
        RegisterProductNodes(loader);
        foreach (Assembly assembly in extraAssemblies ?? []) loader.RegisterNodeTypesFromAssembly(assembly);
        loader.RegisterNodeType<ProbeSourceNode>();
        loader.RegisterNodeType<ProbeSinkNode>();

        ProbeSourceNode.SubflowService = new WorkflowSubflowExecutionService(loader);

        var graph = new WorkflowGraph { Name = "Contrato de ramas" };
        graph.Nodes.Add(new WorkflowNode
        {
            Id = "origen",
            NodeTypeName = nameof(ProbeSourceNode),
            Parameters = new Dictionary<string, object?>
            {
                ["Paths"] = string.Join(';', sourcePaths),
                ["Metadata"] = itemMetadata ?? string.Empty
            }
        });
        graph.Nodes.Add(new WorkflowNode { Id = "nodo", NodeTypeName = nodeTypeName, Parameters = nodeParameters });
        graph.Nodes.Add(new WorkflowNode { Id = "espia", NodeTypeName = nameof(ProbeSinkNode) });

        graph.Edges.Add(new WorkflowEdge { SourceNodeId = "origen", SourcePortName = "Out", TargetNodeId = "nodo", TargetPortName = "In" });
        if (happyPort is not null)
        {
            graph.Edges.Add(new WorkflowEdge { SourceNodeId = "nodo", SourcePortName = happyPort, TargetNodeId = "espia", TargetPortName = "In" });
        }

        graph.Edges.Add(new WorkflowEdge { SourceNodeId = "nodo", SourcePortName = branchPort, TargetNodeId = "espia", TargetPortName = "Branch" });

        var executor = new WorkflowExecutor { TemporaryDirectory = WorkingRoot, GlobalOutputDir = Root };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));

        await executor.ExecuteAsync(graph, loader, cancellationToken: timeout.Token);

        return [.. ProbeSinkNode.Received];
    }

    /// <summary>
    /// Los nodos del producto, un ensamblado por plugin: las ramas están repartidas por todos ellos y registrarlos
    /// completos evita que añadir un caso obligue a tocar el cargador.
    /// </summary>
    private static void RegisterProductNodes(PluginLoader loader)
    {
        loader.RegisterNodeTypesFromAssembly(typeof(ArchiveFanOutNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(SqliteDatabaseSinkNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(AdvancedRenamerNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(HashCalculatorNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(ImageOptimizerNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(NetworkUploadNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(CliExecutionNode).Assembly);
    }

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(Root))
        {
            try { Directory.Delete(Root, true); } catch { /* mejor esfuerzo: la carpeta es temporal */ }
        }

        return ValueTask.CompletedTask;
    }
}
