using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// La guardia de los <b>PANELES DE NODO</b> del host Uno (hito 258): los diálogos que las filas del
/// inspector abren —el editor de texto y prompts («✎») y el catálogo de variables («{x}»)— y el valor que
/// queda ESCRITO en el parámetro del nodo al confirmarlos.
///
/// <para><b>Qué protege</b>: (1) que el catálogo de diálogos del host cubra todas las claves de
/// <c>DialogKeys</c> —cada una servida o declarada, ninguna en silencio—; (2) que cada diálogo de fila del
/// ESCRITORIO tenga destino aquí (dibujado, o declarado pendiente con su razón), y que lo declarado no esté
/// además cableado; (3) que los dos diálogos sean VISTAS de los view models PORTABLES —el mismo
/// <c>TextEditorDialogViewModel</c> y <c>VariablePickerViewModel</c> del escritorio— y que el valor viaje
/// por sus métodos y no por una copia local de la lógica; (4) que el host sirva el <c>IWindowService</c>
/// REAL y no el Nulo declarado (que era exactamente el defecto que dejaba los botones sin efecto); y (5) que
/// sus textos sean los del escritorio, copiados clave por clave en los dos idiomas.</para>
/// </summary>
public class UnoNodeDialogsGuardTests
{
    private const string WindowService = "FileFlow.App.Uno/Platform/UnoWindowService.cs";
    private const string EditorBodyXaml = "FileFlow.App.Uno/Controls/TextEditorDialogBody.xaml";
    private const string EditorBodyCode = "FileFlow.App.Uno/Controls/TextEditorDialogBody.xaml.cs";
    private const string PickerBodyXaml = "FileFlow.App.Uno/Controls/VariablePickerDialogBody.xaml";
    private const string PickerBodyCode = "FileFlow.App.Uno/Controls/VariablePickerDialogBody.xaml.cs";
    private const string InspectorCode = "FileFlow.App.Uno/Controls/NodeInspectorPanel.xaml.cs";
    private const string PresetBodyXaml = "FileFlow.App.Uno/Controls/MediaPresetManagerBody.xaml";
    private const string PresetBodyCode = "FileFlow.App.Uno/Controls/MediaPresetManagerBody.xaml.cs";
    private const string CardViewXaml = "FileFlow.App.Uno/Controls/NodeCardView.xaml";
    private const string CardViewModelCode = "FileFlow.App.Uno/Controls/NodeCardViewModel.cs";
    private const string SelfCheckCode = "FileFlow.App.Uno/RuntimeSelfCheck.cs";
    private const string AppCode = "FileFlow.App.Uno/App.xaml.cs";
    private const string StringsEnglish = "FileFlow.App.Uno/Resources/Strings.resx";
    private const string StringsSpanish = "FileFlow.App.Uno/Resources/Strings.es.resx";

    /// <summary>La plantilla del ESCRITORIO de una fila de parámetro: la referencia de paridad de esta superficie.</summary>
    private const string DesktopRowTemplates = "FileFlow.App/Themes/Templates/NodeParameterTemplates.axaml";
    private const string DesktopStringsEnglish = "FileFlow.App/Resources/Strings.resx";
    private const string DesktopStringsSpanish = "FileFlow.App/Resources/Strings.es.resx";
    private const string DesktopDialogKeys = "FileFlow.Sdk/Services/IWindowService.cs";
    private const string DesktopWindowService = "FileFlow.App/Services/AvaloniaWindowService.cs";

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
    /// Los comandos que el ESCRITORIO dispara desde una fila de parámetro (su plantilla de fila). Es la
    /// referencia: la mitad de estos abre diálogos del host y la otra mitad ventanas que este host no tiene.
    /// </summary>
    private static IReadOnlyList<string> DesktopRowCommands()
    {
        var commands = Regex.Matches(Read(DesktopRowTemplates), @"Command=""\{Binding ([A-Za-z]+Command)\}""")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        commands.Should().Contain("OpenTextEditorCommand",
            "la plantilla del escritorio tiene que leerse de verdad: si no encuentra ni su editor expandido, "
            + "la paridad estaría midiendo el vacío");
        commands.Should().Contain("OpenVariablePickerCommand");
        return commands;
    }

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
        // existe.
        foreach (var (command, anchor) in served)
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

