using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using FileFlow.App;
using FileFlow.App.Services;
using FileFlow.App.Views;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Guardias de la <b>pantalla de carga (splash)</b> del arranque.
///
/// La splash se integró, se perdió cuando el arranque se reescribió por etapas (la ventana existía, con su
/// XAML y su API, pero ningún código la instanciaba: el usuario arrancaba sin feedback), y ninguna prueba
/// del suite lo detectó porque todas las demás montan piezas sueltas del shell. Estas pruebas fijan el
/// contrato completo:
/// <list type="bullet">
///   <item>la ventana existe, se muestra y pinta un fotograma real con contenido (no una ventana en blanco);</item>
///   <item>la API de progreso actualiza estado, barra, porcentaje e insignia de nodos;</item>
///   <item>el XAML consume claves de recursos que existen en ES y EN (una clave ausente se pinta en blanco);</item>
///   <item>y el código de arranque mantiene la secuencia real: recursos, splash, etapas, shell y despedida.</item>
/// </list>
/// </summary>
[Collection(FileFlow.Tests.Unit.Views.VisualSnapshotsCollection.Name)]
public class SplashScreenStartupTests
{
    private const string Spanish = "es-ES";
    private const string English = "en-US";

    private const string SplashXamlPath = "FileFlow.App/Views/SplashScreenWindow.axaml";
    private const string AppSourcePath = "FileFlow.App/App.axaml.cs";

    private static readonly string[] StatusKeys =
    [
        "Splash_StatusServices",
        "Splash_StatusPreferences",
        "Splash_StatusTheme",
        "Splash_StatusPlugins",
        "Splash_StatusInterface",
        "Splash_StatusReady",
    ];

    private static readonly string[] XamlKeys =
    [
        "Splash_InitializingEngine",
        "Splash_LoadingNodes",
        "Splash_StatusServices",
        "Splash_Footer",
    ];

    [Fact]
    public void TheSplash_ShouldShowAndRenderItsContent_WithoutThrowing()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var splash = new SplashScreenWindow();
            splash.Show();

