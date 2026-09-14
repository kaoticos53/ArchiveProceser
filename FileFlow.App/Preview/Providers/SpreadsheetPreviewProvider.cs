using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FileFlow.App.Preview.Core;
using MiniExcelLibs;

namespace FileFlow.App.Preview.Providers;

public class SpreadsheetPreviewProvider : IFilePreviewProvider
{
    public string ProviderName => "Spreadsheet & Tabular Previewer";
    public int Priority => 90;

    private static readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xlsx", ".xls", ".csv", ".tsv"
    };

    public bool CanHandle(FilePreviewContext context)
    {
        return _supportedExtensions.Contains(context.Extension);
    }

    public async Task<FrameworkElement> CreateVisualElementAsync(FilePreviewContext context, CancellationToken cancellationToken)
    {
        var rootGrid = new Grid { Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111318")) };
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var headerBorder = new Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A1D24")),
            Padding = new Thickness(12, 8, 12, 8),
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A2D35")),
            BorderThickness = new Thickness(0, 0, 0, 1)
        };

        var headerText = new TextBlock
        {
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00E5FF")),
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            Text = $"📊 {context.FileName}"
        };
        headerBorder.Child = headerText;
        Grid.SetRow(headerBorder, 0);
        rootGrid.Children.Add(headerBorder);

        var dataGrid = new DataGrid
        {
            IsReadOnly = true,
            AutoGenerateColumns = true,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E1E4EA")),
            RowBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#14161D")),
            AlternatingRowBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#181B22")),
            GridLinesVisibility = DataGridGridLinesVisibility.All,
            HorizontalGridLinesBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#23262F")),
            VerticalGridLinesBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#23262F")),
            BorderThickness = new Thickness(0),
            EnableRowVirtualization = true,
            EnableColumnVirtualization = true,
            CanUserSortColumns = true,
            HeadersVisibility = DataGridHeadersVisibility.Column
        };

        try
        {
            if (File.Exists(context.CurrentPath))
            {
                var dataTable = new DataTable();
                var rows = (await MiniExcel.QueryAsync(context.CurrentPath, useHeaderRow: true).ConfigureAwait(false)).Take(500).ToList();

                if (rows.Count > 0)
                {
                    var firstRow = rows[0] as IDictionary<string, object>;
                    if (firstRow != null)
                    {
                        foreach (var colKey in firstRow.Keys)
                        {
                            dataTable.Columns.Add(colKey, typeof(string));
                        }

                        foreach (var rowObj in rows)
                        {
                            if (rowObj is IDictionary<string, object> rowDict)
                            {
                                var dr = dataTable.NewRow();
                                foreach (var (k, v) in rowDict)
                                {
                                    dr[k] = v?.ToString() ?? string.Empty;
                                }
                                dataTable.Rows.Add(dr);
                            }
                        }
                    }

                    dataGrid.ItemsSource = dataTable.DefaultView;
                    headerText.Text = $"📊 {context.FileName} — {dataTable.Rows.Count} filas cargadas ({dataTable.Columns.Count} columnas)";
                }
            }
        }
        catch (Exception ex)
        {
            headerText.Text = $"⚠️ Error cargando tabla: {ex.Message}";
        }

        Grid.SetRow(dataGrid, 1);
        rootGrid.Children.Add(dataGrid);

        return rootGrid;
    }
}
