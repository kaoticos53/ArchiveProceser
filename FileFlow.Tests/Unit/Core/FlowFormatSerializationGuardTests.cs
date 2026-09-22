using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Core;

/// <summary>
/// Guardia del <b>formato del flujo</b>: falla si alguien (des)serializa un <c>WorkflowGraph</c> con opciones
/// propias —o sin declararlas, que es lo mismo con las de por defecto— en lugar de la definición única,
/// señalando el fichero y la línea infractores.
///
/// <para>
/// Por qué existe: hasta la fase 3E había <b>dos</b> escritores del mismo formato con dialectos distintos —la
/// app con los nombres del modelo tal cual y Core en camelCase, los dos declarando <c>v2</c>— y nada fallaba,
/// porque el lector tolerante acepta cualquier caja. Un formato con dos dialectos se lee; lo que no se puede
/// es saber qué se va a escribir. La forma de reintroducir el tercero no es un error de compilación: es una
/// línea que compila, guarda un archivo que sólo entiende su autor, y no la ve nadie hasta que alguien lee
/// ese archivo con otro lector.
/// </para>
///
/// <para>
/// El análisis es sintáctico (Roslyn), no por expresiones regulares: la línea que se reporta es la de la
/// llamada real, y las menciones en comentarios, cadenas o documentación no cuentan —de otro modo la guardia
/// se dispararía con su propia explicación—. La política vive en
/// <see cref="FlowSerializationAnalyzer"/> y aquí se auto-testea con fragmentos: probar que detecta
/// infracciones no debe exigir dejar código infractor en el árbol, aunque hay una prueba que lo planta en un
/// directorio temporal.
/// </para>
/// </summary>
public class FlowFormatSerializationGuardTests
{
    /// <summary>
    /// Ficheros que escriben o leen un flujo, y que por tanto tienen que estar barridos. Si alguien mueve la
    /// escritura a otro sitio y el barrido deja de mirar donde importa, esta lista lo delata.
    /// </summary>
    private static readonly string[] FilesThatWriteOrReadAWorkflow =
    [
        FlowSerializationAnalyzer.CanonicalFile,
        "FileFlow.App/Services/WorkflowStorageService.cs",
        "FileFlow.App/Services/NodeClipboardService.cs",
        "FileFlow.Core/Engine/WorkflowCliRunner.cs",
        "FileFlow.Core/Engine/WorkflowSubflowExecutionService.cs"
    ];

    // ─────────────────────────────────────────────────────────────────────────────
    // La guardia sobre el árbol real del repositorio
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void NoSourceFile_ShouldSerializeAWorkflowWithItsOwnOptions()
    {
        var violations = Sweep(TestRepositoryLocator.RepositoryRoot())
            .Select(violation => violation.ToString())
            .ToList();

        violations.Should().BeEmpty(
            "el formato del flujo se escribe y se lee con una sola definición de opciones: un segundo juego " +
            "—o ninguno, que son las de por defecto— es un dialecto más, y el archivo que salga de ahí no lo " +
            "entiende el resto del producto");
    }

