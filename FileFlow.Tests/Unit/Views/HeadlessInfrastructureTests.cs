using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using SixLabors.ImageSharp;
using Xunit;

namespace FileFlow.Tests.Unit.Views;

/// <summary>
/// Guardias de la propia infraestructura headless.
///
/// La versión anterior arrancaba Avalonia con <c>SetupWithoutStarting</c> y se tragaba cualquier excepción en
/// un <c>try/catch</c> vacío: si el arranque fallaba, el síntoma aparecía mucho después y en otro test. Estas
/// pruebas fijan las propiedades de las que depende todo lo demás:
/// <list type="bullet">
///   <item>Una sola aplicación viva, con Skia real (sin fotogramas no hay pruebas de render).</item>
///   <item>Despacho serializado y reentrante desde cualquier hilo.</item>
///   <item>Estilos sin animaciones activas: en Avalonia 12 no hay animador público para
///   <c>RenderTransform</c>, así que adjuntar un estilo que anime esa propiedad lanzaba al construir
///   cualquier <c>TopLevel</c>, y una captura con animaciones en vuelo no es determinista.</item>
///   <item>El contrato de hilo: lo que construye controles pasa por el hilo de UI, y equivocarse lo dice un
///   mensaje, no un <c>NullReferenceException</c> tres capas más abajo.</item>
/// </list>
/// </summary>
[Collection(VisualSnapshotsCollection.Name)]
public class HeadlessInfrastructureTests
{
    [Fact]
    public void Session_ShouldExposeASingleInitializedApplicationWithRealRendering()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            Application.Current.Should().NotBeNull("la sesión debe arrancar la aplicación real de FileFlow");
            Dispatcher.UIThread.CheckAccess().Should().BeTrue("el cuerpo del test corre en el hilo de UI de la sesión");

            Application.Current!.Styles.Should().NotBeEmpty("los estilos globales de la aplicación deben estar cargados");

            // Renderizado real: sin Skia (o con 'UseHeadlessDrawing') no hay fotogramas que comparar.
            var window = new Window { Width = 40, Height = 20, Content = new TextBlock { Text = "x" } };
            window.Show();

