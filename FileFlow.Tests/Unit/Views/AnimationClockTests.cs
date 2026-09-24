using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace FileFlow.Tests.Unit.Views;

/// <summary>
/// El <b>reloj de animación</b> del suite, que es lo que permite afirmar el valor de una propiedad animada sin
/// esperar tiempo real.
///
/// <para><b>Qué se guarda aquí</b>: (1) que el reloj que usan las animaciones sea el nuestro y no el de la
/// sesión —si Avalonia dejara de resolverlo por el locator, esto fallaría en vez de volver a producir fallos
/// intermitentes—, (2) que sin pulsación <b>ninguna</b> cantidad de fotogramas mueva una transición, que es la
/// prueba de que el tiempo real ya no decide nada, (3) que una pulsación completa la deje clavada en su valor
/// final y una parcial la deje a medio camino (se interpola de verdad, no salta), (4) que avanzar tiempo de
/// interfaz no cueste ese tiempo en real, y (5) que ninguna transición declarada en el producto dure más que
/// un asentado —porque entonces quedaría a medias y la aserción del token fallaría como si el token estuviera
/// mal—.</para>
///
/// <para><b>Colección exclusiva</b>: abre ventanas y mueve el reloj que comparten todas las animaciones de la
/// sesión, así que va serializada como el resto de la interfaz.</para>
/// </summary>
[Collection(VisualSnapshotsCollection.Name)]
public class AnimationClockTests
{
    [Fact]
    public void TheAnimationClockOfTheSession_ShouldBeTheSuiteOwn()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            AnimationClock.IsInEffect.Should().BeTrue(
                "las animaciones de la sesión tienen que colgar del reloj virtual: si Avalonia deja de resolverlo " +
                $"por el AvaloniaLocator (ahora responde '{AnimationClock.Implementation}'), el suite vuelve a " +
                "medir tiempo real y los fallos intermitentes de las propiedades animadas regresan");

