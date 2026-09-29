using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using FileFlow.App.ViewModels;
using FileFlow.Sdk.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FileFlow.App.Uno.Controls;

/// <summary>
/// La sección de TELEMETRÍA del nodo en la ficha del inspector: las medidas de ejecución que el nodo acumula
/// —<see cref="NodeViewModel.CurrentStats"/>, el agregado que escribe el motor— más su estado, en una fila por
/// medida, con el ancla de automatización de cada valor para que la observación externa las lea por nombre.
///
/// <para><b>De dónde sale esta pieza (hito 275)</b>. El host de ESCRITORIO tiene esta superficie como su pestaña
/// de Telemetría, alimentada por el MISMO <c>CurrentStats</c>. El host Uno la había construido —cinco filas y un
/// botón de vaciado— y <b>nunca la montó</b>: el montaje que tenía en el cuerpo de la ficha (antes de las
/// pestañas) desapareció al entrar éstas, así que las filas se rellenaban para nadie y la ficha no mostraba ni
/// una medida. Montarla aquí es <b>reponer una capacidad que el escritorio sí tiene</b>: la fuente ya existe
/// —la escribe el motor— y esta sección SÓLO la lee.</para>
///
/// <para><b>Por qué es de sólo lectura</b>. El «Vaciar métricas» que la ficha construía no tiene gemelo en el
/// escritorio (su pestaña de telemetría no ofrece borrar nada) y ninguna vista del producto lo monta: dibujarlo
/// aquí sería una capacidad NUEVA de este host, no paridad. La sección no escribe: no cita
/// <c>UpdateTelemetryStats</c> ni el comando de reinicio del view model portable, y eso lo vigila su guardia
/// (<c>UnoInspectorTelemetryGuardTests</c>).</para>
///
/// <para><b>Por qué sin rótulo propio</b>. El rótulo de esta sección ES el de su pestaña («Telemetría»):
/// repetirlo dentro sería un rótulo de más. Y no queda ningún rótulo huérfano — la clave del vaciado, que
/// existía sólo para el botón que nunca se montó, se retiró de los dos diccionarios con él.</para>
/// </summary>
internal sealed class NodeInspectorTelemetrySection : UserControl
{
    /// <summary>
    /// Las cinco medidas que el escritorio enseña en su pestaña de telemetría, en su orden: el rótulo por el
    /// diccionario del host (con su texto de reserva) y el ancla del valor, que es lo que lee la observación
    /// externa. Es una sola tabla porque rótulo, reserva y ancla se materializan juntos y no cambian con el nodo.
    /// </summary>
    private static readonly (string Key, string Fallback, string Anchor)[] Measures =
    [
        ("Uno_InspectorStatus", "Estado", "InspectorTelemetry_Status"),
        ("Uno_InspectorProcessed", "Procesados", "InspectorTelemetry_Processed"),
        ("Uno_InspectorAvgLatency", "Latencia media", "InspectorTelemetry_AvgLatency"),
        ("Uno_InspectorTotalTime", "Tiempo total", "InspectorTelemetry_TotalTime"),
        ("Uno_InspectorPeakRam", "Pico de memoria", "InspectorTelemetry_PeakRam")
    ];

    private readonly StackPanel _rows = new() { Spacing = 2 };

    /// <summary>Los rótulos y valores materializados, en orden: lo que lee la sonda (el texto, no el píxel).</summary>
    private readonly List<(TextBlock Label, TextBlock Value)> _builtRows = [];

    private NodeViewModel? _node;
    private PropertyChangedEventHandler? _nodeSub;

    public NodeInspectorTelemetrySection()
    {
        Content = _rows;
        Refresh();
    }

    /// <summary>Las filas materializadas AHORA (el nodo inspeccionado es de quien son las medidas).</summary>
    internal int RowCount => _rows.Children.Count;

    /// <summary>
    /// Ata la sección al nodo inspeccionado: suelta el anterior (el panel es una sola pieza y el nodo cambia con
    /// la selección) y sigue al nuevo por <c>PropertyChanged</c>, porque las medidas las escribe el motor mientras
    /// la ficha está abierta. Con <c>null</c> la sección se queda sin filas: sin nodo no hay medidas que enseñar.
    /// </summary>
    internal void Bind(NodeViewModel? node)
    {
        if (_node is not null && _nodeSub is not null)
        {
            _node.PropertyChanged -= _nodeSub;
        }

        _node = node;
        _nodeSub = node is null ? null : OnNodePropertyChanged;
        if (node is not null && _nodeSub is not null)
        {
            node.PropertyChanged += _nodeSub;
        }

        Refresh();
    }