    /// <summary>
    /// Sin esto, un barrido que no encontrase nada pasaría siempre y la guardia sería decorativa.
    /// </summary>
    [Fact]
    public void Sweep_ShouldReachTheFilesThatWriteAndReadAWorkflow()
    {
        string root = TestRepositoryLocator.RepositoryRoot();
        var scanned = SweptFiles(root).ToList();

        scanned.Should().NotBeEmpty("un barrido vacío haría pasar la guardia sin analizar nada");
        scanned.Should().Contain(FilesThatWriteOrReadAWorkflow,
            "el barrido tiene que incluir la definición del formato y los caminos que lo escriben y lo leen");

        // El alcance es el árbol entero, no una lista de proyectos: un fichero nuevo queda bajo la guardia al
        // crearse, sin que nadie tenga que añadirlo a nada.
        scanned.Should().Contain("FileFlow.Tests/Unit/Core/FlowFormatSerializationGuardTests.cs");
        scanned.Should().OnlyContain(path => !path.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
                                          && !path.Contains("/bin/", StringComparison.OrdinalIgnoreCase));
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Auto-tests: debe fallar ante cada infracción y callar ante lo legítimo
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// El caso que la fase 3E encontró de verdad: un lector suelto que no declara opciones. Con las de por
    /// defecto el flujo se lee a medias —los nombres no coinciden y los parámetros vuelven como elementos
    /// JSON—, así que el grafo llega vacío o ajeno y nada avisa.
    /// </summary>
    [Fact]
    public void Analyzer_ShouldFlagAReaderWithoutTheSharedOptions_WithFileAndLine()
    {
        string source = """
            using System.Text.Json;
            using FileFlow.Core.Engine;

            public static class LectorSuelto
            {
                public static WorkflowGraph Read(string json)
                {
                    var graph = JsonSerializer.Deserialize<WorkflowGraph>(json);
                    return graph;
                }
            }
            """;

        var violation = FlowSerializationAnalyzer
            .Analyze("FileFlow.App/Services/LectorSuelto.cs", source)
            .Should().ContainSingle().Subject;

        violation.File.Should().Be("FileFlow.App/Services/LectorSuelto.cs");
        violation.Line.Should().Be(8, "la infracción está en la línea de la llamada");
        violation.Member.Should().Be("Deserialize");
        violation.Rule.Should().Be(FlowSerializationAnalyzer.RuleOwnOptions);
        violation.Reason.Should().Contain("SerializationOptions", "el mensaje debe decir por dónde se hace bien");
    }

    /// <summary>
    /// La otra forma del mismo error: opciones propias, construidas ahí mismo. Se reconoce por el argumento
    /// —que es un flujo declarado en el fichero—, así que también cubre la escritura sin argumento de tipo.
    /// </summary>
    [Fact]
    public void Analyzer_ShouldFlagOwnOptionsNextToAFlowVariable()
    {
        string source = """
            using System.Text.Json;
            using FileFlow.Core.Engine;

            public sealed class EscritorPropio
            {
                public string Write(WorkflowGraph graph)
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    return JsonSerializer.Serialize(graph, options);
                }
            }
            """;

        var violation = FlowSerializationAnalyzer
            .Analyze("FileFlow.App/Services/EscritorPropio.cs", source)
            .Should().ContainSingle().Subject;

        violation.Line.Should().Be(9);
        violation.Member.Should().Be("Serialize");
        violation.Rule.Should().Be(FlowSerializationAnalyzer.RuleOwnOptions);
    }

    /// <summary>
    /// Y la forma que más se parece a la correcta: las opciones propias <b>al lado</b> de la llamada que sí
    /// las usa bien. La guardia mira la llamada, no el fichero, así que no convierte en rojo lo que está bien
    /// hecho por haber algo mal hecho cerca.
    /// </summary>
    [Fact]
    public void Analyzer_ShouldAcceptTheSharedOptions_AndTheWrappers()
    {
        string source = """
            using System.Text.Json;
            using FileFlow.Core.Engine;

            public static class EscritorCorrecto
            {
                public static string Write(WorkflowGraph graph) =>
                    JsonSerializer.Serialize(graph, WorkflowGraph.SerializationOptions);

                public static WorkflowGraph Read(string json) => WorkflowGraph.FromJson(json);

                public static string RoundTrip(WorkflowGraph graph) => graph.ToJson();
            }
            """;

        FlowSerializationAnalyzer.Analyze("FileFlow.App/Services/EscritorCorrecto.cs", source).Should().BeEmpty();
    }

    /// <summary>
    /// El falso positivo que había que evitar: un fichero que lee flujos <b>y</b> escribe otra cosa con sus
    /// propias opciones —el resumen de ejecución del CLI—. Una guardia que obliga a silenciarla en el caso
    /// legítimo deja de avisar en el ilegítimo.
    /// </summary>
    [Fact]
    public void Analyzer_ShouldAcceptOwnOptionsForSomethingThatIsNotAFlow()
    {
        string source = """
            using System.Text.Json;
            using FileFlow.Core.Engine;

            public sealed class Runner
            {
                public string ReadFlow(string path) => WorkflowGraph.FromJson(File.ReadAllText(path)).ToJson();

                public void WriteSummary(RunSummary summary) =>
                    File.WriteAllText("resumen.json", JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
            }
            """;

        FlowSerializationAnalyzer.Analyze("FileFlow.Core/Engine/Runner.cs", source).Should().BeEmpty();
    }

    /// <summary>
    /// Cambiar la instancia compartida no es un dialecto por fichero: es cambiárselo a todo el proceso —y
    /// después del primer uso ni eso, porque las opciones se congelan—.
    /// </summary>
    [Fact]
    public void Analyzer_ShouldFlagAMutationOfTheSharedOptions()
    {
        string source = """
            using FileFlow.Core.Engine;

            public static class Ajustador
            {
                public static void Configure()
                {
                    WorkflowGraph.SerializationOptions.WriteIndented = false;
                }
            }
            """;

        var violation = FlowSerializationAnalyzer
            .Analyze("FileFlow.App/Services/Ajustador.cs", source)
            .Should().ContainSingle().Subject;

        violation.Line.Should().Be(7);
        violation.Rule.Should().Be(FlowSerializationAnalyzer.RuleSharedOptionsMutation);
    }

    /// <summary>
    /// Mutarlas por una llamada es lo mismo que por una asignación, y la llamada que las <b>pasa</b> no es
    /// una mutación: la primera es cambiarle el formato a todo el proceso; la segunda, el camino correcto.
    /// Las dos tienen que leerse igual de bien, porque la regla mira la espina de la expresión y no cualquier
    /// acceso que aparezca dentro.
    /// </summary>
    [Fact]
    public void Analyzer_ShouldTellMutatingTheSharedOptionsApartFromPassingThem()
    {
        string mutating = """
            using FileFlow.Core.Engine;

            public static class Ajustador
            {
                public static void Configure()
                {
                    WorkflowGraph.SerializationOptions.Converters.Add(new JsonStringEnumConverter());
                }
            }
            """;

        var violation = FlowSerializationAnalyzer
            .Analyze("FileFlow.App/Services/Ajustador.cs", mutating)
            .Should().ContainSingle().Subject;

        violation.Line.Should().Be(7);
        violation.Rule.Should().Be(FlowSerializationAnalyzer.RuleSharedOptionsMutation);

        // El falso positivo que la guardia tuvo de verdad: la llamada que pasa las opciones va dentro de
        // otra llamada, y mirar los accesos de todo el árbol convertía en mutación la línea correcta.
        string passing = """
            using System.Text.Json;
            using FileFlow.Core.Engine;

            public sealed class Escritor
            {
                public Task EscribirAsync(Stream stream, WorkflowGraph graph, CancellationToken ct) =>
                    JsonSerializer.SerializeAsync(stream, graph, WorkflowGraph.SerializationOptions, ct).ConfigureAwait(false);
            }
            """;

        FlowSerializationAnalyzer.Analyze("FileFlow.App/Services/Escritor.cs", passing).Should().BeEmpty(
            "pasar las opciones compartidas es el camino correcto, no una mutación");
    }

    /// <summary>
    /// La definición está exenta de la regla de las llamadas —es donde las opciones se construyen y desde
    /// donde se usan—, pero no de la de mutarlas: si la definición las cambiara a mitad, el formato sería el
    /// que hubiera en ese instante.
    /// </summary>
    [Fact]
    public void Analyzer_ShouldExemptTheDefinition_ButNotItsMutations()
    {
        string root = TestRepositoryLocator.RepositoryRoot();
        string canonical = File.ReadAllText(Path.Combine(
            root,
            FlowSerializationAnalyzer.CanonicalFile.Replace('/', Path.DirectorySeparatorChar)));

        FlowSerializationAnalyzer.Analyze(FlowSerializationAnalyzer.CanonicalFile, canonical).Should().BeEmpty(
            "la definición del formato no puede ser una infracción de su propia regla");

        string source = """
            using FileFlow.Core.Engine;

            public static class Ajustador
            {
                public static void Configure(WorkflowGraph.SerializationOptionsHolder holder)
                {
                }
            }
            """;

        FlowSerializationAnalyzer.Analyze("FileFlow.Core/Engine/Ajustador.cs", source).Should().BeEmpty();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // La prueba final: un dialecto plantado hace fallar el mismo barrido que usa la guardia
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Sweep_ShouldFail_WhenAThirdDialectIsPlanted()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "FileFlow_FlowFormatGuard_" + Guid.NewGuid().ToString("N"));
        string directory = Path.Combine(tempRoot, "FileFlow.App", "Services");
        Directory.CreateDirectory(directory);

        try
        {
            // Exactamente la forma que la fase 3E eliminó: leer un flujo con las opciones por defecto.
            string violating = """
                using System.Text.Json;
                using FileFlow.Core.Engine;

                public static class Impostor
                {
                    public static WorkflowGraph Read(string json) =>
                        JsonSerializer.Deserialize<WorkflowGraph>(json);
                }
                """;

            string relative = "FileFlow.App/Services/Impostor.cs";
            File.WriteAllText(Path.Combine(tempRoot, relative.Replace('/', Path.DirectorySeparatorChar)), violating);

            var violation = Sweep(tempRoot).Should().ContainSingle().Subject;

            violation.File.Should().Be(relative, "el mensaje debe señalar el fichero infractor");
            violation.Line.Should().Be(7, "y la línea donde se lee con otras opciones");
            violation.Rule.Should().Be(FlowSerializationAnalyzer.RuleOwnOptions);
        }
        finally
        {
            try { Directory.Delete(tempRoot, true); } catch { /* mejor esfuerzo en el limpiado */ }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Utilidades
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Todos los <c>.cs</c> del árbol menos los artefactos de compilación, que son los únicos que no son
    /// código que alguien escriba.
    /// </summary>
    private static IEnumerable<string> SweptFiles(string root) =>
        Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !PluginSourceLocator.IsBuildArtifact(path))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal);

    /// <summary>Barre el árbol y devuelve las infracciones del formato, cada una con su fichero y su línea.</summary>
    private static IEnumerable<FlowSerializationViolation> Sweep(string root)
    {
        foreach (string relative in SweptFiles(root))
        {
            string full = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

            foreach (var violation in FlowSerializationAnalyzer.Analyze(relative, File.ReadAllText(full)))
            {
                yield return violation;
            }
        }
    }
}
