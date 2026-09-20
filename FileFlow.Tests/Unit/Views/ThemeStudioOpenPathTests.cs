using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.App.Views.Components;
using FileFlow.Sdk.Services;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Views;

/// <summary>
/// Guardias del <b>camino de apertura</b> del Theme Studio: la ventana se abre desde la barra de control y
/// tiene que llegar con su view model conectado.
///
/// El defecto que esto impide volver a introducir: el estudio se abría con
/// <c>new ThemeCustomizerWindow()</c> y <b>sin</b> <c>DataContext</c>, de modo que ningún <c>{Binding}</c>
/// resolvía contra nada y la ventana salía inerte —catálogo de temas vacío, editor por secciones sin generar
/// y ningún botón de nuevo/duplicar/eliminar/aplicar con comando—. El síntoma del usuario era «no muestra los
/// temas predefinidos ni puedo crear nuevos ni editarlos ni hacer nada».
///
/// La razón de que el suite no lo detectara: <b>todas</b> las pruebas existentes del estudio construyen la
/// ventana inyectándole el view model a mano (<c>{ DataContext = new ThemeCustomizerViewModel(...) }</c>),
/// que es justo lo que la aplicación no hacía. Estas pruebas ejercitan el camino real, incluida la orden de
/// apertura de la barra de control.
///
/// Vive en la colección exclusiva <c>VisualSnapshots</c> porque aplica el tema del proceso
/// (<c>ThemeManager</c>) al comprobar que el menú sigue al tema aplicado.
/// </summary>
[Collection(VisualSnapshotsCollection.Name)]
public class ThemeStudioOpenPathTests
{
    /// <summary>Botones con comando declarados hoy en <c>ThemeCustomizerWindow.axaml</c>.</summary>
    private const int StudioCommandButtons = 6;

    #region La ventana se abre con contenido, con o sin view model inyectado

    [Fact]
    public void TheStudioWindow_ShouldOpenWithAWorkingViewModel_EvenWhenNobodyInjectsOne()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            // Exactamente lo que hacía la aplicación: abrir la ventana sin darle nada.
            var window = new ThemeCustomizerWindow();
            window.Show();

