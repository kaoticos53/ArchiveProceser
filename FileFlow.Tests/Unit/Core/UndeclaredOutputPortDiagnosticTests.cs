using System.Collections.Concurrent;
using System.IO;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Sdk;
using FileFlow.Sdk.Telemetry;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Core;

/// <summary>
/// Contrato del aviso de <b>puerto no declarado</b>.
///
/// <para>La interfaz sólo puede dibujar cables desde los puertos que un nodo declara, así que un nombre mal
/// escrito en <c>EmitAsync</c> no tiene arista que lo recoja. El motor trataba ese ítem como terminado —sin
/// error, sin log y sin nodos descendentes— y la rama entera quedaba muerta en silencio: le pasó al nodo
/// Fan-Out en producción, que emitía en <c>ItemOut</c> mientras declaraba <c>Out</c>. Estas pruebas fijan que
/// el motor lo diga, y que lo diga <b>una vez</b> por nodo y puerto y no una vez por archivo.</para>
/// </summary>
public class UndeclaredOutputPortDiagnosticTests
{
    private const string UndeclaredPort = "ItemOut";

    /// <summary>Cuenta los ítems que le llegan y los reenvía por su puerto declarado.</summary>
    public class CountingNode : FlowNodeBase
    {
        public static readonly ConcurrentDictionary<string, int> Executions = new();

        public override string Name => "Contador";
        public override string Category => "Test";
        public override string Description => "Nodo de prueba que cuenta los ítems recibidos.";

        public CountingNode()
        {
            Inputs = [new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")];
            Outputs = [new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out")];
        }

        public override Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
        {
            Executions.AddOrUpdate("Contador", 1, static (_, count) => count + 1);
            return context.EmitAsync("Out", item);
        }
    }

    /// <summary>Declara <c>Out</c> y emite por un puerto que no declara: el defecto del Fan-Out.</summary>
    public class EmitsOnUndeclaredPortNode : FlowNodeBase
    {
        public override string Name => "Nodo con puerto no declarado";
        public override string Category => "Test";
        public override string Description => "Emite por un puerto que no declara.";

        public EmitsOnUndeclaredPortNode()
        {
            Inputs = [new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")];
            Outputs = [new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out")];
        }

        public override Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
            => context.EmitAsync(UndeclaredPort, item);
    }

    /// <summary>Control negativo: declara <c>Out</c> y emite por <c>Out</c>.</summary>
    public class EmitsOnDeclaredPortNode : FlowNodeBase
    {
        public override string Name => "Nodo correcto";
        public override string Category => "Test";
        public override string Description => "Emite por el puerto que declara.";

        public EmitsOnDeclaredPortNode()
        {
            Inputs = [new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")];
            Outputs = [new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out")];
        }

        public override Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
            => context.EmitAsync("Out", item);
    }

    [Fact]
    public async Task An_item_emitted_on_an_undeclared_port_should_be_reported_and_should_not_reach_downstream()
    {
        await using var fixture = await FanOutFixture.CreateAsync();

        var warnings = await fixture.RunWithMiddleNodeAsync("EmitsOnUndeclaredPortNode");

        warnings.Should().ContainSingle(
            "tres archivos por el mismo puerto mal escrito son un solo defecto, no tres avisos")
            .Which.Message.Should().Contain(UndeclaredPort,
                "el aviso tiene que nombrar el puerto que el nodo no declara, que es lo que hay que corregir");
        CountingNode.Executions.GetValueOrDefault("Contador").Should().Be(0,
            "el cable declarado existe en el grafo, pero el ítem sale por otro nombre y no llega al contador");
    }

    [Fact]
    public async Task An_item_emitted_on_a_declared_port_should_not_warn_and_should_reach_downstream()
    {
        await using var fixture = await FanOutFixture.CreateAsync();

        var warnings = await fixture.RunWithMiddleNodeAsync("EmitsOnDeclaredPortNode");

        warnings.Should().BeEmpty("el nodo emite por el puerto que declara: no hay nada que avisar");
        CountingNode.Executions.GetValueOrDefault("Contador").Should().Be(FanOutFixture.FileCount);
    }

    /// <summary>
    /// Tres archivos de origen, un nodo intermedio que decide por dónde emite y un contador al final, con el
    /// cable declarado <c>Out → In</c> ya tendido: la misma forma que tenía el flujo roto.
    /// </summary>
    private sealed class FanOutFixture : IAsyncDisposable
    {
        public const int FileCount = 3;

        private readonly string _root;

        private FanOutFixture(string root) => _root = root;

        public static Task<FanOutFixture> CreateAsync()
        {
            string root = Path.Combine(Path.GetTempPath(), "FF_UndeclaredPort_" + Guid.NewGuid().ToString("N"));
            string source = Path.Combine(root, "source");
            Directory.CreateDirectory(source);
            for (int i = 1; i <= FileCount; i++)
            {
                File.WriteAllText(Path.Combine(source, $"archivo{i}.txt"), $"contenido {i}");
            }
            return Task.FromResult(new FanOutFixture(root));
        }

        /// <summary>
        /// Ejecuta el grafo con el nodo intermedio indicado y devuelve los avisos que mencionan el puerto del
        /// defecto, para no depender del idioma con el que el motor componga el texto.
        /// </summary>
        public async Task<IReadOnlyList<StructuredLogRecord>> RunWithMiddleNodeAsync(string middleNodeTypeName)
        {
            CountingNode.Executions.Clear();

            var loader = new PluginLoader();
            loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
            loader.RegisterNodeType<CountingNode>();
            loader.RegisterNodeType<EmitsOnUndeclaredPortNode>();
            loader.RegisterNodeType<EmitsOnDeclaredPortNode>();

            var graph = new WorkflowGraph { Name = "Puerto no declarado" };
            graph.Nodes.Add(new WorkflowNode
            {
                Id = "origen",
                NodeTypeName = "FolderSourceNode",
                Parameters = new Dictionary<string, object?> { ["SourcePath"] = Path.Combine(_root, "source"), ["ExtensionFilter"] = "txt" }
            });
            graph.Nodes.Add(new WorkflowNode { Id = "medio", NodeTypeName = middleNodeTypeName });
            graph.Nodes.Add(new WorkflowNode { Id = "contador", NodeTypeName = "CountingNode" });

            graph.Edges.Add(new WorkflowEdge { SourceNodeId = "origen", SourcePortName = "Out", TargetNodeId = "medio", TargetPortName = "In" });
            graph.Edges.Add(new WorkflowEdge { SourceNodeId = "medio", SourcePortName = "Out", TargetNodeId = "contador", TargetPortName = "In" });

            var executor = new WorkflowExecutor();
            var warnings = new List<StructuredLogRecord>();
            executor.StructuredLogEmitted += record =>
            {
                if (record.Level == LogLevel.Warning && record.Message.Contains(UndeclaredPort, StringComparison.Ordinal))
                {
                    lock (warnings) warnings.Add(record);
                }
            };

            await executor.ExecuteAsync(graph, loader, cancellationToken: CancellationToken.None);

            return warnings;
        }

        public ValueTask DisposeAsync()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
            return ValueTask.CompletedTask;
        }
    }
}
