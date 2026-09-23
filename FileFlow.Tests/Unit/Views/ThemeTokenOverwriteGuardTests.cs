using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Views;

/// <summary>
/// Guardia del <b>contrato entre el código y el tema</b>: falla cuando una vista escribe en una propiedad que
/// su propio XAML enlaza a un token —y luego depende de ese valor— o cuando el código castea a ciegas una
/// propiedad de pincel de un control, señalando el fichero y la línea exactos.
///
/// <para><b>Por qué existe</b>: el barrido de la splash escribía el gradiente en
/// <c>PbProgress.Foreground</c> (enlazado a <c>AccentPrimaryBrush</c>) y lo casteaba en el tick. La etapa de
/// tema del arranque republica los recursos, Avalonia vuelve a evaluar el <c>DynamicResource</c> y escribe un
/// <c>SolidColorBrush</c> encima: el tick lanzaba <c>InvalidCastException</c> en cada arranque y el barrido
/// quedaba muerto (hito 169). El mismo patrón tenía un segundo damnificado, silencioso: el muestrario de
/// <c>ColorPickerButton</c> volvía al acento del tema cada vez que se aplicaba un tema, perdiendo el color
/// elegido.</para>
///
/// <para>La política vive en <see cref="ThemeTokenOverwriteAnalyzer"/> y aquí se auto-testea con snippets:
/// probar que la guardia detecta infracciones no debe exigir dejar código infractor en el árbol.</para>
/// </summary>
public class ThemeTokenOverwriteGuardTests
{
    /// <summary>
    /// Vistas conocidas del host y de un plugin. Si alguien mueve o renombra el árbol de vistas, el barrido
    /// deja de cubrirlas en silencio y esta lista lo delata.
    /// </summary>
    private static readonly string[] KnownViews =
    [
        "FileFlow.App/MainWindow.axaml",
        "FileFlow.App/Views/SplashScreenWindow.axaml",
        "FileFlow.App/Views/Components/ColorPickerButton.axaml",
        "FileFlow.Plugin.AI/UI/MultimodalVlmConfigWindow.axaml",
        "FileFlow.Plugin.FileSystem/UI/Views/SyntheticDataSetDesignerWindow.axaml"
    ];

    // ─────────────────────────────────────────────────────────────────────────────
    // La guardia sobre el árbol real del repositorio
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void NoCodeBehind_ShouldCastAThemeOwnedControlBrushProperty_Blindly()
    {
        string root = TestRepositoryLocator.RepositoryRoot();

        var violations = ProductionCodeFiles(root)
            .SelectMany(file => ThemeTokenOverwriteAnalyzer.AnalyzeCode(
                File.ReadAllText(file),
                Relative(root, file)))
            .Select(violation => violation.ToString())
            .ToList();

        violations.Should().BeEmpty(
            "una propiedad de pincel de un control no tiene tipo garantizado: el tema puede haberla " +
            "reemplazado al republicarse (fue el crash por arranque del hito 169). Comprueba el tipo con " +
            "'is'/'as' o compara la instancia con ReferenceEquals; un casteo directo es una suposición");
    }

    [Fact]
    public void NoView_ShouldWriteOnAPropertyThatItsOwnXamlBindsToTheTheme()
    {
        string root = TestRepositoryLocator.RepositoryRoot();
        var violations = new List<string>();

        foreach (var (xamlFile, codeFile) in ViewPairs(root))
        {
            violations.AddRange(ThemeTokenOverwriteAnalyzer
                .AnalyzeView(
                    File.ReadAllText(xamlFile),
                    File.ReadAllText(codeFile),
                    Relative(root, xamlFile),
                    Relative(root, codeFile))
                .Select(violation => violation.ToString()));
        }

        violations.Should().BeEmpty(
            "una propiedad enlazada con {DynamicResource} no es del código que la escribe: al republicarse " +
            "el tema el recurso se vuelve a evaluar por encima del valor asignado. El color elegido se pierde " +
            "y el valor que el código cree tener deja de estar ahí");
    }

