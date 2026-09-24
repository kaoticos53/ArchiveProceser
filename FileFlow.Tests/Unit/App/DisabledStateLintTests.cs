using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Guardia estructural del <b>estado deshabilitado</b>: falla cuando una regla de estilo vuelve a atenuar con
/// <c>Opacity</c> la parte de plantilla que contiene la etiqueta.
///
/// <para>Es la mitad que la captura no puede vigilar. Las líneas base del producto en los dos temas <b>no</b>
/// vieron el defecto ni su arreglo: el cambio de cara cae dentro de la tolerancia por canal y el de la etiqueta
/// es texto fino, por debajo del 1,5 % de píxeles que la comparación admite. La otra mitad —que el texto
/// deshabilitado se lea— la mide <c>DesignStateBaselinesTests</c> sobre las celdas del tablero; aquí se cierra la
/// puerta al <b>mecanismo</b>, para cualquier control, con o sin celda en el tablero.</para>
///
/// <para>La política vive en <see cref="DisabledStateAnalyzer"/> y se auto-testea con fragmentos: probar que la
/// guardia detecta un infractor no debe exigir dejar una regla infractora en el árbol.</para>
/// </summary>
[Collection("ThemeTokens")]
public class DisabledStateLintTests
{
    /// <summary>Estilos conocidos del host y de los plugins. Si el árbol cambia, el barrido lo delata aquí.</summary>
    private static readonly string[] KnownStyleFiles =
    [
        "FileFlow.App/Styles/Buttons.axaml",
        "FileFlow.App/Styles/Inputs.axaml",
        "FileFlow.App/Styles/Containers.axaml"
    ];

    // ─────────────────────────────────────────────────────────────────────────────
    // La guardia sobre el árbol real del repositorio
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void NoStyleRule_ShouldDimWithOpacityThePartThatCarriesTheLabel()
    {
        string root = TestRepositoryLocator.RepositoryRoot();

        var violations = StyleFiles(root)
            .SelectMany(file => DisabledStateAnalyzer.Analyze(
                File.ReadAllText(file),
                Relative(root, file)))
            .ToList();

        violations.Should().BeEmpty(
            "el tema base ya atenúa el primer plano deshabilitado dentro de la parte de plantilla: una Opacity " +
            "sobre esa misma parte lo atenúa dos veces y deja el texto por debajo de cualquier contraste " +
            "legible (medido: 2,13:1 en oscuro y 1,20:1 en claro). Infracciones: " + string.Join(" | ", violations));
    }

    [Fact]
    public void TheSweep_ShouldCoverTheKnownStyleFiles()
    {
        // Un barrido que deje de ver los estilos del sistema de diseño pasaría en verde sin comprobar nada.
        var covered = StyleFiles(TestRepositoryLocator.RepositoryRoot())
            .Select(file => Relative(TestRepositoryLocator.RepositoryRoot(), file))
            .ToList();

        foreach (string known in KnownStyleFiles)
        {
            covered.Should().Contain(known, $"el barrido del estado deshabilitado debe cubrir '{known}'");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // El analizador, con fragmentos
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheAnalyzer_ShouldDetectTheOpacityOnThePartThatCarriesTheLabel()
    {
        var violations = DisabledStateAnalyzer.Analyze(
            """
            <Styles xmlns="https://github.com/avaloniaui">
                <Style Selector="Button:disabled /template/ ContentPresenter#PART_ContentPresenter">
                    <Setter Property="Background" Value="{DynamicResource BgSurfaceBrush}" />
                    <Setter Property="Opacity" Value="0.45" />
                </Style>
            </Styles>
            """,
            "fragmento.axaml");

        violations.Should().HaveCount(1);
        violations[0].Should().Contain("Button:disabled").And.Contain("Opacity");
    }

    [Fact]
    public void TheAnalyzer_ShouldAcceptTheExplicitFaceAndForeground()
    {
        DisabledStateAnalyzer.Analyze(
            """
            <Styles xmlns="https://github.com/avaloniaui">
                <Style Selector="Button.primary:disabled /template/ ContentPresenter#PART_ContentPresenter">
                    <Setter Property="Background" Value="{DynamicResource AccentPrimaryMutedBrush}" />
                    <Setter Property="Foreground" Value="{DynamicResource TextPrimaryBrush}" />
                </Style>
            </Styles>
            """,
            "fragmento.axaml").Should().BeEmpty();
    }

    [Fact]
    public void TheAnalyzer_ShouldIgnoreAnOpacityOnAPartWithoutContent()
    {
        // La opacidad de la capa de fondo o del borde no toca la etiqueta: es el caso legítimo de los campos de
        // texto y los desplegables, que siguen declarando la suya.
        DisabledStateAnalyzer.Analyze(
            """
            <Styles xmlns="https://github.com/avaloniaui">
                <Style Selector="TextBox:disabled /template/ Border#PART_BorderElement">
                    <Setter Property="Background" Value="Transparent" />
                    <Setter Property="Opacity" Value="0.5" />
                </Style>
                <Style Selector="ComboBox:disabled /template/ Border#Background">
                    <Setter Property="Opacity" Value="0.5" />
                </Style>
            </Styles>
            """,
            "fragmento.axaml").Should().BeEmpty();
    }

    [Fact]
    public void TheAnalyzer_ShouldSeeRulesNestedInsideAControlTheme()
    {
        var violations = DisabledStateAnalyzer.Analyze(
            """
            <Styles xmlns="https://github.com/avaloniaui">
                <Styles.Resources>
                    <ControlTheme x:Key="SegmentTheme" TargetType="RadioButton">
                        <Style Selector="^:disabled /template/ ContentPresenter">
                            <Setter Property="Opacity" Value="0.4" />
                        </Style>
                    </ControlTheme>
                </Styles.Resources>
            </Styles>
            """,
            "fragmento.axaml");

        violations.Should().HaveCount(1, "una regla anidada atenúa la etiqueta igual que una suelta");
    }

    [Fact]
    public void TheAnalyzer_ShouldIgnoreWhatIsWrittenInAComment()
    {
        DisabledStateAnalyzer.Analyze(
            """
            <Styles xmlns="https://github.com/avaloniaui">
                <!-- Antes esto era <Setter Property="Opacity" Value="0.45" /> y dejaba el texto en 1,20:1 -->
                <Style Selector="Button:disabled /template/ ContentPresenter">
                    <Setter Property="Foreground" Value="{DynamicResource TextMutedBrush}" />
                </Style>
            </Styles>
            """,
            "fragmento.axaml").Should().BeEmpty(
            "el comentario que explica el defecto no puede leerse como el defecto");
    }

    // ─────────────────────────────────────────────────────────────────────────────

    private static IEnumerable<string> StyleFiles(string root) =>
        Directory.EnumerateFiles(root, "*.axaml", SearchOption.AllDirectories)
            .Select(path => path.Replace('\\', '/'))
            .Where(path => !path.Contains("/bin/", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains("/obj/", StringComparison.OrdinalIgnoreCase))
            .Where(path => path.Contains("/Styles/", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal);

    private static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');
}
