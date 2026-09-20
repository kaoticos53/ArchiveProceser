using System;
using System.Collections.Generic;
using Avalonia.Controls;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Views;

/// <summary>
/// Capturas de las vistas clave de la aplicación: la ventana principal y cada panel por separado.
///
/// Es la red que faltaba. Las aserciones sobre el XAML (bindings, clases, tokens) comprueban el contrato de
/// cada pieza, pero no cómo queda el conjunto: un cambio de espaciado, un color que pierde contraste, un
/// panel que se recorta, una columna que se desborda o una tipografía que deja de ser la del tema no rompen
/// ninguna de esas aserciones y sólo se ven mirando. Estas pruebas se lo quedan mirando por ti: la imagen se
/// compara píxel a píxel con una línea base.
///
/// Las capturas se hacen sobre la aplicación real montada con dobles de sus puertos (<see
/// cref="AppVisualFixture"/>), no sobre una maqueta: cubren plantillas, clases de estilo, convertidores y
/// los datos que las vistas pintan.
///
/// La fixture se crea <b>una vez por clase</b> (<c>IClassFixture</c>: los view models y el registro de
/// plugins son los caros, ~8 s del arranque en frío del proceso) y cada captura la <b>re-congela</b> antes
/// de construir (ver <see cref="AppVisualFixture.EnsureFrozen"/>): recarga el grafo, vuelve a sembrar la
/// consola y re-afija la barra de estado, de modo que la captura parte del mismo estado que partiría de
/// una fixture recién creada aunque la prueba anterior hubiera tocado view models. Al finalizar la clase,
/// xUnit dispone la fixture: los view models se desuscriben de los singletons del proceso y el timer de
/// la consola se detiene, igual que cuando cada prueba disponía la suya.
///
/// Si un cambio visual es intencionado, se regeneran las líneas base con <c>FILEFLOW_UPDATE_VISUALS=1</c> y
/// se revisan las imágenes resultantes antes de darlas por buenas.
/// </summary>
[Collection(VisualSnapshotsCollection.Name)]
public class AppShellVisualRegressionTests : IClassFixture<SharedAppVisualFixture>
{
    private const string DarkTheme = "dark_fluent";
    private const string LightTheme = "light_studio";

    private readonly SharedAppVisualFixture _shared;

    public AppShellVisualRegressionTests(SharedAppVisualFixture shared) => _shared = shared;

    // ─────────────────────────────────────────────────────────────────────────────
    // La ventana principal, completa
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheWholeShell_ShouldMatchItsBaseline_InTheDarkPreset()
    {
        byte[] capture = CaptureWithFixture(
            _shared,
            surface => surface.Build(AppSurface.Shell),
            AppVisualFixture.ShellWidth,
            AppVisualFixture.ShellHeight,
            DarkTheme);

        VisualSnapshot.AssertMatchesBaseline("app-shell-dark", capture);
    }

