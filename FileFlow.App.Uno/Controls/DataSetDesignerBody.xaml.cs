using System;
using FileFlow.Plugin.FileSystem.UI.ViewModels;
using FileFlow.Sdk.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileFlow.App.Uno.Controls;

/// <summary>
/// El DISEÑADOR DE DATASETS SINTÉTICOS del host Uno: la vista del
/// <see cref="SyntheticDataSetDesignerViewModel"/> portable del plugin de sistema de archivos.
///
/// <para><b>Qué es y qué no.</b> El view model —con su almacén de datasets, su parser del DSL, su árbol y sus
/// cuarenta órdenes— vive en el plugin y lo comparten los dos hosts; lo que cambia es quién lo pinta. El
/// escritorio monta la ventana del plugin (<c>SyntheticDataSetDesignerWindow</c>, Avalonia); este host pinta
/// esta vista sobre el MISMO view model. No hay una segunda versión del diseñador: hay dos vistas de él. La
/// frontera que el tramo anterior declaró («la ventana la monta el plugin, y este host es WinUI») se cruza por
/// el contrato <c>INodeDialogSurfaceProvider</c> del SDK: el nodo declara qué diálogo quiere
/// (<c>DialogKeys.DataSetDesigner</c>) y qué contiene.</para>
///
/// <para><b>Lo que la vista sí decide</b>, porque es del host y no del producto: la pestaña visible (árbol /
/// DSL / JSON, que el view model guarda en <c>SelectedTabIndex</c>) y el cierre del modal, que se pide a
/// <see cref="Platform.UnoWindowService"/> —la superficie vive dentro de su diálogo y no es dueña de él—.</para>
/// </summary>
public sealed partial class DataSetDesignerBody : UserControl
{
    private readonly SyntheticDataSetDesignerViewModel _vm;

    /// <summary>Guarda de reentrada del viaje de ida y vuelta de la selección del árbol.</summary>
    private bool _syncingSelection;

    public DataSetDesignerBody(SyntheticDataSetDesignerViewModel viewModel)
    {
        _vm = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        InitializeComponent();
        DataContext = _vm;

        _vm.PropertyChanged += OnVmPropertyChanged;
        Unloaded += (_, _) => _vm.PropertyChanged -= OnVmPropertyChanged;
        Loaded += (_, _) => SyncTreeSelection();

        RefreshLocalization();
        ApplyTab(_vm.SelectedTabIndex);
    }

    /// <summary>El view model portable en uso (la sonda cuenta datasets, nodos y métricas desde aquí).</summary>
    internal SyntheticDataSetDesignerViewModel Vm => _vm;

    /// <summary>La pestaña visible AHORA (0 = árbol, 1 = DSL, 2 = JSON): lo que la sonda conmuta y mide.</summary>
    internal int VisibleTab { get; private set; }

    /// <summary>¿Se ve el cuerpo del árbol? (la conmutación es de visibilidad, no de materialización).</summary>
    internal bool TreePaneVisible => TreePane.Visibility == Visibility.Visible;

    /// <summary>¿Se ve el cuerpo del DSL?</summary>
    internal bool DslPaneVisible => DslPane.Visibility == Visibility.Visible;

    /// <summary>¿Se ve el cuerpo del JSON?</summary>
    internal bool JsonPaneVisible => JsonPane.Visibility == Visibility.Visible;

    /// <summary>La lista de datasets del catálogo (lo que el canal externo lee como filas).</summary>
    internal ListView DataSets => DataSetList;

    /// <summary>El árbol editable del dataset elegido.</summary>
    internal TreeView Tree => DataSetTree;

    /// <summary>Conmuta la pestaña del diseñador (el mismo camino que el botón: el estado es del view model).</summary>
    internal void ShowTab(string tab)
    {
        int index = tab switch
        {
            "dsl" => 1,
            "json" => 2,
            _ => 0,
        };

        _vm.SelectedTabIndex = index;
        ApplyTab(index);
    }

    private void ApplyTab(int index)
    {
        VisibleTab = index;
        TreePane.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        DslPane.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        JsonPane.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;

        TabTreeButton.IsChecked = index == 0;
        TabDslButton.IsChecked = index == 1;
        TabJsonButton.IsChecked = index == 2;
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // La pestaña es estado del view model (SelectedTabIndex) y la vista lo sigue: si otra orden la cambia
        // —al aplicar el DSL, por ejemplo—, aquí se refleja sin que nadie la duplique.
        if (e.PropertyName == nameof(SyntheticDataSetDesignerViewModel.SelectedTabIndex))
        {
            ApplyTab(_vm.SelectedTabIndex);
        }

        // La selección del árbol es del view model (la comparten el inspector y sus órdenes) y la vista la
        // sigue desde aquí, no por un enlace en dos sentidos: lo que devuelve el control nativo es un dato
        // suyo, y ese viaje de vuelta revienta el marshalling (ver el comentario del XAML).
        if (e.PropertyName == nameof(SyntheticDataSetDesignerViewModel.SelectedTreeNode))
        {
            SyncTreeSelection();
        }
    }

