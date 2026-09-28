using System;
using System.IO;
using System.Text.RegularExpressions;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// La guardia de la <b>sonda de autorrevisión del host de escritorio</b> (hito 267): el modo
/// <c>FileFlow.App.exe --selfcheck</c>, que arranca la aplicación real —contenedor, plugins, vistas y estilos
/// del producto— sobre la plataforma headless con Skia real y mide el lienzo con <b>puntero inyectado</b>,
/// terminando el proceso con su veredicto.
///
/// <para><b>Qué protege</b>: (1) que el modo exista y tenga su puerta en la línea de comandos de la aplicación
/// (<c>Program</c>), no en un andamio aparte; (2) que corra sobre el anfitrión que <b>sabe</b> inyectar un
/// puntero —el escritorio no expone inyección de entrada cruda, así que sin la plataforma headless la sonda
/// mediría un lienzo que ningún gesto toca y daría verde—; (3) que <b>sólo mida</b>: los gestos mueven el
/// encuadre, la sonda no lo escribe (si lo escribiera, mediría su propia mutación y no el gesto); (4) que
/// prepare la escena con el camino que NO escribe estado del usuario; y (5) que el envoltorio de la línea de
/// comandos espere el veredicto, que es el código de salida.</para>
///
/// <para>La medida en sí —el trazo y los gestos— vive en la suite con la misma entrada real
/// (<c>DesktopCanvasGestureTests</c>); ésta guardia defiende la <b>puerta</b> del modo en la aplicación.</para>
/// </summary>
public class DesktopSelfCheckGuardTests
{
    private const string ProbeCode = "FileFlow.App/SelfCheck/DesktopSelfCheck.cs";
    private const string ProgramCode = "FileFlow.App/Program.cs";
    private const string AppCode = "FileFlow.App/App.axaml.cs";
    private const string ProjectFile = "FileFlow.App/FileFlow.App.csproj";