    /// <summary>
    /// Sin esto, un barrido que no encontrase nada pasaría siempre y la guardia sería decorativa.
    /// </summary>
    [Fact]
    public void Sweep_ShouldReachEveryViewAndPluginOfTheSolution()
    {
        string root = TestRepositoryLocator.RepositoryRoot();

        var views = ViewPairs(root)
            .SelectMany(pair => new[] { pair.XamlFile, pair.CodeFile })
            .Select(path => Relative(root, path))
            .ToHashSet(StringComparer.Ordinal);

        views.Should().NotBeEmpty("un barrido vacío haría pasar la guardia sin analizar ninguna vista");
        views.Should().Contain(KnownViews, "el barrido debe cubrir el host y las vistas de los plugins");

        var codeFiles = ProductionCodeFiles(root)
            .Select(path => Relative(root, path))
            .ToHashSet(StringComparer.Ordinal);

        codeFiles.Should().NotBeEmpty();
        codeFiles.Should().Contain(
            path => path.StartsWith("FileFlow.App/", StringComparison.Ordinal),
            "la aplicación es donde vive el código de interfaz");
        codeFiles.Should().Contain(
            path => path.StartsWith("FileFlow.Plugin.", StringComparison.Ordinal),
            "los plugins también montan vistas y se capturan");

        // El alcance de los plugins sale de la solución, no del nombre de la carpeta: un plugin nuevo queda
        // bajo la guardia al añadirlo a FileFlow.slnx, sin tocar este test.
        PluginSourceLocator.PluginProjectNames(root).Should().NotBeEmpty();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Auto-tests del analizador: debe fallar ante cada infracción y callar ante lo legítimo
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Analyzer_ShouldFlagTheBlindCast_OfAThemeOwnedProperty_WithFileAndLine()
    {
        const string Code = """
            using Avalonia.Media;

            public partial class SplashScreenWindow
            {
                private void AdvanceShimmer()
                {
                    var stops = ((LinearGradientBrush)PbProgress.Foreground!).GradientStops;
                }
            }
            """;

        var violation = ThemeTokenOverwriteAnalyzer.AnalyzeCode(Code, "SplashScreenWindow.axaml.cs")
            .Should().ContainSingle().Subject;

        violation.Line.Should().Be(7, "la infracción está en la línea del casteo");
        violation.Member.Should().Be("Foreground");
        violation.Rule.Should().Be(ThemeTokenOverwriteAnalyzer.RuleBlindBrushCast);
        violation.File.Should().Be("SplashScreenWindow.axaml.cs");
        violation.Reason.Should().NotBeNullOrWhiteSpace("el mensaje debe decir cómo se corrige");
    }

    [Fact]
    public void Analyzer_ShouldNotFlag_AValueThatIsNotAControlBrushProperty()
    {
        // Castear el valor de un recurso es legítimo: ahí el tipo sí está bajo control del código, no del
        // tema. Es el caso de TryResolveThemeBrush en la splash.
        const string Code = """
            using Avalonia.Media;

            public static class Resolver
            {
                public static bool TryResolve(object value, out ISolidColorBrush brush)
                {
                    if (value is ISolidColorBrush solid) { brush = solid; return true; }
                    brush = null!;
                    return false;
                }

                public static Colors ColorOf(object accent) => ((ISolidColorBrush)accent!).Color;
            }
            """;

        ThemeTokenOverwriteAnalyzer.AnalyzeCode(Code, "Resolver.cs").Should().BeEmpty();
    }

    [Fact]
    public void Analyzer_ShouldFlagAnAssignmentOverAThemeBoundProperty_AndAcceptTheGuardedRead()
    {
        // El XAML es el de ColorPickerButton antes del arreglo: el muestrario colgaba del token.
        const string Xaml = """
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <Border x:Name="SwatchBorder" Width="26" Height="22"
                      BorderBrush="{DynamicResource BorderDarkBrush}"
                      Background="{DynamicResource AccentPrimaryBrush}" />
            </UserControl>
            """;

        const string WritesWithoutChecking = """
            using Avalonia.Media;

            public partial class ColorPickerButton
            {
                private void UpdateVisuals(Colors color)
                {
                    SwatchBorder.Background = new SolidColorBrush(color);
                }
            }
            """;

        var violation = ThemeTokenOverwriteAnalyzer
            .AnalyzeView(Xaml, WritesWithoutChecking, "ColorPickerButton.axaml", "ColorPickerButton.axaml.cs")
            .Should().ContainSingle().Subject;

        violation.Member.Should().Be("SwatchBorder.Background");
        violation.Rule.Should().Be(ThemeTokenOverwriteAnalyzer.RuleTokenOverwrite);
        violation.Line.Should().Be(7);

        // La misma escritura con una lectura que comprueba el valor es justo lo que hace el barrido de la
        // splash: el tema puede haberlo sustituido y el código se recupera en el tick siguiente.
        const string WritesAndChecks = """
            using Avalonia.Media;

            public partial class SplashScreenWindow
            {
                private bool EnsureShimmerBrush()
                {
                    if (ReferenceEquals(PbProgress.Foreground, _shimmerBrush))
                    {
                        return true;
                    }

                    PbProgress.Foreground = _shimmerBrush;
                    return true;
                }
            }
            """;

        const string BarXaml = """
            <Window xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <ProgressBar x:Name="PbProgress" Foreground="{DynamicResource AccentPrimaryBrush}" />
            </Window>
            """;

        ThemeTokenOverwriteAnalyzer
            .AnalyzeView(BarXaml, WritesAndChecks, "SplashScreenWindow.axaml", "SplashScreenWindow.axaml.cs")
            .Should().BeEmpty("comparar la instancia antes de depender de ella es la forma correcta de escribir " +
                              "sobre una propiedad del tema");
    }

    [Fact]
    public void Analyzer_ShouldAcceptWritingOnAPropertyThatTheThemeDoesNotOwn()
    {
        // El arreglo del muestrario: el chrome (radio y borde) sigue en tokens y el color vive en el relleno,
        // que no está enlazado a ninguno. Aquí no hay nada que el tema pueda reescribir.
        const string Xaml = """
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <Border x:Name="SwatchBorder" CornerRadius="{DynamicResource RadiusXs}"
                      BorderBrush="{DynamicResource BorderDarkBrush}">
                <Border x:Name="SwatchFill" CornerRadius="{DynamicResource RadiusXs}" />
              </Border>
            </UserControl>
            """;

        const string Code = """
            using Avalonia.Media;

            public partial class ColorPickerButton
            {
                private void UpdateVisuals(Colors color)
                {
                    SwatchFill.Background = new SolidColorBrush(color);
                }
            }
            """;

        ThemeTokenOverwriteAnalyzer
            .AnalyzeView(Xaml, Code, "ColorPickerButton.axaml", "ColorPickerButton.axaml.cs")
            .Should().BeEmpty("el relleno no está enlazado al tema: su color es del control");
    }

    [Fact]
    public void Analyzer_ShouldIgnoreXamlThatDoesNotParse()
    {
        ThemeTokenOverwriteAnalyzer
            .TokenBoundMembers("<Border x:Name=\"Sin xmlns declarado\" Background=\"{DynamicResource X}\" />")
            .Should().BeEmpty("un XAML ilegible no debe reventar la guardia con una excepción distinta");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Localización de ficheros
    // ─────────────────────────────────────────────────────────────────────────────

    private static IEnumerable<string> ProductionCodeFiles(string root) =>
        ProductionDirectories(root)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            .Where(path => !PluginSourceLocator.IsBuildArtifact(path))
            .OrderBy(path => path, StringComparer.Ordinal);

    /// <summary>Pares vista/code-behind: el XAML que declara tokens y el código que podría pisarlos.</summary>
    private static IEnumerable<(string XamlFile, string CodeFile)> ViewPairs(string root) =>
        ProductionDirectories(root)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.axaml", SearchOption.AllDirectories))
            .Where(path => !PluginSourceLocator.IsBuildArtifact(path))
            .Select(xaml => (XamlFile: xaml, CodeFile: Path.ChangeExtension(xaml, ".axaml.cs")))
            .Where(pair => File.Exists(pair.CodeFile))
            .OrderBy(pair => pair.XamlFile, StringComparer.Ordinal);

    /// <summary>El host y los proyectos de plugin declarados en la solución.</summary>
    private static IEnumerable<string> ProductionDirectories(string root) =>
        new[] { Path.Combine(root, "FileFlow.App") }
            .Concat(PluginSourceLocator.PluginDirectories(root))
            .Where(Directory.Exists);

    private static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');
}
