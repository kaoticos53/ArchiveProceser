using System;
using FileFlow.App.ViewModels;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.VirtualFileSystem;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileFlow.App.Uno.Controls;

/// <summary>
/// El explorador del SISTEMA DE ARCHIVOS VIRTUAL del host Uno: la vista del
/// <see cref="VirtualFileSystemExplorerViewModel"/> portable, servida por <see cref="Platform.UnoWindowService"/>
/// cuando una orden pide <c>DialogKeys.VirtualFileSystemExplorer</c> con el almacén de la última ejecución
/// como carga útil — el mismo contrato del escritorio.
///
/// <para><b>El view model lo construye el host, no la vista</b> (igual que el <c>AvaloniaWindowService</c>
/// del escritorio): recibe el almacén y arma él solo sus catálogos, sus filtros, su árbol y sus metadatos.
/// La vista sólo enlaza: el buscador escribe <c>SearchText</c>, la lista enlaza <c>FilteredFiles</c> y su
/// selección enlaza <c>SelectedFile</c>, que es lo que dispara la carga de metadatos del propio view model.</para>
/// </summary>
public sealed partial class VirtualFileSystemExplorerBody : UserControl
{
    private readonly VirtualFileSystemExplorerViewModel _vm;

    public VirtualFileSystemExplorerBody(IVirtualFileSystemStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        _vm = new VirtualFileSystemExplorerViewModel(store);

        InitializeComponent();
        DataContext = _vm;
        RefreshLocalization();
    }

    /// <summary>El view model portable en uso (la sonda lee sus totales, no los de una copia).</summary>
    internal VirtualFileSystemExplorerViewModel Vm => _vm;

    /// <summary>El total de ficheros del almacén, tal como lo cuenta el view model.</summary>
    internal int TotalFiles => _vm.TotalFiles;

    /// <summary>Las filas que la lista está mostrando AHORA.</summary>
    internal int RowCount => FileList.Items.Count;

    /// <summary>Los metadatos del fichero seleccionado (su rastro en el pie).</summary>
    internal int MetadataCount => MetadataList.Items.Count;

    /// <summary>El rótulo del KPI de ficheros tal como quedó pintado.</summary>
    internal string TotalFilesText => KpiFilesValue.Text;

    /// <summary>Los rótulos del host (se reescriben en caliente al cambiar de idioma).</summary>
    public void RefreshLocalization()
    {
        var loc = LocalizationManager.Instance;

        HeaderTitle.Text = loc.GetString("VfsExplorer_HeaderTitle", "Explorador de Archivos Virtual");
        HeaderSubtitle.Text = loc.GetString("VfsExplorer_HeaderSubtitle",
            "Almacén virtual de la última ejecución: qué archivos se crearon, de dónde salieron y en qué nodo.");
        SearchBox.PlaceholderText = loc.GetString("VfsExplorer_SearchPlaceholder",
            "Buscar archivos en el sistema virtual...");
        RefreshLabel.Text = loc.GetString("Common_Refresh", "Actualizar");
        CloseLabel.Text = loc.GetString("Common_Close", "Cerrar");

        KpiFilesLabel.Text = loc.GetString("VfsExplorer_KpiFiles", "Archivos");
        KpiSourceLabel.Text = loc.GetString("VfsExplorer_KpiSource", "Origen");
        KpiDestinationLabel.Text = loc.GetString("VfsExplorer_KpiDestination", "Destino");

        ColName.Text = loc.GetString("VfsExplorer_ColName", "Nombre");
        ColVirtualPath.Text = loc.GetString("VfsExplorer_ColVirtualPath", "Ruta Virtual");
        ColRole.Text = loc.GetString("VfsExplorer_ColRole", "Rol");
        ColSourceNode.Text = loc.GetString("VfsExplorer_ColSourceNode", "Nodo Emisor");

        MetadataLabel.Text = loc.GetString("VfsExplorer_MetadataLabel", "Metadatos del archivo seleccionado:");
    }

    /// <summary>El botón «Actualizar» ejecuta la orden canónica del view model.</summary>
    private void OnRefreshClicked(object sender, RoutedEventArgs e) => _vm.RefreshDataCommand.Execute(null);

    /// <summary>El cierre es del host: la superficie vive en el modal que la abrió.</summary>
    private void OnCloseClicked(object sender, RoutedEventArgs e) =>
        Platform.UnoWindowService.CloseActiveWindow();
}