    private static string Code(string relativePath) => SourceText.CodeWithoutComments(relativePath);

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(TestRepositoryLocator.RepositoryRoot(), relativePath));

    // ─────────────────────────────────────────────────────────────────────────────
    // 1. La puerta: su modo, y el anfitrión que sabe inyectar un puntero
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheProbe_ShouldBeItsOwnCommandLineMode_OnTheHostThatCanInjectAPointer()
    {
        string program = Code(ProgramCode);

        program.Should().Contain("SelfCheck.DesktopSelfCheck.IsRequested(args)",
            "el modo tiene que decidirse en la línea de comandos de la aplicación real, no en un andamio aparte");
        program.Should().Contain("SelfCheck.DesktopSelfCheck.ConfigureHost()",
            "y arrancar el anfitrión de la sonda en vez del normal cuando se pide");

        program.IndexOf("ConfigureHost()", StringComparison.Ordinal).Should().BeLessThan(
            program.IndexOf("return BuildAvaloniaApp()", StringComparison.Ordinal),
            "la rama de la sonda va ANTES del arranque normal: si no, el proceso abriría una ventana de verdad "
            + "y el argumento sería decorativo");

        program.Should().Contain("return BuildAvaloniaApp()\r\n            .StartWithClassicDesktopLifetime(args);",
            "y el arranque normal no se toca: es el camino del usuario sin el argumento");

        // La frontera del instrumento: el puntero entra por el pipeline de entrada del framework, y eso sólo
        // existe sobre la plataforma headless con render real.
        string probe = Code(ProbeCode);
        probe.Should().Contain("UseHeadless(new AvaloniaHeadlessPlatformOptions",
            "sin la plataforma headless no hay forma de meter un puntero por el pipeline de entrada desde dentro del proceso");
        probe.Should().Contain("UseHeadlessDrawing = false",
            "y sin dibujo real (Skia) no se compone la cadena de transformaciones que la medida recorre");
        probe.Should().Contain("UseSkia()");

        Read(ProjectFile).Should().Contain("Avalonia.Headless",
            "el paquete de la plataforma headless es lo que hace posible inyectar la entrada; el modo lo usa y nada más lo hace");

        probe.Should().Contain("public static bool IsRequested(IEnumerable<string> args)",
            "la puerta se pregunta por su argumento y nada más: sin argumento, la sonda no enciende ni un fichero");
        probe.Should().Contain("StartsWith(Argument, StringComparison.Ordinal)");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 2. El veredicto: su informe y su código de salida
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheProbe_ShouldHandItsVerdictByExitCode_AndLeaveTheReportBesideTheExecutable()
    {
        string probe = Code(ProbeCode);

        probe.Should().Contain("public const string ReportFileName = \"selfcheck-report.txt\"",
            "el informe va con el nombre y la convención de las sondas del host Uno");
        probe.Should().Contain("Path.Combine(AppContext.BaseDirectory, ReportFileName)",
            "junto al ejecutable, que es donde el usuario lo busca");
        probe.Should().Contain("desktop.Shutdown(verified ? 0 : 1)",
            "el veredicto tiene que ser el CÓDIGO DE SALIDA: es lo que un script puede comprobar");
        probe.Should().Contain("public static void Run(Window window, IClassicDesktopStyleApplicationLifetime desktop)",
            "la sonda se arranca desde la ventana principal y cierra el proceso por el ciclo de vida de la aplicación");

        // El envoltorio espera el proceso y hereda su veredicto (no lo lanza en segundo plano).
        Read("run.ps1").Should().Contain("\"--selfcheck\"");
        Read("run.ps1").Should().Contain("exit $LASTEXITCODE");
        Read("run.ps1").Should().Contain("[switch]$SelfCheck");
        Read("run-fast.ps1").Should().Contain("\"--selfcheck\"");
        Read("run-fast.ps1").Should().Contain("exit $LASTEXITCODE");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 3. La medida: en píxeles de ventana, y sin escribir el encuadre
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheProbe_ShouldMeasureInWindowSpace_AndLetTheGesturesMoveTheView()
    {
        string probe = Code(ProbeCode);

        // La medida del aterrizaje pasa por la cadena de transformaciones real (lo que separa esta sonda de las
        // medidas en unidades del grafo que ya existían) y contra el punto DIBUJADO del socket.
        probe.Should().Contain("TranslatePoint(",
            "el aterrizaje se mide proyectando al espacio de la ventana, no comparando unidades del grafo");
        probe.Should().Contain("Classes.Contains(\"socket\")",
            "y contra la figura dibujada del socket, que es lo que el usuario ve");
        probe.Should().Contain("ConnectionGeometry.BuildWire(",
            "y la curva se compara con el trazador COMPARTIDO del núcleo, que es la pieza que los dos hosts usan");

        // Los gestos: puntero inyectado por el pipeline de entrada, no llamadas al ViewModel.
        probe.Should().Contain("window.MouseDown(");
        probe.Should().Contain("window.MouseUp(");
        probe.Should().Contain("window.MouseMove(");
        probe.Should().Contain("window.MouseWheel(",
            "la rueda entra por donde entra la de una mano");

        // Y la sonda NO escribe el encuadre: si lo escribiera mediría su propia mutación, y el verde diría que
        // el gesto funciona sin haber ejercitado ninguno.
        Regex.IsMatch(probe, @"Viewport(Location|Zoom)\s*=[^=]").Should().BeFalse(
            "la sonda sólo mide: el encuadre lo mueven los gestos, no una asignación");

        // Y no escribe nada del usuario: la escena se prepara cargando el modelo del ejemplo (AddNode escribe
        // contadores de uso en las preferencias reales, que es lo que las fixtures del suite ya evitan).
        probe.Should().Contain("LoadFromGraphModel(");
        probe.Should().NotContain("AddNode(");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 4. El arranque: la sonda no dispara la red ni convive con el trabajo de fondo
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheProbe_ShouldSkipTheBackgroundWork_AndRunWithTheWindowAlreadyOnScreen()
    {
        string app = Code(AppCode);

        app.Should().Contain("DesktopSelfCheck.IsRequested(Environment.GetCommandLineArgs())",
            "el arranque tiene que reconocer el modo por la línea de comandos");
        app.Should().Contain("if (preferences is not null && !selfCheck)",
            "en modo de medida no se lanza la comprobación de actualizaciones: su red y su aviso romperían la hermesis del veredicto");
        app.Should().Contain("DesktopSelfCheck.Run(mainWindow, desktop)");

        app.IndexOf("CreateAndShowMainWindow(", StringComparison.Ordinal).Should().BeLessThan(
            app.IndexOf("DesktopSelfCheck.Run(mainWindow, desktop)", StringComparison.Ordinal),
            "la medida corre con la ventana ya en pantalla: es la aplicación de verdad, no un árbol a medias");
    }
}
