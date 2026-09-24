using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Guardia de los <b>nombres de puerto</b> de los nodos del producto: falla cuando un nodo emite por un puerto
/// que no declara.
///
/// <para>Es la mitad que el aviso del motor no puede dar. El aviso cuenta el defecto <b>cuando la rama se
/// ejecuta</b>, así que una rama de error o de omitido mal escrita puede quedarse ahí durante meses sin que
/// nadie la recorra; esto la señala leyendo el código. El defecto original (hito 188) vivía en el camino feliz
/// del nodo Fan-Out y costó un flujo entero que terminaba en verde sin ejecutar dos de sus cuatro nodos.</para>
///
/// <para>La política vive en <see cref="NodeEmissionPortAnalyzer"/> y se auto-testea con fragmentos: probar que
/// la guardia detecta un infractor no debe exigir dejar un nodo infractor en el árbol.</para>
/// </summary>
public class NodeEmissionPortGuardTests
{
    /// <summary>
    /// Nodos cuyos puertos de salida se calculan en código, así que sus nombres no están en el texto y no se
    /// pueden juzgar leyéndolo. Están aquí <b>uno por uno y con su motivo</b>: si aparece un cuarto, esta prueba
    /// se cae y obliga a mirarlo a mano en lugar de dejarlo fuera en silencio.
    ///
    /// <para>La tabla vive en <see cref="DynamicPortResolver.Shapes"/>, que además guarda la configuración con la
    /// que se materializan sus puertos y <b>las pruebas que los ejecutan</b>: aplazar un nodo ya no es dejarlo sin
    /// mirar, es resolverlo en ejecución y decir dónde se cubre (ver
    /// <c>ComputedPortContractIntegrationTests</c>).</para>
    /// </summary>
    private static IReadOnlyDictionary<string, string> NodesWithComputedPorts { get; } =
        DynamicPortResolver.Shapes.ToDictionary(s => s.NodeClass, s => s.Configuration, StringComparer.Ordinal);

    /// <summary>Ficheros que el barrido tiene que ver: si deja de verlos, pasaría en verde sin comprobar nada.</summary>
    private static readonly string[] KnownNodeFiles =
    [
        "FileFlow.Plugin.Archives/ArchiveFanOutNode.cs",
        "FileFlow.Plugin.Data/Nodes/Exporters/SqliteDatabaseSinkNode.cs",
        "FileFlow.Plugin.FileSystem/Nodes/Processing/AdvancedRenamerNode.cs"
    ];

    // ─────────────────────────────────────────────────────────────────────────────
    // La guardia sobre el árbol real del repositorio
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void NoNode_ShouldEmitOnAPortItDoesNotDeclare()
    {
        var sweep = NodeEmissionPortAnalyzer.Sweep(PluginSources(TestRepositoryLocator.RepositoryRoot()));

        sweep.Violations.Should().BeEmpty(
            "un puerto que el nodo no declara no tiene arista que lo recoja, y el motor da por terminado el " +
            "ítem sin error ni nodo descendente. Infracciones: " + string.Join(" | ", sweep.Violations));
    }

    [Fact]
    public void TheSweep_ShouldCoverEveryFileThatDeclaresANode()
    {
        var sources = PluginSources(TestRepositoryLocator.RepositoryRoot()).ToList();
        var sweep = NodeEmissionPortAnalyzer.Sweep(sources);

        foreach (string known in KnownNodeFiles)
        {
            sweep.SweptFiles.Should().Contain(known, $"el barrido de puertos debe cubrir '{known}'");
        }

        // La cobertura se afirma contra los propios nodos, no contra un número: un fichero que declara un nodo
        // del producto tiene que aparecer juzgado o aplazado. Los nodos de IA heredan de `AiFlowNodeBase` y con
        // la base directa se quedaban fuera del barrido entero, con la guardia en verde.
        var filesWithNodes = sources.Where(s => s.Source.Contains("[NodeDefinition(", StringComparison.Ordinal))
            .Select(s => s.File)
            .ToList();

        filesWithNodes.Should().NotBeEmpty();
        foreach (string file in filesWithNodes)
        {
            sweep.SweptFiles.Should().Contain(file, $"'{file}' declara nodos y el barrido tiene que verlos");
        }

        sweep.Reports.Should().HaveCountGreaterThan(60, "el barrido cubre los nodos de los plugins, no un puñado");
    }

