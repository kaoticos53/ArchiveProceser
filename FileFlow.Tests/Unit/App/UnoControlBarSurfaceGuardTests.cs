using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;
/// <summary>
/// La guardia de las <b>ENTRADAS CON VENTANA</b> del menú principal del host Uno (hitos 255, 259 y 261): las
/// que no dibujan una superficie propia sino que piden al catálogo de diálogos del host la ventana que el
/// núcleo (o el plugin) declara —ajustes, explorador del VFS, métricas, estudio de temas, aviso de
/// actualización y diseñador de datasets—.
///
/// <para><b>Qué protege</b>: que cada una esté de verdad servida por el catálogo de diálogos del host y que lo
/// declarado no esté además dibujado; que el canal de ejecución de la barra siga siendo el observable del
/// núcleo; que el estudio de temas declare lo que NO puede servir en vez de dibujarlo y que la comprobación de
/// actualización alimente su distintivo sin meterse en los sondeos; y que el diseñador de datasets lo declare el
/// NODO al SDK y lo sirva el host por el mismo contrato.</para>
///
/// <para><b>Por qué en su propio archivo</b>: su sujeto es una SUPERFICIE ajena —el catálogo de diálogos, el
/// nodo que la declara—, no el censo de la barra (en <c>UnoControlBarEntryGuardTests</c>) ni los textos (en
/// <c>UnoControlBarTextsGuardTests</c>).</para>
/// </summary>
public class UnoControlBarSurfaceGuardTests
{
    private const string BarXaml = "FileFlow.App.Uno/Controls/ControlBar.xaml";
    private const string BarCode = "FileFlow.App.Uno/Controls/ControlBar.xaml.cs";
    private const string DrawerXaml = "FileFlow.App.Uno/Controls/MainMenuDrawer.xaml";
    private const string DrawerCode = "FileFlow.App.Uno/Controls/MainMenuDrawer.xaml.cs";
    private const string WindowCode = "FileFlow.App.Uno/MainWindow.xaml.cs";
    private const string AppCode = "FileFlow.App.Uno/App.xaml.cs";

    /// <summary>Dónde vive la orden de una entrada: en el code-behind (un comando) o en el XAML (un enlace).</summary>
    private enum Home
    {
        Code,
        Xaml,
    }

