using System.Collections.Generic;
using System.IO;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Guardia del TECLADO del lienzo Uno (hito 252): los atajos del editor (<c>Ctrl+Z</c>, <c>Ctrl+Y</c>,
/// <c>Supr</c>, <c>F2</c>, el buscador) tienen que resolverse sin depender de quién sea dueño del foco.
///
/// <para><b>El defecto que guarda</b>: la sesión manual del 250 midió con puntero real que los atajos no
/// llegaban al lienzo (18 s sin un píxel de cambio con la tarjeta seleccionada y la app en primer plano).
/// El primer arreglo —entregar el foco al <c>UserControl</c> en vez de al <c>Grid</c> del handler— era
/// necesario pero NO suficiente: el rastro con puntero real de este hito dejó escrito que el clic entregaba el
/// foco (<c>GotFocus enfocado=EditorCanvasControl#Canvas</c>) y que ~0,5 s después se lo llevaba un
/// <c>ScrollViewer</c> de un panel que reacciona a la selección. Ese robo no se reproduce sin puntero (el
/// vigilante que lo intentó desde el sondeo seleccionaba un nodo por el mismo camino y el foco no se movía),
/// así que el arreglo no persigue al ladrón: hace que el atajo no necesite el foco. La ventana enruta al
/// lienzo las teclas que nadie consumió —el burbujeo que el escritorio ya usa en su vista de editor— y el
/// handler del control llama al MISMO resolver.</para>
///
/// <para><b>Por qué se censa la fuente</b>: el puntero no se puede inyectar en este entorno (231, 250) y el
/// teclado tampoco desde el suite, así que lo que se guarda es el cableado —quién enruta a quién y con qué
/// cortesías—; la sonda <c>ProbeShortcutResolution</c> mide en la app viva que el resolver no dependa del
/// foco, y la sesión humana de este hito es la que certifica con dedos de verdad.</para>
/// </summary>
public class UnoCanvasKeyboardGuardTests
{
    private const string CanvasCodePath = "FileFlow.App.Uno/Controls/EditorCanvasControl.xaml.cs";
    private const string CanvasXamlPath = "FileFlow.App.Uno/Controls/EditorCanvasControl.xaml";
    private const string WindowCodePath = "FileFlow.App.Uno/MainWindow.xaml.cs";
    private const string SelfCheckPath = "FileFlow.App.Uno/RuntimeSelfCheck.cs";
    private const string MutationPath = "mutations/atajo-que-no-llega-sin-foco.json";
    private const string ReclaimMutationPath = "mutations/reclamacion-que-roba-al-cuadro-de-texto.json";

    /// <summary>El caso del suite que es testigo de la mutación (la cobertura casa el filtro por nombre).</summary>
    private const string WitnessCase = "TheWindow_ShouldRouteTheKeysNobodyConsumed_ToTheCanvas";

    private static string CanvasCode() => SourceText.CodeWithoutComments(CanvasCodePath);
    private static string CanvasXaml() => SourceText.CodeWithoutComments(CanvasXamlPath);
    private static string WindowCode() => SourceText.CodeWithoutComments(WindowCodePath);
    private static string SelfCheck() => SourceText.CodeWithoutComments(SelfCheckPath);

    /// <summary>
    /// Los dueños del teclado a los que el resolver NO puede pasar por encima, y el código que lo garantiza.
    /// </summary>
    private static IReadOnlyList<(string Owner, string KeptBy)> KeyboardCourtesyTable() =>
    [
        ("un cuadro de texto (buscador del spotlight, caja de renombrado)",
            "IsTextInput(source as DependencyObject)"),
        ("una tarjeta en renombrado (la caja manda mientras se edita)",
            "_editor.Nodes.Any(n => n.IsEditingTitle)"),
    ];

    [Fact]
    public void TheWindow_ShouldRouteTheKeysNobodyConsumed_ToTheCanvas()
    {
        string window = WindowCode();

        window.Should().Contain(
            "keyboardRoot.KeyDown += OnRootKeyDown;",
            "el teclado del editor se resuelve por BURBUJEO desde la raíz de la ventana — como la vista de " +
            "editor del escritorio—: sin ese cableado, un panel que se lleve el foco deja los atajos sin " +
            "destinatario, que es el defecto que el 250 midió con puntero real");

        window.Should().Contain(
            "if (e.Handled || Canvas is null)",
            "el respeto por lo ya manejado va PRIMERO: un botón con la barra espaciadora o un ListView " +
            "manda en su tecla, y el lienzo sólo recibe lo que nadie consumió");

        window.Should().Contain(
            "Canvas.TryHandleShortcutKey(e.Key, e.OriginalSource)",
            "la ventana enruta al MISMO resolver que el handler del lienzo: dos tablas de atajos serían dos " +
            "formas de divergir");

        window.Should().Contain(
            "e.Handled = true;",
            "si el lienzo consume la tecla, el evento se marca manejado para que no la vea nadie más");

        string mutation = File.ReadAllText(Path.Combine(TestRepositoryLocator.RepositoryRoot(), MutationPath));
        mutation.Should().Contain(
            $"\"FullyQualifiedName~{WitnessCase}\"",
            "esta guardia es el testigo de la mutación del enrutado: es lo que la convierte en una prueba " +
            "que muerde y no en una que se limita a describir el fuente");
    }

    [Fact]
    public void TheCanvas_ShouldResolveEveryShortcutInOnePlace()
    {
        string code = CanvasCode();

        code.Should().Contain(
            "internal bool TryHandleShortcutKey(Windows.System.VirtualKey key, object? source)",
            "el resolver es un método del control (no el cuerpo del handler) para que el enrutado de la " +
            "ventana y el foco del lienzo entren por el mismo camino");

        code.Should().Contain(
            "if (TryHandleShortcutKey(e.Key, e.OriginalSource))",
            "el handler del control delega: si volviera a resolverse por su cuenta, las dos vías podrían " +
            "divergir y una de ellas quedaría sin probar");

        code.Should().Contain(
            "return EditorKeyboardShortcuts.Execute(command.Value, _editor, GraphPointFromScreen(cursor));",
            "el resolver devuelve si consumió la tecla (lo que la ventana necesita para marcar el evento) " +
            "en vez de tocar el evento, que en la vía enrutada no existe");

        code.Split("EditorKeyboardShortcuts.Resolve(").Length.Should().Be(
            2,
            "la tabla compartida del núcleo se consulta en UN solo sitio (`Resolve(`) y `Execute(` en otro: " +
            "cada copia de la tabla fue un defecto esperando su turno");
    }

    [Fact]
    public void ThePointerPress_ShouldStillDeliverTheFocus_ButNotAsTheContract()
    {
        string code = CanvasCode();
        string xaml = CanvasXaml();

        code.Should().Contain(
            "FocusCanvasForShortcuts(e.OriginalSource as DependencyObject)",
            "el clic sigue entregando el foco al control (es lo correcto en un control tabulable), aunque ya " +
            "no sea el contrato del que dependen los atajos");

        code.Should().Contain(
            "return this.Focus(FocusState.Programmatic);",
            "el foco va al CONTROL del lienzo (el UserControl con IsTabStop), no al elemento del handler");

        code.Should().NotContain(
            "((FrameworkElement)sender).Focus(FocusState.Programmatic)",
            "el patrón que rompía los atajos no puede volver: el árbol no avisa de que Focus() devolvió " +
            "false, así que el silencio se cuela hasta una sesión con dedos de verdad");

        xaml.Should().Contain(
            "IsTabStop=\"True\"",
            "el control sigue siendo tabulable: sin eso Focus() devuelve false y el lienzo no puede ser " +
            "dueño del teclado ni con el teclado del usuario");

        xaml.Should().Contain(
            "KeyDown=\"OnKeyDown\"",
            "el handler del teclado tiene que estar cableado en el árbol del control");

        code.Should().Contain(
            "KeyDown += OnKeyDown;",
            "el control es el dueño del handler (la segunda vía, para las teclas que llegan con el foco ya " +
            "en el control)");
    }

    [Fact]
    public void TheShortcutCourtesies_ShouldKeepEveryOtherKeyboardOwner()
    {
        string code = CanvasCode();

        foreach (var (owner, keptBy) in KeyboardCourtesyTable())
        {
            code.Should().Contain(
                keptBy,
                $"el resolver no puede llevarse por delante el teclado de {owner}: es la misma cortesía que " +
                "el escritorio aplica en su vista de editor");
        }

        code.Should().Contain(
            "current is UIElement element ? VisualTreeHelper.GetParent(element) : null",
            "la comprobación del cuadro de texto sube por el árbol (el origen puede ser un hijo del cuadro) " +
            "y se para en lo que no es UIElement, para no reventar recorriendo un TextElement");
    }

    [Fact]
    public void TheCanvas_ShouldReclaimTheKeyboard_FromAForeignOwnerAfterAClick_WithItsCourtesies()
    {
        string code = CanvasCode();

        // La ventana de propiedad: el foco se pierde DESPUÉS del gesto (el rastro con puntero real mide
        // 78–141 ms), así que la reclamación se acota al clic que la justifica.
        code.Should().Contain(
            "_keyboardOwnedUntil = Environment.TickCount64 + KeyboardOwnershipMs;",
            "sin ventana de propiedad, la reclamación sería una pelea por el teclado con cualquier otro " +
            "dueño legítimo; sin reclamación, el framework deja el teclado en un envoltorio que no edita nada");

        code.Should().Contain(
            "Environment.TickCount64 > _keyboardOwnedUntil",
            "pasada la ventana, si otro se lleva el teclado por su cuenta, el lienzo no discute");

        // Las tres cortesías, en una sola condición legible: cuadro de texto, subárbol propio y paneles.
        code.Should().Contain(
            "IsTextInput(owner) || IsInsideSelf(owner) || IsInsideEditorPanel(owner)",
            "la reclamación no puede llevarse por delante el teclado de un cuadro de texto (el buscador del " +
            "cajón), el de un control del propio lienzo ni el de un panel que el usuario acaba de tocar");

        code.Should().Contain(
            "ReclaimKeyboardIfStolenByFramework();",
            "la mitad que falta del arreglo del 253: el lienzo recupera el teclado cuando el envoltorio de la " +
            "plantilla de ventana se lo lleva después del clic");

        // El censo de las llamadas: declaración + el tick siguiente a perder el foco + la sonda. Una llamada
        // nueva sin declarar aquí es exactamente la que se colaría sin sus cortesías.
        int usages = code.Split("ReclaimKeyboardIfStolenByFramework").Length - 1;
        usages.Should().Be(
            3,
            "el método se declara una vez y tiene dos usos declarados: el tick siguiente a perder el foco (el " +
            "arreglo) y la sonda (la medida)");

        // La reclamación se hace en el tick siguiente a propósito: el envoltorio del framework necesita su
        // pase de layout para quedar como dueño, y reclamar antes sería una carrera que a veces se perdería.
        code.Should().Contain(
            "DispatcherQueue.TryEnqueue(() =>",
            "la reclamación vive en el tick siguiente a perder el foco, no en el propio LostFocus");

        SelfCheck().Should().Contain(
            "canvas.ProbeKeyboardReclaim()",
            "la sonda de la reclamación la corre el selfcheck: es lo que la mide en la app viva, con los cuatro " +
            "objetivos reales de la ventana");

        SelfCheck().Should().Contain(
            "el lienzo recupera el teclado cuando el framework se lo lleva tras el clic",
            "el renglón del selfcheck nombra lo que mide");

        SelfCheck().Should().Contain(
            "la reclamación del teclado no se lo quita a un cuadro de texto, ni a un control del lienzo, ni a un panel del editor",
            "las cortesías también se miden, no sólo se declaran");

        string mutation = File.ReadAllText(Path.Combine(TestRepositoryLocator.RepositoryRoot(), ReclaimMutationPath));
        mutation.Should().Contain(
            "\"FullyQualifiedName~TheCanvas_ShouldReclaimTheKeyboard_FromAForeignOwnerAfterAClick_WithItsCourtesies\"",
            "esta guardia es el testigo de la mutación de la reclamación: sin ella, el caso sería una " +
            "descripción del fuente en vez de una prueba que muerde");
    }

    [Fact]
    public void TheRoutingProbe_ShouldBeRunByTheSelfCheck()
    {
        CanvasCode().Should().Contain(
            "internal (bool ResolvedWithoutFocus, bool RespectsTextInput, string Detail) ProbeShortcutResolution()",
            "sin sonda, el enrutado sólo se puede medir con una sesión humana: el 250 necesitó dedos de " +
            "verdad para ver que los atajos morían, y este hito volvió a necesitarlos para ver que el foco se " +
            "iba después del clic");

        SelfCheck().Should().Contain(
            "canvas.ProbeShortcutResolution()",
            "el selfcheck corre la sonda en la app viva, que es donde el foco existe");

        SelfCheck().Should().Contain(
            "el atajo del editor se resuelve sin ser dueño del foco (el caso que el 250 midió con puntero real)",
            "el renglón del selfcheck nombra lo que mide: se lee en el informe sin abrir el código");

        SelfCheck().Should().Contain(
            "el enrutado del atajo respeta al cuadro de texto",
            "la cortesía con los cuadros de texto también se mide, no sólo se declara");
    }
}
