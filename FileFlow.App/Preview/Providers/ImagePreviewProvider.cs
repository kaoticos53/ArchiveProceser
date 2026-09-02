using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FileFlow.App.Preview.Controls;
using FileFlow.App.Preview.Core;

namespace FileFlow.App.Preview.Providers;

public class ImagePreviewProvider : IFilePreviewProvider
{
    public string ProviderName => "Image Previewer";
    public int Priority => 100;

    private static readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif", ".ico", ".tiff", ".tif", ".svg"
    };

    public bool CanHandle(FilePreviewContext context)
    {
        return _supportedExtensions.Contains(context.Extension);
    }

    public Task<FrameworkElement> CreateVisualElementAsync(FilePreviewContext context, CancellationToken cancellationToken)
    {
        if (context.HasOriginalComparison && !string.IsNullOrWhiteSpace(context.OriginalPath))
        {
            var compareControl = new ImageCompareSliderControl();
            compareControl.LoadImages(context.OriginalPath, context.CurrentPath);
            return Task.FromResult<FrameworkElement>(compareControl);
        }

        // Visor interactivo con Zoom y Paneo
        var rootGrid = new Grid { Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111318")) };

        var scrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            CanContentScroll = true,
            Background = Brushes.Transparent
        };

        var imageContainer = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(20)
        };

        var img = new Image
        {
            Stretch = Stretch.Uniform
        };
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);

        try
        {
            if (File.Exists(context.CurrentPath))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(context.CurrentPath);
                bmp.EndInit();
                bmp.Freeze();
                img.Source = bmp;
            }
        }
        catch { }

        var scaleTransform = new ScaleTransform(1.0, 1.0);
        var rotateTransform = new RotateTransform(0);
        var transformGroup = new TransformGroup();
        transformGroup.Children.Add(scaleTransform);
        transformGroup.Children.Add(rotateTransform);
        img.LayoutTransform = transformGroup;

        imageContainer.Child = img;
        scrollViewer.Content = imageContainer;
        rootGrid.Children.Add(scrollViewer);

        // Barra flotante inferior de controles de Zoom y Rotación
        var toolbar = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 16),
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E61A1D24")),
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#33FFFFFF")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(12, 6, 12, 6)
        };

        var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };

        var zoomOutBtn = new Button { Content = "➖", Width = 28, Height = 28, Margin = new Thickness(2, 0, 2, 0), Cursor = Cursors.Hand };
        var zoomInBtn = new Button { Content = "➕", Width = 28, Height = 28, Margin = new Thickness(2, 0, 2, 0), Cursor = Cursors.Hand };
        var resetBtn = new Button { Content = "1:1", Width = 32, Height = 28, Margin = new Thickness(2, 0, 2, 0), Cursor = Cursors.Hand };
        var rotateBtn = new Button { Content = "🔄", Width = 28, Height = 28, Margin = new Thickness(2, 0, 2, 0), Cursor = Cursors.Hand };

        zoomOutBtn.Click += (_, _) =>
        {
            scaleTransform.ScaleX = Math.Max(0.2, scaleTransform.ScaleX - 0.2);
            scaleTransform.ScaleY = Math.Max(0.2, scaleTransform.ScaleY - 0.2);
        };

        zoomInBtn.Click += (_, _) =>
        {
            scaleTransform.ScaleX = Math.Min(5.0, scaleTransform.ScaleX + 0.2);
            scaleTransform.ScaleY = Math.Min(5.0, scaleTransform.ScaleY + 0.2);
        };

        resetBtn.Click += (_, _) =>
        {
            scaleTransform.ScaleX = 1.0;
            scaleTransform.ScaleY = 1.0;
        };

        rotateBtn.Click += (_, _) =>
        {
            rotateTransform.Angle = (rotateTransform.Angle + 90) % 360;
        };

        scrollViewer.PreviewMouseWheel += (s, e) =>
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                double delta = e.Delta > 0 ? 0.15 : -0.15;
                scaleTransform.ScaleX = Math.Clamp(scaleTransform.ScaleX + delta, 0.2, 5.0);
                scaleTransform.ScaleY = Math.Clamp(scaleTransform.ScaleY + delta, 0.2, 5.0);
            }
        };

        stackPanel.Children.Add(zoomOutBtn);
        stackPanel.Children.Add(resetBtn);
        stackPanel.Children.Add(zoomInBtn);
        stackPanel.Children.Add(new Separator { Margin = new Thickness(6, 0, 6, 0) });
        stackPanel.Children.Add(rotateBtn);

        toolbar.Child = stackPanel;
        rootGrid.Children.Add(toolbar);

        return Task.FromResult<FrameworkElement>(rootGrid);
    }
}
