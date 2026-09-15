using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
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

    public async Task<Control> CreateVisualElementAsync(FilePreviewContext context, CancellationToken cancellationToken)
    {
        var rootGrid = new Grid { Background = new SolidColorBrush(Color.Parse("#111318")) };
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var headerBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1A1D24")),
            Padding = new Thickness(12, 8, 12, 8),
            BorderBrush = new SolidColorBrush(Color.Parse("#2A2D35")),
            BorderThickness = new Thickness(0, 0, 0, 1)
        };

        var headerText = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.Parse("#00E5FF")),
            FontWeight = FontWeight.SemiBold,
            FontSize = 12,
            Text = $"📊 {context.FileName}"
        };
        headerBorder.Child = headerText;
        Grid.SetRow(headerBorder, 0);
        rootGrid.Children.Add(headerBorder);

        var listBox = new ListBox
        {
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.Parse("#E1E4EA")),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12)
        };

        try
        {
            if (File.Exists(context.CurrentPath))
            {
                var rows = (await MiniExcel.QueryAsync(context.CurrentPath, useHeaderRow: true).ConfigureAwait(false)).Take(500).ToList();

                if (rows.Count > 0)
                {
                    var lines = new List<string>();
                    foreach (var rowObj in rows)
                    {
                        if (rowObj is IDictionary<string, object> rowDict)
                        {
                            lines.Add(string.Join(" | ", rowDict.Select(kv => $"{kv.Key}: {kv.Value}")));
                        }
                    }

                    listBox.ItemsSource = lines;
                    headerText.Text = $"📊 {context.FileName} — {lines.Count} filas cargadas";
                }
            }
        }
        catch (Exception ex)
        {
            headerText.Text = $"⚠️ Error cargando tabla: {ex.Message}";
        }

        Grid.SetRow(listBox, 1);
        rootGrid.Children.Add(listBox);

        return rootGrid;
    }
}