            try
            {
                splash.IsVisible.Should().BeTrue("la pantalla de carga debe estar visible durante el arranque");

                Dispatcher.UIThread.RunJobs();

                // El contenido debe pintarse de verdad: un XAML roto en la splash dejaría una ventana vacía
                // (o transparente) que el usuario vería como «no hay splash» aunque el proceso siga vivo.
                var frame = splash.CaptureRenderedFrame();
                frame.Should().NotBeNull("la splash debe producir un fotograma real");
                frame!.PixelSize.Width.Should().BeGreaterThan(0);

                var texts = splash.GetLogicalDescendants().OfType<TextBlock>().ToList();
                texts.Should().Contain(t => !string.IsNullOrWhiteSpace(t.Text),
                    "la splash debe mostrar texto legible, no etiquetas en blanco");

                splash.GetLogicalDescendants().OfType<ProgressBar>().Should().ContainSingle(
                    "el progreso del arranque se comunica con la barra");
            }
            finally
            {
                VisualSnapshot.DetachTree(splash);
                splash.Close();
            }
        });
    }

    [Fact]
    public void TheSplash_ShouldShowTheVersion_ExactlyOncePrefixed()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var splash = new SplashScreenWindow();

            try
            {
                var version = splash.GetLogicalDescendants().OfType<TextBlock>()
                    .Single(t => t.Name == "TxtVersion");

                version.Text.Should().Be(
                    FileFlow.Sdk.AppVersionInfo.DisplayVersion,
                    "la splash muestra la versión del SDK, y 'Acerca de' usa esa misma cadena");

                version.Text.Should().StartWith("v").And.NotStartWith(
                    "vv",
                    "el prefijo se aplicaba dos veces (DisplayVersion ya lo trae): la primera pantalla del " +
                    "producto mostraba «vv1.0.0-…», visible en su línea base visual");
            }
            finally
            {
                VisualSnapshot.DetachTree(splash);
                splash.Close();
            }
        });
    }

    [Fact]
    public void UpdateStatus_ShouldDriveBarStatusAndPercentage()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var splash = new SplashScreenWindow();
            splash.Show();

            try
            {
                splash.UpdateStatus("Etapa de prueba", 42);

                var bar = splash.GetLogicalDescendants().OfType<ProgressBar>().Single();
                bar.Value.Should().Be(42, "la barra debe reflejar el progreso de la etapa");
                TextOf(splash, "TxtStatus").Should().Be("Etapa de prueba");
                TextOf(splash, "TxtPercentage").Should().Be("42%", "el porcentaje acompaña siempre a la barra");

                // Fuera de rango: el progreso se acota, no se desborda.
                splash.UpdateStatus("Fase final", 250);
                bar.Value.Should().Be(100);
                TextOf(splash, "TxtPercentage").Should().Be("100%");
            }
            finally
            {
                VisualSnapshot.DetachTree(splash);
                splash.Close();
            }
        });
    }

    [Fact]
    public void SetNodeCount_ShouldFormatTheBadge_WithTheLocalizedTemplate()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            try
            {
                foreach (string culture in new[] { Spanish, English })
                {
                    AvaloniaTestHelper.SetCultureOnUI(culture);

                    var splash = new SplashScreenWindow();
                    splash.Show();

                    try
                    {
                        splash.SetNodeCount(78);

                        TextOf(splash, "TxtNodesBadge").Should().Be(
                            FileFlow.Sdk.Localization.LocalizationManager.Instance["Splash_NodesBadge"]
                                .Replace("{0}", "78", StringComparison.Ordinal),
                            $"la insignia debe usar la plantilla localizada del idioma activo ({culture})");
                    }
                    finally
                    {
                        VisualSnapshot.DetachTree(splash);
                        splash.Close();
                    }
                }
            }
            finally
            {
                AvaloniaTestHelper.SetCultureOnUI(AvaloniaTestHelper.PinnedLanguage);
            }
        });
    }

    [Fact]
    public void EverySplashText_ShouldExistInBothDictionaries_WithRealContent()
    {
        string root = TestRepositoryLocator.RepositoryRoot();

        // Claves de estado citadas por el código de arranque + claves consumidas por el XAML.
        var keys = StatusKeys
            .Concat(XamlKeys)
            .Append("Splash_NodesBadge")
            .Append("Splash_LoadingNodes")
            .Append("Splash_InitializingEngine")
            .Append("Splash_Footer")
            .Distinct()
            .ToList();

        var english = ReadResxKeys(Path.Combine(root, "FileFlow.App/Resources/Strings.resx"));
        var spanish = ReadResxKeys(Path.Combine(root, "FileFlow.App/Resources/Strings.es.resx"));

        var missing = keys.Where(k => !english.Contains(k) || !spanish.Contains(k)).ToList();
        missing.Should().BeEmpty(
            "toda clave del splash debe existir en ambos idiomas: una clave ausente se pinta en blanco. " +
            "Faltan: " + string.Join(", ", missing));

        foreach (string key in keys)
        {
            FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString(key, string.Empty)
                .Should().NotBeNullOrWhiteSpace($"'{key}' debe tener texto real");
        }
    }

    [Fact]
    public void TheSplashXaml_ShouldConsumeLocalizationKeys_NotHardcodedStrings()
    {
        string xaml = File.ReadAllText(Path.Combine(TestRepositoryLocator.RepositoryRoot(), SplashXamlPath));

        foreach (string key in XamlKeys)
        {
            xaml.Should().Contain(
                $"[{key}]",
                $"el texto de la splash debe venir de la clave localizada '{key}', no de una cadena fija");
        }

        // Regresión: el splash mostraba un pictograma junto a la cuenta de nodos. La iconografía de la UI
        // es vectorial; el glifo del badge ya no puede volver al XAML.
        xaml.Should().NotMatchRegex(@"[\uD800-\uDBFF][\uDC00-\uDFFF]", "el XAML del splash no debe llevar pictogramas");
    }

    [Fact]
    public void TheSplashXaml_ShouldUseOnlyThemeTokens_AndNoInlineLiterals()
    {
        // Regresión: la splash llegó a acumular 11 colores literales (#0F172A, #6366F1, #94A3B8…) fuera del
        // tema. El Theme Studio no podía ajustarla y un tema claro la dejaba ilegible. Ahora todo sale de
        // los tokens (AccentPrimary, TextSecondary, BgDark…) y las clases del sistema de diseño.
        string xaml = File.ReadAllText(Path.Combine(TestRepositoryLocator.RepositoryRoot(), SplashXamlPath));

        var hexLiterals = Regex.Matches(xaml, "#[0-9A-Fa-f]{6,8}\\b").Select(m => m.Value).ToList();
        hexLiterals.Should().BeEmpty(
            "el XAML del splash debe usar DynamicResource, no colores literales. Encontrados: " +
            string.Join(", ", hexLiterals));

        var fontSizes = Regex.Matches(xaml, "FontSize=\"[0-9.]+\"").Select(m => m.Value).ToList();
        fontSizes.Should().BeEmpty(
            "la escala tipográfica es del tema: usa las clases (caption/body/micro…) o DynamicResource. " +
            "Encontrados: " + string.Join(", ", fontSizes));

        // Y debe consumir la capa de estilos: sin clases, los tokens caen otra vez como literales.
        xaml.Should().Contain("Classes=", "el splash se compone con las clases del sistema de diseño");
    }

    [Fact]
    public void TheShimmer_ShouldStayUnderTestControl_AndNotStartByItself()
    {
        // La animación vive en código (la sesión headless purga las animaciones de estilo y una captura con
        // animaciones en vuelo no es determinista). El contrato: el temporizador NO arranca en el
        // constructor —sólo la aplicación real llama StartShimmer—, así que las capturas ven siempre el
        // primer fotograma quieto.
        string codeBehind = SourceText.CodeWithoutComments("FileFlow.App/Views/SplashScreenWindow.axaml.cs");

        codeBehind.Should().Contain("public void StartShimmer()",
            "el arranque del barrido debe ser explícito, no un efecto del constructor");

        // El cuerpo de StartShimmer está entre su firma y el siguiente miembro (las llaves anidadas del
        // guard-clause impiden un tramo por regex simple, así que se acota por rango).
        int startShimmerAt = codeBehind.IndexOf("public void StartShimmer()", StringComparison.Ordinal);
        int closeFadeAt = codeBehind.IndexOf("public async Task CloseWithFadeAsync()", StringComparison.Ordinal);

        (startShimmerAt >= 0 && closeFadeAt > startShimmerAt).Should().BeTrue(
            "el lint debe estar viendo StartShimmer real del code-behind");

        codeBehind.Substring(startShimmerAt, closeFadeAt - startShimmerAt).Should().Contain(
            "_shimmerTimer.Start()",
            "StartShimmer es el único punto que activa el barrido");

        // El constructor no arranca el temporizador: su cuerpo está entre su firma y el siguiente miembro.
        int constructorAt = codeBehind.IndexOf("public SplashScreenWindow()", StringComparison.Ordinal);
        int updateStatusAt = codeBehind.IndexOf("public void UpdateStatus", StringComparison.Ordinal);

        (constructorAt >= 0 && updateStatusAt > constructorAt).Should().BeTrue(
            "el lint debe estar viendo el constructor real del code-behind");

        codeBehind.Substring(constructorAt, updateStatusAt - constructorAt).Should().NotContain(
            "_shimmerTimer.Start()",
            "el constructor no puede arrancar el temporizador: sólo StartShimmer lo hace");

        // Y la aplicación real lo llama tras mostrar la ventana (lint con comentarios fuera).
        string startup = SourceText.CodeWithoutComments(AppSourcePath);

        startup.Should().Contain("splash.StartShimmer();",
            "la aplicación real debe activar el barrido al mostrar la splash");

        int showAt = startup.IndexOf("splash.Show();", StringComparison.Ordinal);
        int shimmerAt = startup.IndexOf("splash.StartShimmer();", StringComparison.Ordinal);

        (showAt >= 0 && shimmerAt > showAt).Should().BeTrue(
            "el barrido se activa una vez la ventana está mostrada");
    }

    [Fact]
    public void TheShimmerStep_ShouldRecoverTheBrush_ThatTheThemePhaseReplaces()
    {
        // Regresión real (medida en la aplicación, no supuesta): la barra declara
        // Foreground="{DynamicResource AccentPrimaryBrush}" y la etapa StartupPhase.Theme republica ese recurso
        // mientras la splash sigue en pantalla, así que Avalonia vuelve a evaluar el recurso y escribe un
        // pincel SÓLIDO encima del gradiente del barrido. El tick casteaba a ciegas y lanzaba
        // InvalidCastException: exactamente una entrada por arranque en crash.log y el barrido muerto para el
        // resto de la pantalla. Ninguna prueba lo veía porque todas mostraban la splash SIN arrancar el
        // barrido — el temporizador sólo corre en la aplicación real.
        string originalThemeId = ThemeManager.Instance.CurrentThemeId;

        AvaloniaTestHelper.RunOnUI(() =>
        {
            var splash = new SplashScreenWindow();
            splash.Show();

            try
            {
                splash.StartShimmer();

                var bar = splash.GetLogicalDescendants().OfType<ProgressBar>().Single();
                bar.Foreground.Should().BeOfType<LinearGradientBrush>(
                    "el constructor impone el gradiente que recorre la barra");

                // Lo que hace la fase de tema del arranque: republicar el tema activo.
                ThemeManager.Instance.SetThemeById("dark_fluent");
                Dispatcher.UIThread.RunJobs();

                bar.Foreground.Should().BeOfType<SolidColorBrush>(
                    "el tema sustituye el pincel de la barra: éste es el escenario que rompía el tick. " +
                    "Si esto deja de cumplirse, la guardia debe reescribirse sobre el escenario real, no relajarse");

                Action step = splash.AdvanceShimmer;
                step.Should().NotThrow("el tick no puede dar por hecho que la barra conserva nuestro gradiente");

                bar.Foreground.Should().BeOfType<LinearGradientBrush>("el barrido se recupera y sigue animando");
                ((LinearGradientBrush)bar.Foreground!).GradientStops.Should().HaveCount(3);
            }
            finally
            {
                ThemeManager.Instance.SetThemeById(originalThemeId);
                Dispatcher.UIThread.RunJobs();
                VisualSnapshot.DetachTree(splash);
                splash.Close();
            }
        });
    }

    [Fact]
    public void AdvanceShimmer_ShouldAdoptTheNewThemeAccent_AndKeepTheSweepMoving()
    {
        string originalThemeId = ThemeManager.Instance.CurrentThemeId;

        AvaloniaTestHelper.RunOnUI(() =>
        {
            var splash = new SplashScreenWindow();
            splash.Show();

            try
            {
                splash.StartShimmer();
                var bar = splash.GetLogicalDescendants().OfType<ProgressBar>().Single();

                ThemeManager.Instance.SetThemeById("light_studio");
                Dispatcher.UIThread.RunJobs();

                splash.AdvanceShimmer();

                Application.Current!.TryFindResource("AccentPrimaryBrush", out var accent).Should().BeTrue(
                    "el tema publicado debe exponer el token que consume el barrido");
                var expected = ((ISolidColorBrush)accent!).Color;

                var stops = ((LinearGradientBrush)bar.Foreground!).GradientStops;
                stops[0].Color.Should().Be(expected, "el barrido sigue el tema en caliente, no el color del arranque");
                stops[2].Color.Should().Be(expected);

                // Y avanza: un gradiente recuperado pero quieto sería la animación muerta de otro modo.
                double first = stops[1].Offset;
                Thread.Sleep(60);
                splash.AdvanceShimmer();
                double second = ((LinearGradientBrush)bar.Foreground!).GradientStops[1].Offset;

                second.Should().NotBe(first, "el barrido debe recorrer la barra, no quedarse en una posición fija");
                second.Should().BeInRange(0.0, 1.0);
            }
            finally
            {
                ThemeManager.Instance.SetThemeById(originalThemeId);
                Dispatcher.UIThread.RunJobs();
                VisualSnapshot.DetachTree(splash);
                splash.Close();
            }
        });

        // Y el tick no puede volver a asumir el tipo del pincel: el casteo directo era el fallo.
        string codeBehind = SourceText.CodeWithoutComments("FileFlow.App/Views/SplashScreenWindow.axaml.cs");

        codeBehind.Should().NotContain(
            "(LinearGradientBrush)PbProgress.Foreground",
            "el pincel de la barra se recupera antes de animarlo; castearlo a ciegas es la regresión");

        codeBehind.Should().Contain(
            "public void AdvanceShimmer()",
            "el paso del barrido debe ser alcanzable desde las pruebas: es el camino que fallaba en la app");
    }

    [Fact]
    public void TheStartup_ShouldKeepTheSplashIntegrated_InItsRealSequence()
    {
        // Sin comentarios: un «new SplashScreenWindow()» comentado es exactamente el falso negativo que
        // dejó pasar el bug original (la guardia anterior veía el texto, no el código).
        string source = SourceText.CodeWithoutComments(AppSourcePath);

        // Lint de integración: la splash se perdió cuando el arranque se reescribió y ninguna prueba lo vio.
        // El contrato se vigila sobre el código real, no sobre una copia simulada que podría divergir.
        source.Should().Contain("new SplashScreenWindow()",
            "el arranque debe instanciar la pantalla de carga");

        source.Should().Contain("StartupPhase.Splash",
            "la splash debe ser una etapa aislada del orquestador, con su nombre en el informe de fallos");

        source.Should().Contain("splash?.CloseWithFadeAsync()",
            "el splash se retira desvaneciéndose cuando la ventana principal ya está en pantalla");

        source.Should().Contain("splash?.Close()",
            "un arranque fallido debe retirar la splash antes de mostrar la ventana de error");

        source.Should().Contain("splash.SetNodeCount(",
            "la insignia debe presentar el número real de nodos descubiertos");

        source.Should().Contain("StartupPhase.Resources, RegisterHostResources",
            "los recursos del host se registran antes de mostrar la splash, para que sus textos salgan traducidos");

        int resourcesAt = source.IndexOf("StartupPhase.Resources", StringComparison.Ordinal);
        int splashAt = source.IndexOf("StartupPhase.Splash", StringComparison.Ordinal);
        int shellAt = source.IndexOf("StartupPhase.Shell", StringComparison.Ordinal);

        (resourcesAt >= 0 && splashAt > resourcesAt && shellAt > splashAt).Should().BeTrue(
            "el orden del arranque debe ser: recursos del host, splash, resto de etapas y shell");
    }

    [Fact]
    public void EveryStartupPhase_ShouldExposeTheSplashStage_WithALegibleName()
    {
        StartupPhaseDescriptions.Describe(StartupPhase.Splash).Should().NotBeNullOrWhiteSpace();
        StartupPhaseDescriptions.Fallback(StartupPhase.Splash).Should().NotBeNullOrWhiteSpace();
        StartupPhaseDescriptions.Key(StartupPhase.Splash).Should().Contain(nameof(StartupPhase.Splash));
    }

    /// <summary>
    /// Retira comentarios de línea y de bloque del código C#, respetando los literales de cadena (que
    /// pueden contener «//» legítimos, como «https://»). Sin esto, el lint de integración acepta código
    /// comentado como si estuviera vivo.
    /// </summary>
    /// <summary>Texto actual de un control nombrado de la splash.</summary>
    private static string TextOf(Window splash, string name) =>
        splash.GetLogicalDescendants().OfType<TextBlock>().Single(t => t.Name == name).Text ?? string.Empty;

    private static HashSet<string> ReadResxKeys(string path)
    {
        File.Exists(path).Should().BeTrue($"debe existir el diccionario de recursos {path}");

        return Regex.Matches(File.ReadAllText(path), "name=\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
    }
}