    private static string Code(string relativePath) => SourceText.CodeWithoutComments(relativePath);

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(TestRepositoryLocator.RepositoryRoot(), relativePath));

    // ─────────────────────────────────────────────────────────────────────────────
    // 1. Las VENTANAS del menú (hito 259): las sirve el catálogo de diálogos del host
    // ─────────────────────────────────────────────────────────────────────────────

    private const string WindowServiceCode = "FileFlow.App.Uno/Platform/UnoWindowService.cs";
    private const string DialogKeysCode = "FileFlow.Sdk/Services/IWindowService.cs";

    /// <summary>
    /// El «Ejecutar» del host y su canal observable: el botón ejecuta la MISMA orden del núcleo que la
    /// versión anterior, canta su ancla para la observación externa y el ciclo deja su línea donde un observador sin
    /// UIAccess puede leerla. Este caso vivía en la guardia del panel del inspector (de donde salió en el
    /// reorden del hito 276): su sujeto es la barra de control y la franja de estado, no la ficha.
    /// </summary>
    [Fact]
    public void TheUnoHost_ShouldExposeTheCanonicalExecuteCommand_AsAnObservableChannel()
    {
        string window = Code(WindowCode);

        window.Should().Contain(
            "controlBar.ExecuteWorkflowCommand.ExecuteAsync(null)",
            "el Ejecutar del host Uno es el MISMO comando del ControlBar del núcleo que el botón " +
            "de la versión anterior: una segunda vía de ejecución duplicaría la orquestación (coordinador, " +
            "dry-run, checkpoint) que la suite ya defiende");

        window.Should().Contain(
            "AutomationProperties.SetAutomationId(runButton, \"ExecuteButton\")",
            "el botón canta su AutomationId para la observación UIA externa (el guion del ciclo " +
            "completo lo localiza por ancla estable, no por título)");

        window.Should().Contain(
            "StatusLineWriter.Padded(line)",
            "la línea de ejecución vive en el canal del writer (renglón padded, escritura " +
            "atómica): el estado de la ejecución es legible desde fuera sin fragmentado");

        string writer = SourceText.CodeWithoutComments("FileFlow.App.Uno/StatusLineWriter.cs");

        writer.Should().Contain(
            "File.WriteAllText(CurrentExecutionStatusFile, line)",
            "el fichero espejo es la segunda vía de lectura del ciclo para un observador externo " +
            "(la que no depende del fragmentado del TextBlock en el árbol UIA)");
    }

    /// <summary>
    /// Las cuatro entradas del hito 259 abren su ventana por el CATÁLOGO DE DIÁLOGOS del host: la orden
    /// canónica del núcleo pide una clave y el servicio la sirve. Sin esta mitad, la entrada existiría y su
    /// orden pediría una ventana que nadie tiene —el modo de fallo que el censo declara ya no puede quedar
    /// escondido—.
    /// </summary>
    [Fact]
    public void TheWindowEntries_ShouldBeServedByTheHostsDialogCatalogue()
    {
        string service = Code(WindowServiceCode);
        string bar = Code(BarCode);
        string drawer = Code(DrawerCode);

        // La tabla de lo servido: las cuatro entradas con su destino, declaradas en el control (no en la guardia).
        var served = UnoControlBarTables.Of("ServedWindowEntries");
        served.Should().BeEquivalentTo(
            ["OpenThemeCustomizerCommand", "OpenMetricsDashboardCommand", "OpenVirtualFileSystemExplorerCommand", "OpenUpdateDialogCommand"],
            "las cuatro entradas con ventana del hito 259 tienen que estar en la tabla de lo servido, que es lo que"
            + " la mantiene legible cuando se añada la siguiente (el Diseñador de Datasets del hito 261 va por su"
            + " propio contrato y vive en HostOwnedOrders)");

        // Y cada una tiene que estar DIBUJADA en el host: la tabla no sustituye a la entrada. Se busca la
        // orden FUERA de las tablas de declaración —una fila de `ServedWindowEntries` nombra la orden y
        // eso no es dibujarla—, en el code-behind o en el XAML de las dos vistas.
        string drawn = WithoutDeclarationTables(bar) + drawer + Read(BarXaml) + Read(DrawerXaml);
        foreach (string command in served)
        {
            drawn.Should().Contain(command,
                $"la entrada servida {command} tiene que estar dibujada en la barra o en el cajón: la tabla de "
                + "lo servido lo deja escrito, pero no es la entrada");
            service.Should().Contain($"(DialogKeys.",
                "y el servicio tiene que seguir sirviendo claves del catálogo con su vista");
        }

        // El catálogo: las claves SERVIBLES (con su vista) y las que quedan declaradas con su razón. Las
        // declaradas no pueden aparecer como implementadas —sería una mentira en la tabla—.
        foreach (string key in new[] { "ThemeCustomizer", "WorkflowMetricsDashboard", "VirtualFileSystemExplorer", "UpdateDialog", "DataSetDesigner", "AiModelUrlsConfig" })
        {
            service.Should().Contain($"(DialogKeys.{key},", $"la clave {key} tiene que estar entre las servidas");
        }

        // El editor de URLs por modelo entró aquí en el hito 262: cuando su acción pasó a estar dibujada en la
        // fila del catálogo, la clave dejó de poder declararse pendiente. Sólo queda una declarada, y es una
        // decisión: la superficie de ajustes tiene su puerta en la barra y el cajón, y abrirla por este canal
        // sería una segunda copia de lo mismo.
        foreach (string key in new[] { "WorkflowSettings" })
        {
            service.Should().Contain($"(DialogKeys.{key}, \"", $"la clave {key} tiene que seguir declarada con su razón");
        }

        service.Should().NotContain("(DialogKeys.AiModelUrlsConfig, \"",
            "y la de URLs por modelo ya NO puede estar declarada: está servida, y declararla además sería no decir nada");

        // El censo completo: ninguna clave del catálogo puede quedarse fuera de las dos tablas.
        string keys = Code(DialogKeysCode);
        var all = Regex.Matches(keys, @"public const string [A-Za-z]+ = ""([A-Za-z]+)"";")
            .Select(m => m.Groups[1].Value)
            .ToList();
        all.Should().HaveCountGreaterThanOrEqualTo(9,
            "el catálogo de claves tiene que leerse de verdad del contrato del SDK");

        string serviceRaw = Code(WindowServiceCode);
        var missing = all.Where(key => !serviceRaw.Contains($"DialogKeys.{key}", StringComparison.Ordinal)).ToList();
        missing.Should().BeEmpty(
            "toda clave del catálogo tiene que aparecer en la tabla de servidas o en la de declaradas: una clave"
            + " nueva sin destino es un diálogo que se pide y se cae sin decir nada");
    }

    /// <summary>
    /// El ESTUDIO DE TEMAS es la ventana que más se apoya en la versión anterior, así que declara sus partes
    /// pendientes en su propia tabla: las DOS órdenes que necesitan el selector de fichero SÍNCRONO (que desde
    /// el hilo de UI devuelve nulo) y la vista previa en vivo. La guardia exige que estén declaradas y que NO
    /// estén dibujadas —un botón cuyo destino no existe sería la mentira con forma de botón que el proyecto no
    /// acepta—, y por el otro lado exige DIBUJADA la que dejó de serlo: «Eliminar tema» (hito 265), cuya orden
    /// ya pregunta por el contrato asíncrono.
    /// </summary>
    [Fact]
    public void TheThemeStudio_ShouldDeclareWhatItCannotServe_AndNotDrawIt()
    {
        string body = Code("FileFlow.App.Uno/Controls/ThemeCustomizerBody.xaml.cs");
        string view = Read("FileFlow.App.Uno/Controls/ThemeCustomizerBody.xaml");

        body.Should().Contain("internal static readonly (string Part, string Reason)[] DeclaredPendingParts");
        foreach (string part in new[] { "ExportThemeAsyncCommand", "ImportThemeAsyncCommand", "LivePreviewResources" })
        {
            body.Should().Contain(part, $"la parte {part} del estudio de la versión anterior tiene que estar declarada con su razón");
            view.Should().NotContain(part, $"y no puede estar además dibujada en la vista: si ya está, se quita de la tabla");
        }

        // «Eliminar tema» salió de esa tabla en el hito 265: su orden ya pregunta por el contrato ASÍNCRONO de
        // diálogos (el que este host contesta), así que el botón se DIBUJA y se ejecuta su comando canónico. Una
        // parte dibujada NO puede seguir declarada pendiente, y una declarada no puede estar dibujada: la
        // guardia lo exige por los dos lados.
        body.Should().NotContain("\"DeleteThemeCommand\"",
            "si el botón está dibujado, la fila se quita de la tabla de pendientes");
        view.Should().Contain("AutomationProperties.AutomationId=\"ThemeStudioDeleteButton\"",
            "el borrado del estudio tiene que tener su botón: su orden ya se puede cumplir en este host");
        body.Should().Contain("private void OnDeleteClicked(object sender, RoutedEventArgs e) => _vm.DeleteThemeCommand.Execute(null);",
            "y su manejador ejecuta la orden del view model: la vista no confirma ni borra por su cuenta");

        // Las órdenes que SÍ se dibujan son las canónicas del view model portable, no copias en la vista.
        foreach (string order in new[] { "NewCustomThemeCommand", "DuplicateThemeCommand", "DeleteThemeCommand",
                                          "ApplyToApplicationCommand", "SaveAndApplyCommand" })
        {
            body.Should().Contain(order + ".Execute(null)", $"la orden {order} del estudio la ejecuta su view model, no la vista");
        }

        body.Should().NotContain("RemoveCustomTheme(",
            "borrar un tema lo hace el servicio de temas del núcleo: la vista no reimplementa el producto");
        body.Should().NotContain("ShowConfirmation(",
            "y la confirmación es del producto (el contrato asíncrono del view model), no de la vista");
    }

    /// <summary>
    /// El AVISO DE ACTUALIZACIÓN tiene que estar alimentado por alguien: el host comprueba las actualizaciones
    /// al arrancar (la misma mitad de la versión anterior) y ese resultado es el que enciende el distintivo. Sin la
    /// comprobación, el distintivo no se enciende nunca y el aviso que el host ya sirve no lo pide nadie.
    /// </summary>
    [Fact]
    public void TheUpdateCheck_ShouldFeedTheBadge_AndStayOutOfTheProbes()
    {
        string app = Code(AppCode);

        // La LLAMADA, no sólo la definición: un método de comprobación que nadie invoca deja el distintivo
        // apagado para siempre y no rompe nada — el fallo exacto que esta guardia tiene que ver.
        app.Should().Contain("StartUpdateCheck(s_services);",
            "el arranque del host tiene que ARRANCAR la comprobación de actualizaciones: definirla y no "
            + "llamarla deja el aviso invisible para siempre");
        app.Should().Contain("CheckForUpdatesAsync(",
            "y la comprobación tiene que ser la del servicio del núcleo, la misma que la de la versión anterior");
        app.Should().Contain("ApplyPendingUpdate(",
            "y entregar la novedad a la ventana, que es quien tiene el view model de la barra");
        app.Should().Contain("StartsWith(\"--selfcheck\"",
            "la comprobación se SALTA ENTERA en los modos de sondeo —no basta con ignorar su resultado—: su "
            + "veredicto tiene que ser hermético y una novedad real abriría un aviso en mitad de la medición");

        Code(WindowCode).Should().Contain("internal void ApplyPendingUpdate(",
            "la ventana es la que tiene el ControlBar del núcleo y la que puede encender el distintivo");
        Code(WindowCode).Should().Contain("_controlBar?.SetPendingUpdate(info)",
            "y lo enciende por el MISMO camino de la versión anterior (ControlBar.SetPendingUpdate)");

        string bar = Code(BarCode);
        bar.Should().Contain("OpenUpdateDialogCommand", "el distintivo ejecuta la orden canónica del aviso");
        bar.Should().Contain("PendingUpdateVersionTag",
            "y su rótulo sale de la versión que el arranque anunció, no de una copia en la vista");
    }

    /// <summary>
    /// El código de la barra SIN sus tablas de declaración (censo de declaradas, de cumplidas por el host y
    /// de entradas servidas): lo que quede es lo que la vista HACE.
    ///
    /// <para>Hace falta para medir de verdad un dibujado: una tabla que nombra la orden dice que la entrada
    /// existe, no que se ejecute —y confundir las dos cosas deja pasar una entrada dibujada cuyo manejador se
    /// quedó vacío, que es justo el fallo que este tramo quiere que se vea.</para>
    /// </summary>
    private static string WithoutDeclarationTables(string barCode)
    {
        string result = barCode;
        foreach (string table in new[] { "DeclaredPendingEntries", "HostOwnedOrders", "ServedWindowEntries" })
        {
            int at = result.IndexOf(table + " =", StringComparison.Ordinal);
            if (at < 0)
            {
                continue;
            }

            int end = result.IndexOf("];", at, StringComparison.Ordinal);
            if (end > at)
            {
                result = result.Remove(at, end - at);
            }
        }

        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 2. El DISEÑADOR DE DATASETS: la superficie la declara el NODO, y el host la sirve
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// El DISEÑADOR DE DATASETS del hito 261 es la única superficie cuya ORDEN no la cumple ni el comando
    /// canónico ni el canal asíncrono de la ventana: la cumple el contrato de superficie del SDK, que declara
    /// el propio nodo del plugin. Esta guardia ata las cuatro piezas de esa cadena — el contrato en el SDK, el
    /// nodo que lo implementa con su clave y su view model PORTABLE, el servicio del host que sirve esa clave
    /// con vista propia, y esa vista como vista del view model del plugin (no una segunda versión del
    /// diseñador).
    ///
    /// <para>Sin el contrato, el host tendría que conocer el tipo del plugin por su nombre; sin la clave en el
    /// catálogo, la superficie no tendría identidad compartida; y sin esta guardia, la vista podría empezar a
    /// reimplementar el diseñador sin que nadie lo note.</para>
    /// </summary>
    [Fact]
    public void TheDataSetDesigner_ShouldBeDeclaredByTheNode_AndServedByTheHost()
    {
        const string Contract = "FileFlow.Sdk/Descriptors/INodeDialogSurfaceProvider.cs";
        const string Node = "FileFlow.Plugin.FileSystem/Nodes/Sources/SyntheticDataSourceNode.cs";
        const string ViewModel = "FileFlow.Plugin.FileSystem/UI/ViewModels/SyntheticDataSetDesignerViewModel.cs";
        const string BodyCode = "FileFlow.App.Uno/Controls/DataSetDesignerBody.xaml.cs";
        const string BodyXaml = "FileFlow.App.Uno/Controls/DataSetDesignerBody.xaml";

        // 1. El contrato (SDK): qué diálogo quiere el nodo y qué contiene, sin tipos de UI.
        string contract = Code(Contract);
        contract.Should().Contain("interface INodeDialogSurfaceProvider");
        contract.Should().Contain("string DialogKey");
        contract.Should().Contain("object? CreateDialogPayload(");

        // 2. El nodo: lo implementa, declara la clave del catálogo y su view model portable.
        string node = Code(Node);
        node.Should().Contain("INodeDialogSurfaceProvider",
            "el nodo de datos sintéticos es el dueño de la superficie: la declara él, no el host");
        node.Should().Contain("DialogKeys.DataSetDesigner",
            "y la identidad del diálogo es la clave del catálogo del SDK, la misma para todos los hosts");
        node.Should().Contain("new UI.ViewModels.SyntheticDataSetDesignerViewModel(",
            "y su carga útil es el view model portable, no una ventana");
        node.Should().Contain("(context as NodeCustomActionContext)?.Dialogs",
            "y viaja con los diálogos de quien abre: sin ellos el contenido cae al doble nulo, que a una "
            + "confirmación contesta «sí» sin preguntar");

        // El view model del plugin no puede depender de un toolkit: si lo hiciera, la «lógica portable» sería
        // una promesa y el host no podría pintarlo.
        string viewModel = Read(ViewModel);
        viewModel.Should().NotContain("using Avalonia", "el view model del diseñador tiene que seguir siendo portable");

        // 3. El servicio del host: sirve la clave con vista propia y declara que espera ESE view model.
        string service = Code(WindowServiceCode);
        service.Should().Contain("(DialogKeys.DataSetDesigner, nameof(DataSetDesignerBody))",
            "la clave tiene que estar entre las servidas, con la vista que la sirve");
        service.Should().Contain("payload is SyntheticDataSetDesignerViewModel designer",
            "y el servicio tiene que comprobar la carga útil que espera, no tragarse cualquier cosa");

        // 4. La vista del host es una vista del view model del PLUGIN: sus órdenes son sus comandos y la vista
        //    no reimplementa ni el almacén ni las reglas del diseñador.
        string body = Code(BodyCode);
        body.Should().Contain("SyntheticDataSetDesignerViewModel",
            "la vista del host pinta el view model del plugin, no una copia del diseñador");
        foreach (string command in new[]
                 {
                     "NewDataSetCommand", "SaveCommand", "DuplicateDataSetCommand", "DeleteDataSetCommand",
                     "ImportCommand", "ExportCommand", "AddFileCommand", "AddFolderCommand",
                     "AddArchiveToTreeCommand", "AddArchiveEntryCommand", "RemoveItemCommand",
                     "ExpandAllTreeCommand", "CollapseAllTreeCommand", "ApplyDslToItemsCommand", "ApplyJsonToItemsCommand",
                 })
        {
            Read(BodyXaml).Should().Contain("Command=\"{Binding " + command + "}\"",
                $"la orden {command} del diseñador la ejecuta su view model, no la vista");
        }

        body.Should().NotContain("new SyntheticDataSetDesignerViewModel(",
            "el view model lo construye quien lo declara (el nodo), no la vista: si lo construyera la vista, "
            + "habría dos diseñadores");
        body.Should().NotContain("SyntheticDataSetStorageService",
            "el almacén de datasets es del plugin: la vista no habla con él");
        body.Should().NotContain("SyntheticTreeDslParser",
            "y el parser del DSL también: la vista no reimplementa el producto");

        // Y la superficie tiene que estar censada en el cajón, que es por donde el usuario la alcanza.
        Code(DrawerCode).Should().Contain("ControlBarDrawerDataSetButton");
        Code(BarCode).Should().Contain("OpenSyntheticDataSetDesignerCommand",
            "la orden de la versión anterior tiene que quedar reconocida en el censo del host (cumplida por su canal)");
    }
}