    /// <summary>
    /// Lleva al árbol la selección del view model. Se protege con <see cref="_syncingSelection"/> porque el
    /// control avisa de su cambio de selección cuando se le escribe: sin la guarda, los dos avisos se
    /// turnarían.
    /// </summary>
    private void SyncTreeSelection()
    {
        if (_syncingSelection)
        {
            return;
        }

        var selected = _vm.SelectedTreeNode;
        if (ReferenceEquals(DataSetTree.SelectedItem, selected))
        {
            return;
        }

        _syncingSelection = true;
        try
        {
            DataSetTree.SelectedItem = selected;
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    /// <summary>
    /// La selección del árbol, traducida al view model. <c>SelectedItem</c> puede llegar como el dato del
    /// view model o envuelto en el nodo del control: se desenvuelve lo que llegue en vez de dar por hecho
    /// uno de los dos, que es justo lo que rompía el enlace directo.
    /// </summary>
    private void OnTreeSelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args)
    {
        if (_syncingSelection)
        {
            return;
        }

        var item = sender.SelectedItem as SyntheticTreeNodeItem
            ?? (sender.SelectedNode?.Content as SyntheticTreeNodeItem);

        if (item is not null && !ReferenceEquals(_vm.SelectedTreeNode, item))
        {
            _syncingSelection = true;
            try
            {
                _vm.SelectedTreeNode = item;
            }
            finally
            {
                _syncingSelection = false;
            }
        }
    }

    private void OnTabClicked(object sender, RoutedEventArgs e) =>
        ShowTab((sender as FrameworkElement)?.Tag as string ?? "tree");

    /// <summary>
    /// Cierra la superficie. El modal lo posee el servicio de ventanas —esta vista vive dentro de él—, así que
    /// el pie le pide que lo retire, igual que los pies de las demás ventanas del host.
    /// </summary>
    private void OnCloseClicked(object sender, RoutedEventArgs e) => Platform.UnoWindowService.CloseActiveWindow();

    /// <summary>Los rótulos del host (se reescriben en caliente al cambiar de idioma).</summary>
    public void RefreshLocalization()
    {
        var loc = LocalizationManager.Instance;

        HeaderTitle.Text = loc.GetString("DataSetDesigner_WindowTitle", "Diseñador de Conjuntos de Datos Sintéticos");
        HeaderDesc.Text = loc.GetString("DataSetDesigner_HeaderDesc", "");
        CatalogTitle.Text = loc.GetString("DataSetDesigner_CatalogTitle", "Catálogo de Datasets");
        DataSetSearchBox.PlaceholderText = loc.GetString("DataSetDesigner_SearchPlaceholder", "Buscar dataset…");
        DataSetEmpty.Text = loc.GetString("DataSetDesigner_NoDatasets", "No hay datasets que coincidan.");
        PropertiesTitle.Text = loc.GetString("DataSetDesigner_PropertiesTitle", "Propiedades");
        NameLabel.Text = loc.GetString("DataSetDesigner_NameLabel", "Nombre");
        CategoryLabel.Text = loc.GetString("DataSetDesigner_CategoryLabel", "Categoría");
        DescriptionLabel.Text = loc.GetString("DataSetDesigner_DescriptionLabel", "Descripción");
        MetricsFilesLabel.Text = loc.GetString("DataSetDesigner_MetricsFiles", "Archivos");
        MetricsFoldersLabel.Text = loc.GetString("DataSetDesigner_MetricsFolders", "Carpetas");
        MetricsSizeLabel.Text = loc.GetString("DataSetDesigner_MetricsSize", "Tamaño");

        NewLabel.Text = loc.GetString("DataSetDesigner_NewButton", "Nuevo");
        SaveLabel.Text = loc.GetString("DataSetDesigner_SaveButton", "Guardar");
        DuplicateLabel.Text = loc.GetString("DataSetDesigner_DuplicateButton", "Duplicar");
        DeleteLabel.Text = loc.GetString("DataSetDesigner_DeleteToolTip", "Eliminar");
        ImportLabel.Text = loc.GetString("DataSetDesigner_ImportButton", "Importar");
        ExportLabel.Text = loc.GetString("DataSetDesigner_ExportButton", "Exportar");

        TabTreeLabel.Text = loc.GetString("DataSetDesigner_TabTree", "Árbol jerárquico");
        TabDslLabel.Text = loc.GetString("DataSetDesigner_TabDsl", "DSL Árbol");
        TabJsonLabel.Text = loc.GetString("DataSetDesigner_TabJson", "JSON");

        AddFileLabel.Text = loc.GetString("DataSetDesigner_AddFile", "Añadir archivo");
        AddFolderLabel.Text = loc.GetString("DataSetDesigner_AddFolder", "Añadir carpeta");
        AddArchiveLabel.Text = loc.GetString("DataSetDesigner_AddArchive", "Añadir comprimido");
        AddArchiveEntryLabel.Text = loc.GetString("DataSetDesigner_AddArchiveEntry", "Añadir entrada");
        RemoveNodeLabel.Text = loc.GetString("DataSetDesigner_RemoveNode", "Quitar");
        EmptyTree.Text = loc.GetString("DataSetDesigner_EmptyTree", "El dataset no tiene elementos todavía.");
        ApplyDslLabel.Text = loc.GetString("DataSetDesigner_ApplyDsl", "Aplicar DSL al árbol");
        ApplyJsonLabel.Text = loc.GetString("DataSetDesigner_ApplyJson", "Aplicar JSON");

        InspectorTitle.Text = loc.GetString("DataSetDesigner_InspectorTitle", "Inspector");
        InspectorEmpty.Text = loc.GetString("DataSetDesigner_InspectorNoSelection", "Selecciona un nodo del árbol.");
        NodeNameLabel.Text = loc.GetString("DataSetDesigner_NodeNameLabel", "Nodo");
        NodeSizeLabel.Text = loc.GetString("DataSetDesigner_NodeSizeLabel", "Tamaño");
        ArchiveEntriesTitle.Text = loc.GetString("DataSetDesigner_ArchiveEntriesTitle", "Entradas del comprimido");

        CloseLabel.Text = loc.GetString("Common_Close", "Cerrar");
    }
}
