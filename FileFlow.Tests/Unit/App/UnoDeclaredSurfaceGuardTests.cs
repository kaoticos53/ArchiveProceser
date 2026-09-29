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
/// La guardia de las <b>SUPERFICIES DECLARADAS POR EL NODO</b> y sus órdenes destructivas (hitos 261, 263, 265
/// y 269): la superficie que un nodo declara al SDK —el gestor de presets, el diseñador de datasets— la sirven
/// los DOS hosts montando el MISMO view model del plugin, por las DOS puertas (la fila del parámetro y la
/// tarjeta del nodo), y con los diálogos de quien la abre.
///
/// <para><b>Qué protege</b>: que la superficie la declare el nodo y no la pinte cada host por su cuenta; que la
/// tarjeta del nodo tenga su puerta para las acciones rápidas; que toda orden destructiva pregunte por la vía
/// ASÍNCRONA —la síncrona devuelve «no» desde el hilo de UI y «sí» sin preguntar desde el doble nulo: ninguna
/// de las dos es la respuesta del usuario—; que el contenido de la superficie reciba los diálogos del host,
/// porque el plugin no puede resolverlos; y que la medición de la puerta de la tarjeta viva en el sondeo de los
/// diálogos.</para>
///
/// <para><b>Por qué en su propio archivo</b>: son superficies que el host sirve SIN ser suyas —las declara el
/// nodo—, y ése es un sujeto distinto del catálogo de diálogos propio (en
/// <c>UnoNodeDialogCatalogGuardTests</c>).</para>
/// </summary>
public class UnoDeclaredSurfaceGuardTests
{
    private const string WindowService = "FileFlow.App.Uno/Platform/UnoWindowService.cs";
    private const string PresetBodyXaml = "FileFlow.App.Uno/Controls/MediaPresetManagerBody.xaml";
    private const string PresetBodyCode = "FileFlow.App.Uno/Controls/MediaPresetManagerBody.xaml.cs";
    private const string InspectorCode = "FileFlow.App.Uno/Controls/NodeInspectorPanel.xaml.cs";
    private const string CardViewXaml = "FileFlow.App.Uno/Controls/NodeCardView.xaml";
    private const string CardViewModelCode = "FileFlow.App.Uno/Controls/NodeCardViewModel.cs";
    private const string SelfCheckCode = "FileFlow.App.Uno/SelfCheckDialogs.cs";

