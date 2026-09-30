using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Guardia del repintado en caliente del host Uno (fase 3.5): <c>SetBrush</c> tiene que MUTAR el
/// color del pincel singleton — la lección del 233 es que en WinUI la republicación por claves no
/// llega a los consumidores vivos (el <c>StaticResource</c> captura la instancia y el
/// <c>ThemeResource</c> de aplicación no re-evalúa), así que la única vía que repinta el lienzo al
/// cambiar de tema es cambiar el COLOR del pincel que todos ya consumen.
///
/// <para><b>Por qué guarda la fuente y no el runtime</b>: el host es WinUI y no se materializa en
/// la sesión de pruebas; el criterio «cambiar el tema re-tematiza el lienzo» queda demostrado por
/// la sonda 3.5 del selfcheck (hito 233), y esta guardia fija el CÓDIGO que la hace posible — si la
/// asignación del color desaparece (o se queda en un parseo sin efecto), la sonda no corre aquí y
/// el tema dejaría de repintar en silencio.</para>
/// </summary>
public class UnoThemeRepaintGuardTests
{
    private const string HostPath = "FileFlow.App.Uno/Platform/UnoThemeHost.cs";

    private static string HostCode() => SourceText.CodeWithoutComments(HostPath);

    [Fact]
    public void ThemeHost_ShouldMutateTheBrushColorInPlace()
    {
        // El testigo de tema-sin-repintado: el mutante deja la línea en un parseo sin asignación
        // (o la borra) y esta aserción cae. Es la única línea que hace que el tema repinte.
        HostCode().Should().Contain(
            "brush.Color = NodeCardViewModel.ParseHex(hex);",
            "SetBrush tiene que MUTAR el color del pincel singleton: sin la asignación, el cambio de " +
            "tema deja de repintar el lienzo (los consumidores vivos conservan el pincel viejo)");
    }

    [Fact]
    public void ThemeHost_ShouldRunTheRepublishOnEveryVariantChange()
    {
        // El pipeline completo: la publicación de variante termina en la aplicación de tokens.
        string code = HostCode();

        code.Should().Contain(
            "RepublishTokens();",
            "cada cambio de variante tiene que terminar aplicando los tokens: un PublishThemeVariant " +
            "que no repinta deja la variante sin la mitad de colores");

        code.Should().Contain(
            "SetBrush(resources, \"CanvasWireBrush\"",
            "el cable es uno de los pinceles aplicados: si el repintado se recorta a un subconjunto, " +
            "el lienzo queda a medio tematizar");
    }

    [Fact]
    public void HostXaml_ShouldConsumeTheThemeBrushesByStaticResource()
    {
        // El testigo del cajón «claro con texto claro»: el panel del catálogo de nodos era el ÚNICO
        // consumidor del host que pedía los pinceles del tema por `ThemeResource` — y como ese no
        // re-evalúa (la lección del 233), el panel quedaba congelado en los colores de arranque al
        // cambiar de tema: claro por fuera, con el texto claro por dentro. La cura es la misma que ya
        // usaba el resto del host: `StaticResource`, que captura el pincel singleton que
        // `UnoThemeHost` muta en caliente. La aserción de presencia es el control del mutante: borrar
        // los consumidores por completo también satisfaría el «no contiene ThemeResource».
        foreach (string path in new[]
                 {
                     "FileFlow.App.Uno/Controls/NodeToolboxPanel.xaml",
                     "FileFlow.App.Uno/MainWindow.xaml",
                     "FileFlow.App.Uno/Themes/ControlStyles.xaml",
                 })
        {
            string xaml = SourceText.CodeWithoutComments(path);

            xaml.Should().NotContain(
                "{ThemeResource",
                $"{path}: los pinceles del tema se consumen por StaticResource — el ThemeResource de " +
                "aplicación no re-evalúa y el control queda fuera del repintado en caliente");

            xaml.Should().Contain(
                "{StaticResource Canvas",
                $"{path}: el archivo tiene que consumir de verdad los pinceles Canvas* del tema (si no, " +
                "no habría nada que repintar y el defecto de legibilidad volvería por la puerta de atrás)");
        }
    }

    [Fact]
    public void ThemeHost_ShouldKeepCreatingMissingBrushes_AndThePortableGenerator()
    {
        // El caso hermano (el control del mutante): el mutante cambia UNA línea (la asignación del
        // color) y deja intactas la creación del pincel ausente y el generador portable que el
        // Theme Studio consume por BuildResources.
        string code = HostCode();

        code.Should().Contain(
            "resources[key] = brush;",
            "la creación del pincel ausente es parte del contrato (primera aplicación antes de que " +
            "App.xaml lo declare) y el mutante no la toca");

        code.Should().Contain(
            "private static IReadOnlyDictionary<string, object?> BuildResources(object themeDefinition)",
            "el generador portable de tokens (la vía del Theme Studio y del núcleo) sigue en pie");
    }
}