            AnimationClock.Implementation.Should().NotContain("MediaContextClock",
                "el reloj de la sesión headless avanza con el tiempo transcurrido y no se puede pulsar a mano");
        });
    }

    [Fact]
    public void RenderTicks_ShouldNotMoveATransition_WithoutAPulse()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var (window, border) = AnimatedBorder();

            try
            {
                border.Background = new SolidColorBrush(Colors.White);

                // Medio segundo de reloj de render (el presupuesto de un asentado real de los de antes). Con el
                // reloj de la sesión encima de la máquina, esto bastaba para completar la transición.
                Ticks(200);

                ColorOf(border).Should().Be(Colors.Black,
                    "sin pulsar el reloj de animación ninguna cantidad de fotogramas puede mover la transición: " +
                    "es la diferencia entre medir la animación y medir el reloj del sistema");
            }
            finally
            {
                Close(window);
            }
        });
    }

    [Fact]
    public void Settling_ShouldLandTheTransitionExactlyOnItsFinalValue()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var (window, border) = AnimatedBorder();

            try
            {
                border.Background = new SolidColorBrush(Colors.White);

                InputSimulator.Settle();

                ColorOf(border).Should().Be(Colors.White,
                    "un asentado completo avanza 192 ms virtuales, por encima de la transición de 100 ms: el valor " +
                    "final se afirma exacto, sin esperar nada");
            }
            finally
            {
                Close(window);
            }
        });
    }

    [Fact]
    public void APartialAdvance_ShouldLeaveTheTransitionHalfway()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var (window, border) = AnimatedBorder();

            try
            {
                border.Background = new SolidColorBrush(Colors.White);

                // 64 ms virtuales de los 100 ms de la transición (menos el pulso que se consume como base).
                InputSimulator.Settle(4);

                var midway = ColorOf(border);
                midway.R.Should().BeGreaterThan(0, "a mitad de camino el blanco no ha llegado todavía")
                    .And.BeLessThan(255, "y el negro ya se ha dejado: la transición interpola, no salta");

                InputSimulator.Settle();

                ColorOf(border).Should().Be(Colors.White, "y al terminar el tiempo virtual, el valor final");
            }
            finally
            {
                Close(window);
            }
        });
    }

    /// <summary>
    /// Una captura no puede fotografiar una transición <b>a medio camino</b>: antes del fotograma tiene que
    /// asentar el reloj de animación, igual que hace un asentado de interacción.
    ///
    /// <para>Es el caso real: la captura construye el árbol, lo muestra y dispara —y un estado que cambia al
    /// montarse arranca su transición justo antes de la foto—. Sin asentar, el fotograma se toma con la
    /// transición en su valor de partida, así que la línea base congela un estado que el usuario nunca ve (y
    /// que se compara contra futuras capturas como si fuera el correcto). Aquí se pide el estado final al
    /// montarse y se exige que la captura lo haya alcanzado.</para>
    /// </summary>
    [Fact]
    public void ACapture_ShouldPhotographTheTransitionSettled()
    {
        byte[] captured = VisualSnapshot.Capture(
            () =>
            {
                var border = new Border
                {
                    Width = 80,
                    Height = 80,
                    Background = new SolidColorBrush(Colors.Black),
                    Transitions = new Transitions
                    {
                        new BrushTransition
                        {
                            Property = Border.BackgroundProperty,
                            Duration = TimeSpan.FromMilliseconds(100)
                        }
                    }
                };

                // El estado final se pide <b>al montarse</b>: es lo único que arranca la transición con el árbol
                // ya construido, y por tanto lo que la deja en vuelo cuando la captura va a disparar.
                border.AttachedToVisualTree += (_, _) => border.Background = new SolidColorBrush(Colors.White);

                return border;
            },
            200,
            200,
            "dark_fluent");

        VisualSnapshot.PixelAt(captured, 100, 100).Should().Be(
            new Rgba32(255, 255, 255),
            "la captura tiene que asentar el reloj de animación antes de fotografiar: con la transición en su " +
            "valor de partida la línea base congelaría un intermedio que el usuario nunca ve");
    }

    /// <summary>
    /// Ningún camino de captura puede fotografiar sin asentar: es la mitad estructural de la guardia anterior
    /// (la de comportamiento demuestra que asentar completa una transición; ésta, que <i>todos</i> los que
    /// fotografían lo hacen). Un tercer camino de captura sin asentado no rompería ninguna captura existente —
    /// sólo congelaría líneas base futuras a medio camino—, que es exactamente el tipo de fallo que se descubre
    /// hitos después.
    /// </summary>
    [Fact]
    public void EveryCapturePath_ShouldSettleBeforePhotographing()
    {
        const string source = "FileFlow.Tests/TestHelpers/VisualSnapshot.cs";

        string code = SourceText.CodeWithoutComments(source);

        int photographs = Regex.Matches(code, @"\bCaptureRenderedFrame\s*\(").Count;
        int settles = Regex.Matches(code, @"\bAnimationClock\.Settle\s*\(").Count;

        photographs.Should().BeGreaterThan(0,
            "el barrido tiene que encontrar dónde se fotografía: si no ve nada, esta guardia no guarda nada");

        settles.Should().Be(photographs,
            $"{source} fotografía en {photographs} sitios y asienta en {settles}: cada camino de captura tiene " +
            "que asentar el reloj de animación antes del fotograma (ver ACapture_ShouldPhotographTheTransitionSettled)");
    }

    [Fact]
    public void AdvancingVirtualTime_ShouldNotCostRealTime()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var watch = Stopwatch.StartNew();

            // 960 ms de interfaz (60 fotogramas). Con el reloj de la sesión, esto costaría 960 ms de reloj.
            AnimationClock.Advance(60);

            watch.Stop();

            watch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(200),
                $"avanzar {AnimationClock.Now.TotalMilliseconds:F0} ms de animación no puede costar ese tiempo en " +
                "real: el reloj es virtual, y si alguien lo devuelve al del sistema esta prueba lo dice");
        });
    }

    [Fact]
    public void EveryDeclaredTransition_ShouldFitInOneSettle()
    {
        var declared = DeclaredTransitions();

        declared.Should().NotBeEmpty(
            "el barrido tiene que encontrar las transiciones del producto: si no ve ninguna, no está guardando nada");

        declared.Select(t => t.File).Should().Contain(
            t => t.EndsWith("FileFlow.App/Styles/Buttons.axaml", StringComparison.OrdinalIgnoreCase),
            "el sistema de diseño declara transiciones en los estilos de componente");

        int budget = InputSimulator.SettleFrames * AnimationClock.FrameMilliseconds;

        var tooLong = declared
            .Where(t => t.Duration.TotalMilliseconds + AnimationClock.FrameMilliseconds > budget)
            .Select(t => $"{t.File}: {t.Duration.TotalMilliseconds:F0} ms")
            .ToList();

        tooLong.Should().BeEmpty(
            $"un asentado avanza {budget} ms virtuales y cada animación consume un pulso como base, así que una " +
            "transición más larga quedaría a medias y la aserción del token fallaría como si el token estuviera " +
            "mal. Sube 'InputSimulator.SettleFrames' o acorta la transición: " + string.Join(" | ", tooLong));
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Andamiaje
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Un borde con una transición de fondo de 100 ms y su ventana ya mostrada y asentada.</summary>
    private static (Window Window, Border Border) AnimatedBorder()
    {
        var border = new Border
        {
            Width = 80,
            Height = 80,
            Background = new SolidColorBrush(Colors.Black),
            Transitions = new Transitions
            {
                new BrushTransition
                {
                    Property = Border.BackgroundProperty,
                    Duration = TimeSpan.FromMilliseconds(100)
                }
            }
        };

        var window = new Window { Content = border, Width = 200, Height = 200 };
        window.Show();
        InputSimulator.Settle();

        return (window, border);
    }

    private static void Close(Window window)
    {
        VisualSnapshot.DetachTree(window);
        window.Close();
    }

    /// <summary>Fotogramas de reloj de render <b>sin</b> pulsar el de animación (lo contrario de asentar).</summary>
    private static void Ticks(int count)
    {
        for (int frame = 0; frame < count; frame++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        Dispatcher.UIThread.RunJobs();
    }

    private static Color ColorOf(Border border) => border.Background is ISolidColorBrush solid
        ? solid.Color
        : throw new InvalidOperationException(
            $"El fondo del borde no es un color sólido sino '{border.Background?.GetType().Name ?? "null"}'.");

    /// <summary>
    /// Toda transición declarada en el producto, con su duración. Se lee el XAML de disco (host <b>y</b>
    /// plugins: la doctrina del repositorio es que un plugin es autónomo, así que también trae sus animaciones)
    /// en vez de fiarse de una lista escrita a mano, que caducaría con la siguiente vista.
    /// </summary>
    private static List<(string File, TimeSpan Duration)> DeclaredTransitions()
    {
        string root = TestRepositoryLocator.RepositoryRoot();
        var transition = new Regex(
            @"<[\w:]*Transition\b[^>]*?\bDuration=""(?<duration>[^""]+)""",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        var found = new List<(string File, TimeSpan Duration)>();

        foreach (string project in Directory.EnumerateDirectories(root, "FileFlow.*", SearchOption.TopDirectoryOnly))
        {
            string name = Path.GetFileName(project);

            if (!name.StartsWith("FileFlow.App", StringComparison.Ordinal)
                && !name.StartsWith("FileFlow.Plugin.", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (string file in Directory.EnumerateFiles(project, "*.axaml", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string text = File.ReadAllText(file);
                string relative = Path.GetRelativePath(root, file).Replace('\\', '/');

                foreach (Match match in transition.Matches(text))
                {
                    found.Add((relative, TimeSpan.Parse(match.Groups["duration"].Value, CultureInfo.InvariantCulture)));
                }
            }
        }

        return found;
    }
}
