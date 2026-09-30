using System;
using System.ComponentModel;
using FileFlow.App.ViewModels;
using FileFlow.Sdk.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace FileFlow.App.Uno.Controls;

/// <summary>
/// El cajón del menú principal del host Uno: el mismo cajón de la versión anterior, con su estado en el MISMO
/// <see cref="ControlBarViewModel"/> portable (<c>IsMenuOpen</c>, el que conmuta el botón «Menú» de la
/// barra y el botón de cerrar de aquí).
///
/// <para><b>Sus dos selectores son los del núcleo</b>: el tema y el idioma del <c>ControlBar</c> aplican
/// y guardan al elegir (a diferencia de la superficie de ajustes, que aplica al guardar: son las dos
/// semánticas de la versión anterior, y aquí se conservan tal cual). El punto de entrada a los AJUSTES abre la
/// superficie del host, y el del Inspector conmuta el panel del editor.</para>
/// </summary>
public sealed partial class MainMenuDrawer : UserControl
{
    private ControlBarViewModel? _vm;

    /// <summary>La orden de abrir la superficie de AJUSTES; la ventana la conecta con su panel.</summary>
    public event RoutedEventHandler? SettingsRequested;

    /// <summary>
    /// Las tres órdenes de FLUJO que el host cumple por su propio canal (hito 258).
    ///
    /// <para>No se ejecutan aquí porque necesitan la ventana: el comando del núcleo pide un diálogo
    /// SÍNCRONO de fichero o de confirmación —el contrato que, desde el hilo de UI, este host no puede
    /// cumplir y que devuelve nulo/falso—, así que la ventana usa sus diálogos ASÍNCRONOS y llama después
    /// a los métodos del view model portable que ya no dependen del diálogo. El cajón sólo declara QUÉ
    /// orden se ha pedido, igual que la entrada de ajustes.</para>
    /// </summary>
    public event RoutedEventHandler? NewWorkflowRequested;

    /// <inheritdoc cref="NewWorkflowRequested"/>
    public event RoutedEventHandler? LoadWorkflowRequested;

    /// <inheritdoc cref="NewWorkflowRequested"/>
    public event RoutedEventHandler? SaveWorkflowRequested;

    /// <summary>
    /// La orden de abrir el DISEÑADOR DE DATASETS (hito 261).
    ///
    /// <para>No ejecuta aquí el comando canónico del núcleo —<c>OpenSyntheticDataSetDesignerCommand</c>— porque
    /// ese camino abre la ventana que construye el PROPIO plugin con el toolkit de la versión anterior: en este host no
    /// se puede montar. El cajón declara qué se ha pedido y la ventana sirve la superficie del catálogo de
    /// diálogos sobre el view model portable que el nodo declara al SDK.</para>
    /// </summary>
    public event RoutedEventHandler? DataSetDesignerRequested;

    public MainMenuDrawer()
    {
        InitializeComponent();
        RefreshLocalization();
    }

    /// <summary>El view model de la barra (el cajón es su menú): el <c>ControlBar</c> del núcleo.</summary>
    public ControlBarViewModel? Vm
    {
        get => _vm;
        set
        {
            if (ReferenceEquals(_vm, value))
            {
                return;
            }

            if (_vm is not null)
            {
                _vm.PropertyChanged -= OnVmPropertyChanged;
            }

            _vm = value;
            DataContext = _vm;

            if (_vm is not null)
            {
                _vm.PropertyChanged += OnVmPropertyChanged;
            }

            SyncVisibility();
            RefreshLocalization();
        }
    }

    /// <summary>La versión del producto que enseña el pie (la misma cadena que el cajón de la versión anterior).</summary>
    public string VersionText
    {
        get => DrawerVersion.Text;
        set => DrawerVersion.Text = value;
    }

    /// <summary>¿Está el cajón desplegado? (lo que la sonda y los gestos leen).</summary>
    public bool IsOpen => OverlayRoot.Visibility == Visibility.Visible;

    /// <summary>Despliega el cajón: el mismo estado que conmuta el botón «Menú».</summary>
    public void Open()
    {
        if (_vm is not null)
        {
            _vm.ToggleMenuCommand.Execute(null);   // IsMenuOpen ← true, notifique donde notifique
        }

        SyncVisibility();
    }

    /// <summary>Recoge el cajón.</summary>
    public void Close()
    {
        if (_vm is not null)
        {
            _vm.ToggleMenuCommand.Execute(null);
        }

        SyncVisibility();
    }