    [Fact]
    public void TheWholeShell_ShouldMatchItsBaseline_InTheLightPreset()
    {
        byte[] capture = CaptureWithFixture(
            _shared,
            surface => surface.Build(AppSurface.Shell),
            AppVisualFixture.ShellWidth,
            AppVisualFixture.ShellHeight,
            LightTheme);

        VisualSnapshot.AssertMatchesBaseline("app-shell-light", capture);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Paneles por separado: la imagen es más grande y el fallo señala al panel culpable
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheEditorCanvas_ShouldMatchItsBaseline()
    {
        byte[] capture = CaptureWithFixture(
            _shared,
            surface => surface.Build(AppSurface.Editor),
            AppVisualFixture.EditorWidth,
            560,
            DarkTheme);

        VisualSnapshot.AssertMatchesBaseline("panel-editor-dark", capture);
    }

    [Fact]
    public void TheNodeToolbox_ShouldMatchItsBaseline()
    {
        byte[] capture = CaptureWithFixture(
            _shared,
            surface => surface.Build(AppSurface.Toolbox),
            300,
            null,
            DarkTheme);

        VisualSnapshot.AssertMatchesBaseline("panel-toolbox-dark", capture);
    }

    [Fact]
    public void TheNodeInspector_ShouldMatchItsBaseline()
    {
        byte[] capture = CaptureWithFixture(
            _shared,
            surface => surface.Build(AppSurface.Inspector),
            400,
            620,
            DarkTheme);

        VisualSnapshot.AssertMatchesBaseline("panel-inspector-dark", capture);
    }

    [Fact]
    public void TheExecutionConsole_ShouldMatchItsBaseline()
    {
        byte[] capture = CaptureWithFixture(
            _shared,
            surface => surface.Build(AppSurface.LogConsole),
            AppVisualFixture.ConsoleWidth,
            240,
            DarkTheme);

        VisualSnapshot.AssertMatchesBaseline("panel-log-console-dark", capture);
    }

    [Fact]
    public void TheStatusBar_ShouldMatchItsBaseline()
    {
        byte[] capture = CaptureWithFixture(
            _shared,
            surface => surface.Build(AppSurface.StatusBar),
            AppVisualFixture.BarWidth,
            null,
            DarkTheme);

        VisualSnapshot.AssertMatchesBaseline("panel-status-bar-dark", capture);
    }

    [Fact]
    public void TheControlBar_ShouldMatchItsBaseline()
    {
        byte[] capture = CaptureWithFixture(
            _shared,
            surface => surface.Build(AppSurface.ControlBar),
            AppVisualFixture.BarWidth,
            null,
            DarkTheme);

        VisualSnapshot.AssertMatchesBaseline("panel-control-bar-dark", capture);
    }

    /// <summary>
    /// El cajón desplegado, que es el índice de herramientas de la aplicación: por él se abren el inspector, las
    /// métricas, el explorador virtual, el diseñador de datasets, los ajustes y la ayuda. Era la única superficie
    /// principal sin captura, de modo que una entrada nueva podía nacer invisible, recortada o sin estilo.
    /// </summary>
    [Fact]
    public void TheOpenDrawer_ShouldMatchItsBaseline()
    {
        byte[] capture = CaptureWithFixture(
            _shared,
            surface => surface.Build(AppSurface.Drawer),
            AppVisualFixture.ShellWidth,
            AppVisualFixture.ShellHeight,
            DarkTheme);

        VisualSnapshot.AssertMatchesBaseline("app-shell-drawer-dark", capture);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Sondas: la captura no puede quedarse vacía ni perder el tema por el camino
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EveryPanel_ShouldRenderWithItsOwnThemedBackground()
    {
        // Un panel invisible, con el tema sin aplicar o con el árbol sin construir sale como una imagen
        // plana: comprobarlo evita que una línea base «verde» congele una pantalla en blanco.
        foreach (var surface in new[]
                 {
                     AppSurface.Editor,
                     AppSurface.Toolbox,
                     AppSurface.Inspector,
                     AppSurface.LogConsole,
                     AppSurface.StatusBar,
                     AppSurface.ControlBar,
                     AppSurface.Drawer
                 })
        {
            byte[] capture = CaptureWithFixture(
            _shared,
            fixture => fixture.Build(surface),
                surface == AppSurface.Inspector ? 400 : AppVisualFixture.BarWidth,
                surface == AppSurface.Inspector ? 620 : 380,
                DarkTheme);

            var colors = DistinctColors(capture);

            colors.Should().BeGreaterThan(4,
                $"'{surface}' debe pintar su contenido con las clases del tema, no un lienzo plano");
        }
    }

    [Fact]
    public void TheShell_ShouldShowItsPanelsBoundToRealData()
    {
        // La muestra tiene un grafo de tres nodos, una conexión y registros en la consola: si los paneles
        // dejaran de recibir sus view models, la captura seguiría siendo «una imagen» pero sin contenido.
        var fixture = _shared.Fixture;

        AvaloniaTestHelper.RunOnUI(fixture.EnsureFrozen);

        fixture.Editor.Nodes.Should().HaveCount(3, "el lienzo de muestra lleva un origen, un filtro y un destino");
        fixture.Editor.Connections.Should().HaveCount(2);
        fixture.LogConsole.Logs.Should().NotBeEmpty("la consola debe mostrar registros con marca de tiempo fija");
        fixture.Inspector.IsOpen.Should().BeTrue("el inspector se captura abierto, con un nodo inspeccionado");
    }

    /// <summary>
    /// Captura con la fixture compartida de la clase: la re-congela dentro de la fábrica (en el hilo de
    /// UI, como exige <c>EnsureFrozen</c>) y construye sobre ella. La disposición ocurre al finalizar la
    /// clase (xUnit dispone el <c>IClassFixture</c>), no por captura.
    /// </summary>
    private static byte[] CaptureWithFixture(
        SharedAppVisualFixture shared,
        Func<AppVisualFixture, Control> build,
        int width,
        int? height,
        string themeId)
    {
        return height is { } fixedHeight
            ? VisualSnapshot.Capture(
                () => { shared.Fixture.EnsureFrozen(); return build(shared.Fixture); },
                width, fixedHeight, themeId)
            : VisualSnapshot.CaptureNaturalHeight(
                () => { shared.Fixture.EnsureFrozen(); return build(shared.Fixture); },
                width, themeId);
    }

    /// <summary>Cuenta colores distintos de una captura (con muestreo, para que sea rápido).</summary>
    private static int DistinctColors(byte[] png)
    {
        var seen = new HashSet<uint>();

        using var image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(png);

        for (int y = 0; y < image.Height; y += 3)
        {
            for (int x = 0; x < image.Width; x += 3)
            {
                var pixel = image[x, y];
                seen.Add(((uint)pixel.R << 16) | ((uint)pixel.G << 8) | pixel.B);
            }
        }

        return seen.Count;
    }
}
