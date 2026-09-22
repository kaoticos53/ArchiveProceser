using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

/// <summary>
/// Guardia de la <b>jerarquía de nodos</b>: falla cuando un nodo implementa <c>IFlowNode</c> a mano o
/// vuelve a declarar el ciclo de vida (identidad, puertos, parámetros, metadatos) que <c>FlowNodeBase</c>
/// ya aporta, señalando el fichero y la línea infractores.
///
/// Por qué existe: la fase 2D migró los 70 nodos del proyecto a la jerarquía — 52 de los once plugins y
/// 18 del plugin de IA — eliminando ~700 líneas de boilerplate repetido. El patrón antiguo, sin embargo,
/// no es un error de compilación: en diez de los once plugins sólo produce una advertencia de ocultación
/// (<c>CS0108</c>) que nadie mira, y en algunos casos ni siquiera eso. Sin esta guardia, el siguiente nodo
/// añadido a mano reintroduce el boilerplate y nadie se entera hasta que vuelve a haber 70 copias.
///
/// El análisis es sintáctico (Roslyn), no por expresiones regulares, de modo que la línea reportada es la
/// declaración real y no hay falsos positivos por llaves, comentarios o cadenas. La política vive en
/// <see cref="NodeArchitectureAnalyzer"/> y aquí se auto-testea con snippets: probar que la guardia detecta
/// infracciones no debe exigir dejar ficheros infractores en el árbol —aunque hay una prueba que lo hace
/// contra un directorio temporal—.
/// </summary>
public class NodeArchitectureGuardTests
{
    /// <summary>
    /// Un nodo conocido por cada plugin migrado. Si alguien mueve, renombra o desregistra el árbol de un
    /// plugin, el barrido deja de cubrirlo en silencio y esta lista lo delata.
    /// </summary>
    private static readonly string[] KnownNodeFiles =
    [
        "FileFlow.Plugin.AI/Nodes/Vision/ObjectDetectorNode.cs",
        "FileFlow.Plugin.Archives/SmartUnpackNode.cs",
        "FileFlow.Plugin.Data/Nodes/Readers/CsvReaderNode.cs",
        "FileFlow.Plugin.Documents/PdfMergeNode.cs",
        "FileFlow.Plugin.FileSystem/Nodes/Actions/DestinationSinkNode.cs",
        "FileFlow.Plugin.Hashing/HashCalculatorNode.cs",
        "FileFlow.Plugin.Images/ImageOptimizerNode.cs",
        "FileFlow.Plugin.Integrations/CliExecutionNode.cs",
        "FileFlow.Plugin.Logic/ThrottleDelayNode.cs",
        "FileFlow.Plugin.Network/NetworkDownloadNode.cs",
        "FileFlow.Plugin.Scripting/CustomScriptNode.cs",
        "FileFlow.Plugin.Subflows/SubflowNode.cs"
    ];

    // ─────────────────────────────────────────────────────────────────────────────
    // La guardia sobre el árbol real del repositorio
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void NoNode_ShouldImplementIFlowNodeByHand_OrRedeclareTheInheritedLifecycle()
    {
        var violations = Sweep(TestRepositoryLocator.RepositoryRoot())
            .Select(violation => violation.ToString())
            .ToList();

        violations.Should().BeEmpty(
            "todos los nodos deben heredar de FlowNodeBase o de una de sus bases de IA: implementar " +
            "IFlowNode a mano vuelve a duplicar identidad, puertos y parámetros, y redeclarar los miembros " +
            "heredados los oculta sin avisar fuera de FileFlow.Plugin.AI");
    }