    /// <summary>Las órdenes de fila servidas, con el prefijo de AutomationId de su botón (leído del host).</summary>
    private static Dictionary<string, string> ServedRowActions(string inspector)
    {
        int at = inspector.IndexOf("HostRowActions =", StringComparison.Ordinal);
        at.Should().BeGreaterThan(-1, "el inspector tiene que declarar HostRowActions");
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

        // Y ni el editor ni el número son texto libre: un botón de variables en una casilla no es paridad,
        // es ruido.
        Read(DesktopRowTemplates).Should().Contain("IsVisible=\"{Binding IsStandardInput}\"",
            "el escritorio enseña su fila de texto estándar por el mismo flag del view model");

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

    // ─────────────────────────────────────────────────────────────────────────────
    // 3. Los diálogos son VISTAS de los view models portables
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheDialogs_ShouldBeViewsOfThePortableViewModels_NotCopiesOfThem()
    {
        string service = Code(WindowService);

        // Los dos diálogos construyen los view models PORTABLES: los mismos que envuelve el escritorio.
        service.Should().Contain("new TextEditorDialogViewModel(");
        service.Should().Contain("new VariablePickerViewModel(");

        // Y el valor no se recalcula aquí: sale de los métodos del view model, los mismos que lee el
        // escritorio al cerrar su ventana.
        service.Should().Contain("vm.SaveResult();");
        service.Should().Contain("string token = vm.SelectedToken;");

        // El payload es el que el núcleo manda: el parámetro (texto) y la petición del catálogo.
        service.Should().Contain("payload is NodeParameterViewModel parameter");
        service.Should().Contain("payload as VariablePickerRequest");

        // Las vistas se atan a las propiedades del view model, no a un estado propio.
        Read(EditorBodyXaml).Should().Contain("Text=\"{Binding Text, Mode=TwoWay}\"");
        Read(EditorBodyXaml).Should().Contain("ItemsSource=\"{Binding FilteredSideVariables}\"");
        Read(EditorBodyXaml).Should().Contain("Visibility=\"{Binding IsSidePanelVisible,");
        Read(PickerBodyXaml).Should().Contain("ItemsSource=\"{Binding FilteredVariables}\"");
        Read(PickerBodyXaml).Should().Contain("SelectedItem=\"{Binding SelectedVariable, Mode=TwoWay}\"");
        Read(PickerBodyXaml).Should().Contain("Text=\"{Binding SearchText, Mode=TwoWay}\"");

        // La inserción en el editor es la del view model (inserta en el punto del cursor y devuelve dónde
        // queda), no un pegado propio.
        Code(EditorBodyCode).Should().Contain("vm.InsertTokenAt(EditorBox.SelectionStart, item.Token)");

        // Y ninguna de las dos vistas guarda el resultado por su cuenta.
        foreach (string body in new[] { Code(EditorBodyCode), Code(PickerBodyCode) })
        {
            body.Should().NotContain("new TextEditorDialogViewModel(",
                "el view model viene del catálogo de diálogos; construirlo en la vista crearía un segundo estado");
            body.Should().NotContain("new VariablePickerViewModel(");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 4. Los textos son los del escritorio, en los dos idiomas
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>De la clave del host a la clave del diccionario del escritorio: el texto se COPIA, no se re-traduce.</summary>
    private static IReadOnlyList<(string Host, string Desktop)> SharedTexts() =>
    [
        ("Uno_Dialog_TextEditor_Title", "TextEditor_WindowTitle"),
        ("Uno_Dialog_TextEditor_InsertVar", "TextEditor_InsertVar"),
        ("Uno_Dialog_TextEditor_Clear", "TextEditor_Clear"),
        ("Uno_Dialog_TextEditor_Preview", "TextEditor_EvaluatedPreview"),
        ("Uno_Dialog_TextEditor_VariablesHeader", "Node_VariablesHeader"),
        ("Uno_Dialog_TextEditor_Save", "TextEditor_SaveBtn"),
        ("Uno_Dialog_Cancel", "TextEditor_CancelBtn"),
        ("Uno_Dialog_OpenEditorToolTip", "TextEditor_Header"),
        ("Uno_Dialog_InsertVariableToolTip", "Node_Param_InsertVariableToolTip"),
        ("Uno_Dialog_VarPicker_Title", "VarPicker_WindowTitle"),
        ("Uno_Dialog_VarPicker_SearchPlaceholder", "VarPicker_SearchPlaceholder"),
        ("Uno_Dialog_VarPicker_Insert", "VarPicker_InsertBtn"),
        ("Uno_Dialog_VarPicker_NoResults", "VarPicker_NoResults"),
        ("Uno_Dialog_VarPicker_DetailTitle", "VarPicker_DetailTitle"),
        ("Uno_Dialog_VarPicker_TokenLabel", "VarPicker_TokenLabel"),
        ("Uno_Dialog_VarPicker_DescriptionLabel", "VarPicker_DescriptionLabel"),
        ("Uno_Dialog_VarPicker_CategoryLabel", "VarPicker_CategoryLabel"),
        ("Uno_Dialog_VarPicker_SourceLabel", "VarPicker_SourceLabel"),
        ("Uno_Dialog_VarPicker_SampleLabel", "VarPicker_SamplePreviewLabel"),
        ("Uno_Dialog_VarPicker_CopyToken", "VarPicker_CopyToken"),
    ];

    [Fact]
    public void TheSharedTexts_ShouldBeTheDesktopOnes_InBothLanguages()
    {
        foreach (var (hostPath, desktopPath) in new[]
                 {
                     (StringsEnglish, DesktopStringsEnglish),
                     (StringsSpanish, DesktopStringsSpanish),
                 })
        {
            var host = Dictionary(hostPath);
            var desktop = Dictionary(desktopPath);
            var wrong = new List<string>();

            foreach (var (hostKey, desktopKey) in SharedTexts())
            {
                if (!host.TryGetValue(hostKey, out string? hostValue))
                {
                    wrong.Add($"{hostKey}: falta en {hostPath}");
                    continue;
                }

                if (!desktop.TryGetValue(desktopKey, out string? desktopValue))
                {
                    wrong.Add($"{desktopKey}: falta en {desktopPath} (la referencia)");
                    continue;
                }

                if (!string.Equals(hostValue, desktopValue, StringComparison.Ordinal))
                {
                    wrong.Add($"{hostKey}='{hostValue}' contra {desktopKey}='{desktopValue}'");
                }
            }

            wrong.Should().BeEmpty(
                "los textos de los dos diálogos son los del escritorio, copiados: una traducción propia sería "
                + $"otra interfaz ({Path.GetFileName(hostPath)})");
        }
    }

    [Fact]
    public void EveryTextUsedByTheDialogs_ShouldExistInBothHostDictionaries()
    {
        var english = Dictionary(StringsEnglish);
        var spanish = Dictionary(StringsSpanish);
        string views = Code(EditorBodyCode) + Code(PickerBodyCode) + Code(WindowService)
            + Code(EditorBodyXaml) + Code(PickerBodyXaml)
            + Code(PresetBodyCode) + Code(PresetBodyXaml) + Code(InspectorCode)
            + Code(CardViewXaml) + Code(CardViewModelCode);

        // Las claves del host (`Uno_…`) y las que el nodo o el plugin traen consigo en la superficie (`PresetManager_…`
        // y el tooltip de la fila): las tres familias tienen que estar en los dos diccionarios del host, porque el
        // diccionario del plugin no lo carga este host. Con ellas viaja el rótulo del conmutador de parámetros de
        // la tarjeta (`ToggleParametersToolTip`), que es la clave con la que el ESCRITORIO rotula el suyo: el
        // nombre del texto es el mismo en los dos hosts, y por eso la guardia lo mira con el mismo patrón.
        var cited = Regex.Matches(views, @"""((?:Uno|PresetManager|Node_Param)_[A-Za-z_]+|ToggleParametersToolTip)""")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        cited.Should().NotBeEmpty("los diálogos del host citan sus claves del diccionario del host");
        cited.Should().Contain("PresetManager_WindowTitle",
            "el gestor de presets tiene que leerse de verdad: si no, la guardia no está mirando su superficie");
        cited.Should().Contain("Node_Param_OpenPresetManager");
        cited.Should().Contain("ToggleParametersToolTip",
            "el conmutador de parámetros de la tarjeta es la puerta del panel donde vive la acción «🎬 Presets...»: "
            + "si su rótulo se cae del censo, el host la pintaría con el texto incrustado");

        var missing = cited.Where(key => !english.ContainsKey(key) || !spanish.ContainsKey(key)).ToList();
        missing.Should().BeEmpty(
            "sin entrada en los dos diccionarios, GetString resuelve el fallback incrustado y cambiar de idioma "
            + "deja la mitad del modal en el idioma equivocado");

        english.Keys.OrderBy(k => k, StringComparer.Ordinal)
            .Should().Equal(spanish.Keys.OrderBy(k => k, StringComparer.Ordinal),
                "los dos diccionarios del host declaran las mismas claves");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 5. El GESTOR DE PRESETS: la superficie la declara el NODO y las dos vistas la pintan
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// El GESTOR DE PRESETS DE MEDIOS (hito 263) es la segunda superficie que declara un nodo (tras el
    /// diseñador de datasets): el botón «🎬» de la fila del preset y el de la tarjeta del nodo llaman a la MISMA
    /// acción, y esta guardia ata la cadena entera — el contrato del SDK que une los dos botones con la
    /// superficie, el nodo que la declara con su clave y su view model PORTABLE, el servicio del host que la
    /// sirve, y las DOS vistas (la ventana del escritorio y el cuerpo del host) como vistas de ese view model y
    /// no como copias de la lógica del gestor.
    ///
    /// <para>Sin esto, el camino fácil —reescribir el gestor en el host— se ve igual desde dentro que el
    /// correcto, y la regla del producto (qué se normaliza, qué no se puede borrar) viviría en dos sitios.</para>
    /// </summary>
    [Fact]
    public void TheMediaPresetManager_ShouldBeDeclaredByTheNode_AndPaintedByBothHosts()
    {
        const string Contract = "FileFlow.Sdk/Descriptors/INodeDialogSurfaceProvider.cs";
        const string Node = "FileFlow.Plugin.Integrations/MediaTranscoderNode.cs";
        const string ViewModel = "FileFlow.Plugin.Integrations/UI/ViewModels/MediaPresetManagerViewModel.cs";
        const string DesktopWindow = "FileFlow.Plugin.Integrations/UI/Views/MediaPresetManagerWindow.axaml.cs";
        const string DesktopWindowXaml = "FileFlow.Plugin.Integrations/UI/Views/MediaPresetManagerWindow.axaml";

        // 1. El contrato: la superficie dice qué acción personalizada sustituye, que es el hilo que une el
        //    botón de la fila y el de la tarjeta con la MISMA puerta.
        string contract = Code(Contract);
        contract.Should().Contain("string? ReplacesCustomActionId",
            "sin el nombre de la acción que sustituye, un host no puede saber qué botón de la tarjeta corresponde a "
            + "la superficie declarada, y ese botón seguiría abriendo la ventana del toolkit");

        // 2. El nodo: la declara, con la clave del catálogo, su view model portable y la acción que sustituye.
        string node = Code(Node);
        node.Should().Contain("INodeDialogSurfaceProvider");
        node.Should().Contain("DialogKeys.MediaPresetManager");
        node.Should().Contain("ReplacesCustomActionId => \"ManageMediaPresets\"");
        node.Should().Contain("new UI.ViewModels.MediaPresetManagerViewModel(",
            "y su carga útil es el view model portable, no una ventana");

        // El view model es portable (sin toolkit) y es QUIEN escribe en el almacén: la regla del producto vive
        // aquí y no en ninguna de las dos vistas.
        string viewModel = Read(ViewModel);
        viewModel.Should().NotContain("using Avalonia");
        viewModel.Should().NotContain("Microsoft.UI.Xaml");
        viewModel.Should().Contain("_presetService.SavePreset(");
        viewModel.Should().Contain("_presetService.DeletePreset(");
        viewModel.Should().Contain("_presetService.ResetToDefaults()");
        viewModel.Should().Contain("NormalizeExtension",
            "la normalización de la extensión es del producto, no de la vista");

        // 3. El host: sirve la clave con su vista y espera ESE view model como carga útil.
        string service = Code(WindowService);
        service.Should().Contain("(DialogKeys.MediaPresetManager, nameof(MediaPresetManagerBody))",
            "la clave tiene que estar entre las servidas, con la vista que la sirve");
        service.Should().Contain("payload is MediaPresetManagerViewModel presetManager",
            "y el servicio tiene que comprobar la carga útil que espera, no tragarse cualquier cosa");

        // 4. La vista del host pinta el view model del PLUGIN y no habla con el almacén.
        string body = Code(PresetBodyCode);
        body.Should().Contain("MediaPresetManagerViewModel",
            "la vista del host pinta el view model portable del plugin, no una copia del gestor");
        body.Should().NotContain("new MediaPresetManagerViewModel(",
            "el view model lo construye quien lo declara (el nodo), no la vista");
        body.Should().NotContain("MediaPresetManagerService",
            "el almacén de presets es del plugin: la vista no habla con él");
        foreach (string command in new[] { "NewPresetCommand", "SaveCurrentCommand", "DeletePresetCommand", "ResetDefaultsCommand" })
        {
            Read(PresetBodyXaml).Should().Contain("Command=\"{Binding " + command + "}\"",
                $"la orden {command} del gestor la ejecuta su view model, no la vista");
        }

        // 5. La ventana del ESCRITORIO también es una vista del mismo view model: si recuperara su propia
        //    lógica, habría dos gestores que podrían divergir sin que nadie lo note.
        string desktop = Code(DesktopWindow);
        desktop.Should().Contain("MediaPresetManagerViewModel");
        desktop.Should().NotContain("MediaPresetManagerService.Instance.SavePreset",
            "la ventana del escritorio no guarda: guarda el view model");
        desktop.Should().NotContain("ShowConfirmation",
            "ni confirma el borrado por su cuenta: esa decisión es del view model, con los diálogos del host");
        Read(DesktopWindowXaml).Should().NotContain("Click=\"SaveCurrent_Click\"",
            "las órdenes van por el view model, no por manejadores de la ventana");

        // 6. El núcleo abre la superficie declarada por el servicio de ventanas del host —las dos puertas: la
        //    fila del parámetro y la tarjeta del nodo— en vez de exigir la ventana del toolkit.
        Code("FileFlow.App.Core/ViewModels/NodeParameterViewModel.cs")
            .Should().Contain("surface.ReplacesCustomActionId is { } replaced");
        Code("FileFlow.App.Core/ViewModels/NodeViewModel.cs")
            .Should().Contain("declared.ReplacesCustomActionId is { } replaced",
                "la tarjeta del nodo tiene que abrir la MISMA superficie por el mismo contrato");
        Code("FileFlow.App/Services/AvaloniaWindowService.cs")
            .Should().Contain("DialogKeys.MediaPresetManager => new MediaPresetManagerWindow(",
                "y el escritorio la sirve montando la ventana del plugin sobre ese view model");

        // 7. Y la medición: la sonda la ejerce por el mismo canal que el usuario y lee el valor ESCRITO.
        Code(SelfCheckCode).Should().Contain("ParamPreset_");
        Code(SelfCheckCode).Should().Contain("ActivePresetManager");
        Code(SelfCheckCode).Should().Contain("presetStore.GetPresets()");
    }

    /// <summary>
    /// La PUERTA DE LA TARJETA, que es una de las dos mitades de la superficie. El «🎬 Presets...» del nodo vive
    /// entre sus acciones rápidas, y ese bloque —como el panel de parámetros que lo contiene— sólo se pinta con
    /// la tarjeta desplegada (<c>Node.IsExpanded</c>). El escritorio lo conmuta con su <c>ToggleButton</c>; este
    /// host no tenía ninguno, así que el panel era <b>inalcanzable</b>: la acción estaba dibujada y sin puerta.
    ///
    /// <para>La guardia ata las tres piezas —el conmutador de la cabecera, el estado del NÚCLEO que conmuta y el
    /// bloque que cuelga de él— porque el defecto no se veía en ninguna por separado: el view model tenía el
    /// estado, la vista tenía el bloque, y faltaba justo lo que los une.</para>
    /// </summary>
    [Fact]
    public void TheNodeCard_ShouldBeAbleToShowThePanelWhereTheQuickActionsLive()
    {
        string xaml = Read(CardViewXaml);

        // El panel (parámetros + acciones rápidas) y la condición que lo enseña.
        xaml.Should().Contain("ItemsSource=\"{Binding Node.CustomActions}\"",
            "las acciones rápidas del nodo —entre ellas «🎬 Presets...»— son el contenido del panel");
        xaml.Should().Contain("Visibility=\"{Binding Node.IsExpanded, Converter={StaticResource BoolToVis}}\"",
            "y el panel entero cuelga del estado desplegado del nodo");

        // La puerta: sin conmutador, ese estado no se puede cambiar desde el ratón (el escritorio sí lo tiene).
        xaml.Should().Contain("AutomationProperties.AutomationId=\"NodeCardExpandToggle\"",
            "la cabecera de la tarjeta necesita su conmutador de parámetros, como la del escritorio");
        xaml.Should().Contain("IsChecked=\"{Binding Node.IsExpanded, Mode=TwoWay}\"",
            "y tiene que conmutar el estado del NÚCLEO, no uno propio de la vista");

        // Y las dos piezas del adaptador que hacen que el conmutador se vea y se lea en el idioma activo.
        string viewModel = Read(CardViewModelCode);
        viewModel.Should().Contain("MaterialIconKind.ChevronUp",
            "el chevron tiene que contar el estado: uno fijo mentiría en la mitad de los casos");
        viewModel.Should().Contain("nameof(NodeViewModel.IsExpanded)",
            "sin el refresco, conmutar cambia el estado en el núcleo y la tarjeta sigue pintando el chevron de antes");
        viewModel.Should().Contain("ToggleParametersToolTip",
            "el rótulo del conmutador es una cadena del diccionario del host, en los dos idiomas");
    }

    /// <summary>
    /// Las ÓRDENES DESTRUCTIVAS que un modal del host pide confirmar (hito 263): la confirmación tiene que ser
    /// la REAL, por las DOS puertas, y sin bloquear el hilo de UI.
    ///
    /// <para>El defecto que esto vigila ya se midió y es de los que se ven verdes por partes: la orden existe,
    /// la pregunta existe y la regla existe —pero la puerta de la fila le daba al view model el servicio de
    /// diálogos NULO (que responde «sí» sin preguntar, así que borraba en silencio) y la de la tarjeta le daba
    /// el del host, cuyo `ShowConfirmation` SÍNCRONO devuelve «no» desde el hilo de UI (así que no borraba y
    /// tampoco avisaba). Dos comportamientos para una sola regla, y ninguno de los dos preguntaba.</para>
    ///
    /// <para>La guardia ata las cuatro piezas que hacen que no vuelva: el contrato con su variante asíncrona
    /// (que delega en la síncrona, para no romper a los hosts que sí saben confirmar), el view model esperando
    /// esa respuesta y sin usar la síncrona para destruir, el host preguntando DENTRO del modal abierto (WinUI
    /// no admite dos <c>ContentDialog</c>), y las dos puertas resolviendo el servicio del host.</para>
    /// </summary>
    [Fact]
    public void TheDestructiveOrders_ShouldAskAndWaitForTheRealAnswer_OnBothDoors()
    {
        // 1. El contrato: hay confirmación asíncrona y su implementación por defecto delega en la síncrona.
        string contract = Code("FileFlow.Sdk/Services/IDialogService.cs");
        contract.Should().Contain("Task<bool> ConfirmAsync(",
            "un host cuyo modal sólo existe en asíncrono necesita pedir la respuesta sin bloquear el hilo de UI");
        contract.Should().Contain("Task.Run(() => ShowConfirmation(message, title))",
            "y la implementación por defecto tiene que seguir sirviendo al escritorio y a los dobles de prueba");

        // 2. El view model portable: pregunta por la vía asíncrona, espera la respuesta y NO usa la síncrona.
        string viewModel = Code("FileFlow.Plugin.Integrations/UI/ViewModels/MediaPresetManagerViewModel.cs");
        viewModel.Should().Contain("await _dialogService.ConfirmAsync(",
            "la operación destructiva depende de la respuesta REAL del usuario");
        viewModel.Should().NotContain("_dialogService.ShowConfirmation(",
            "la variante síncrona es la que en un host moderno contesta «no» sin preguntar");
        viewModel.Should().Contain("public async Task DeletePresetAsync()");
        viewModel.Should().Contain("public async Task ResetDefaultsAsync()",
            "las dos órdenes destructivas, no sólo la primera");

        // 3. El host: confirmación de verdad, y montada DENTRO del modal abierto (WinUI admite uno solo).
        Code("FileFlow.App.Uno/Platform/UnoDialogService.cs").Should().Contain("public Task<bool> ConfirmAsync(");
        string windowService = Code(WindowService);
        windowService.Should().Contain("AskInsideActiveDialogAsync");
        windowService.Should().Contain("HostConfirmationAccept",
            "los botones de la pregunta llevan su ancla: es como la sonda y el canal externo la contestan");
        windowService.Should().Contain("TearDownInlineQuestion(confirmed)",
            "lo que devuelve la pregunta es lo que el usuario contestó");
        windowService.Should().Contain("s_inlineHost.Children.Remove(s_inlineLayer)",
            "y retirarla es devolver el cuerpo a su sitio: la capa sale del cuerpo (no se cambia el contenido "
            + "del modal, que WinUI no deja colgar de dos padres)");
        windowService.Should().Contain("HostPanel(body)",
            "la capa se monta DENTRO del cuerpo del modal: dentro, un segundo ContentDialog no cabe en WinUI");
        Regex.Matches(windowService, @"TearDownInlineQuestion\(false\)").Count.Should().Be(2,
            "hay DOS caminos por los que la pregunta en pantalla se pierde sin que nadie la conteste —el modal "
            + "que se va con ella (Escape, su botón de cerrar) y otra pregunta que la sustituye— y los dos tienen "
            + "que soltarla como un «no»: sin eso el host rechazaba toda pregunta posterior (la orden no hacía "
            + "NADA y tampoco avisaba) y la que la esperaba no terminaba nunca");
        windowService.Should().Contain("TaskCompletionSource<bool>? answer = s_inlineAnswer;",
            "soltar la pregunta es liberar SU estado, no el de otra que venga después");

        // 4. Las DOS puertas resuelven el servicio del host: ninguna puede caer en el Nulo que auto-confirma.
        string rowDoor = Code("FileFlow.App.Core/ViewModels/NodeParameterViewModel.cs");
        rowDoor.Should().Contain("CoreDialogHost.ResolveDialogService()");
        rowDoor.Should().NotContain("_dialogService = dialogService ?? NullDialogService.Instance;",
            "con el Nulo, la puerta de la fila borra sin preguntar");
        Code("FileFlow.App.Core/ViewModels/NodeViewModel.cs")
            .Should().Contain("CoreDialogHost.ResolveDialogService()",
                "y la de la tarjeta tiene que pedir el MISMO servicio, o las dos puertas vuelven a divergir");

        // 5. Las DOS órdenes destructivas del gestor, no sólo el borrado: el restablecimiento vacía el catálogo
        //    del usuario y pregunta igual. La vista del host expone las dos para poder medirlas.
        Code(PresetBodyCode).Should().Contain("ResetAction => ResetButton");
        Code(PresetBodyCode).Should().Contain("DeleteAction => DeleteButton");

        // 6. Y la medición: el sondeo contesta la pregunta por sus botones reales, en las DOS puertas.
        string selfCheck = Code(SelfCheckCode);
        selfCheck.Should().Contain("ActiveConfirmationAccept");
        selfCheck.Should().Contain("ActiveConfirmationCancel");
        selfCheck.Should().Contain("por la TARJETA, «Eliminar» también PREGUNTA",
            "la puerta de la tarjeta se mide con su botón real, no se da por buena con la de la fila");
        selfCheck.Should().Contain("void DismissNotice()",
            "la sonda responde el AVISO de guardado como el usuario (su «Aceptar»): dejarlo puesto tapaba el "
            + "cuerpo y convertía el borrado siguiente en «no pasa nada»");
    }

    /// <summary>
    /// TODAS las órdenes destructivas del producto preguntan por la vía ASÍNCRONA (hito 265), no sólo las del
    /// gestor de presets.
    ///
    /// <para>El defecto era de producto y estaba en seis sitios más —borrar un modelo descargado, restablecer
    /// o cerrar un flujo con cambios sin guardar, revertir una ejecución, vaciar el registro del VFS, quitar un
    /// dataset sintético y borrar un tema propio—: todas preguntaban con la variante <b>síncrona</b>, que en un
    /// host WinUI **no muestra nada y contesta «no»** desde el hilo de UI (el botón no hace nada y no avisa) y
    /// en un servicio sin diálogos **contesta «sí» sin preguntar** (destruye en silencio). Ninguna de las dos es
    /// la respuesta del usuario.</para>
    ///
    /// <para>La guardia lo mide por los dos lados: cada orden de la tabla tiene que esperar la respuesta real
    /// de <c>ConfirmAsync</c> en su view model portable, y el barrido del árbol de fuentes exige que NINGUNA
    /// vista ni view model del producto vuelva a preguntar por la vía síncrona.</para>
    /// </summary>
    [Fact]
    public void EveryDestructiveOrder_ShouldAskByTheAsyncPath_NotByTheSilentSyncOne()
    {
        // La tabla: la orden, su view model y el método que decide. Se leen del código fuente, no de una
        // copia: añadir una orden destructiva nueva obliga a meterla aquí (y a que pregunte por la vía buena).
        var orders = new (string File, string Method, string Source)[ ]
        {
            ("FileFlow.Plugin.Integrations/UI/ViewModels/MediaPresetManagerViewModel.cs", "DeletePresetAsync", "el gestor de presets: «Eliminar»"),
            ("FileFlow.Plugin.Integrations/UI/ViewModels/MediaPresetManagerViewModel.cs", "ResetDefaultsAsync", "el gestor de presets: «Restablecer»"),
            ("FileFlow.App.Core/ViewModels/ControlBarViewModel.cs", "NewWorkflowAsync", "cerrar el flujo abierto para empezar uno nuevo"),
            ("FileFlow.App.Core/ViewModels/ControlBarViewModel.cs", "RollbackLastExecutionAsync", "revertir las operaciones de la última ejecución"),
            ("FileFlow.App.Core/ViewModels/ThemeCustomizerViewModel.cs", "DeleteThemeAsync", "borrar un tema propio"),
            ("FileFlow.App.Core/ViewModels/VirtualFileSystemExplorerViewModel.cs", "ClearVirtualFileSystemAsync", "vaciar el registro del Sistema de Archivos Virtual"),
            ("FileFlow.App.Core/ViewModels/AiModelManagerViewModel.cs", "DeleteModelAsync", "borrar un modelo descargado del disco"),
            ("FileFlow.Plugin.FileSystem/UI/ViewModels/SyntheticDataSetDesignerViewModel.cs", "DeleteDataSetAsync", "quitar un dataset sintético propio"),
        };

        foreach (var (file, method, what) in orders)
        {
            string code = Code(file);

            // La orden se dibuja en un botón —de ahí el atributo— y su método es ASÍNCRONO, que es lo que le
            // permite ESPERAR la respuesta real en vez de suponerla. La visibilidad no se fija aquí: el
            // diseñador de datasets usa el estilo privado de su propio view model, y el atributo es lo que
            // hace que la orden sea alcanzable desde su vista.
            var declaration = Regex.Match(
                code,
                @"\[RelayCommand\]\s+(?:public|private|internal) async Task " + method + @"\(");
            declaration.Success.Should().BeTrue(
                $"{what}: la orden tiene que estar dibujada ([RelayCommand]) y su método tiene que ser "
                + "asíncrono, o no puede esperar la respuesta del usuario");

            // La llamada se busca DENTRO del método: un fichero con dos órdenes destructivas tiene dos
            // llamadas, y la segunda no vale por la primera.
            code.IndexOf("await _dialogService.ConfirmAsync(", declaration.Index, StringComparison.Ordinal)
                .Should().BeGreaterThan(-1, $"{what}: y preguntar por la vía asíncrona del contrato");

            // Y la pregunta es TEXTO DEL DICCIONARIO, no un literal del código: una orden destructiva se lee
            // en el idioma del usuario como el resto de la superficie. El borrado de un modelo era el único
            // de los ocho que llevaba su pregunta escrita —en español, siempre, cambiara el idioma o no—.
            int bodyEnd = code.IndexOf("[RelayCommand]", declaration.Index + 1, StringComparison.Ordinal);
            string body = code[declaration.Index..(bodyEnd < 0 ? code.Length : bodyEnd)];
            Regex.IsMatch(body, @"(?:GetString|GetFormattedString)\(")
                .Should().BeTrue(
                    $"{what}: y su pregunta tiene que venir del diccionario (clave y texto de reserva), porque "
                    + "el texto de una orden destructiva también se lee en el idioma del usuario");
        }

        // Y el barrido: ninguna vista ni view model del producto pregunta ya por la vía síncrona. El árbol se
        // recorre entero —los proyectos del producto, sin las pruebas— porque el defecto no vivía en un fichero,
        // vivía en el contrato mal usado. Quedan fuera las implementaciones del propio contrato (los tres
        // servicios de diálogos), que son quienes TIENEN que ofrecer las dos vías.
        string root = TestRepositoryLocator.RepositoryRoot();
        var offenders = new List<string>();
        foreach (string project in Directory.EnumerateDirectories(root, "FileFlow.*"))
        {
            if (Path.GetFileName(project) == "FileFlow.Tests")
            {
                continue;
            }

            foreach (string path in Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories))
            {
                if (path.Contains("\\bin\\", StringComparison.Ordinal)
                    || path.Contains("\\obj\\", StringComparison.Ordinal)
                    || Path.GetFileName(path).Contains("DialogService", StringComparison.Ordinal))
                {
                    continue;
                }

                if (File.ReadAllText(path).Contains("ShowConfirmation(", StringComparison.Ordinal))
                {
                    offenders.Add(Path.GetRelativePath(root, path).Replace('\\', '/'));
                }
            }
        }

        offenders.Should().BeEmpty(
            "la confirmación síncrona no puede contestar por el usuario: en un host WinUI devuelve «no» desde el "
            + "hilo de UI (la orden no hace nada y no avisa) y en un servicio sin diálogos devuelve «sí» sin "
            + "preguntar (destruye en silencio). Las órdenes destructivas preguntan por ConfirmAsync");

        // El contrato sigue teniendo la síncrona: los hosts que SÍ saben confirmar en síncrono (el escritorio,
        // con su bomba anidada de mensajes) y la implementación por defecto de la asíncrona la usan.
        Code("FileFlow.Sdk/Services/IDialogService.cs").Should().Contain("bool ShowConfirmation(string message, string title = \"FileFlow Studio\");");
    }

    /// <summary>
    /// El CONTENIDO de una superficie declarada tiene que llevar los <b>diálogos del host</b> (hito 265): el
    /// nodo declara la superficie y construye su contenido, pero vive en un ensamblado de plugin y no puede
    /// resolver los diálogos de quien la sirve; se los pasa quien abre, por el contexto.
    ///
    /// <para>Sin ellos el contenido cae al doble nulo, y a una confirmación el doble nulo contesta <b>«sí» sin
    /// preguntar</b>: cambiar la pregunta a la vía asíncrona no basta si nadie con quien preguntar. El
    /// diseñador de datasets era exactamente ese caso —borraba en silencio en los dos hosts— mientras el gestor
    /// de presets ya recibía los del host por el mismo camino.</para>
    /// </summary>
    [Fact]
    public void EveryDeclaredSurface_ShouldCarryTheHostDialogs_SoItsDestructiveOrdersCanAskForReal()
    {
        const string DataSetNode = "FileFlow.Plugin.FileSystem/Nodes/Sources/SyntheticDataSourceNode.cs";
        const string PresetNode = "FileFlow.Plugin.Integrations/MediaTranscoderNode.cs";

        foreach (string node in new[] { DataSetNode, PresetNode })
        {
            Code(node).Should().Contain("(context as NodeCustomActionContext)?.Dialogs",
                $"el contenido que declara {Path.GetFileName(node)} tiene que recibir los diálogos de quien abre: "
                + "el nodo no puede resolverlos y el doble nulo contesta «sí» sin preguntar");
        }

        // El camino del ESCRITORIO del diseñador de datasets —la ventana que monta el propio plugin— también
        // construye su contenido con esos diálogos, y la orden del núcleo que lo abre los entrega.
        Code(DataSetNode).Should().Contain("new UI.ViewModels.SyntheticDataSetDesignerViewModel(null, dialogs)");
        Code("FileFlow.App.Core/ViewModels/ControlBarViewModel.cs")
            .Should().Contain("new NodeCustomActionContext(_windows.MainWindowOwner, null, _dialogService)",
                "el comando del núcleo abre el diseñador con los diálogos de SU host, como hace con el gestor de presets");

        // Y la puerta del host Uno (la entrada del cajón) entrega el servicio de SU contenedor: es la misma
        // entrega que el núcleo hace en las puertas de la fila y de la tarjeta.
        Code("FileFlow.App.Uno/MainWindow.xaml.cs")
            .Should().Contain("CreateDialogPayload(new NodeCustomActionContext(");
        Code("FileFlow.App.Uno/MainWindow.xaml.cs")
            .Should().Contain("App.Services.GetRequiredService<IDialogService>()");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 5. La medición: modo propio, por el canal del usuario y leyendo el valor escrito
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheProbe_ShouldHaveItsOwnMode_AndReadTheWrittenValue()
    {
        Code(AppCode).Should().Contain("\"--selfcheck-dialogs\"",
            "el sondeo de los paneles de nodo tiene que tener su propio modo: abre modales sobre la misma raíz "
            + "y escribe en el nodo inspeccionado");
        Code(AppCode).Should().Contain("RuntimeSelfCheck.RunDialogsProbe(");

        string selfCheck = Code(SelfCheckCode);
        selfCheck.Should().Contain("public static int RunDialogsProbe(Window window, DispatcherQueue dispatcher)");
        selfCheck.Should().Contain("selfcheck-dialogs-report.txt",
            "el veredicto tiene que quedar en su fichero, como el de las otras sondas");

        Regex.Matches(selfCheck, @"RunDialogsProbe").Count.Should().Be(1,
            "el sondeo se arranca desde la línea de comandos, no desde el recorrido del lienzo");

        // Pulsa por el MISMO canal que un lector de pantalla (el peer de automatización del control) y lee su
        // veredicto del PARÁMETRO del nodo: el valor que el nodo ejecuta, no el texto del diálogo.
        Code(WindowService).Should().Contain("FrameworkElementAutomationPeer.CreatePeerForElement");
        selfCheck.Should().Contain("UnoWindowService.Press(");
        selfCheck.Should().Contain("ParamEditor_");
        selfCheck.Should().Contain("ParamVariable_");
        // El veredicto es el VALOR del parámetro del nodo —el dato que el nodo ejecuta—, no el texto que el
        // modal enseña: un diálogo que se abre y no escribe nada cae aquí igual que un botón sin efecto.
        selfCheck.Should().Contain("exampleParam?.Value?.ToString()");
        selfCheck.Should().Contain("longParam?.Value?.ToString()");

        // El envoltorio de la línea de comandos tiene que enrutar el modo nuevo (y esperar el veredicto).
        Read("run-uno.ps1").Should().Contain("\"--selfcheck-dialogs\"");
        Read("run-uno-fast.ps1").Should().Contain("\"--selfcheck-dialogs\"");
    }

    // ─────────────────────────────────────────────────────────────────────────────

    private static Dictionary<string, string> Dictionary(string relativePath)
    {
        XDocument document = XDocument.Parse(Read(relativePath));
        return document.Root!.Elements("data")
            .ToDictionary(
                element => element.Attribute("name")!.Value,
                element => element.Element("value")?.Value ?? string.Empty,
                StringComparer.Ordinal);
    }
}