            try
            {
                var frame = window.CaptureRenderedFrame();
                frame.Should().NotBeNull("la sesión headless debe poder capturar fotogramas (Skia + UseHeadlessDrawing=false)");
                frame!.PixelSize.Should().Be(new PixelSize(40, 20));
            }
            finally
            {
                VisualSnapshot.DetachTree(window);
                window.Close();
            }
        });
    }

    [Fact]
    public void Session_ShouldBeInitializedOnlyOnce_EvenWhenManyThreadsAskForIt()
    {
        var failures = new List<string>();

        Parallel.For(0, 16, _ =>
        {
            try
            {
                AvaloniaTestHelper.EnsureInitialized();
                AvaloniaTestHelper.IsInitialized.Should().BeTrue();
            }
            catch (Exception ex)
            {
                lock (failures)
                {
                    failures.Add(ex.Message);
                }
            }
        });

        failures.Should().BeEmpty("'EnsureInitialized' debe ser idempotente y segura ante llamadas concurrentes");
    }

    [Fact]
    public async Task Dispatch_ShouldSerializeWorkOnTheUiThread_FromAnyCallerThread()
    {
        const int workers = 8;
        var seen = new List<int>();

        var tasks = Enumerable.Range(0, workers).Select(index => Task.Run(() =>
        {
            AvaloniaTestHelper.RunOnUI(() =>
            {
                Dispatcher.UIThread.CheckAccess().Should().BeTrue("todo despacho debe ejecutarse en el hilo de UI");

                lock (seen)
                {
                    seen.Add(index);
                }
            });
        })).ToArray();

        var all = Task.WhenAll(tasks);
        var finished = await Task.WhenAny(all, Task.Delay(TimeSpan.FromSeconds(60)));

        finished.Should().BeSameAs(all, "el despacho concurrente no debe bloquearse");

        await all;

        seen.Should().HaveCount(workers, "todas las tareas encoladas deben ejecutarse: el bucle de la sesión las serializa");
    }

    [Fact]
    public async Task NestedDispatch_ShouldRunInline_InsteadOfDeadlockingTheUiLoop()
    {
        // Un helper público usado desde dentro de una fábrica (o de otro despacho) anidaría dos veces. Si el
        // despacho anidado se encolara y se esperara su resultado, el bucle quedaría esperando a un trabajo
        // que sólo él puede atender: el test no fallaría, se quedaría colgado hasta el 'timeout' del runner.
        var probe = Task.Run(() =>
        {
            int fromNested = AvaloniaTestHelper.RunOnUI(() => AvaloniaTestHelper.RunOnUI(() => 7));

            bool nestedRanOnTheSameThread = AvaloniaTestHelper.RunOnUI(
                () => AvaloniaTestHelper.RunOnUI(() => AvaloniaTestHelper.IsOnUIThread));

            return (fromNested, nestedRanOnTheSameThread);
        });

        var finished = await Task.WhenAny(probe, Task.Delay(TimeSpan.FromSeconds(30)));

        finished.Should().BeSameAs(probe, "un despacho anidado debe resolverse en línea, no esperando al bucle");

        var (fromNested, nestedRanOnTheSameThread) = await probe;

        fromNested.Should().Be(7);
        nestedRanOnTheSameThread.Should().BeTrue("el trabajo anidado sigue ejecutándose en el hilo de UI");
    }

    [Fact]
    public void AppStyles_ShouldNotAnimateUnsupportedRenderTransform()
    {
        string stylesDirectory = Path.Combine(TestRepositoryLocator.RepositoryRoot(), "FileFlow.App", "Styles");
        var offenders = Directory.EnumerateFiles(stylesDirectory, "*.axaml", SearchOption.TopDirectoryOnly)
            .SelectMany(file =>
            {
                var document = XDocument.Load(file);
                return document.Descendants()
                    .Where(element => element.Name.LocalName == "Animation")
                    .SelectMany(animation => animation.Descendants()
                        .Where(element => element.Name.LocalName == "Setter" &&
                                          string.Equals((string?)element.Attribute("Property"), "RenderTransform", StringComparison.Ordinal))
                        .Select(_ => Path.GetFileName(file)));
            })
            .Distinct()
            .ToArray();

        offenders.Should().BeEmpty(
            "Avalonia 12 no registra un animador para RenderTransform; use una propiedad animable como Opacity");
    }

    [Fact]
    public void Styles_ShouldHaveNoActiveAnimations_InTheHeadlessSession()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var animated = new List<string>();
            CollectAnimatedStyles(Application.Current!.Styles, animated);

            animated.Should().BeEmpty(
                "la sesión desactiva las animaciones de los estilos: no hay animador público para RenderTransform " +
                "en Avalonia 12 (adjuntar la ventana lanzaba InvalidOperationException) y una captura con " +
                "animaciones en vuelo no es determinista. Animaciones aún activas: " + string.Join(", ", animated));
        });

        static void CollectAnimatedStyles(IEnumerable<IStyle> styles, List<string> collected)
        {
            foreach (var style in styles)
            {
                switch (style)
                {
                    case Style concrete:
                        if (concrete.Animations is { Count: > 0 })
                        {
                            collected.Add(concrete.Selector?.ToString() ?? "<sin selector>");
                        }

                        CollectAnimatedStyles(concrete.Children, collected);
                        break;

                    case ControlTheme theme:
                        if (theme.Animations is { Count: > 0 })
                        {
                            collected.Add("ControlTheme con animaciones");
                        }

                        CollectAnimatedStyles(theme.Children, collected);
                        break;

                    case Styles group:
                        CollectAnimatedStyles(group, collected);
                        break;
                }
            }
        }
    }

    [Fact]
    public void HostResources_ShouldBeRegisteredSoRenderedTextsAreReal()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            // Sin el diccionario registrado (que en la app real carga OnFrameworkInitializationCompleted),
            // los textos de las vistas se renderizarían vacíos y las capturas no servirían como referencia.
            var localized = FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("ThemeCustomizer_ImportBtn");

            localized.Should().NotBeNullOrWhiteSpace("los textos del host deben resolverse en las capturas");
            FileFlow.Sdk.Localization.LocalizationManager.Instance.CurrentLanguage
                .Should().Be(AvaloniaTestHelper.PinnedLanguage[..2], "el idioma de las capturas está fijado para que no dependan del entorno");
        });
    }

    [Fact]
    public void WindowsWithApplicationStyles_ShouldBeConstructibleAndRenderable()
    {
        // Regresión concreta: con los estilos de la aplicación adjuntos, cualquier TopLevel lanzaba
        // "No animator registered for the property RenderTransform".
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var window = new FileFlow.App.Views.Components.ThemeCustomizerWindow
            {
                DataContext = new FileFlow.App.ViewModels.ThemeCustomizerViewModel(
                    new FileFlow.App.Services.CustomThemeService(
                        Path.Combine(Path.GetTempPath(), $"infra_window_{Guid.NewGuid():N}.json")))
            };

            window.Show();

            try
            {
                window.CaptureRenderedFrame().Should().NotBeNull("las ventanas reales de la aplicación deben poder renderizarse");
            }
            finally
            {
                VisualSnapshot.DetachTree(window);
                window.Close();
            }
        });
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Contrato de hilo
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildingControlsOffTheSessionThread_ShouldFailWithAnActionableMessage()
    {
        // El fallo que sustituye esta guardia era invisible: construir la galería desde el hilo del runner
        // «funcionaba» y lo que aparecía después era «el token no existe en el tema activo», un mensaje que
        // apuntaba al tema cuando el problema era el hilo.
        Func<Control> buildOffThread = DesignGallery.Build;

        buildOffThread.Should().Throw<InvalidOperationException>()
            .WithMessage("*hilo de UI*", "el mensaje debe decir qué hacer, no sólo que algo falló");
    }

    [Fact]
    public void ResolvingATokenOffTheSessionThread_ShouldFailWithAnActionableMessage()
    {
        Action offThread = () => VisualSnapshot.ResolveToken<IBrush>("BgCardBrush");

        offThread.Should().Throw<InvalidOperationException>()
            .WithMessage("*hilo de UI*");
    }

    [Fact]
    public void Capture_ShouldBuildItsContentInsideTheSessionThread()
    {
        // La fábrica es la única puerta de entrada a una captura, y esta prueba fija por qué: se invoca ya en
        // el hilo de UI, con la aplicación arrancada y los tokens del tema publicados.
        bool sawApplication = false;
        string resolvedToken = string.Empty;

        byte[] capture = VisualSnapshot.Capture(
            () =>
            {
                AvaloniaTestHelper.IsOnUIThread.Should().BeTrue("la fábrica corre en el hilo de UI de la sesión");
                Dispatcher.UIThread.CheckAccess().Should().BeTrue();

                Application.Current.Should().NotBeNull("la aplicación ya está arrancada cuando la fábrica se ejecuta");
                sawApplication = true;

                resolvedToken = VisualSnapshot.ResolveToken<IBrush>("BgCardBrush").GetType().Name;

                return new Border { Width = 30, Height = 20, Background = VisualSnapshot.ResolveToken<IBrush>("AccentPrimaryBrush") };
            },
            30,
            20,
            "dark_fluent");

        sawApplication.Should().BeTrue();
        resolvedToken.Should().Be("SolidColorBrush");

        var pixel = VisualSnapshot.PixelAt(capture, 15, 10);
        var accent = Avalonia.Media.Color.Parse(VisualSnapshot.ResolveTheme("dark_fluent").AccentPrimary);

        pixel.R.Should().BeCloseTo(accent.R, 12, "la captura debe contener el color del token, con el tema aplicado");
        pixel.G.Should().BeCloseTo(accent.G, 12);
        pixel.B.Should().BeCloseTo(accent.B, 12);
    }

    [Fact]
    public void Session_ShouldDecodeImagesOnTheUiThread()
    {
        // La interfaz de render no sólo pinta: también decodifica imágenes. Hacerlo desde el hilo del runner
        // no lanza, devuelve 'null' (y el cargador universal de la aplicación se lo traga al caer a su plan
        // B), de modo que el síntoma es un mapa de bits vacío en cualquier parte. Esta guardia fija dónde se
        // puede decodificar y deja constancia de por qué las pruebas de imágenes despachan su trabajo.
        byte[] png = CreateTinyPng();

        var size = AvaloniaTestHelper.RunOnUI(() =>
        {
            using var bitmap = new Avalonia.Media.Imaging.Bitmap(new MemoryStream(png));
            return bitmap.PixelSize;
        });

        size.Should().Be(new PixelSize(4, 3), "la sesión debe poder decodificar un PNG en su hilo de UI");
    }

    [Fact]
    public void Capture_ShouldRejectAnEmptyFactory()
    {
        Action captureNothing = () => VisualSnapshot.Capture(() => null!, 40, 30, "dark_fluent");

        captureNothing.Should().Throw<Exception>()
            .WithMessage("*null*", "una fábrica que no devuelve nada es un error del test, no una captura en negro");
    }

    /// <summary>PNG diminuto y determinista, para comprobar que la sesión puede decodificar imágenes.</summary>
    private static byte[] CreateTinyPng()
    {
        using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(4, 3);
        using var stream = new MemoryStream();

        image.SaveAsPng(stream);
        return stream.ToArray();
    }
}