    private static string Code(string relativePath) => SourceText.CodeWithoutComments(relativePath);

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(TestRepositoryLocator.RepositoryRoot(), relativePath));
    // ─────────────────────────────────────────────────────────────────────────────
    // 1. El GESTOR DE PRESETS: la superficie la declara el NODO y las dos vistas la pintan
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
    /// entre sus acciones rápidas, y ese bloque sólo se pinta con la tarjeta desplegada (<c>Node.IsExpanded</c>).
    /// El escritorio lo conmuta con su <c>ToggleButton</c>; este host no tenía ninguno, así que el panel era
    /// <b>inalcanzable</b>: la acción estaba dibujada y sin puerta.
    ///
    /// <para>La guardia ata las tres piezas —el conmutador de la cabecera, el estado del NÚCLEO que conmuta y el
    /// bloque que cuelga de él— porque el defecto no se veía en ninguna por separado: el view model tenía el
    /// estado, la vista tenía el bloque, y faltaba justo lo que los une.</para>
    ///
    /// <para><b>Qué cambió en el hito 269 y por qué la guardia lo fija</b>: el panel ya no enseña el listado de
    /// parámetros del nodo —nombres sin editor, que se pulsaban y no hacían nada; los editores son de la ficha
    /// del inspector—, así que el desplegable se quedó con las acciones y su puerta sólo se dibuja donde hay
    /// algo que desplegar. Las dos condiciones son el mismo contrato, y por eso se atan juntas.</para>
    /// </summary>
    [Fact]
    public void TheNodeCard_ShouldBeAbleToShowThePanelWhereTheQuickActionsLive()
    {
        string xaml = Read(CardViewXaml);

        // El panel de ACCIONES y la condición que lo enseña: desplegado Y con algo dentro.
        xaml.Should().Contain("ItemsSource=\"{Binding Node.CustomActions}\"",
            "las acciones rápidas del nodo —entre ellas «🎬 Presets...»— son el contenido del panel");
        xaml.Should().Contain("Visibility=\"{Binding ActionsPanelVisible, Converter={StaticResource BoolToVis}}\"",
            "y el panel cuelga del estado desplegado del nodo Y de que haya acciones que enseñar");
        xaml.Should().NotContain("ItemsSource=\"{Binding Node.Parameters}\"",
            "el listado de parámetros de la tarjeta era una lista muerta (sólo nombres, sin editor): los "
            + "parámetros se editan en la ficha del inspector, no en el lienzo");

        // La puerta: sin conmutador, ese estado no se puede cambiar desde el ratón (el escritorio sí lo tiene).
        xaml.Should().Contain("AutomationProperties.AutomationId=\"NodeCardExpandToggle\"",
            "la cabecera de la tarjeta necesita su conmutador de acciones, como la del escritorio");
        xaml.Should().Contain("IsChecked=\"{Binding Node.IsExpanded, Mode=TwoWay}\"",
            "y tiene que conmutar el estado del NÚCLEO, no uno propio de la vista");
        xaml.Should().Contain("Visibility=\"{Binding HasCustomActions, Converter={StaticResource BoolToVis}}\"",
            "el conmutador no se dibuja en un nodo sin acciones: desplegaría un panel vacío");

        // Y las tres piezas del adaptador: el chevron que cuenta el estado, el refresco que lo sigue y la
        // conjunción que decide si hay algo que enseñar.
        string viewModel = Read(CardViewModelCode);
        viewModel.Should().Contain("MaterialIconKind.ChevronUp",
            "el chevron tiene que contar el estado: uno fijo mentiría en la mitad de los casos");
        viewModel.Should().Contain("nameof(NodeViewModel.IsExpanded)",
            "sin el refresco, conmutar cambia el estado en el núcleo y la tarjeta sigue pintando el chevron de antes");
        viewModel.Should().Contain("ToggleParametersToolTip",
            "el rótulo del conmutador es una cadena del diccionario del host, en los dos idiomas");
        viewModel.Should().Contain("public bool ActionsPanelVisible => HasCustomActions && _node.IsExpanded;",
            "y la visibilidad del panel es UNA sola condición: sin acciones no hay nada que desplegar, "
            + "y plegado tampoco se enseña");
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
    // 2. El GESTOR DE CONTRASEÑAS: la superficie que se OFRECÍA sin poder servirse
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// El GESTOR DE CONTRASEÑAS (hito 278) es la tercera superficie que declara un nodo, y la que estaba a medio
    /// portar de la peor manera: la TARJETA del nodo ofrecía «🔑 Claves...» y la fila del parámetro lo declaraba
    /// pendiente en la tabla de puertas; pulsarla en este host no abría el gestor sino que AVISABA de que la
    /// ventana era del escritorio. La oferta y la capacidad se contradecían.
    ///
    /// <para><b>Qué ata esta guardia</b>: que la capacidad la declare el NODO (los dos nodos de descompresión del
    /// plugin de archivos, porque los dos ofrecen la misma acción) con su clave del catálogo y su view model
    /// portable; que el host la SIRVA —en la tabla de servidos, no en la de pendientes—; que las dos puertas
    /// (la fila y la tarjeta) abran la MISMA superficie; y que ningún nodo pueda volver a ofrecer la acción sin
    /// declararla. Sin lo último, copiar el patrón de un nodo a otro reintroduce el defecto en silencio.</para>
    /// </summary>
    [Fact]
    public void ThePasswordManager_ShouldBeDeclaredByItsNodes_AndServedByTheHostThatOffersIt()
    {
        const string DialogKeysFile = "FileFlow.Sdk/Services/IWindowService.cs";
        const string DesktopWindow = "FileFlow.Plugin.Archives/UI/Views/PasswordManagerWindow.axaml.cs";
        const string DesktopService = "FileFlow.App/Services/AvaloniaWindowService.cs";
        const string ViewModel = "FileFlow.Plugin.Archives/UI/ViewModels/PasswordManagerViewModel.cs";
        const string BodyCode = "FileFlow.App.Uno/Controls/PasswordManagerBody.xaml.cs";
        const string RowViewModel = "FileFlow.App.Core/ViewModels/NodeParameterViewModel.cs";

        // 1. La clave canónica: sin ella no hay identidad de diálogo que los dos hosts puedan servir.
        Code(DialogKeysFile).Should().Contain("public const string PasswordManager = \"PasswordManager\";");

        // 2. Los nodos que ofrecen la acción la declaran, con su clave, su acción sustituida y su view model.
        //    El barrido es la mitad que muerde: recorre TODOS los ficheros de nodo del producto que ofrecen
        //    «ManagePasswords» —no una lista escrita a mano— y exige que cada uno declare la superficie.
        string root = TestRepositoryLocator.RepositoryRoot();
        var offeringNodes = new List<string>();
        foreach (string project in Directory.EnumerateDirectories(root, "FileFlow.Plugin.*"))
        {
            foreach (string path in Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories))
            {
                if (path.Contains("\\bin\\", StringComparison.Ordinal)
                    || path.Contains("\\obj\\", StringComparison.Ordinal))
                {
                    continue;
                }

                if (File.ReadAllText(path).Contains("new(\"ManagePasswords\"", StringComparison.Ordinal))
                {
                    offeringNodes.Add(Path.GetRelativePath(root, path).Replace('\\', '/'));
                }
            }
        }

        offeringNodes.Should().HaveCountGreaterThanOrEqualTo(2,
            "el censo tiene que leer de verdad los nodos que ofrecen la acción: si no encuentra ninguno, "
            + "la guardia estaría midiendo el vacío");
        offeringNodes.Should().Contain("FileFlow.Plugin.Archives/SmartUnpackNode.cs");

        foreach (string node in offeringNodes)
        {
            string code = Code(node);
            code.Should().Contain("INodeDialogSurfaceProvider",
                $"{Path.GetFileName(node)} ofrece «🔑 Claves...» en su tarjeta: o declara la superficie —para que "
                + "cualquier host la sirva sobre el view model portable— o su única salida en un host sin el toolkit "
                + "del escritorio es avisar de que no puede (la contradicción entre la oferta y la capacidad)");
            code.Should().Contain("DialogKeys.PasswordManager");
            code.Should().Contain("ReplacesCustomActionId => \"ManagePasswords\"",
                "y tiene que decir qué acción sustituye: es el hilo que une el botón de la tarjeta y el de la fila "
                + "con esta misma superficie");
            code.Should().Contain("new UI.ViewModels.PasswordManagerViewModel(",
                "y su carga útil es el view model portable, no una ventana");
        }

        // 3. El view model es portable (sin toolkit) y es QUIEN lleva la regla: la lista, su recuento y lo que se
        //    guarda en el parámetro del nodo.
        string viewModel = Read(ViewModel);
        viewModel.Should().NotContain("using Avalonia");
        viewModel.Should().NotContain("Microsoft.UI.Xaml");
        viewModel.Should().Contain("public string PasswordsText =>");
        viewModel.Should().Contain("public void Save() => _onSaved?.Invoke(PasswordsText);",
            "el guardado es del view model: la vista no escribe el parámetro del nodo");
        viewModel.Should().Contain("private static IReadOnlyList<string> Split(",
            "y qué es una clave (una por línea) también es del producto, no de cada vista");

        // 4. El host Uno la SIRVE: en la tabla de servidos y con la carga útil que espera. Estar en la de
        //    pendientes es exactamente el defecto que este tramo cierra.
        string service = Code(WindowService);
        service.Should().Contain("(DialogKeys.PasswordManager, nameof(PasswordManagerBody))",
            "la clave tiene que estar entre las servidas, con la vista que la sirve");
        service.Should().Contain("payload is PasswordManagerViewModel passwordManager",
            "y el servicio tiene que comprobar la carga útil que espera, no tragarse cualquier cosa");
        int pendingAt = service.IndexOf("DeclaredPendingDialogs =", StringComparison.Ordinal);
        pendingAt.Should().BeGreaterThan(-1, "el host tiene que declarar lo que NO sirve, con su razón");
        string pending = service[pendingAt..service.IndexOf("];", pendingAt, StringComparison.Ordinal)];
        pending.Should().NotContain("PasswordManager",
            "no puede estar además en la tabla de lo que este host NO sirve: la capacidad que la tarjeta ofrece "
            + "no puede estar declarada pendiente a la vez");

        // 5. Y su vista pinta el view model del PLUGIN y no habla con el nodo.
        string body = Code(BodyCode);
        body.Should().Contain("PasswordManagerViewModel");
        body.Should().NotContain("new PasswordManagerViewModel(",
            "el view model lo construye quien lo declara (el nodo), no la vista");

        // 6. El escritorio sirve la MISMA superficie: la ventana del plugin como vista de ese view model. Sin
        //    esta mitad, declararla rompería el escritorio (su catálogo no conocería la clave).
        Code(DesktopService).Should().Contain("DialogKeys.PasswordManager => new PasswordManagerWindow(");
        string desktop = Code(DesktopWindow);
        desktop.Should().Contain("PasswordManagerViewModel");
        desktop.Should().NotContain("string.Join(",
            "la ventana del escritorio no parte ni junta la lista: eso es del view model");
        desktop.Should().NotContain("TxtPasswordEditor.Text",
            "ni escribe el texto del editor por su cuenta: lo enlaza el view model");

        // 7. La PUERTA DE LA FILA: la orden existe, el botón está dibujado en la fila de las claves y la tabla la
        //    declara SERVIDA (con su ancla), no pendiente. Las dos puertas, de acuerdo.
        string inspector = Code(InspectorCode);
        inspector.Should().Contain("(\"OpenPasswordManagerCommand\", \"ParamPassword_\",");
        inspector.Should().Contain("bool wantsPassword = p.IsPasswordList;");
        inspector.Should().Contain("p.OpenPasswordManagerCommand");
        inspector.Should().NotContain("(\"OpenPasswordManagerCommand\", \"abre el gestor de contraseñas");

        // 8. Y el núcleo abre la superficie declarada desde la fila, como en la tarjeta: con la vuelta para
        //    resincronizar el parámetro que el gestor acaba de escribir.
        string row = Code(RowViewModel);
        int methodAt = row.IndexOf("public async Task OpenPasswordManagerAsync()", StringComparison.Ordinal);
        methodAt.Should().BeGreaterThan(-1, "la orden de la fila tiene que ser asíncrona: espera a que se cierre la superficie");
        int methodEnd = row.IndexOf("[RelayCommand]", methodAt, StringComparison.Ordinal);
        string methodBody = row[methodAt..(methodEnd < 0 ? row.Length : methodEnd)];
        methodBody.Should().Contain("surface.ReplacesCustomActionId is { } replaced",
            "la fila tiene que abrir la superficie que declara el nodo antes de caer al camino del toolkit");
        methodBody.Should().Contain("await _windows.ShowDialogAsync(surface.DialogKey, surface.CreateDialogPayload(context));");
    }
}