    /// <summary>
    /// Sin esto, un barrido que no encontrase nada pasaría siempre y la guardia sería decorativa.
    /// </summary>
    [Fact]
    public void Sweep_ShouldReachEveryPluginOfTheSolution()
    {
        string root = TestRepositoryLocator.RepositoryRoot();

        // El alcance sale de la solución, no del nombre de la carpeta: un plugin nuevo con otro nombre
        // queda bajo la guardia al añadirlo a FileFlow.slnx, sin tocar este test.
        var scannedDirectories = PluginSourceLocator.PluginDirectories(root)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .ToHashSet(StringComparer.Ordinal);

        scannedDirectories.Should().BeEquivalentTo(
            PluginSourceLocator.PluginProjectNames(root),
            "todo proyecto de la solución que no sea app/core/sdk/tests es un plugin y debe quedar barrido");

        var scannedFiles = PluginSourceLocator.NodeSourceFiles(root)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .ToHashSet(StringComparer.Ordinal);

        scannedFiles.Should().NotBeEmpty("un barrido vacío haría pasar la guardia sin analizar ningún nodo");
        scannedFiles.Should().Contain(KnownNodeFiles, "el barrido debe cubrir todos los plugins con nodos");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Auto-tests del analizador: debe fallar ante cada infracción y callar ante lo legítimo
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Analyzer_ShouldFlagANodeImplementingIFlowNodeByHand_WithFileAndLine()
    {
        string source = """
            using FileFlow.Sdk;

            public sealed class LegacyNode : IFlowNode
            {
                public string Id { get; set; } = "";

                public string Name => "Nodo antiguo";
            }
            """;

        var violation = Analyze("FileFlow.Plugin.Legacy/Nodes/LegacyNode.cs", source)
            .Should().ContainSingle().Subject;

        violation.File.Should().Be("FileFlow.Plugin.Legacy/Nodes/LegacyNode.cs");
        violation.Line.Should().Be(3, "la infracción se comete en la línea de la declaración de clase");
        violation.ClassName.Should().Be("LegacyNode");
        violation.Member.Should().Be("IFlowNode");
        violation.Rule.Should().Be(NodeArchitectureAnalyzer.RuleDirectImplementation);
    }

    [Theory]
    [InlineData("public string Id { get; set; } = \"\";", "Id")]
    [InlineData("public Dictionary<string, object?> Parameters { get; } = new();", "Parameters")]
    [InlineData("public IReadOnlyList<NodePort> Inputs { get; } = [];", "Inputs")]
    [InlineData("public IReadOnlyList<NodePort> Outputs { get; } = [];", "Outputs")]
    [InlineData("public override string Name => \"\";", "Name", true)]
    [InlineData("public string Name => \"Nodo\";", "Name")]
    [InlineData("public string Category => \"General\";", "Category")]
    [InlineData("public string Description => \"Descripción\";", "Description")]
    [InlineData("public int MaxConcurrency => 1;", "MaxConcurrency")]
    [InlineData("public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => [];", "ParameterDescriptors")]
    [InlineData("public IReadOnlyList<NodeActionDescriptor> CustomActions => [];", "CustomActions")]
    [InlineData("public Task ExecuteAsync(string port, FileItemContext item, IFlowExecutionContext context, CancellationToken token) => Task.CompletedTask;", "ExecuteAsync")]
    [InlineData("public Task OnWorkflowCompletedAsync(IFlowExecutionContext context, CancellationToken token) => Task.CompletedTask;", "OnWorkflowCompletedAsync")]
    public void Analyzer_ShouldFlagEveryRedeclaredMember_AtItsOwnLine(
        string member,
        string expectedMember,
        bool declaresOverride = false)
    {
        var violations = Analyze("FileFlow.Plugin.New/Nodes/ModernNode.cs", NodeSnippet(member));

        if (declaresOverride)
        {
            violations.Should().BeEmpty("un 'override' explícito es la forma correcta de tocar un miembro heredado");
            return;
        }

        var violation = violations.Should().ContainSingle().Subject;

        violation.Line.Should().Be(5, "el miembro infractor está en la quinta línea del snippet");
        violation.Member.Should().Be(expectedMember);
        violation.Rule.Should().Be(NodeArchitectureAnalyzer.RuleDuplicatedLifecycle);
        violation.Reason.Should().NotBeNullOrWhiteSpace("el mensaje debe explicar cómo corregirlo");
    }

    [Fact]
    public void Analyzer_ShouldFlagTheClass_NotTheBaseDeclaration_WhenABaseIsDefinedInTheSameFile()
    {
        // El cierre de herencia resuelve bases intermedias del propio fichero: sólo el nodo concreto
        // declara el miembro, y la base abstracta no debe atribuirse la línea de su descendiente.
        string source = """
            using FileFlow.Sdk;

            public abstract class ProjectNodeBase : FlowNodeBase
            {
            }

            public sealed class ConcreteNode : ProjectNodeBase
            {
                public string Id { get; set; } = "";
            }
            """;

        var violation = Analyze("FileFlow.Plugin.New/Nodes/ConcreteNode.cs", source)
            .Should().ContainSingle().Subject;

        violation.Line.Should().Be(9);
        violation.ClassName.Should().Be("ConcreteNode");
        violation.Member.Should().Be("Id");
    }

    [Fact]
    public void Analyzer_ShouldAcceptOverriddenMetadataAndDynamicPortHooks()
    {
        // Los nodos que traducen sus metadatos declaran 'override', y los de puertos dinámicos sobrescriben
        // el hook y anuncian el cambio: todo ello es legítimo y no debe convertirse en ruido rojo.
        string source = """
            using FileFlow.Sdk;

            public sealed class ModernNode : FlowNodeBase
            {
                private IReadOnlyList<NodePort> _ports = [];

                public override string Name => "Nodo moderno";
                public override string Category => "General";
                public override string Description => "Descripción";

                protected override IReadOnlyList<NodePort> BuildOutputPorts() => _ports;

                public void SetPorts(IReadOnlyList<NodePort> ports)
                {
                    _ports = ports;
                    NotifyPortsChanged();
                }

                public override Task ExecuteAsync(string port, FileItemContext item, IFlowExecutionContext context, CancellationToken token)
                    => EmitAsync(context, item);
            }
            """;

        Analyze("FileFlow.Plugin.New/Nodes/ModernNode.cs", source).Should().BeEmpty();
    }

    [Theory]
    [InlineData("protected override IReadOnlyList<NodePort> BuildOutputPorts() => [];", "BuildOutputPorts")]
    [InlineData("public void ReplacePorts() { Outputs = []; }", "Outputs")]
    [InlineData("public void ReplacePorts() { this.Inputs = []; }", "Inputs")]
    public void Analyzer_ShouldFlagADynamicPortNodeThatNeverAnnouncesItsTopology(string member, string reported)
    {
        // Un nodo que deriva puertos y no lo anuncia deja al editor con los puertos viejos: los cables que
        // apuntaban a un puerto que ya no existe sobreviven en el grafo y el motor no los vuelve a trazar.
        var violation = Analyze("FileFlow.Plugin.New/Nodes/SilentNode.cs", NodeSnippet(member))
            .Should().ContainSingle().Subject;

        violation.Line.Should().Be(5);
        violation.Member.Should().Be(reported);
        violation.Rule.Should().Be(NodeArchitectureAnalyzer.RuleSilentPortTopology);
        violation.Reason.Should().Contain("NotifyPortsChanged");
    }

    [Fact]
    public void Analyzer_ShouldAcceptFixedPortsAssignedInTheConstructor()
    {
        // La vía de los ~60 nodos con puertos fijos: asignarlos en el constructor no es derivar topología,
        // así que no hay nada que anunciar.
        string source = """
            using FileFlow.Sdk;

            public sealed class FixedPortsNode : FlowNodeBase
            {
                public FixedPortsNode()
                {
                    Inputs = [new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")];
                    Outputs = [new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out")];
                }
            }
            """;

        Analyze("FileFlow.Plugin.New/Nodes/FixedPortsNode.cs", source).Should().BeEmpty();
    }

    [Fact]
    public void Analyzer_ShouldAcceptATopologyAnnouncementDeclaredInAnotherFileOfTheSamePartialClass()
    {
        string withDynamicPorts = """
            using FileFlow.Sdk;

            public partial class SplitNode : FlowNodeBase
            {
                protected override IReadOnlyList<NodePort> BuildOutputPorts() => [];
            }
            """;

        string withAnnouncement = """
            using FileFlow.Sdk;

            public partial class SplitNode
            {
                public void SetPorts()
                {
                    NotifyPortsChanged();
                }
            }
            """;

        NodeArchitectureAnalyzer.TopologyAnnouncingClassNames(withAnnouncement).Should().Contain("SplitNode");

        Analyze("FileFlow.Plugin.Split/Nodes/SplitNode.cs", withDynamicPorts)
            .Should().ContainSingle()
            .Which.Rule.Should().Be(NodeArchitectureAnalyzer.RuleSilentPortTopology);

        NodeArchitectureAnalyzer
            .Analyze("FileFlow.Plugin.Split/Nodes/SplitNode.cs", withDynamicPorts, null, ["SplitNode"])
            .Should().BeEmpty("el anuncio vive en el otro fichero de la clase partial");
    }

    [Fact]
    public void Sweep_ShouldFail_WhenADynamicPortNodeNeverAnnouncesItsTopology()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "FileFlow_SilentPortsGuard_" + Guid.NewGuid().ToString("N"));
        string pluginDirectory = Path.Combine(tempRoot, "FileFlow.Plugin.Silent", "Nodes");
        Directory.CreateDirectory(pluginDirectory);

