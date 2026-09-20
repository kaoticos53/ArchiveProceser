using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using FileFlow.App;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Core.Plugins;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Services;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Material.Icons.Avalonia;
using Xunit;

namespace FileFlow.Tests.Unit.Views;

/// <summary>
/// Guardias de la <b>entrada del cajón</b> del diseñador de conjuntos de datos sintéticos.
///
/// El diseñador existía y funcionaba, pero no se podía alcanzar desde la interfaz de la aplicación: sólo respondía
/// a la acción personalizada del nodo de origen y al botón del renamer avanzado, mientras el cajón no tenía ninguna
/// entrada. Las claves <c>Drawer_DataSetDesigner</c> y <c>Drawer_DataSetDesignerToolTip</c> ya estaban traducidas en
/// ambos idiomas y <c>ControlBarViewModel.OpenSyntheticDataSetDesigner</c> ya era una orden pública — pero nadie las
/// había enlazado, así que ambas eran código y traducciones muertas.
///
/// La cadena que estas pruebas fijan, de la vista al plugin:
/// <list type="number">
///   <item>la entrada <b>existe</b> en el cajón, con etiqueta traducida (ES/EN), sin pictogramas y con icono vectorial;</item>
///   <item>está enlazada a <b>esa</b> orden (<c>ReferenceEquals</c>, no sólo «no es nulo»: un botón con cualquier otro comando pasaría);</item>
///   <item>al ejecutarla, el cajón se cierra y la orden <b>llega al plugin</b> con el identificador correcto y sobre un
///   proveedor que declara esa acción.</item>
/// </list>
/// Lo que ocurre después —que el nodo abra su ventana con un view model conectado— lo cubre
/// <see cref="WindowActivationContractTests"/>, que abre esa ventana por el camino real.
/// </summary>
[Collection(VisualSnapshotsCollection.Name)]
public class DrawerDataSetDesignerEntryTests
{
    private const string Spanish = "es-ES";
    private const string English = "en-US";

    private const string EntryLabelKey = "Drawer_DataSetDesigner";
    private const string EntryTooltipKey = "Drawer_DataSetDesignerToolTip";

    /// <summary>Identificador de la acción que el nodo del plugin declara para abrir el diseñador.</summary>
    private const string DesignerActionId = "OpenDataSetDesigner";

    [Fact]
    public void TheDrawerEntry_ShouldExist_TranslatedInBothLanguages_WithAVectorIcon()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            try
            {
                foreach (string culture in new[] { Spanish, English })
                {
                    AvaloniaTestHelper.SetCultureOnUI(culture);

                    var (window, _) = ShellWithCapturingControlBar();
                    try
                    {
                        var entry = DrawerEntry(window);
                        entry.Should().NotBeNull(
                            "el cajón debe ofrecer la entrada del diseñador para que el usuario pueda abrirlo");

                        string expected = LocalizationManager.Instance[EntryLabelKey];
                        expected.Should().NotBeNullOrWhiteSpace(
                            $"la clave '{EntryLabelKey}' debe existir: una clave ausente deja la etiqueta en blanco");

                        LabelOf(entry!).Text.Should().Be(expected,
                            $"la entrada del cajón debe pintar el texto traducido del idioma activo ({culture})");

                        LabelOf(entry!).Text.Should().NotMatchRegex(@"[^\p{L}\p{N}\p{P}\p{Z}]",
                            "el icono de la entrada es vectorial: la etiqueta no puede llevar pictogramas");

                        entry!.GetLogicalDescendants().OfType<MaterialIcon>().Should().NotBeEmpty(
                            "la entrada del cajón se identifica con un icono vectorial, igual que sus hermanas");

                        ToolTip.GetTip(entry!).Should().Be(LocalizationManager.Instance[EntryTooltipKey],
                            "la ayuda emergente explica qué abre la entrada");
                    }
                    finally
                    {
                        VisualSnapshot.DetachTree(window);
                        window.Close();
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
    public void TheDrawerEntry_ShouldInvokeTheDesignerAction_AndCloseTheDrawer()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var (window, capturingControlBar) = ShellWithCapturingControlBar();

            try
            {
                var entry = DrawerEntry(window);
                entry.Should().NotBeNull();

                entry!.Command.Should().BeSameAs(capturingControlBar.OpenSyntheticDataSetDesignerCommand,
                    "la entrada del cajón tiene que ejecutar la orden que abre el diseñador, no otra cualquiera");

                entry.Command!.CanExecute(entry.CommandParameter).Should().BeTrue();

                capturingControlBar.IsMenuOpen = true;
                entry.Command.Execute(entry.CommandParameter);
                Dispatcher.UIThread.RunJobs();

                capturingControlBar.IsMenuOpen.Should().BeFalse(
                    "al elegir la entrada, el cajón se retira para dejar ver el diseñador");

                capturingControlBar.InvokedActionId.Should().Be(DesignerActionId,
                    "la orden debe pedirle al plugin exactamente la acción que abre el diseñador");

                capturingControlBar.InvokedProvider.Should().NotBeNull(
                    "la orden tiene que encontrar el nodo que declara la acción en el catálogo de tipos del cargador");

                capturingControlBar.InvokedProvider!.GetType().Name.Should().Be("SyntheticDataSourceNode",
                    "la acción del diseñador vive en el nodo de origen de datos sintéticos");

                capturingControlBar.InvokedProvider.CustomActions
                    .Select(action => action.ActionId)
                    .Should().Contain(DesignerActionId,
                        "la acción invocada debe ser una de las que el nodo declara: un identificador inventado no abriría nada");
            }
            finally
            {
                VisualSnapshot.DetachTree(window);
                window.Close();
            }
        });
    }

