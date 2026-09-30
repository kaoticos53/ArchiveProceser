using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;
/// <summary>
/// La guardia del <b>CATÁLOGO de diálogos</b> de los paneles de nodo del host Uno (hito 258): qué diálogos sabe
/// servir el host y cuáles son las filas del inspector que los abren.
///
/// <para><b>Qué protege</b>: que el catálogo cubra TODAS las claves de <c>DialogKeys</c> —cada una servida o
/// declarada, ninguna en silencio—; que las pendientes se contesten con su razón y no con un «cancelar» mudo;
/// que cada diálogo de fila del ESCRITORIO tenga destino aquí (dibujado, o declarado pendiente con su razón) y
/// que lo declarado no esté además cableado; que cada acción de fila se dibuje en las MISMAS filas que el
/// escritorio; y que el botón de texto libre siga a su parámetro en los dos sentidos.</para>
///
/// <para><b>Qué NO vive aquí</b>: que esos diálogos sean VISTAS de los view models portables está en
/// <c>UnoDialogPortabilityGuardTests</c>, las superficies que el nodo declara y sus órdenes destructivas en
/// <c>UnoDeclaredSurfaceGuardTests</c>, y la medición en <c>UnoNodeDialogProbeGuardTests</c>.</para>
/// </summary>
public class UnoNodeDialogCatalogGuardTests
{
    private const string WindowService = "FileFlow.App.Uno/Platform/UnoWindowService.cs";
    private const string InspectorCode = "FileFlow.App.Uno/Controls/NodeInspectorPanel.xaml.cs";
    private const string SelfCheckCode = "FileFlow.App.Uno/SelfCheckDialogs.cs";
    private const string AppCode = "FileFlow.App.Uno/App.xaml.cs";

    private const string DesktopDialogKeys = "FileFlow.Sdk/Services/IWindowService.cs";

