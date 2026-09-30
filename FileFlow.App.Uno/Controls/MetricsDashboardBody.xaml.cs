using System;
using FileFlow.App.ViewModels;
using FileFlow.Sdk.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileFlow.App.Uno.Controls;

/// <summary>
/// El panel de MÉTRICAS del host Uno: la vista del <see cref="WorkflowMetricsDashboardViewModel"/>
/// portable, servida por <see cref="Platform.UnoWindowService"/> cuando una orden del menú pide
/// <c>DialogKeys.WorkflowMetricsDashboard</c> — exactamente la clave que pide la versión anterior para abrir su
/// <c>WorkflowMetricsDashboardWindow</c>.
///
/// <para><b>La vista no calcula nada.</b> Las tarjetas y la tabla leen propiedades del view model (ya
/// formateadas por él: duraciones, bytes, porcentajes), y el único botón que ejecuta es su comando
/// canónico <c>RefreshMetricsCommand</c>. La tabla de la versión anterior es de 7 columnas; aquí se dibujan los
/// mismos 7 campos y se dejan fuera las columnas que allí declaran pero no enlazan a nada —tamaño y fecha
/// del fichero, que el <c>VirtualFileEntry</c> del núcleo no tiene en la fila de métricas—.</para>
/// </summary>
public sealed partial class MetricsDashboardBody : UserControl
{
    private readonly WorkflowMetricsDashboardViewModel _vm;

    public MetricsDashboardBody(WorkflowMetricsDashboardViewModel viewModel)
    {
        _vm = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        InitializeComponent();
        DataContext = _vm;
        RefreshLocalization();
    }

    /// <summary>El view model portable en uso (la sonda lee de aquí, no de una copia de la vista).</summary>
    internal WorkflowMetricsDashboardViewModel Vm => _vm;

    /// <summary>Las filas que la tabla está mostrando AHORA (lo que la sonda mide).</summary>
    internal int RowCount => NodeList.Items.Count;

    /// <summary>La duración total ya formateada por el view model (el número de la primera tarjeta).</summary>
    internal string TotalDurationText => KpiDurationValue.Text;

    /// <summary>El nodo más lento según el view model (la cuarta tarjeta).</summary>
    internal string SlowestNodeText => KpiSlowestValue.Text;

    /// <summary>El estado del análisis (el pie): cuántos nodos y cuántos cuellos de botella.</summary>
    internal string StatusLine => StatusText.Text;

    /// <summary>Los rótulos del host (se reescriben en caliente al cambiar de idioma).</summary>
    public void RefreshLocalization()
    {
        var loc = LocalizationManager.Instance;

        HeaderTitle.Text = loc.GetString("MetricsDashboard_Title", "Métricas y Rendimiento");
        RefreshLabel.Text = loc.GetString("Common_Refresh", "Actualizar");
        CloseLabel.Text = loc.GetString("Common_Close", "Cerrar");

        KpiDurationLabel.Text = loc.GetString("Metrics_TotalWorkflowDuration", "Duración Total");
        KpiBytesLabel.Text = loc.GetString("Metrics_TotalBytesProcessed", "Datos Procesados");
        KpiItemsLabel.Text = loc.GetString("Metrics_TotalItemsProcessed", "Items Procesados");
        KpiSlowestLabel.Text = loc.GetString("Metrics_SlowestNodeLabel", "Nodo Más Lento");

        ColNode.Text = loc.GetString("Metrics_ColNode", "Nodo");
        ColCategory.Text = loc.GetString("Metrics_ColCategory", "Categoría");
        ColDuration.Text = loc.GetString("Metrics_ColDuration", "Duración");
        ColItems.Text = loc.GetString("Metrics_ColItems", "Items");
        ColBytes.Text = loc.GetString("Metrics_ColBytes", "Datos");
        ColTimePercent.Text = loc.GetString("Metrics_ColTimePercent", "% Tiempo");
        ColStatus.Text = loc.GetString("Metrics_ColStatus", "Estado");
    }

    /// <summary>El botón «Actualizar» ejecuta la orden canónica del view model (no una copia de su lógica).</summary>
    private void OnRefreshClicked(object sender, RoutedEventArgs e) => _vm.RefreshMetricsCommand.Execute(null);

    /// <summary>El cierre es del host: la superficie vive en el modal que la abrió.</summary>
    private void OnCloseClicked(object sender, RoutedEventArgs e) =>
        Platform.UnoWindowService.CloseActiveWindow();
}