    [Fact]
    public void TheSweep_ShouldNotHaveBlindSpots()
    {
        var sweep = NodeEmissionPortAnalyzer.Sweep(PluginSources(TestRepositoryLocator.RepositoryRoot()));

        sweep.UndecidedClasses.Select(u => u.Describe()).Should().BeEmpty(
            "una clase que emite con una base de fuera del árbol se queda sin juzgar: o se resuelve su base, o se " +
            "declara aquí por qué no se puede");
    }

    [Fact]
    public void TheNodesThatCannotBeJudged_ShouldBeExactlyTheOnesWithComputedPorts()
    {
        var sweep = NodeEmissionPortAnalyzer.Sweep(PluginSources(TestRepositoryLocator.RepositoryRoot()));

        var deferred = sweep.DeferredClasses.Select(d => d.Class).OrderBy(n => n, StringComparer.Ordinal).ToList();

        deferred.Should().Equal(
            NodesWithComputedPorts.Keys.OrderBy(n => n, StringComparer.Ordinal),
            "aplazar un nodo sin decirlo es dejar una comprobación a medias; motivos: " +
            string.Join("; ", NodesWithComputedPorts.Select(kv => $"{kv.Key} → {kv.Value}")));
    }

    /// <summary>
    /// Cada nodo aplazado cita la prueba que lo ejecuta, y esa prueba tiene que existir: un aplazamiento con una
    /// referencia muerta vuelve a ser un punto ciego, esta vez con apariencia de estar cubierto.
    /// </summary>
    [Fact]
    public void EveryDeferredNode_ShouldNameTestsThatExist()
    {
        var testMethodNames = TestSuiteIndex.MethodNames(TestRepositoryLocator.RepositoryRoot());

        DynamicPortResolver.Shapes.Should().NotBeEmpty();

        foreach (var shape in DynamicPortResolver.Shapes)
        {
            shape.CoveredBy.Should().NotBeEmpty(
                $"'{shape.NodeClass}' se aplaza porque sus puertos no están en el texto: ejecutarlo es la única " +
                $"forma de juzgarlo, así que tiene que citar la prueba que lo hace");

            foreach (string testName in shape.CoveredBy)
            {
                testMethodNames.Should().Contain(testName,
                    $"'{shape.NodeClass}' cita '{testName}', que no existe en el suite");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // El analizador, con fragmentos
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheAnalyzer_ShouldDetectAPortThatIsEmittedButNotDeclared()
    {
        var sweep = Sweep(
            """
            public sealed class FanOutDePrueba : FlowNodeBase
            {
                public FanOutDePrueba()
                {
                    Outputs = [new("Out", typeof(FileItemContext), PortDirection.Output, "Out")];
                }

                public override async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken ct)
                {
                    await context.EmitAsync("ItemOut", item);
                }
            }
            """);

        sweep.Violations.Should().ContainSingle()
            .Which.Should().Contain("ItemOut", "el aviso tiene que nombrar el puerto que no se declara");
        sweep.Reports.Single().DeclaredOutputs.Should().Equal("Out");
    }

    [Fact]
    public void TheAnalyzer_ShouldAcceptTheSecondPortWhenItIsDeclared()
    {
        Sweep(
            """
            public sealed class NodoDePrueba : FlowNodeBase
            {
                public NodoDePrueba()
                {
                    Outputs =
                    [
                        new("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
                        new("Error", typeof(FileItemContext), PortDirection.Output, "Error")
                    ];
                }

                public override async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken ct)
                {
                    await context.EmitAsync("Error", item);
                }
            }
            """).Violations.Should().BeEmpty();
    }

    [Fact]
    public void TheAnalyzer_ShouldResolveTheWellKnownPortsConstantsOnBothSides()
    {
        Sweep(
            """
            public sealed class NodoDePrueba : FlowNodeBase
            {
                public NodoDePrueba()
                {
                    Outputs = [new NodePort(WellKnownPorts.Out, typeof(FileItemContext), PortDirection.Output, WellKnownPorts.Out)];
                }

                public override async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken ct)
                {
                    await context.EmitAsync(WellKnownPorts.Out, item);
                }
            }
            """).Violations.Should().BeEmpty();
    }

    [Fact]
    public void TheAnalyzer_ShouldSeeAPortInheritedFromABaseClassOfTheSameTree()
    {
        var sweep = NodeEmissionPortAnalyzer.Sweep(
        [
            ("base.cs",
             """
             public abstract class BaseDePrueba : FlowNodeBase
             {
                 protected BaseDePrueba()
                 {
                     Outputs = [new("Skipped", typeof(FileItemContext), PortDirection.Output, "Skipped")];
                 }
             }
             """),
            ("hijo.cs",
             """
             public sealed class HijoDePrueba : BaseDePrueba
             {
                 public override async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken ct)
                 {
                     await context.EmitAsync("Skipped", item);
                 }
             }
             """)
        ]);

        sweep.Violations.Should().BeEmpty("el puerto lo declara su base, y el nodo lo hereda");
        sweep.Reports.Single(r => r.Class == "HijoDePrueba").DeclaredOutputs.Should().Contain("Skipped");
    }

    [Fact]
    public void TheAnalyzer_ShouldDeferTheNodesThatComputeTheirPorts()
    {
        var sweep = NodeEmissionPortAnalyzer.Sweep(
        [
            ("dinamico.cs",
             """
             public sealed class NodoDinamico : FlowNodeBase
             {
                 protected override IReadOnlyList<NodePort> BuildOutputPorts()
                 {
                     return [new NodePort(NombreDeParametro, typeof(FileItemContext), PortDirection.Output, NombreDeParametro)];
                 }

                 public override async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken ct)
                 {
                     await context.EmitAsync("Cualquiera", item);
                 }
             }
             """)
        ]);

        sweep.Violations.Should().BeEmpty("sus puertos no están en el texto: juzgarlo daría un infractor falso");
        sweep.DeferredClasses.Should().ContainSingle().Which.Class.Should().Be("NodoDinamico");
    }

    [Fact]
    public void TheAnalyzer_ShouldIgnoreWhatIsWrittenInAComment()
    {
        Sweep(
            """
            public sealed class NodoDePrueba : FlowNodeBase
            {
                public NodoDePrueba()
                {
                    Outputs = [new("Out", typeof(FileItemContext), PortDirection.Output, "Out")];
                }

                // Antes esto era await context.EmitAsync("ItemOut", item); y cortaba la rama en silencio
                public override async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken ct)
                {
                    await context.EmitAsync("Out", item);
                }
            }
            """).Violations.Should().BeEmpty(
            "el comentario que explica el defecto no puede leerse como el defecto");
    }

    [Fact]
    public void TheAnalyzer_ShouldReportAClassThatLooksLikeANodeButHasNoResolvableBase()
    {
        var sweep = Sweep(
            """
            public sealed class NodoDeFuera : BaseDesconocida
            {
                public NodoDeFuera()
                {
                    Outputs = [new("Out", typeof(FileItemContext), PortDirection.Output, "Out")];
                }

                public override async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken ct)
                {
                    await context.EmitAsync("Out", item);
                }
            }
            """);

        sweep.Violations.Should().BeEmpty("no se puede juzgar lo que no se puede resolver");
        sweep.UndecidedClasses.Should().ContainSingle()
            .Which.Describe().Should().Contain("NodoDeFuera").And.Contain("BaseDesconocida",
                "una clase que emite con una base de fuera del árbol es un punto ciego, y se dice");
    }

    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Barre un único fragmento: los nodos del producto usan la misma ruta que esta guardia.</summary>
    private static NodeEmissionPortAnalyzer.SweepResult Sweep(string source) =>
        NodeEmissionPortAnalyzer.Sweep([("fragmento.cs", source)]);

    [Fact]
    public void TheAnalyzer_ShouldNotFlagAHelperThatEmitsWithoutBeingANode()
    {
        // Las estrategias de transporte y los motores de script reciben un contexto y emiten en su nombre: no son
        // nodos y no declaran puertos. Señalarlos como punto ciego convertiría la guardia en ruido.
        var sweep = Sweep(
            """
            public sealed class EstrategiaDePrueba : IEstrategiaDeTransporte
            {
                public async Task SendAsync(IFlowExecutionContext context, FileItemContext item, CancellationToken ct)
                {
                    await context.EmitAsync("Out", item);
                }
            }
            """);

        sweep.Violations.Should().BeEmpty();
        sweep.UndecidedClasses.Should().BeEmpty();
    }

    /// <summary>Fuentes de los plugins: donde viven los nodos del producto.</summary>
    private static IEnumerable<(string File, string Source)> PluginSources(string root) =>
        SourceTree.Plugins(root);
}