        try
        {
            string silent = """
                using FileFlow.Sdk;

                public sealed class SilentNode : FlowNodeBase
                {
                    protected override IReadOnlyList<NodePort> BuildOutputPorts() => [];
                }
                """;

            File.WriteAllText(Path.Combine(pluginDirectory, "SilentNode.cs"), silent);

            var violation = Sweep(tempRoot).Should().ContainSingle().Subject;

            violation.File.Should().Be("FileFlow.Plugin.Silent/Nodes/SilentNode.cs");
            violation.Rule.Should().Be(NodeArchitectureAnalyzer.RuleSilentPortTopology);
        }
        finally
        {
            try { Directory.Delete(tempRoot, true); } catch { /* mejor esfuerzo en el limpiado */ }
        }
    }

    [Fact]
    public void Analyzer_ShouldIgnoreNonNodeTypes()
    {
        // Servicios, DTOs e interfaces comparten nombres con el ciclo de vida del nodo pero no son nodos:
        // marcarlos sería un falso positivo que obligaría a silenciar la guardia.
        string source = """
            using System;
            using FileFlow.Sdk;

            namespace FileFlow.Plugin.New;

            public interface ILegacyBoundary : IFlowNode
            {
                string PortNames { get; set; }
            }

            public sealed class PresetDto
            {
                public string Id { get; set; } = Guid.NewGuid().ToString();
                public string Name { get; set; } = string.Empty;
            }

            public abstract class NodeSettings
            {
                public int MaxConcurrency { get; set; } = 4;
            }

            public sealed class Helper
            {
                public string Id => "no soy un nodo";
            }
            """;

        Analyze("FileFlow.Plugin.New/PresetDto.cs", source).Should().BeEmpty();
    }

    [Fact]
    public void Analyzer_ShouldFlagAManualParameterRead_WithItsKeyAndLine()
    {
        // El patrón que la migración a GetParameter<T> eliminó: leer el diccionario a mano porque el
        // ayudante tipado no entendía JsonElement ni los números embebidos en texto.
        string source = """
            using FileFlow.Sdk;

            public sealed class LegacyNode : FlowNodeBase
            {
                public override string Name => "Nodo";
                public override string Category => "General";
                public override string Description => "Descripción";

                public override Task ExecuteAsync(string port, FileItemContext item, IFlowExecutionContext context, CancellationToken token)
                {
                    int delayMs = Parameters.TryGetValue("DelayMilliseconds", out var value) ? ParameterHelper.GetInt32(value, 100) : 100;
                    return Task.CompletedTask;
                }
            }
            """;

        var violation = Analyze("FileFlow.Plugin.New/Nodes/LegacyNode.cs", source)
            .Should().ContainSingle().Subject;

        violation.Line.Should().Be(11, "la infracción está en la línea que lee el parámetro");
        violation.Member.Should().Be("\"DelayMilliseconds\"", "el mensaje debe decir qué parámetro se leyó a mano");
        violation.Rule.Should().Be(NodeArchitectureAnalyzer.RuleManualParameterRead);
    }

    [Fact]
    public void Analyzer_ShouldAcceptTheTypedHelper_PresenceChecksAndWrites()
    {
        // Comprobar presencia (ContainsKey) y escribir parámetros siguen siendo legítimos: la migración
        // los usa para los nombres heredados y las listas.
        string source = """
            using FileFlow.Sdk;

            public sealed class ModernNode : FlowNodeBase
            {
                public ModernNode()
                {
                    SetParameter("Mode", "Auto");
                    Parameters["Legacy"] = null;
                }

                public override string Name => "Nodo";
                public override string Category => "General";
                public override string Description => "Descripción";

                public override Task ExecuteAsync(string port, FileItemContext item, IFlowExecutionContext context, CancellationToken token)
                {
                    int delayMs = GetParameter("DelayMilliseconds", 100);
                    object? steps = GetParameter<object?>("MethodSteps", null);
                    string mode = Parameters.ContainsKey("Mode") ? GetParameter("Mode", "Auto") : "Manual";
                    Parameters.Remove("Legacy");
                    return Task.CompletedTask;
                }
            }
            """;

        Analyze("FileFlow.Plugin.New/Nodes/ModernNode.cs", source).Should().BeEmpty();
    }

    [Fact]
    public void Analyzer_ShouldReportConcreteNodeFullNames_AndSkipAbstractBases()
    {
        // Es la forma que necesita la comparación con el catálogo de runtime: nombre completo como lo
        // compone Type.FullName, resolviendo la herencia del propio fichero y descartando las bases
        // abstractas (que el cargador tampoco registra) y las clases que no son nodos.
        string source = """
            using FileFlow.Sdk;

            namespace FileFlow.Plugin.New.Nodes;

            public abstract class IntermediateBase : FlowNodeBase
            {
            }

            public sealed class ConcreteNode : FlowNodeBase
            {
            }

            public sealed class InheritingNode : IntermediateBase
            {
            }

            public sealed class NotANode
            {
            }
            """;

        NodeArchitectureAnalyzer.ConcreteNodeTypeFullNames(source).Should().BeEquivalentTo(
            "FileFlow.Plugin.New.Nodes.ConcreteNode",
            "FileFlow.Plugin.New.Nodes.InheritingNode");
    }

    [Fact]
    public void Analyzer_ShouldFlagAPartialNode_OnlyWithTheProjectWideNames()
    {
        // Una clase partial puede repartirse: un fichero declara la herencia y otro sólo aporta la
        // propiedad. Sin los nombres del proyecto, el segundo fichero no parece un nodo.
        string withInheritance = """
            using FileFlow.Sdk;

            public partial class SplitNode : FlowNodeBase
            {
            }
            """;

        string withLegacyMember = """
            using FileFlow.Sdk;

            public partial class SplitNode
            {
                public string Id { get; set; } = "fijo";
            }
            """;

        NodeArchitectureAnalyzer.NodeClassNames(withInheritance).Should().Contain("SplitNode");

        Analyze("FileFlow.Plugin.Split/Nodes/SplitNode.Legacy.cs", withLegacyMember)
            .Should().BeEmpty("sin contexto del proyecto, este fichero no declara ser un nodo");

        var violation = NodeArchitectureAnalyzer
            .Analyze("FileFlow.Plugin.Split/Nodes/SplitNode.Legacy.cs", withLegacyMember, ["SplitNode"])
            .Should().ContainSingle().Subject;

        violation.Line.Should().Be(5);
        violation.Member.Should().Be("Id");
        violation.Rule.Should().Be(NodeArchitectureAnalyzer.RuleDuplicatedLifecycle);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // La prueba final: un nodo infractor plantado hace fallar el mismo barrido que usa la guardia
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Sweep_ShouldFail_WhenAViolatingNodeIsPlantedInAPluginTree()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "FileFlow_NodeGuard_" + Guid.NewGuid().ToString("N"));
        string pluginDirectory = Path.Combine(tempRoot, "FileFlow.Plugin.Impostor", "Nodes");
        Directory.CreateDirectory(pluginDirectory);

        try
        {
            // Exactamente el patrón que la fase 2D eliminó: un nodo que se declara el Id a mano.
            string violating = """
                using FileFlow.Sdk;

                public sealed class ImpostorNode : FlowNodeBase
                {
                    public string Id { get; set; } = "fijo";
                }
                """;

            string relative = "FileFlow.Plugin.Impostor/Nodes/ImpostorNode.cs";
            File.WriteAllText(Path.Combine(tempRoot, relative.Replace('/', Path.DirectorySeparatorChar)), violating);

            var violation = Sweep(tempRoot).Should().ContainSingle().Subject;

            violation.File.Should().Be(relative, "el mensaje debe señalar el fichero infractor");
            violation.Line.Should().Be(5, "y la línea donde está la declaración duplicada");
            violation.Rule.Should().Be(NodeArchitectureAnalyzer.RuleDuplicatedLifecycle);
        }
        finally
        {
            try { Directory.Delete(tempRoot, true); } catch { /* mejor esfuerzo en el limpiado */ }
        }
    }

    [Fact]
    public void Sweep_ShouldReportEveryRedeclaredMember_WithItsOwnLine()
    {
        // Los cuatro miembros que la jerarquía ya aporta, plantados a la vez: la guardia debe señalar cada
        // uno con su línea, no sólo el primero.
        string tempRoot = Path.Combine(Path.GetTempPath(), "FileFlow_MemberGuard_" + Guid.NewGuid().ToString("N"));
        string pluginDirectory = Path.Combine(tempRoot, "FileFlow.Plugin.Impostor", "Nodes");
        Directory.CreateDirectory(pluginDirectory);

        try
        {
            File.WriteAllText(
                Path.Combine(pluginDirectory, "ImpostorNode.cs"),
                """
                using FileFlow.Sdk;

                public sealed class ImpostorNode : FlowNodeBase
                {
                    public string Id { get; set; } = "fijo";
                    public Dictionary<string, object?> Parameters { get; } = new();
                    public IReadOnlyList<NodePort> Inputs { get; protected set; } = [];
                    public IReadOnlyList<NodePort> Outputs { get; protected set; } = [];
                }
                """);

            var violations = Sweep(tempRoot).ToList();

            violations.Should().HaveCount(4, "los cuatro miembros heredados se declaran a mano");

            violations.Select(violation => (violation.Line, violation.Member)).Should().Equal(
                (5, "Id"),
                (6, "Parameters"),
                (7, "Inputs"),
                (8, "Outputs"));

            violations.Should().OnlyContain(violation => violation.Rule == NodeArchitectureAnalyzer.RuleDuplicatedLifecycle);
            violations.Should().OnlyContain(violation => violation.File == "FileFlow.Plugin.Impostor/Nodes/ImpostorNode.cs");
        }
        finally
        {
            try { Directory.Delete(tempRoot, true); } catch { /* mejor esfuerzo en el limpiado */ }
        }
    }

    [Fact]
    public void Sweep_ShouldFail_WhenAPartialNodeIsSplitAcrossTwoFiles()
    {
        // La misma mutación que la prueba del fichero plantado, pero repartida: si el barrido no acumulase
        // los nodos del proyecto antes de analizar, este caso pasaría inadvertido.
        string tempRoot = Path.Combine(Path.GetTempPath(), "FileFlow_PartialGuard_" + Guid.NewGuid().ToString("N"));
        string pluginDirectory = Path.Combine(tempRoot, "FileFlow.Plugin.Split", "Nodes");
        Directory.CreateDirectory(pluginDirectory);

        try
        {
            File.WriteAllText(
                Path.Combine(pluginDirectory, "SplitNode.cs"),
                """
                using FileFlow.Sdk;

                public partial class SplitNode : FlowNodeBase
                {
                }
                """);

            File.WriteAllText(
                Path.Combine(pluginDirectory, "SplitNode.Legacy.cs"),
                """
                using FileFlow.Sdk;

                public partial class SplitNode
                {
                    public string Id { get; set; } = "fijo";
                }
                """);

            var violation = Sweep(tempRoot).Should().ContainSingle().Subject;

            violation.File.Should().Be("FileFlow.Plugin.Split/Nodes/SplitNode.Legacy.cs");
            violation.Line.Should().Be(5);
            violation.Member.Should().Be("Id");
        }
        finally
        {
            try { Directory.Delete(tempRoot, true); } catch { /* mejor esfuerzo en el limpiado */ }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // La documentación también es material de partida: el ejemplo que la guardia enseña
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheDocumentedExampleNode_ShouldPassTheSameRulesAsTheRealNodes()
    {
        // El ejemplo es la plantilla que copian los autores de nodos. Si reincidiera en el patrón que la
        // migración eliminó, la guardia estaría enseñando justo lo que prohíbe.
        string root = TestRepositoryLocator.RepositoryRoot();
        string relativePath = "docs/nodes/examples/SampleMultiPortNode.cs";
        string source = File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        Analyze(relativePath, source).Should().BeEmpty();
    }

    [Fact]
    public void TheGuideCodeBlocks_ShouldNotTeachTheRejectedPattern()
    {
        string root = TestRepositoryLocator.RepositoryRoot();
        string guide = File.ReadAllText(Path.Combine(root, "docs", "nodes", "CREATING_NODES.md"));
        var codeBlocks = CSharpCodeBlocks(guide);

        codeBlocks.Should().NotBeEmpty("la guía debe enseñar código, no sólo describirlo");

        string[] rejectedPatterns =
        [
            ": IFlowNode",                                          // implementar el contrato a mano
            "public string Id { get",                               // identidad propia
            "public Dictionary<string, object?> Parameters { get",  // diccionario de parámetros propio
            "public IReadOnlyList<NodePort> Inputs { get",          // puertos como propiedad propia
            "public IReadOnlyList<NodePort> Outputs { get",
            "Parameters.TryGetValue"                                // lectura de parámetro a mano
        ];

        foreach (string block in codeBlocks)
        {
            Analyze("docs/nodes/CREATING_NODES.md", block).Should().BeEmpty(
                "ningún fragmento de la guía puede reintroducir una infracción que la guardia rechaza");

            foreach (string pattern in rejectedPatterns)
            {
                block.Should().NotContain(
                    pattern,
                    "un fragmento con el patrón antiguo es una plantilla para el siguiente autor de nodos");
            }
        }
    }

    /// <summary>Bloques <c>```csharp</c> de un markdown, que es donde vive el código que se copia.</summary>
    private static IReadOnlyList<string> CSharpCodeBlocks(string markdown) =>
        Regex.Matches(markdown, @"```csharp\r?\n(?<code>.*?)```", RegexOptions.Singleline)
            .Select(match => match.Groups["code"].Value)
            .ToList();

    // ─────────────────────────────────────────────────────────────────────────────
    // Utilidades
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Barre los plugins bajo <paramref name="root"/> y devuelve todas las infracciones.
    ///
    /// Se hace en dos pasadas por proyecto: la primera acumula qué clases son nodos en CUALQUIER fichero,
    /// y la segunda analiza cada fichero con ese conocimiento. Sin la primera, una clase <c>partial</c>
    /// cuyo fichero no declara la herencia (el que sólo aporta la propiedad <c>Id</c>) pasaría por no ser
    /// un nodo y su infracción quedaría invisible.
    /// </summary>
    private static IEnumerable<NodeArchitectureViolation> Sweep(string root)
    {
        foreach (string directory in PluginSourceLocator.PluginDirectories(root))
        {
            var sources = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Where(path => !PluginSourceLocator.IsBuildArtifact(path))
                .ToDictionary(path => path, File.ReadAllText, StringComparer.OrdinalIgnoreCase);

            var nodeClassNames = sources.Values
                .SelectMany(NodeArchitectureAnalyzer.NodeClassNames)
                .ToHashSet(StringComparer.Ordinal);

            // Los anuncios también se acumulan por proyecto: una clase partial puede derivar sus puertos en
            // un fichero y anunciar el cambio en el otro.
            var announcingClassNames = sources.Values
                .SelectMany(NodeArchitectureAnalyzer.TopologyAnnouncingClassNames)
                .ToHashSet(StringComparer.Ordinal);

            foreach ((string file, string source) in sources)
            {
                foreach (var violation in NodeArchitectureAnalyzer.Analyze(
                    Path.GetRelativePath(root, file).Replace('\\', '/'),
                    source,
                    nodeClassNames,
                    announcingClassNames))
                {
                    yield return violation;
                }
            }
        }
    }

    /// <summary>
    /// Snippet de un nodo con un único miembro, en una posición de línea estable (la 5) para que el
    /// auto-test pueda exigir la línea exacta que la guardia reporta al usuario.
    /// </summary>
    private static string NodeSnippet(string member) => $$"""
        using FileFlow.Sdk;

        public sealed class SyntheticNode : FlowNodeBase
        {
            {{member}}
        }
        """;

    /// <summary>Analiza un snippet como si viviera en un fichero de nodo convencional.</summary>
    private static IReadOnlyList<NodeArchitectureViolation> Analyze(string relativePath, string source) =>
        NodeArchitectureAnalyzer.Analyze(relativePath, source);
}