    /// <summary>Vuelve a rotular las filas con el idioma en curso (el cambio de idioma es en caliente).</summary>
    internal void ApplyLocalization() => Refresh();

    /// <summary>Los textos de las filas materializadas, en orden, para la sonda en proceso.</summary>
    internal IReadOnlyList<(string Label, string Value)> RowTextsForProbe() =>
        [.. _builtRows.Select(row => (row.Label.Text, row.Value.Text))];

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NodeViewModel.CurrentStats) or nameof(NodeViewModel.ExecutionStatus))
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        if (_node is null)
        {
            _rows.Children.Clear();
            _builtRows.Clear();
            return;
        }

        EnsureRows();

        var loc = LocalizationManager.Instance;
        var culture = CultureInfo.CurrentCulture;

        // El orden del escritorio: primero el estado del nodo (la propiedad localizada del view model) y después
        // las medidas del agregado del motor. Los valores se formatean en la cultura en curso, como el resto de
        // la ficha; ninguno se inventa aquí — todos salen del nodo inspeccionado.
        var values = CurrentValues(culture);
        for (var i = 0; i < _builtRows.Count; i++)
        {
            _builtRows[i].Label.Text = loc.GetString(Measures[i].Key, Measures[i].Fallback);
            _builtRows[i].Value.Text = values[i];
        }
    }

    /// <summary>
    /// Las cinco medidas del nodo inspeccionado, en el orden de <see cref="Measures"/>: el estado del nodo y el
    /// agregado del motor (<c>CurrentStats</c>), formateados en la cultura en curso.
    /// </summary>
    private string[] CurrentValues(CultureInfo culture)
    {
        var node = _node!;
        var stats = node.CurrentStats;
        return
        [
            node.ExecutionStatusText,
            stats.ProcessedCount.ToString(culture),
            string.Format(culture, "{0:F1} ms", stats.AverageTimeMs),
            string.Format(culture, "{0:F1} ms", stats.TotalTimeMs),
            FormatBytes(stats.PeakAllocatedBytes, culture)
        ];
    }

    /// <summary>
    /// Materializa las filas UNA vez. El latido del motor reescribe las medidas mientras hay una ejecución, así
    /// que el refresco sólo reescribe su texto: volver a construir la fila en cada fotograma tiraría y crearía
    /// quince elementos por latido sin cambiar lo que se ve.
    /// </summary>
    private void EnsureRows()
    {
        if (_builtRows.Count > 0)
        {
            return;
        }

        foreach (var measure in Measures)
        {
            var labelBlock = new TextBlock
            {
                FontSize = 11,
                Opacity = 0.7,
                Foreground = Brush("CanvasSecondaryBrush")
            };
            var valueBlock = new TextBlock
            {
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Right,
                Foreground = Brush("CanvasTextBrush")
            };

            // El VALOR es quien canta su ancla —el contenedor de la fila no materializa en el árbol de
            // accesibilidad—, así que la observación externa lee cada medida por un nombre estable.
            AutomationProperties.SetAutomationId(valueBlock, measure.Anchor);

            var grid = new Grid { ColumnSpacing = 8 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(labelBlock, 0);
            Grid.SetColumn(valueBlock, 1);
            grid.Children.Add(labelBlock);
            grid.Children.Add(valueBlock);

            _builtRows.Add((labelBlock, valueBlock));
            _rows.Children.Add(grid);
        }
    }

    private static string FormatBytes(long bytes, CultureInfo culture)
    {
        if (bytes <= 0)
        {
            return "—";
        }

        const long kb = 1024;
        const long mb = kb * 1024;
        return bytes >= mb
            ? string.Format(culture, "{0:F1} MB", bytes / (double)mb)
            : bytes >= kb
                ? string.Format(culture, "{0:F1} KB", bytes / (double)kb)
                : string.Format(culture, "{0} B", bytes);
    }

    /// <summary>El pincel de un token Canvas* resuelto de los recursos de la app (el patrón de la ficha).</summary>
    private static Brush Brush(string key)
    {
        return (Brush)Application.Current.Resources[key];
    }
}