    /// <summary>El idioma vigente en los textos del cajón (claves del diccionario del host).</summary>
    public void RefreshLocalization()
    {
        var loc = LocalizationManager.Instance;

        DrawerTitle.Text = loc.GetString("Uno_ControlBar_Brand", "FileFlow Studio");
        DrawerSubtitle.Text = loc.GetString("Uno_Drawer_Subtitle", "Gestor de Flujos v1.0");
        FlowSectionLabel.Text = loc.GetString("Uno_Drawer_FlowManagement", "GESTIÓN DE FLUJOS");
        DrawerNewLabel.Text = loc.GetString("Uno_Drawer_NewWorkflow", "Nuevo Flujo");
        DrawerLoadLabel.Text = loc.GetString("Uno_Drawer_LoadWorkflow", "Cargar Flujo...");
        DrawerSaveLabel.Text = loc.GetString("Uno_Drawer_SaveWorkflow", "Guardar Flujo...");
        AppearanceSectionLabel.Text = loc.GetString("Uno_Drawer_AppearanceLanguage", "APARIENCIA E IDIOMA");
        PanelsSectionLabel.Text = loc.GetString("Uno_Drawer_PanelsTools", "PANELES Y HERRAMIENTAS");
        HelpSectionLabel.Text = loc.GetString("Uno_Drawer_HelpResources", "AYUDA Y RECURSOS");
        DrawerManualLabel.Text = loc.GetString("Uno_Drawer_UserManual", "Manual de Usuario");
        DrawerExamplesLabel.Text = loc.GetString("Uno_Drawer_ExampleFlows", "Ejemplos de Flujos");
        DrawerAboutLabel.Text = loc.GetString("Uno_Drawer_About", "Acerca de FileFlow Studio");
        ThemeLabel.Text = loc.GetString("Uno_Drawer_ThemeLabel", "Tema Visual:");
        LanguageLabel.Text = loc.GetString("Uno_Drawer_LanguageLabel", "Idioma:");
        DrawerSettingsLabel.Text = loc.GetString("Uno_Drawer_Settings", "Ajustes");
        DrawerInspectorLabel.Text = loc.GetString("Uno_ControlBar_Inspector", "🔍 Inspector");
        DrawerThemeStudioLabel.Text = loc.GetString("Drawer_CustomizeTheme", "Estudio de Temas");
        DrawerMetricsLabel.Text = loc.GetString("Drawer_MetricsDashboard", "Métricas y Rendimiento");
        DrawerVfsLabel.Text = loc.GetString("VfsExplorer_HeaderTitle", "Explorador de Archivos Virtual");
        DrawerDataSetLabel.Text = loc.GetString("Drawer_DataSetDesigner", "Diseñador de Datasets");

        ToolTipService.SetToolTip(DrawerCloseButton,
            loc.GetString("Uno_Drawer_CloseToolTip", "Cerrar menú"));
        ToolTipService.SetToolTip(DrawerSettingsButton, loc.GetString("Uno_ControlBar_SettingsToolTip", "Ajustes"));
        ToolTipService.SetToolTip(DrawerInspectorButton, loc.GetString("Uno_ControlBar_InspectorToolTip",
            "Abrir / Ocultar Inspector de Datos del Nodo"));
        ToolTipService.SetToolTip(DrawerManualButton,
            loc.GetString("Uno_Drawer_UserManualToolTip", "Abrir el Manual de Usuario detallado (PDF)"));
        ToolTipService.SetToolTip(DrawerExamplesButton,
            loc.GetString("Uno_Drawer_ExampleFlowsToolTip", "Explorar la carpeta con los flujos de ejemplo incluidos"));
        ToolTipService.SetToolTip(DrawerAboutButton,
            loc.GetString("Uno_Drawer_AboutToolTip", "Ver información de la aplicación, versión, autoría y repositorio"));
        ToolTipService.SetToolTip(DrawerThemeStudioButton,
            loc.GetString("Drawer_CustomizeThemeToolTip", "Abrir el Estudio de Personalización de Temas"));
        ToolTipService.SetToolTip(DrawerMetricsButton,
            loc.GetString("Drawer_MetricsDashboardToolTip", "Abrir el panel de métricas y rendimiento del flujo"));
        ToolTipService.SetToolTip(DrawerVfsButton,
            loc.GetString("Drawer_VfsExplorerToolTip", "Ver el almacén virtual de la última ejecución"));
        ToolTipService.SetToolTip(DrawerDataSetButton,
            loc.GetString("Drawer_DataSetDesignerToolTip", "Abrir el Diseñador Visual de Datasets Sintéticos"));

        // Los desplegables del cajón no son catálogo de la vista: la lista de temas la mantiene el núcleo
        // (ControlBar la recarga al abrir el estudio de temas) y aquí sólo se pide que esté poblada.
        _vm?.LoadAvailableThemes();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ControlBarViewModel.IsMenuOpen))
        {
            SyncVisibility();
        }
    }

    /// <summary>
    /// El cajón se despliega o se recoge siguiendo el estado del view model —una sola fuente, la misma que
    /// el botón «Menú»— en vez de que la vista decida por su cuenta cuándo está abierto.
    /// </summary>
    private void SyncVisibility() =>
        OverlayRoot.Visibility = _vm?.IsMenuOpen == true ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Las entradas del cajón con su AutomationId, para la sonda (las mismas que ve el canal externo).</summary>
    internal (string Id, FrameworkElement Element)[] Entries() =>
    [
        ("ControlBarDrawerNewButton", DrawerNewButton),
        ("ControlBarDrawerLoadButton", DrawerLoadButton),
        ("ControlBarDrawerSaveButton", DrawerSaveButton),
        ("ControlBarThemeCombo", ThemeCombo),
        ("ControlBarLanguageCombo", LanguageCombo),
        ("ControlBarDrawerSettingsButton", DrawerSettingsButton),
        ("ControlBarDrawerThemeStudioButton", DrawerThemeStudioButton),
        ("ControlBarDrawerInspectorButton", DrawerInspectorButton),
        ("ControlBarDrawerMetricsButton", DrawerMetricsButton),
        ("ControlBarDrawerVfsButton", DrawerVfsButton),
        ("ControlBarDrawerDataSetButton", DrawerDataSetButton),
        ("ControlBarDrawerManualButton", DrawerManualButton),
        ("ControlBarDrawerExamplesButton", DrawerExamplesButton),
        ("ControlBarDrawerAboutButton", DrawerAboutButton),
        ("ControlBarDrawerCloseButton", DrawerCloseButton),
    ];

    /// <summary>El selector de tema del cajón (su catálogo lo mantiene el view model del núcleo).</summary>
    internal ComboBox ThemeSelector => ThemeCombo;

    /// <summary>El selector de idioma del cajón.</summary>
    internal ComboBox LanguageSelector => LanguageCombo;

    private void OnScrimTapped(object sender, TappedRoutedEventArgs e) => Close();

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();

    private void OnSettingsClicked(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(sender, e);

    private void OnInspectorClicked(object sender, RoutedEventArgs e) =>
        _vm?.ToggleInspectorCommand.Execute(null);

    // ── Las órdenes de flujo: el cajón declara lo pedido y la ventana lo cumple por su canal asíncrono ──

    private void OnNewWorkflowClicked(object sender, RoutedEventArgs e)
    {
        Close();
        NewWorkflowRequested?.Invoke(this, e);
    }

    private void OnLoadWorkflowClicked(object sender, RoutedEventArgs e)
    {
        Close();
        LoadWorkflowRequested?.Invoke(this, e);
    }

    private void OnSaveWorkflowClicked(object sender, RoutedEventArgs e)
    {
        Close();
        SaveWorkflowRequested?.Invoke(this, e);
    }

    // ── Las de ayuda: las órdenes CANÓNICAS del núcleo (el cajón no reimplementa nada) ──

    private void OnUserManualClicked(object sender, RoutedEventArgs e) =>
        _vm?.OpenUserManualCommand.Execute(null);

    private void OnExamplesClicked(object sender, RoutedEventArgs e) =>
        _vm?.OpenExamplesFolderCommand.Execute(null);

    private void OnAboutClicked(object sender, RoutedEventArgs e) =>
        _vm?.OpenAboutDialogCommand.Execute(null);

    // ── Las tres entradas de VENTANA (hito 259): las órdenes CANÓNICAS del núcleo, que piden su ventana al
    //    catálogo de diálogos del host. El cajón no reimplementa nada: ejecuta el comando y se recoge. ──

    private void OnThemeStudioClicked(object sender, RoutedEventArgs e)
    {
        Close();
        _vm?.OpenThemeCustomizerCommand.Execute(null);
    }

    private void OnMetricsClicked(object sender, RoutedEventArgs e)
    {
        Close();
        _vm?.OpenMetricsDashboardCommand.Execute(null);
    }

    private void OnVfsClicked(object sender, RoutedEventArgs e)
    {
        Close();
        _vm?.OpenVirtualFileSystemExplorerCommand.Execute(null);
    }

    /// <summary>
    /// El DISEÑADOR DE DATASETS: la superficie la declara el NODO (no el cajón), así que lo único que se hace
    /// aquí es declarar la intención; la ventana resuelve el nodo y sirve su superficie por el catálogo.
    /// </summary>
    private void OnDataSetDesignerClicked(object sender, RoutedEventArgs e)
    {
        Close();
        DataSetDesignerRequested?.Invoke(this, e);
    }

    /// <summary>
    /// PULSA una entrada del cajón como la pulsa el usuario: por el peer de automatización del control
    /// (Invoke del botón), el mismo canal que la sonda del inspector usa para lo suyo.
    /// </summary>
    internal bool Press(string id)
    {
        foreach (var (entryId, element) in Entries())
        {
            if (!string.Equals(entryId, id, StringComparison.Ordinal) || element is not Control control)
            {
                continue;
            }

            return ControlBar.Press(control);
        }

        return false;
    }
}
