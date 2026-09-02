using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FileFlow.App.Preview.Core;
using SharpCompress.Archives;

namespace FileFlow.App.Preview.Providers;

public class ArchiveTreePreviewProvider : IFilePreviewProvider
{
    public string ProviderName => "Archive File Tree Previewer";
    public int Priority => 75;

    private static readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz"
    };

    public bool CanHandle(FilePreviewContext context)
    {
        return _supportedExtensions.Contains(context.Extension);
    }

    public Task<FrameworkElement> CreateVisualElementAsync(FilePreviewContext context, CancellationToken cancellationToken)
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
            Text = $"📦 {context.FileName}"
        };
        headerBorder.Child = headerText;
        Grid.SetRow(headerBorder, 0);
        rootGrid.Children.Add(headerBorder);

        var treeView = new TreeView
        {
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E1E4EA")),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12)
        };

        try
        {
            if (File.Exists(context.CurrentPath))
            {
                using var archive = ArchiveFactory.OpenArchive(new FileInfo(context.CurrentPath));
                int count = 0;
                long totalUncompressed = 0;

                foreach (var entry in archive.Entries)
                {
                    if (entry.IsDirectory) continue;
                    count++;
                    totalUncompressed += entry.Size;

                    var item = new TreeViewItem
                    {
                        Header = $"📄 {entry.Key} ({entry.Size / 1024.0:F1} KB)",
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D0D5DD"))
                    };
                    treeView.Items.Add(item);
                }

                headerText.Text = $"📦 {context.FileName} — {count} archivos ({totalUncompressed / 1024.0:F1} KB descomprimidos)";
            }
        }
        catch (Exception ex)
        {
            headerText.Text = $"⚠️ Error leyendo archivo comprimido: {ex.Message}";
        }

        Grid.SetRow(treeView, 1);
        rootGrid.Children.Add(treeView);

        return Task.FromResult<FrameworkElement>(rootGrid);
    }
}