    private static string Code(string relativePath) => SourceText.CodeWithoutComments(relativePath);

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(TestRepositoryLocator.RepositoryRoot(), relativePath));

    // ─────────────────────────────────────────────────────────────────────────────
    // 1. El catálogo de diálogos: servido o declarado, nunca en silencio
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Las claves canónicas de <c>DialogKeys</c>, leídas del SDK (la fuente, no una copia).</summary>
    private static IReadOnlyList<string> CanonicalDialogKeys()
    {
        var keys = Regex.Matches(Code(DesktopDialogKeys), @"public const string \w+ = ""([^""]+)"";")
            .Select(m => m.Groups[1].Value)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        keys.Should().HaveCountGreaterThanOrEqualTo(9,
            "las claves del catálogo canónico tienen que leerse de verdad: una lectura vacía haría pasar la "
            + "guardia sin haber mirado nada");
        return keys;
    }

    /// <summary>
    /// Las filas de una tabla de cadenas del host (<c>(clave, …)</c>), leídas del propio código: la tabla vive
    /// donde trabaja quien la mantiene, y la guardia la lee de ahí para que añadir un diálogo obligue a
    /// declararlo.
    /// </summary>
    private static HashSet<string> Table(string relativePath, string tableName)
    {
        string code = Code(relativePath);
        int at = code.IndexOf(tableName + " =", StringComparison.Ordinal);
        at.Should().BeGreaterThan(-1, $"el host tiene que declarar la tabla {tableName} en {relativePath}");

        int end = code.IndexOf("];", at, StringComparison.Ordinal);
        end.Should().BeGreaterThan(at, $"la tabla {tableName} tiene que cerrarse con '];'");

        var keys = Regex.Matches(code[at..end], @"\(\s*(DialogKeys\.[A-Za-z]+)\s*,")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        keys.Should().NotBeEmpty($"la tabla {tableName} tiene que leerse desde el código, con su razón");
        return keys;
    }

    /// <summary>De la referencia <c>DialogKeys.X</c> a la clave literal del SDK.</summary>
    private static string Resolve(string reference)
    {
        string name = reference.Replace("DialogKeys.", string.Empty);
        var match = Regex.Match(Code(DesktopDialogKeys), @"public const string " + name + @" = ""([^""]+)"";");
        match.Success.Should().BeTrue($"el host nombra el diálogo {reference}, que tiene que existir en DialogKeys");
        return match.Groups[1].Value;
    }

    [Fact]
    public void TheWindowService_ShouldCoverEveryDialogKey_ServedOrDeclared()
    {
        var served = Table(WindowService, "ImplementedDialogs").Select(Resolve).ToHashSet(StringComparer.Ordinal);
        var declared = Table(WindowService, "DeclaredPendingDialogs").Select(Resolve).ToHashSet(StringComparer.Ordinal);

        served.Should().NotBeEmpty("el host sirve diálogos: una tabla de servidos vacía es el Nulo con otro nombre");
        declared.Should().NotBeEmpty("lo que el host no sirve tiene que estar declarado con su razón");

        served.Should().NotIntersectWith(declared,
            "un diálogo o lo sirve el host o está pendiente: declararlo de las dos maneras sería no decirla");

        var missing = CanonicalDialogKeys()
            .Where(key => !served.Contains(key) && !declared.Contains(key))
            .ToList();
        missing.Should().BeEmpty(
            "toda clave de DialogKeys tiene que estar servida por el host o declarada en DeclaredPendingDialogs: "
            + "una clave nueva sin destino se cerraría en silencio y el usuario leería un «cancelado» mudo");

        // Y el Nulo declarado no puede ser el que sirve: el host registra el suyo.
        Code(AppCode).Should().Contain("AddSingleton<IWindowService, UnoWindowService>();",
            "el contenedor tiene que registrar el catálogo de diálogos del host, no dejarlo en el Nulo declarado");

        // El anclaje que hace que las filas del inspector (que construyen sus parámetros sin servicios por
        // constructor) encuentren el servicio: sin él los dos diálogos vuelven al Nulo y los botones quedan
        // sin efecto — el defecto que este tramo cierra.
        Code(AppCode).Should().Contain("ServiceHolders.WindowService = s_services.GetRequiredService<IWindowService>();");
    }

    [Fact]
    public void EveryDeclaredPendingDialog_ShouldBeAnsweredWithItsReason_NotWithASilentCancel()
    {
        string code = Code(WindowService);

        // El camino de la clave no servida TIENE que anotarse y avisar: un «cancelado» mudo se lee como un
        // fallo del usuario y es justo lo que hace invisible una frontera.
        code.Should().Contain("Decline(dialogKey,",
            "una clave sin destino se declina por el camino que anota la frontera, no con un return suelto");
        code.Should().Contain("public static IReadOnlyList<string> DeclinedDialogs",
            "la traza de declinados es la prueba medible de que la frontera existe (y la lee la sonda)");
        code.Should().Contain("Console.Error.WriteLine",
            "y se escribe también fuera del proceso: un hueco sin rastro no se puede diagnosticar");

        // Y la traza no puede ser una lista que se llena y nunca se lee: la sonda la lee.
        Code(SelfCheckCode).Should().Contain("UnoWindowService.DeclinedDialogs");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 2. La paridad de fila contra el escritorio
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Los comandos que una fila de parámetro dispara (su plantilla de fila).
    /// </summary>
    private static IReadOnlyList<string> DesktopRowCommands() =>
    [
        "OpenTextEditorCommand",
        "OpenVariablePickerCommand"
    ];

    [Fact]
    public void EveryDesktopRowDialog_ShouldBeServedHere_OrDeclaredPending()
    {
        string inspector = Code(InspectorCode);
        var served = ServedRowActions(inspector);
        var declared = PendingRowActions(inspector);

        served.Should().NotBeEmpty("el host tiene que declarar las órdenes de fila que dibuja");
        declared.Should().NotBeEmpty("y las que no: lo que no llega queda declarado, nunca fingido");
        served.Keys.Should().NotIntersectWith(declared.Keys,
            "una orden de fila o la dibuja el host o está pendiente: declararla de las dos maneras sería no decirla");

        var orphan = DesktopRowCommands()
            .Where(command => !served.ContainsKey(command) && !declared.ContainsKey(command))
            .ToList();
        orphan.Should().BeEmpty(
            "toda orden que el escritorio dispara desde una fila de parámetro tiene que estar dibujada en el "
            + "host o declarada en DeclaredPendingRowActions con su razón");

        // Lo declarado NO puede estar cableado: sería una mentira en la tabla (y un botón que nadie ve).
        foreach (string command in declared.Keys)
        {
            inspector.Should().NotContain("p." + command,
                $"la orden declarada pendiente {command} no puede estar además cableada en el inspector: si ya "
                + "está, se quita de la tabla");
        }

        // Y lo servido TIENE que estar cableado y tener su ancla: la tabla no puede prometer un botón que no
        // existe. El cableado se busca por el nombre TAL CUAL lo declara la tabla (con su variante).
        foreach (var (command, anchor) in ServidaRaw(inspector))
        {
            inspector.Should().Contain("p." + command,
                $"la orden de fila {command} está declarada servida y tiene que estar ejecutada por su botón");
            inspector.Should().Contain("\"" + anchor + "\"",
                $"la orden {command} tiene que poner su AutomationId con el prefijo {anchor}: sin él no hay "
                + "canal externo que la alcance");
        }

        // La otra dirección: toda orden de fila que el inspector EJECUTE tiene que estar en una de las dos
        // tablas. Sin esto, cablear un botón nuevo (o cambiar el destino de uno) se saltaría el censo.
        // Sólo los que ABREN algo (los «Open…» de la fila): copiar el valor evaluado o insertar un token van
        // por el mismo camino pero no son puertas de diálogo, y meterlos aquí convertiría el censo en una
        // lista de todo el fichero. BrowsePathCommand entra por la tabla de servidos.
        var wired = Regex.Matches(inspector, @"p\.(Open[A-Za-z]+Command)")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        wired.Should().NotBeEmpty("el inspector ejecuta las órdenes de sus filas: el censo de cableadas no puede estar vacío");

        var uncensused = wired
            .Where(command => !served.ContainsKey(command) && !declared.ContainsKey(command))
            .ToList();
        uncensused.Should().BeEmpty(
            "toda orden de fila que el inspector ejecute tiene que estar declarada servida o pendiente: un "
            + "cableado fuera de la tabla es un hueco que la tabla no cuenta");
    }

    /// <summary>
    /// Las órdenes de fila servidas, con el prefijo de AutomationId de su botón (leído del host).
    ///
    /// <para>El nombre se NORMALIZA quitando el sufijo «Async»: este host abre sus pickers desde el clic de
    /// UI, y allí la variante síncrona del núcleo no puede funcionar (exige el hilo de UI y bloquearlo
    /// interbloquearía, así que su servicio de diálogos devuelve null). Servir la MISMA operación por su
    /// variante asíncrona es paridad: lo que el censo persigue es que la puerta esté, no el sufijo del
    /// comando con el que se abre.</para>
    /// </summary>
    private static Dictionary<string, string> ServedRowActions(string inspector)
    {
        int at = inspector.IndexOf("HostRowActions =", StringComparison.Ordinal);
        at.Should().BeGreaterThan(-1, "el inspector tiene que declarar HostRowActions");
        int end = inspector.IndexOf("];", at, StringComparison.Ordinal);
        return Regex.Matches(inspector[at..end], @"\(""([A-Za-z]+Command)"",\s*""([A-Za-z_]+)""")
            .ToDictionary(m => Normalize(m.Groups[1].Value), m => m.Groups[2].Value, StringComparer.Ordinal);
    }

    /// <summary>El mismo nombre sin la marca de asíncrono: <c>OpenXAsyncCommand</c> → <c>OpenXCommand</c>.</summary>
    private static string Normalize(string command) =>
        command.EndsWith("Command", StringComparison.Ordinal)
            ? command[..^"Command".Length].Replace("Async", string.Empty) + "Command"
            : command;

    /// <summary>Las órdenes servidas SIN normalizar, tal como las escribe la tabla del host.</summary>
    private static Dictionary<string, string> ServidaRaw(string inspector)
    {
        int at = inspector.IndexOf("HostRowActions =", StringComparison.Ordinal);
        int end = inspector.IndexOf("];", at, StringComparison.Ordinal);
        return Regex.Matches(inspector[at..end], @"\(""([A-Za-z]+Command)"",\s*""([A-Za-z_]+)""")
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value, StringComparer.Ordinal);
    }

    /// <summary>Las órdenes de fila declaradas pendientes, con su razón (leídas del host).</summary>
    private static Dictionary<string, string> PendingRowActions(string inspector)
    {
        int at = inspector.IndexOf("DeclaredPendingRowActions =", StringComparison.Ordinal);
        at.Should().BeGreaterThan(-1, "el inspector tiene que declarar DeclaredPendingRowActions");
        int end = inspector.IndexOf("];", at, StringComparison.Ordinal);
        return Regex.Matches(inspector[at..end], @"\(""([A-Za-z]+Command)"",\s*""[^""]+""\)")
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value, StringComparer.Ordinal);
    }

    [Fact]
    public void EveryRowAction_ShouldBeDrawnOnTheSameRowsAsTheDesktop()
    {
        string inspector = Code(InspectorCode);

        // La tabla de editores y la de acciones salen de los MISMOS flags del view model que el escritorio usa
        // en su selector: el editor expandido en el valor largo, el catálogo en las filas de texto.
        inspector.Should().Contain("bool wantsEditor = p.IsMultiLine;",
            "el editor expandido va donde el escritorio lo pone: el valor largo (IsMultiLine)");
        inspector.Should().Contain("bool wantsVariables = p.IsMultiLine || p.HasBrowseButton || RowValueIsPlainText(p);");
        inspector.Should().Contain(
            "!p.IsToggle && !p.IsSlider && !p.IsDropdown && !p.HasBrowseButton && !p.IsMultiLine;",
            "el resto de texto libre es exactamente el mismo del selector del escritorio");

        // El GESTOR DE CONTRASEÑAS va en la fila de la lista de claves, que es donde el escritorio pone su botón
        // «Claves»: la misma fila, el mismo flag del view model. Antes esta puerta no se dibujaba aquí mientras la
        // TARJETA del nodo ofrecía la misma capacidad, así que el usuario veía el gestor ofrecido y no alcanzable.
        inspector.Should().Contain("bool wantsPassword = p.IsPasswordList;",
            "el gestor de claves va donde el escritorio lo pone: la fila de la lista de claves");

        // El ancla de la caja del valor sigue existiendo (la sonda escribe por ella): es la mitad de la
        // medición.
        inspector.Should().Contain("\"ParamBox_\" + p.Key");
    }

    [Fact]
    public void EveryTextRow_ShouldFollowTheParameter_BothWays()
    {
        string inspector = Code(InspectorCode);

        // La IDA: la caja escribe el valor (lo que teclea el usuario).
        inspector.Should().Contain("p.Value = box.Text;");

        // La VUELTA: el valor —lo que escriben los diálogos por el view model— actualiza la caja. Sin ella,
        // insertar una variable cambiaba el parámetro del nodo y dejaba el campo con el texto viejo: el
        // usuario veía que no había pasado nada.
        inspector.Should().Contain("box.Text = value;");
        inspector.Should().Contain("e.PropertyName != nameof(NodeParameterViewModel.Value)");

        // Y la vuelta tiene que estar SUSCRITA (una función que nadie escucha es una caja que no se entera) y
        // dada de alta en la tabla que se suelta al reconstruir.
        inspector.Should().Contain("p.PropertyChanged += OnParameterChanged;");
        inspector.Should().Contain("_rowValueSubscriptions.Add((p, OnParameterChanged));");

        // Y las suscripciones se SUELTAN al reconstruir las filas: si no, cada selección de nodo dejaría al
        // parámetro escribiendo en una caja que ya no está.
        inspector.Should().Contain("param.PropertyChanged -= handler;");
        inspector.Should().Contain("_rowValueSubscriptions.Clear();");

        // Las cajas de texto de las filas (texto estándar, multilínea y ruta con explorar) se atan con el
        // MISMO método: una suelta con sólo la ida quedaría ciega a lo que escriben los diálogos.
        Regex.Matches(inspector, @"WireBoxToParameter\(box, p\);").Count.Should().BeGreaterThanOrEqualTo(3,
            "las tres filas de caja de la tabla de editores tienen que atarse por el mismo camino");
    }
}
