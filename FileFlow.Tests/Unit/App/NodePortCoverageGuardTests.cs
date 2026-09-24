using System;
using System.Collections.Generic;
using System.Linq;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Guardia del <b>censo de puertos</b>: falla cuando un puerto del producto —de rama o del camino feliz— no
/// está declarado en <see cref="NodePortInventory"/>, cuando un asiento apunta a un puerto que el árbol dejó de
/// declarar, cuando el testigo citado no existe, no habla del nodo o no dice lo que su grado promete, y cuando
/// el grado se ha quedado anticuado respecto al suite.
///
/// <para><b>Qué problema resuelve</b>: el inventario del hito 190 vigilaba las ramas —la mitad que nadie
/// ejecuta y donde se esconden los defectos del 188—, pero un puerto del <b>camino feliz</b> sin prueba es el
/// otro extremo del mismo agujero: un nodo que se arrastra al lienzo, se cablea y se ejecuta sin que nada haya
/// recorrido nunca su salida. Aquí la pregunta es la misma para todos los puertos, y el grado de cada asiento
/// no es una opinión: se comprueba sobre el texto del suite con <see cref="PortWitnessIndex"/> (qué casos
/// hablan del nodo, cuáles lo ejecutan, cuál nombra el puerto).</para>
///
/// <para>La política vive en <see cref="NodePortInventory.Audit"/> y se auto-testea aquí con entradas
/// sintéticas: probar que la guardia muerde no debe exigir dejar un nodo sin prueba en el árbol.</para>
/// </summary>
public class NodePortCoverageGuardTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // La guardia sobre el árbol real del repositorio
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EveryPortOfTheProduct_ShouldBeDeclaredInTheCensusWithTheTestThatCoversIt()
    {
        var violations = AuditTheTree();

        violations.Should().BeEmpty(
            "cada puerto de salida que el producto declara o emite tiene que estar en el censo con la prueba que " +
            "lo cubre o con el motivo por el que nadie lo ejecuta. Infracciones: " + string.Join(" | ", violations));
    }

    [Fact]
    public void TheCensus_ShouldCoverEveryPortAndKeepTheGapsFewAndExplained()
    {
        // Si el barrido dejara de ver los nodos, el censo podría «pasar» sin comprobar nada.
        NodePortInventory.Entries.Count(e => e.IsBranch).Should().BeGreaterThanOrEqualTo(20,
            "el producto declara ramas en más de una docena de nodos");
        NodePortInventory.Entries.Count(e => !e.IsBranch).Should().BeGreaterThanOrEqualTo(100,
            "los puertos del camino feliz son la mayoría del censo: es lo que esta guardia vino a vigilar");

        // Un puerto sin nadie que lo ejecute es un hueco y se paga declarándolo: los huecos del censo son un
        // presupuesto. Hoy son cinco, los de los dos únicos nodos que ninguna prueba del suite pone a trabajar;
        // un nodo nuevo sin pruebas tiene que venir con su motivo **y** con esta cifra subida a mano.
        var gaps = NodePortInventory.Entries
            .Where(e => e.Coverage == NodePortInventory.Coverage.WithoutExecution)
            .ToList();

        gaps.Select(g => g.NodeClass).Distinct().Should().BeEquivalentTo(
            ["ForkJoinBarrierNode", "LocalOcrNode"],
            "son los dos únicos nodos del producto cuyos puertos no ejecuta ninguna prueba");
        gaps.Should().HaveCount(5, "cada puerto de esos dos nodos, declarado con su motivo");

        NodePortInventory.Entries.Should().Contain(e => e.Coverage == NodePortInventory.Coverage.ByNamedTest);
        NodePortInventory.Entries.Should().Contain(e => e.Coverage == NodePortInventory.Coverage.ByExecutingTest);
    }

    /// <summary>
    /// Los nodos de puertos calculados no pueden esconder un puerto detrás de un nombre que sólo existe al
    /// materializarlo. Se resuelven sus puertos con el mismo materializador que usa el motor y se auditan como
    /// cualquier otro.
    /// </summary>
    [Fact]
    public void TheNodesWithComputedPorts_ShouldNotHideAnyPortBehindARuntimeName()
    {
        var resolved = ResolvedComputedPorts();

        resolved.Should().HaveCount(DynamicPortResolver.Shapes.Count,
            "cada nodo aplazado tiene que resolverse, no quedarse en la lista de aplazados");

        var declared = NodePortInventory.Entries.Select(e => e.Key).ToHashSet(StringComparer.Ordinal);

        foreach ((string nodeClass, IReadOnlyList<string> ports) in resolved)
        {
            foreach (string port in ports)
            {
                declared.Should().Contain($"{nodeClass}.{port}",
                    $"'{nodeClass}' declara el puerto '{port}' al materializar sus puertos: es un puerto como " +
                    $"cualquier otro y el censo tiene que declararlo con su testigo o su motivo");
            }
        }
    }

    /// <summary>
    /// Cada forma declarada de un nodo aplazado resuelve puertos de verdad y cita pruebas que existen: la tabla
    /// no puede ser un recuerdo.
    /// </summary>
    [Fact]
    public void EveryComputedPortShape_ShouldResolveItsPortsAndNameRealTests()
    {
        var testMethodNames = TestSuiteIndex.MethodNames(TestRepositoryLocator.RepositoryRoot());

        foreach (var shape in DynamicPortResolver.Shapes)
        {
            DynamicPortResolver.DeclaredOutputs(shape.Create, shape.Parameters)
                .Should().NotBeEmpty($"'{shape.NodeClass}' tiene que exponer puertos con su configuración representativa");

            shape.CoveredBy.Should().NotBeEmpty($"'{shape.NodeClass}' no puede aplazarse sin prueba que lo ejecute");

            foreach (string testName in shape.CoveredBy)
            {
                testMethodNames.Should().Contain(testName,
                    $"'{shape.NodeClass}' cita la prueba '{testName}', que tiene que existir en el suite");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Auto-tests del auditor
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Audit_ShouldFlagAPortOfTheHappyPathThatTheCensusIgnores()
    {
        var violations = Audit(ports: [("NodoDePrueba", "Out")]);

        violations.Should().ContainSingle().Which.Should().Contain("NodoDePrueba").And.Contain("Out")
            .And.Contain("camino feliz");
    }

    [Fact]
    public void Audit_ShouldFlagABranchPortThatTheCensusIgnores()
    {
        var violations = Audit(ports: [("NodoDePrueba", "Error")]);

        violations.Should().ContainSingle().Which.Should().Contain("NodoDePrueba").And.Contain("Error")
            .And.Contain("hito 188");
    }

    [Fact]
    public void Audit_ShouldFlagAnEntryWhosePortTheTreeNoLongerDeclares()
    {
        var violations = Audit(
            ports: [("NodoDePrueba", "Out")],
            witnesses: Witnesses(("NodoDePrueba", [("UnaPrueba", OutPortBlock), ("OtraPrueba", ErrorPortBlock)])),
            entries:
            [
                Entry("NodoDePrueba", "Out", NamedTest, "UnaPrueba"),
                Entry("NodoDePrueba", "Error", NamedTest, "OtraPrueba")
            ]);

        violations.Should().ContainSingle().Which.Should().Contain("ya no declara ni emite");
    }

    [Fact]
    public void Audit_ShouldFlagAWitnessThatDoesNotExistInTheSuite()
    {
        var violations = Audit(
            ports: [("NodoDePrueba", "Out")],
            entries: [Entry("NodoDePrueba", "Out", NamedTest, "PruebaInventada")]);

        violations.Should().ContainSingle().Which.Should().Contain("PruebaInventada").And.Contain("no existe");
    }

    [Fact]
    public void Audit_ShouldFlagAWitnessThatDoesNotTalkAboutTheNode()
    {
        var violations = Audit(
            ports: [("NodoDePrueba", "Out")],
            witnesses: Witnesses(("NodoDePrueba", [("UnaPrueba", """await AssertSomething();""")])),
            entries: [Entry("NodoDePrueba", "Out", NamedTest, "UnaPrueba")]);

        violations.Should().ContainSingle().Which.Should().Contain("no habla de 'NodoDePrueba'");
    }

    [Fact]
    public void Audit_ShouldFlagANamedTestThatDoesNotNameThePort()
    {
        var violations = Audit(
            ports: [("NodoDePrueba", "Out")],
            witnesses: Witnesses(("NodoDePrueba", [("UnaPrueba", """var node = new NodoDePrueba(); await node.ExecuteAsync();""")])),
            entries: [Entry("NodoDePrueba", "Out", NamedTest, "UnaPrueba")]);

        violations.Should().ContainSingle().Which.Should().Contain("no nombra").And.Contain("ByExecutingTest");
    }

    [Fact]
    public void Audit_ShouldFlagAnExecutingGradeWhenNobodyExecutesTheNode()
    {
        var violations = Audit(
            ports: [("NodoDePrueba", "Out")],
            witnesses: Witnesses(("NodoDePrueba", [("UnaPrueba", """var tipos = new[] { typeof(NodoDePrueba) };""")])),
            entries: [Entry("NodoDePrueba", "Out", ExecutingTest, "UnaPrueba")]);

        violations.Should().ContainSingle().Which.Should().Contain("no hay ningún caso que ejecute")
            .And.Contain("WithoutExecution");
    }

    [Fact]
    public void Audit_ShouldFlagAnExecutingGradeThatTheSuiteAlreadyOutgrew()
    {
        var violations = Audit(
            ports: [("NodoDePrueba", "Out")],
            witnesses: Witnesses(("NodoDePrueba",
            [
                ("UnaPrueba", """var node = new NodoDePrueba(); await node.ExecuteAsync();"""),
                ("OtraPrueba", """var node = new NodoDePrueba(); await node.ExecuteAsync(); Assert.Equal("Out", port);""")
            ])),
            entries: [Entry("NodoDePrueba", "Out", ExecutingTest, "UnaPrueba")]);

        violations.Should().ContainSingle().Which.Should().Contain("ya tiene un caso que nombra el puerto")
            .And.Contain("OtraPrueba").And.Contain("ByNamedTest");
    }

    [Fact]
    public void Audit_ShouldFlagAGapWhoseReasonDoesNotExplainItself()
    {
        var violations = Audit(
            ports: [("NodoDePrueba", "Out")],
            entries: [Entry("NodoDePrueba", "Out", WithoutExecution, "no se puede")]);

        violations.Should().ContainSingle().Which.Should().Contain("sin explicar por qué");
    }

    [Fact]
    public void Audit_ShouldFlagAGapInANodeThatTheSuiteDoesExecute()
    {
        var violations = Audit(
            ports: [("NodoDePrueba", "Out")],
            witnesses: Witnesses(("NodoDePrueba", [("UnaPrueba", """var node = new NodoDePrueba(); await node.ExecuteAsync();""")])),
            entries: [Entry("NodoDePrueba", "Out", WithoutExecution, Reason)]);

        violations.Should().ContainSingle().Which.Should().Contain("no se declara hueco");
    }

    [Fact]
    public void Audit_ShouldFlagAPortThatOnlyExistsWhenThePortsAreComputed()
    {
        var violations = Audit(
            ports: [],
            computedPortNodes: [("NodoDePuertosCalculados", (IReadOnlyList<string>)["Case 1", "Default"])]);

        violations.Should().HaveCount(2, "un puerto que sólo existe al materializar los puertos no puede ser invisible al censo");
    }

    [Fact]
    public void Audit_ShouldFlagARepeatedEntry()
    {
        var violations = Audit(
            ports: [("NodoDePrueba", "Out")],
            witnesses: Witnesses(("NodoDePrueba", [("UnaPrueba", OutPortBlock)])),
            entries:
            [
                Entry("NodoDePrueba", "Out", NamedTest, "UnaPrueba"),
                Entry("NodoDePrueba", "Out", NamedTest, "UnaPrueba")
            ]);

        violations.Should().ContainSingle().Which.Should().Contain("repite la entrada");
    }

    /// <summary>
    /// El control positivo: un censo que declara los tres grados —un puerto nombrado, uno bajo un nodo probado y
    /// un hueco explicado— pasa sin una sola infracción. Es lo que demuestra que la guardia no es
    /// «todo rojo».
    /// </summary>
    [Fact]
    public void Audit_ShouldAcceptACensusThatDeclaresEveryGradeOfCoverage()
    {
        var violations = Audit(
            ports: [("NodoDePrueba", "Out"), ("NodoDePrueba", "Error"), ("OtroNodo", "Done"), ("TercerNodo", "Out")],
            witnesses: Witnesses(
                ("NodoDePrueba", [("UnaPrueba", """var node = new NodoDePrueba(); await node.ExecuteAsync(); Assert.Equal("Out", port);""")]),
                ("OtroNodo", [("OtraPrueba", """var node = new OtroNodo(); await node.ExecuteAsync();""")]),
                ("TercerNodo", [("Confusa", """var tipos = new[] { typeof(TercerNodo) };""")])),
            entries:
            [
                Entry("NodoDePrueba", "Out", NamedTest, "UnaPrueba"),
                Entry("NodoDePrueba", "Error", ExecutingTest, "UnaPrueba"),
                Entry("OtroNodo", "Done", ExecutingTest, "OtraPrueba"),
                Entry("TercerNodo", "Out", WithoutExecution, Reason)
            ]);

        violations.Should().BeEmpty(
            "los tres grados, bien declarados, son el censo cumplido: el auditar no puede exigir siempre más de " +
            "lo que el suite tiene");
    }

    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Un caso que habla del nodo y dice por dónde sale el ítem: el testigo de grado máximo.</summary>
    private const string OutPortBlock =
        """var node = new NodoDePrueba(); await node.ExecuteAsync(); Assert.Equal("Out", port);""";

    /// <summary>Un caso que habla del nodo y nombra su rama de error.</summary>
    private const string ErrorPortBlock =
        """var node = new NodoDePrueba(); await node.ExecuteAsync(); Assert.Equal("Error", port);""";

    private const string Reason =
        "Ninguna prueba ejecuta este nodo, así que su puerto se declara con este motivo hasta que exista un caso " +
        "que lo ponga a trabajar de verdad.";

    private static NodePortInventory.Coverage NamedTest => NodePortInventory.Coverage.ByNamedTest;
    private static NodePortInventory.Coverage ExecutingTest => NodePortInventory.Coverage.ByExecutingTest;
    private static NodePortInventory.Coverage WithoutExecution => NodePortInventory.Coverage.WithoutExecution;

    private static NodePortInventory.Entry Entry(string node, string port, NodePortInventory.Coverage coverage, string evidence) =>
        new(node, port, coverage, evidence);

    /// <summary>
    /// Un índice de testigos fabricado a mano: cada nodo con los bloques que lo mencionan. Es lo que permite
    /// probar cada regla del auditor sin tocar el árbol real.
    /// </summary>
    private static IReadOnlyDictionary<string, PortWitnessIndex.NodeWitnesses> Witnesses(
        params (string Node, (string Name, string Source)[] Blocks)[] nodes) =>
        nodes.ToDictionary(
            n => n.Node,
            n => new PortWitnessIndex.NodeWitnesses(
                n.Node,
                [.. n.Blocks.Select(b => new TestSuiteIndex.TestBlock(b.Name, $"{n.Node}Tests.cs", b.Source))]),
            StringComparer.Ordinal);

    private static IReadOnlyList<string> Audit(
        IReadOnlyList<(string NodeClass, string Port)> ports,
        IReadOnlyDictionary<string, PortWitnessIndex.NodeWitnesses>? witnesses = null,
        IReadOnlyList<NodePortInventory.Entry>? entries = null,
        IReadOnlyList<(string NodeClass, IReadOnlyList<string> DeclaredPorts)>? computedPortNodes = null) =>
        NodePortInventory.Audit(
            ports,
            witnesses ?? new Dictionary<string, PortWitnessIndex.NodeWitnesses>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal) { "UnaPrueba", "OtraPrueba" },
            entries ?? [],
            computedPortNodes);

    /// <summary>
    /// Audita el árbol real: los puertos que los nodos declaran y emiten (leídos de su código, con el analizador
    /// de puertos), los que calculan en ejecución, los casos del suite que hablan de cada nodo y el censo.
    /// </summary>
    private static IReadOnlyList<string> AuditTheTree()
    {
        string root = TestRepositoryLocator.RepositoryRoot();

        var sweep = NodeEmissionPortAnalyzer.Sweep(SourceTree.Plugins(root));

        var ports = sweep.Reports
            .SelectMany(r => r.DeclaredOutputs.Select(port => (r.Class, Port: port)))
            .Concat(sweep.Reports.SelectMany(r => r.EmittedPorts.Select(port => (r.Class, Port: port))));

        return NodePortInventory.Audit(
            ports,
            PortWitnessIndex.Build(root, NodePortInventory.Entries.Select(e => e.NodeClass).Distinct(StringComparer.Ordinal)),
            TestSuiteIndex.MethodNames(root),
            NodePortInventory.Entries,
            ResolvedComputedPorts());
    }

    /// <summary>
    /// Los puertos que sólo existen al materializarlos: los nodos aplazados por el analizador, resueltos con su
    /// configuración representativa. Sin esto, un puerto calculado sería invisible para el censo.
    /// </summary>
    private static IReadOnlyList<(string NodeClass, IReadOnlyList<string> DeclaredPorts)> ResolvedComputedPorts() =>
        [.. DynamicPortResolver.Shapes.Select(s =>
            (s.NodeClass, (IReadOnlyList<string>)DynamicPortResolver.DeclaredOutputs(s.Create, s.Parameters)))];
}
