using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.App.Views.Components;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace FileFlow.Tests.Unit.Views;

/// <summary>
/// Regresiones visuales de la capa de diseño: tokens del tema, clases de estilo y composición.
///
/// Hay dos niveles a propósito:
/// <list type="bullet">
///   <item><b>Instantáneas</b>: la composición completa se compara píxel a píxel con una línea base en
///   <c>FileFlow.Tests/VisualBaselines</c>. Detectan cambios de layout, de color, de espaciado o de
///   tipografía que ninguna aserción sobre propiedades capturaría.</item>
///   <item><b>Sondas de píxel</b>: comprobaciones pequeñas y estables (el botón primario se pinta con el
///   acento del tema, el radio recorta la esquina, la elevación proyecta sombra). No dependen de las fuentes
///   del sistema, así que son el respaldo multiplataforma cuando la línea base se regenera en otro equipo.</item>
/// </list>
///
/// Las vistas de la aplicación se capturan en <see cref="AppShellVisualRegressionTests"/>; aquí vive la
/// galería de diseño, que es el contrato de los tokens en sí.
///
/// Cada captura recibe una <b>fábrica</b> de contenido: se invoca en el hilo de UI de la sesión, que es donde
/// se pueden crear controles y leer los recursos del tema.
/// </summary>
[Collection(VisualSnapshotsCollection.Name)]
public class VisualRegressionTests
{
    private const string DarkTheme = "dark_fluent";
    private const string LightTheme = "light_studio";

    // ─────────────────────────────────────────────────────────────────────────────
    // Instantáneas
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DesignGallery_ShouldMatchItsBaseline_InTheDarkPreset()
    {
        byte[] capture = VisualSnapshot.Capture(DesignGallery.Build, DesignGallery.Width, DesignGallery.Height, DarkTheme);

        VisualSnapshot.AssertMatchesBaseline("design-gallery-dark", capture);
    }

    [Fact]
    public void DesignGallery_ShouldMatchItsBaseline_InTheLightPreset()
    {
        byte[] capture = VisualSnapshot.Capture(DesignGallery.Build, DesignGallery.Width, DesignGallery.Height, LightTheme);

        VisualSnapshot.AssertMatchesBaseline("design-gallery-light", capture);
    }

    [Fact]
    public void NodeCard_ShouldMatchItsBaseline()
    {
        byte[] capture = VisualSnapshot.Capture(BuildNodeCard, 340, 260, DarkTheme);

        VisualSnapshot.AssertMatchesBaseline("node-card-dark", capture);
    }