    #region Utilidades

    /// <summary>Etiqueta visible de la entrada del cajón (su bloque de texto).</summary>
    private static TextBlock LabelOf(Button entry) =>
        entry.GetLogicalDescendants().OfType<TextBlock>().Single();

    /// <summary>Entrada del cajón del diseñador, localizada por su nombre en el XAML.</summary>
    private static Button? DrawerEntry(Window window) =>
        window.FindControl<Button>("DrawerDataSetDesignerButton");

    /// <summary>
    /// Ventana principal real (el XAML del cajón tal cual lo ve la aplicación) sobre los view models reales, con
    /// dobles en los puertos que tocan el entorno (preferencias, almacén de flujos, diálogos) y el cargador de
    /// plugins que configura la aplicación, del que sale el tipo del nodo.
    /// </summary>
    private static (Window Window, CapturingControlBar ControlBar) ShellWithCapturingControlBar()
    {
        AvaloniaTestHelper.RequireUIThread(nameof(ShellWithCapturingControlBar));

        var preferences = new InMemoryUserPreferencesService();
        var monitor = new FrozenPerformanceMonitor();
        var logs = new LogViewModel(new InMemoryLogStore());
        var pluginLoader = PluginRegistryHelper.CreateConfiguredLoader();
        var fileDialog = new NullFileDialogService();
        var storage = new InMemoryWorkflowStorageService();

        var editor = new EditorViewModel(pluginLoader, userPreferencesService: preferences);
        var toolbox = new ToolboxViewModel(pluginLoader, preferences);
        var inspector = new NodeInspectorViewModel(editor, fileDialog, logs);

        var controlBar = new CapturingControlBar(
            editor, pluginLoader, logs, inspector, fileDialog, storage, preferences, NullDialogService.Instance);

        var statusBar = new StatusBarViewModel(editor, controlBar, monitor, logs);

        var shell = new MainViewModel(
            pluginLoader, editor, toolbox, inspector, controlBar, logs, statusBar, monitor, fileDialog, storage);

        var window = new MainWindow(shell);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, controlBar);
    }

    /// <summary>Barra de control que anota qué acción se le pide al plugin, en lugar de abrir la ventana.</summary>
    private sealed class CapturingControlBar(
        EditorViewModel editor,
        PluginLoader pluginLoader,
        LogViewModel logs,
        NodeInspectorViewModel inspector,
        IFileDialogService fileDialog,
        IWorkflowStorageService storage,
        IUserPreferencesService preferences,
        IDialogService dialogService)
        : ControlBarViewModel(
            editorViewModel: editor,
            pluginLoader: pluginLoader,
            logViewModel: logs,
            nodeInspectorViewModel: inspector,
            fileDialogService: fileDialog,
            workflowStorageService: storage,
            userPreferencesService: preferences,
            themeService: null,
            localizationService: null,
            dialogService: dialogService)
    {
        public string? InvokedActionId { get; private set; }

        public IFlowNode? InvokedProvider { get; private set; }

        protected override void OpenDataSetDesigner(INodeCustomActionProvider provider)
        {
            InvokedProvider = provider as IFlowNode;
            InvokedActionId = InvokedProvider?.CustomActions
                .Select(action => action.ActionId)
                .FirstOrDefault(id => id.Equals("OpenDataSetDesigner", StringComparison.OrdinalIgnoreCase));
        }
    }

    #endregion
}
