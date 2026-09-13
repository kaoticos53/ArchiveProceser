using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FileFlow.App.Preview.Core;

namespace FileFlow.App.Preview.Providers;

public class FallbackPreviewProvider : IFilePreviewProvider
{
    public string ProviderName => "Default File Inspector";
    public int Priority => -1; // Menor prioridad, fallback universal

    public bool CanHandle(FilePreviewContext context) => true;

    public Task<Control> CreateVisualElementAsync(FilePreviewContext context, CancellationToken cancellationToken)
    {
        var rootGrid = new Grid
        {
            Background = new SolidColorBrush(Color.Parse("#111318")),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var card = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1A1D24")),
            BorderBrush = new SolidColorBrush(Color.Parse("#2A2D35")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(32),
            MaxWidth = 500
        };

        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

        stack.Children.Add(new TextBlock { Text = "📄", FontSize = 48, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 16) });
        stack.Children.Add(new TextBlock { Text = context.FileName, FontSize = 16, FontWeight = FontWeight.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
        stack.Children.Add(new TextBlock { Text = $"{context.FileSizeBytes / 1024.0:F1} KB • Archivo {context.Extension.ToUpperInvariant()}", FontSize = 12, Foreground = new SolidColorBrush(Color.Parse("#8F95A3")), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 16) });

        var openBtn = new Button
        {
            Content = "📂 Abrir en el Administrador de Archivos",
            Padding = new Thickness(16, 8, 16, 8),
            Background = new SolidColorBrush(Color.Parse("#00D2FF")),
            Foreground = Brushes.Black,
            FontWeight = FontWeight.Bold,
            Cursor = Avalonia.Input.Cursor.Default,
            Margin = new Thickness(0, 8, 0, 0)
        };

        openBtn.Click += (_, _) =>
        {
            if (File.Exists(context.CurrentPath))
            {
                FileFlow.Core.Platform.OsPlatformServiceFactory.Instance.OpenFileInFileManager(context.CurrentPath);
            }
        };

        stack.Children.Add(openBtn);
        card.Child = stack;
        rootGrid.Children.Add(card);

        return Task.FromResult<Control>(rootGrid);
    }
}