            try
            {
                window.DataContext.Should().BeOfType<ThemeCustomizerViewModel>(
                    "el estudio no puede depender de que quien lo abre se acuerde de conectarle su view model: " +
                    "sin DataContext no hay catálogo, ni editor, ni botones");

                var viewModel = (ThemeCustomizerViewModel)window.DataContext!;

                viewModel.AvailableThemes.Should().NotBeEmpty(
                    "el catálogo de temas predefinidos es lo primero que debe verse al abrir el estudio");

                viewModel.Sections.Should().NotBeEmpty(
                    "el editor por secciones se genera del catálogo de ajustes al cargar un tema");
                viewModel.Sections.SelectMany(section => section.Rows).Should().NotBeEmpty(
                    "cada sección del editor debe traer sus filas de ajuste");

                // La prueba de verdad de que los enlaces resuelven: un botón dibujado por el XAML con
                // Command="{Binding ...}" sin DataContext sale con Command == null y no hace nada al pulsarlo.
                var commandButtons = window.GetLogicalDescendants()
                    .OfType<Button>()
                    .Count(button => button.Command != null);

                commandButtons.Should().BeGreaterThanOrEqualTo(StudioCommandButtons,
                    "los botones de nuevo, duplicar, eliminar, importar, exportar y aplicar resuelven sus " +
                    "comandos contra el view model; sin él ningún botón hace nada");
            }
            finally
            {
                VisualSnapshot.DetachTree(window);
                window.Close();
            }
        });
    }

    [Fact]
    public void TheStudioWindow_ShouldRespectAnInjectedViewModel()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            string storage = TemporaryStorage();
            var injected = new ThemeCustomizerViewModel(new CustomThemeService(storage));

            var window = new ThemeCustomizerWindow { DataContext = injected };
            window.Show();

            try
            {
                window.DataContext.Should().BeSameAs(injected,
                    "el ViewModel por defecto es una red de seguridad: si quien abre trae el suyo, manda el suyo");
            }
            finally
            {
                VisualSnapshot.DetachTree(window);
                window.Close();
                File.Delete(storage);
            }
        });
    }

    [Fact]
    public void TheStudioThemeCatalog_ShouldBeListedByTheView()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var window = new ThemeCustomizerWindow();
            window.Show();

            try
            {
                var viewModel = (ThemeCustomizerViewModel)window.DataContext!;

                var catalog = window.GetLogicalDescendants()
                    .OfType<ListBox>()
                    .Distinct()
                    .SingleOrDefault(list => ReferenceEquals(list.ItemsSource, viewModel.AvailableThemes));

                catalog.Should().NotBeNull(
                    "la lista de temas de la ventana debe estar enlazada al catálogo del view model");

                viewModel.AvailableThemes.Should().Contain(theme => theme.Id == ThemeManager.DefaultThemeId,
                    "los temas predefinidos son los que el usuario espera ver en el estudio");
            }
            finally
            {
                VisualSnapshot.DetachTree(window);
                window.Close();
            }
        });
    }

    #endregion

    #region Apertura desde la barra de control y sincronización del menú

    [Fact]
    public void OpeningTheStudioFromTheControlBar_ShouldDeliverAReadyStudio()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            string storage = TemporaryStorage();

            try
            {
                var controlBar = ControlBarOn(storage);
                try
                {
                    controlBar.OpenThemeCustomizerCommand.Execute(null);

                    var studio = controlBar.OpenedStudio;
                    studio.Should().NotBeNull("la orden del menú debe abrir el estudio");

                    studio!.DataContext.Should().BeOfType<ThemeCustomizerViewModel>(
                        "la barra de control entrega el estudio ya conectado a su view model");

                    ((ThemeCustomizerViewModel)studio.DataContext!).AvailableThemes.Should().NotBeEmpty();
                }
                finally
                {
                    controlBar.Dispose();
                }
            }
            finally
            {
                File.Delete(storage);
            }
        });
    }

    [Fact]
    public void ClosingTheStudio_ShouldBringTheMenuUpToDate()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            string storage = TemporaryStorage();
            string? previousThemeId = ThemeManager.Instance.CurrentThemeId;

            try
            {
                var controlBar = ControlBarOn(storage);
                try
                {
                    controlBar.OpenThemeCustomizerCommand.Execute(null);

                    var studio = controlBar.OpenedStudio!;
                    var studioViewModel = (ThemeCustomizerViewModel)studio.DataContext!;

                    // El flujo real del usuario: crear un tema propio dentro del estudio y aplicarlo.
                    studioViewModel.NewCustomThemeCommand.Execute(null);
                    var created = studioViewModel.SelectedTheme!;
                    created.Id.Should().NotBeNullOrWhiteSpace();
                    created.IsBuiltIn.Should().BeFalse();

                    controlBar.AvailableThemes.Should().NotContain(theme => theme.Id == created.Id,
                        "mientras el estudio está abierto el menú aún no conoce el tema recién creado");

                    ThemeManager.Instance.SetTheme(created);

                    studio.Close();

                    controlBar.AvailableThemes.Should().Contain(theme => theme.Id == created.Id,
                        "al cerrar el estudio el menú debe listar el tema que se acaba de crear");
                    controlBar.SelectedTheme.Should().Be(created.Id,
                        "el selector del menú tiene que mostrar el tema que quedó aplicado, no el anterior");
                }
                finally
                {
                    controlBar.Dispose();
                }
            }
            finally
            {
                // El tema activo es estado global del proceso: se devuelve como estaba.
                if (previousThemeId != null)
                {
                    ThemeManager.Instance.SetThemeById(previousThemeId);
                }

                File.Delete(storage);
            }
        });
    }

    [Fact]
    public void ClosingTheStudio_AfterDeletingTheAppliedTheme_ShouldNotLeaveTheMenuBlank()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            string storage = TemporaryStorage();
            string? previousThemeId = ThemeManager.Instance.CurrentThemeId;

            try
            {
                var controlBar = ControlBarOn(storage);
                try
                {
                    controlBar.OpenThemeCustomizerCommand.Execute(null);

                    var studio = controlBar.OpenedStudio!;
                    var studioViewModel = (ThemeCustomizerViewModel)studio.DataContext!;

                    // Crear un tema propio, aplicarlo y borrarlo acto seguido: el tema que estaba en uso
                    // desaparece del catálogo, así que el selector no puede quedarse con un valor inexistente
                    // (sería un campo en blanco).
                    studioViewModel.NewCustomThemeCommand.Execute(null);
                    var created = studioViewModel.SelectedTheme!;
                    ThemeManager.Instance.SetTheme(created);

                    studioViewModel.DeleteThemeCommand.Execute(null);
                    studioViewModel.AvailableThemes.Should().NotContain(theme => theme.Id == created.Id,
                        "el tema borrado desaparece del catálogo del estudio");

                    studio.Close();

                    controlBar.AvailableThemes.Should().Contain(theme => theme.Id == controlBar.SelectedTheme,
                        "el selector del menú tiene que quedar mostrando un tema que exista");
                    ThemeManager.Instance.CurrentThemeId.Should().Be(controlBar.SelectedTheme,
                        "el tema válido elegido se reaplica: el menú y lo que se ve en la interfaz deben coincidir");
                }
                finally
                {
                    controlBar.Dispose();
                }
            }
            finally
            {
                if (previousThemeId != null)
                {
                    ThemeManager.Instance.SetThemeById(previousThemeId);
                }

                File.Delete(storage);
            }
        });
    }

    #endregion

    #region Utilidades

    private static string TemporaryStorage() =>
        Path.Combine(Path.GetTempPath(), $"studio_open_{Guid.NewGuid():N}.json");

    /// <summary>
    /// Barra de control real, con el catálogo de temas aislado en un fichero temporal: crear o aplicar un tema
    /// durante la prueba no puede tocar la colección de temas del usuario.
    /// </summary>
    private static StudioCapturingControlBar ControlBarOn(string themeStorage)
    {
        var preferences = new InMemoryUserPreferencesService();
        var logs = new LogViewModel(new InMemoryLogStore());
        var pluginLoader = PluginRegistryHelper.CreateConfiguredLoader();
        var fileDialog = new NullFileDialogService();

        var editor = new EditorViewModel(pluginLoader, userPreferencesService: preferences);
        var inspector = new NodeInspectorViewModel(editor, fileDialog, logs);

        return new StudioCapturingControlBar(
            editor,
            pluginLoader,
            logs,
            inspector,
            fileDialog,
            new InMemoryWorkflowStorageService(),
            preferences,
            themeService: ThemeManager.Instance,
            dialogService: NullDialogService.Instance,
            customThemeService: new CustomThemeService(themeStorage));
    }

    /// <summary>Barra de control que conserva la referencia del estudio que abre, para poder inspeccionarlo.</summary>
    private sealed class StudioCapturingControlBar(
        EditorViewModel editor,
        FileFlow.Core.Plugins.PluginLoader pluginLoader,
        LogViewModel logs,
        NodeInspectorViewModel inspector,
        IFileDialogService fileDialog,
        IWorkflowStorageService storage,
        IUserPreferencesService preferences,
        IThemeService themeService,
        IDialogService dialogService,
        CustomThemeService customThemeService)
        : ControlBarViewModel(
            editorViewModel: editor,
            pluginLoader: pluginLoader,
            logViewModel: logs,
            nodeInspectorViewModel: inspector,
            fileDialogService: fileDialog,
            workflowStorageService: storage,
            userPreferencesService: preferences,
            themeService: themeService,
            localizationService: null,
            dialogService: dialogService,
            processLauncher: null,
            customThemeService: customThemeService)
    {
        public ThemeCustomizerWindow? OpenedStudio { get; private set; }

        protected override ThemeCustomizerWindow CreateThemeStudio()
        {
            OpenedStudio = base.CreateThemeStudio();
            return OpenedStudio;
        }
    }

    #endregion
}