    [Fact]
    public void ThemeStudio_ShouldMatchItsBaseline()
    {
        string storage = Path.Combine(Path.GetTempPath(), $"visual_studio_{Guid.NewGuid():N}.json");

        try
        {
            byte[] capture = VisualSnapshot.Capture(
                () => BuildStudioEditor(storage),
                1200,
                760,
                DarkTheme);

            VisualSnapshot.AssertMatchesBaseline("theme-studio-dark", capture);
        }
        finally
        {
            File.Delete(storage);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Sondas de píxel (independientes de las fuentes del sistema)
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void PrimaryButton_ShouldBePaintedWithTheAccentToken()
    {
        var theme = VisualSnapshot.ResolveTheme(DarkTheme);
        var accent = Color.Parse(theme.AccentPrimary);

        // Botón ancho con el texto centrado: se muestrea la zona sólida de la izquierda, sin tocar el glifo.
        byte[] capture = VisualSnapshot.Capture(
            () => new Button
            {
                Content = "Aceptar",
                Width = 220,
                Height = 40,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Classes = { "primary" }
            },
            260,
            80,
            DarkTheme);

        var sample = VisualSnapshot.PixelAt(capture, 40, 40);

        sample.R.Should().BeCloseTo(accent.R, 12, "el botón primario debe pintarse con el acento del tema");
        sample.G.Should().BeCloseTo(accent.G, 12);
        sample.B.Should().BeCloseTo(accent.B, 12);
    }

    [Fact]
    public void RadiusToken_ShouldRoundTheCornerOfTheRenderedBox()
    {
        // Dos cajas idénticas salvo el radio: la primera con la esquina redondeada del tema y la segunda sin
        // redondeo. El píxel de la esquina debe ser el fondo en la redondeada y la caja en la recta.
        byte[] capture = VisualSnapshot.Capture(
            () => new StackPanel
            {
                Spacing = 0,
                // Alineado a la izquierda y arriba: con el alineamiento por defecto (estirado) las cajas se
                // centrarían y las muestras caerían fuera de ellas, comparando fondo contra fondo.
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                Children =
                {
                    new Border
                    {
                        Width = 80,
                        Height = 40,
                        Background = Brushes.Red,
                        CornerRadius = VisualSnapshot.ResolveToken<CornerRadius>("RadiusXxl")
                    },
                    new Border
                    {
                        Width = 80,
                        Height = 40,
                        Background = Brushes.Red,
                        CornerRadius = new CornerRadius(0)
                    }
                }
            },
            100,
            100,
            DarkTheme);

        var roundedCorner = VisualSnapshot.PixelAt(capture, 1, 1);
        var squareCorner = VisualSnapshot.PixelAt(capture, 1, 43);

        squareCorner.Should().Be(new Rgba32(255, 0, 0, 255), "sin radio la esquina debe quedar rellena: si no, la muestra no cae dentro de la caja");

        (roundedCorner.R + roundedCorner.G + roundedCorner.B)
            .Should().BeLessThan(squareCorner.R + squareCorner.G + squareCorner.B,
                "'CornerRadius' debe recortar la esquina: si el token deja de aplicarse, la esquina vuelve a pintarse");
    }

    [Fact]
    public void ElevationToken_ShouldCastAShadowBelowTheSurface()
    {
        byte[] capture = VisualSnapshot.Capture(
            () => new Border
            {
                Width = 120,
                Height = 60,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Background = Brushes.SteelBlue,
                BoxShadow = VisualSnapshot.ResolveToken<BoxShadows>("Elev4")
            },
            200,
            160,
            DarkTheme);

        // La caja (120x60) queda centrada en la ventana de 200x160: ocupa de y=50 a y=110. Se muestrea su
        // centro, el borde inferior (donde el desenfoque de la sombra es máximo) y una esquina lejana, que es
        // el fondo de la aplicación sin sombra.
        var surface = VisualSnapshot.PixelAt(capture, 100, 80);
        var belowSurface = VisualSnapshot.PixelAt(capture, 100, 116);
        var background = VisualSnapshot.PixelAt(capture, 4, 4);

        surface.B.Should().BeGreaterThan(surface.R, "el centro de la caja debe ser la superficie pintada");

        belowSurface.Should().NotBe(surface, "el píxel bajo la caja no puede seguir siendo la caja: la muestra está mal situada");

        (belowSurface.R + belowSurface.G + belowSurface.B)
            .Should().BeLessThan((background.R + background.G + background.B),
                "'Elev4' debe proyectar sombra bajo la superficie: sin sombra el píxel de debajo sería el fondo de la aplicación tal cual");
    }

    [Fact]
    public void ThemePreset_ShouldReachTheRenderedSurface()
    {
        var dark = VisualSnapshot.ResolveTheme(DarkTheme);
        var light = VisualSnapshot.ResolveTheme(LightTheme);

        byte[] darkCapture = VisualSnapshot.Capture(DesignGallery.Build, DesignGallery.Width, DesignGallery.Height, DarkTheme);
        byte[] lightCapture = VisualSnapshot.Capture(DesignGallery.Build, DesignGallery.Width, DesignGallery.Height, LightTheme);

        // La esquina superior izquierda de la ventana es el fondo de la aplicación: cada preset tiene el suyo.
        var darkBackground = VisualSnapshot.PixelAt(darkCapture, 2, 2);
        var lightBackground = VisualSnapshot.PixelAt(lightCapture, 2, 2);

        darkBackground.R.Should().BeCloseTo(Color.Parse(dark.AppBackground).R, 12);
        darkBackground.G.Should().BeCloseTo(Color.Parse(dark.AppBackground).G, 12);
        darkBackground.B.Should().BeCloseTo(Color.Parse(dark.AppBackground).B, 12);

        lightBackground.R.Should().BeCloseTo(Color.Parse(light.AppBackground).R, 12);
        lightBackground.G.Should().BeCloseTo(Color.Parse(light.AppBackground).G, 12);
        lightBackground.B.Should().BeCloseTo(Color.Parse(light.AppBackground).B, 12);

        darkBackground.Should().NotBe(lightBackground,
            "cada preset debe llegar al renderizado: si el fondo es el mismo, el tema no se está aplicando");
    }

    [Fact]
    public void Capture_ShouldBeDeterministic_BetweenTwoRuns()
    {
        // La línea base sólo tiene sentido si dos capturas seguidas del mismo estado son idénticas.
        byte[] first = VisualSnapshot.Capture(DesignGallery.Build, DesignGallery.Width, DesignGallery.Height, DarkTheme);
        byte[] second = VisualSnapshot.Capture(DesignGallery.Build, DesignGallery.Width, DesignGallery.Height, DarkTheme);

        first.Should().Equal(second, "dos capturas del mismo estado deben ser idénticas byte a byte");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Composiciones (se construyen en el hilo de UI: ver VisualSnapshot.Capture)
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Tarjeta de nodo con datos reales de un nodo del plugin de sistema de archivos.</summary>
    private static Control BuildNodeCard()
    {
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.FileSystem.FolderSourceNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Logic.ExpressionFilterNode).Assembly);
        loader.ScanCurrentAppDomain();

        var editor = new EditorViewModel(loader);
        var node = editor.AddNode("FolderSourceNode", new Point(0, 0));

        node.Should().NotBeNull("el nodo de origen de carpeta debe existir para poder capturar su tarjeta");

        return new NodeCardView
        {
            DataContext = node,
            Width = 320,
            Height = 240
        };
    }

    /// <summary>Editor del Theme Studio: el panel de ajustes generado desde el catálogo.</summary>
    private static Control BuildStudioEditor(string storagePath)
    {
        var viewModel = new ThemeCustomizerViewModel(new CustomThemeService(storagePath));
        var window = new ThemeCustomizerWindow { DataContext = viewModel };

        // La ventana sólo actúa como contenedor del XAML: se extrae su contenido para capturarlo dentro de la
        // ventana sin decoración de las pruebas (una Window no puede ser hija de otra Window).
        var content = (Control)window.Content!;
        window.Content = null;

        viewModel.Sections.SelectMany(section => section.Rows)
            .Should().NotBeEmpty("el editor debe estar poblado por el catálogo de ajustes antes de capturarlo");

        return content;
    }
}
