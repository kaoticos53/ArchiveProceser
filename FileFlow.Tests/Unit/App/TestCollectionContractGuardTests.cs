using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Guardia del <b>contrato de colecciones</b> del suite: falla cuando una clase de test toca un estado
/// global de proceso (registros del clúster IA, preferencias reales, sesión headless de Avalonia) sin
/// declarar la colección exclusiva que lo confina.
///
/// Por qué existe: el estado global es invisible para la prueba que lo usa —el fallo aparece en OTRA
/// prueba, más tarde, con un mensaje ajeno—. Fue exactamente la historia del suite paralelo: bindings
/// zombi de capturas ya terminadas fallando dentro de pruebas de localización, y la sesión headless
/// arrancada desde una colección paralela compitiendo con la exclusiva. Este test barre el árbol del
/// repositorio y convierte esas infracciones latentes en un fallo rojo, inmediato y en el fichero
/// culpable, antes de que lleguen a ejecutarse.
///
/// La política (qué estado exige qué colección) vive en <see cref="TestCollectionContractAnalyzer"/> y es
/// la misma que documenta el comentario de <c>TestAssemblyParallelism.cs</c>. La lógica está auto-testeada
/// aquí con snippets sintéticos: probar que la guardia detecta infracciones no debe exigir plantar
/// ficheros infractores en el árbol —aunque hay una prueba que lo hace contra un directorio temporal—.
/// </summary>
[Collection("ThemeTokens")]
public class TestCollectionContractGuardTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // La guardia sobre el árbol real del repositorio
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void NoTestClass_ShouldTouchExclusiveState_WithoutDeclaringItsCollection()
    {
        var violations = new List<string>();

        foreach (string file in EnumerateTestFiles())
        {
            string relative = Path.GetRelativePath(TestRepositoryLocator.RepositoryRoot(), file).Replace('\\', '/');
            string source = File.ReadAllText(file);

            foreach (var violation in TestCollectionContractAnalyzer.Analyze(relative, source))
            {
                violations.Add(
                    $"{relative}: una clase toca {violation.State} sin declarar ninguna colección exclusiva " +
                    $"(canónica para este estado: {violation.CanonicalCollection}). {violation.Reason}");
            }
        }

        violations.Should().BeEmpty(
            "el contrato de colecciones existe para que el suite paralelo sea reproducible: " +
            "cada estado global de proceso sólo puede tocarse desde su colección exclusiva");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Auto-tests de la lógica: la guardia debe fallar ante cada infracción plantada
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Analyzer_ShouldRequireOnnxInference_WhenClassUsesModelSessionRegistry()
    {
        string source = Snippet("[Collection(\"ThemeTokens\")]", "ModelSessionRegistry.ClearAllSessions();");

        var violations = AnalyzeSnippet(source);

        violations.Should().ContainSingle().Which.State.Should().Be(ExclusiveTestState.ModelSessionRegistry);
    }

    [Fact]
    public void Analyzer_ShouldRequireExclusivity_WhenACollectionlessClassUsesOnnxSessionManager()
    {
        // Cualquier colección exclusiva vale (la exclusividad es total), así que la infracción realista
        // es tocar el gestor desde una clase en una colección PARALELA como ThemeTokens.
        string source = Snippet("[Collection(\"ThemeTokens\")]", "var n = OnnxSessionManager.GetLoadedSessionCount();");

        var violations = AnalyzeSnippet(source);

        violations.Should().ContainSingle().Which.State.Should().Be(ExclusiveTestState.OnnxSessionManager);
    }

    [Fact]
    public void Analyzer_ShouldRequireLocalization_WhenClassUsesRealUserPreferences()
    {
        string source = Snippet(null, "UserPreferencesService.Instance.ToggleFavorite(\"X\");");

        var violations = AnalyzeSnippet(source);

        violations.Should().ContainSingle().Which.State.Should().Be(ExclusiveTestState.RealUserPreferences);
    }

    [Fact]
    public void Analyzer_ShouldIgnoreInMemoryPreferencesDouble()
    {
        // El double de TestHelpers es justamente la vía para no tocar el singleton real: no es infracción.
        string source = Snippet(null, "var preferences = new InMemoryUserPreferencesService();");

        AnalyzeSnippet(source).Should().BeEmpty();
    }

    [Fact]
    public void Analyzer_ShouldRequireVisualSnapshots_WhenClassAppliesAThemeFromAParallelCollection()
    {
        // Aplicar un tema desde una colección paralela cambia el tema activo y el diccionario de recursos
        // mientras las capturas headless los están renderizando: el fallo aparece en otra prueba y sólo a veces.
        string source = Snippet("[Collection(\"ThemeTokens\")]", "ThemeManager.Instance.SetTheme(AppTheme.Light);");

        var violations = AnalyzeSnippet(source);

        violations.Should().ContainSingle().Which.State.Should().Be(ExclusiveTestState.ActiveTheme);
    }

    [Fact]
    public void Analyzer_ShouldAcceptApplyingAThemeFromAnExclusiveCollection()
    {
        string source = Snippet("[Collection(VisualSnapshotsCollection.Name)]", "ThemeManager.Instance.SetThemeById(\"dark_fluent\");");

        AnalyzeSnippet(source).Should().BeEmpty();
    }

    [Fact]
    public void Analyzer_ShouldRequireTheExampleFlowBank_WhenClassMovesTheProcessWorkingDirectory()
    {
        // Apuntar el directorio de trabajo a otro sitio desde una colección paralela cambia dónde caen las rutas
        // relativas de las demás, y la sala limpia del banco de ejemplos deja de ser sólo suya.
        string source = Snippet("[Collection(\"ThemeTokens\")]", "Directory.SetCurrentDirectory(Path.GetTempPath());");

        var violations = AnalyzeSnippet(source);

        violations.Should().ContainSingle().Which.State.Should().Be(ExclusiveTestState.ProcessWorkingDirectory);
    }

    [Fact]
    public void Analyzer_ShouldAcceptMovingTheWorkingDirectoryFromTheExampleFlowBank()
    {
        string source = Snippet("[Collection(ExampleFlowBankCollection.Name)]", "Directory.SetCurrentDirectory(room);");

        AnalyzeSnippet(source).Should().BeEmpty();
    }

    [Fact]
    public void Analyzer_ShouldRequireVisualSnapshots_WhenClassUsesTheHeadlessSession()
    {
        string source = Snippet(null, "AvaloniaTestHelper.EnsureInitialized();");

        var violations = AnalyzeSnippet(source);

        violations.Should().ContainSingle().Which.State.Should().Be(ExclusiveTestState.HeadlessUiSession);
    }

    [Theory]
    [InlineData("Dispatcher.UIThread.CheckAccess();")]
    [InlineData("var app = Application.Current;")]
    [InlineData("var fixture = AppVisualFixture.Create();")]
    [InlineData("byte[] png = VisualSnapshot.Capture(() => new TextBlock(), 10, 10, \"dark_fluent\");")]
    public void Analyzer_ShouldRequireVisualSnapshots_ForEveryWayOfTouchingTheSession(string usage)
    {
        string source = Snippet(null, usage);

        var violations = AnalyzeSnippet(source);

        violations.Should().ContainSingle().Which.State.Should().Be(ExclusiveTestState.HeadlessUiSession);
    }

    [Fact]
    public void Analyzer_ShouldPass_WhenEachStateUsesItsOwnExclusiveCollection()
    {
        string session = Snippet("[Collection(VisualSnapshotsCollection.Name)]", "AvaloniaTestHelper.EnsureInitialized();");
        string onnx = Snippet("[Collection(OnnxInferenceCollection.Name)]", "ModelSessionRegistry.ClearAllSessions();");
        string prefs = Snippet("[Collection(\"VisualSnapshots\")]", "UserPreferencesService.Instance.Reload();");

        AnalyzeSnippet(session).Should().BeEmpty();
        AnalyzeSnippet(onnx).Should().BeEmpty();
        AnalyzeSnippet(prefs).Should().BeEmpty();
    }

    [Fact]
    public void Analyzer_ShouldFlagOnlyTheViolatingClass_WhenAFileHasSeveralClasses()
    {
        // El análisis es por clase: la clase declarada en OnnxInference es inmune aunque su vecina
        // infrinja el contrato en el mismo fichero.
        string source = """
            [Collection(OnnxInferenceCollection.Name)]
            public class CompliantTests
            {
                [Fact]
                public void UsesRegistry()
                {
                    ModelSessionRegistry.ClearAllSessions();
                }
            }

            public class NonCompliantTests
            {
                [Fact]
                public void AlsoTouchesRegistry()
                {
                    ModelSessionRegistry.ClearAllSessions();
                }
            }
            """;

        var violations = AnalyzeSnippet(source);

        violations.Should().HaveCount(1, "sólo la clase sin colección infringe el contrato");
    }

    [Fact]
    public void Analyzer_ShouldResolveCollectionByNameConstant_AndByLiteral()
    {
        // Ambas formas de declarar la colección deben resolverse igual.
        string byConstant = Snippet("[Collection(OnnxInferenceCollection.Name)]", "OnnxSessionManager.GetLoadedSessionCount();");
        string byLiteral = Snippet("[Collection(\"OnnxInference\")]", "OnnxSessionManager.GetLoadedSessionCount();");

        AnalyzeSnippet(byConstant).Should().BeEmpty();
        AnalyzeSnippet(byLiteral).Should().BeEmpty();
    }

    [Fact]
    public void Analyzer_ShouldIgnoreTestHelpers_AndNonTestFiles()
    {
        string source = Snippet(null, "AvaloniaTestHelper.EnsureInitialized();");

        TestCollectionContractAnalyzer.Analyze("FileFlow.Tests/TestHelpers/SomeHelper.cs", source)
            .Should().BeEmpty("la infraestructura usa los estados en nombre de quien declara la colección");

        TestCollectionContractAnalyzer.Analyze("src/Feature/SomeService.cs", source)
            .Should().BeEmpty("sólo los ficheros de test están sujetos al contrato");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // La prueba final: un fichero infractor plantado hace fallar el mismo barrido que usa la guardia
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Sweep_ShouldFail_WhenAViolatingFileIsPlantedInTheTree()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "FileFlow_Guard_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "FileFlow.Tests", "Unit", "AI"));

        try
        {
            // Un test que toca OnnxSessionManager desde una colección equivocada: exactamente la
            // infracción que la guardia debe convertir en rojo.
            string violating = """
                using Xunit;

                namespace FileFlow.Tests.Unit.AI;

                [Collection("ThemeTokens")]
                public class PlantedViolationTests
                {
                    [Fact]
                    public void TouchesOnnxState()
                    {
                        var count = OnnxSessionManager.GetLoadedSessionCount();
                    }
                }
                """;

            string relative = "FileFlow.Tests/Unit/AI/PlantedViolationTests.cs";
            File.WriteAllText(Path.Combine(tempRoot, relative.Replace('/', Path.DirectorySeparatorChar)), violating);

            string source = File.ReadAllText(Path.Combine(tempRoot, relative.Replace('/', Path.DirectorySeparatorChar)));

            var violations = TestCollectionContractAnalyzer.Analyze(relative, source);

            violations.Should().ContainSingle().Which.CanonicalCollection.Should().Be("OnnxInference");
        }
        finally
        {
            try { Directory.Delete(tempRoot, true); } catch { /* mejor esfuerzo en el limpiado */ }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Utilidades
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Snippets sintético con (opcionalmente) un atributo de colección y una línea de uso.</summary>
    private static string Snippet(string? collectionAttribute, string usage) =>
        (collectionAttribute is null ? string.Empty : collectionAttribute + Environment.NewLine) +
        "public class SyntheticTests" + Environment.NewLine +
        "{" + Environment.NewLine +
        "    [Fact]" + Environment.NewLine +
        "    public void Synthetic()" + Environment.NewLine +
        "    {" + Environment.NewLine +
        "        " + usage + Environment.NewLine +
        "    }" + Environment.NewLine +
        "}";

    /// <summary>Analiza un snippet como si viviera en un fichero de test convencional.</summary>
    private static IReadOnlyList<TestCollectionContractAnalyzer.ContractViolation> AnalyzeSnippet(string source) =>
        TestCollectionContractAnalyzer.Analyze("FileFlow.Tests/Unit/App/SyntheticTests.cs", source);

    /// <summary>Todos los .cs de test del repositorio, excluidos obj/bin.</summary>
    private static IEnumerable<string> EnumerateTestFiles() =>
        Directory.EnumerateFiles(TestRepositoryLocator.RepositoryRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(path =>
                path.Replace('\\', '/').Contains("/FileFlow.Tests/", StringComparison.OrdinalIgnoreCase)
                && !path.Replace('\\', '/').Contains("/obj/", StringComparison.OrdinalIgnoreCase)
                && !path.Replace('\\', '/').Contains("/bin/", StringComparison.OrdinalIgnoreCase));
}
