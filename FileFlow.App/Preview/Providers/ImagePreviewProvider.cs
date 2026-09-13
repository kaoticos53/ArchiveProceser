using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using FileFlow.App.Preview.Controls;
using FileFlow.App.Preview.Core;
using FileFlow.App.Preview.Helpers;

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

    public Task<Control> CreateVisualElementAsync(FilePreviewContext context, CancellationToken cancellationToken)
    {
        if (context.HasOriginalComparison && !string.IsNullOrWhiteSpace(context.OriginalPath))
        {
            var compareControl = new ImageCompareSliderControl();
            compareControl.LoadImages(context.OriginalPath, context.CurrentPath);
            return Task.FromResult<Control>(compareControl);
        }

        // Deserializar posibles cajas de rostros u objetos detectados
        List<PreviewDetectionBox>? detectedBoxes = null;
        string badgeEmoji = "👤";

        if (context.Metadata.TryGetValue("AI:FaceBoxes", out var fbObj) && fbObj != null)
        {
            badgeEmoji = "👤";
            try
            {
                string? jsonStr = fbObj switch
                {
                    string s => s,
                    System.Text.Json.JsonElement je => je.ValueKind == System.Text.Json.JsonValueKind.String ? je.GetString() : je.GetRawText(),
                    _ => System.Text.Json.JsonSerializer.Serialize(fbObj)
                };

                if (!string.IsNullOrWhiteSpace(jsonStr))
                {
                    detectedBoxes = System.Text.Json.JsonSerializer.Deserialize<List<PreviewDetectionBox>>(jsonStr, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
            }
            catch { }
        }
        else if (context.Metadata.TryGetValue("AI:DetectedBoxes", out var dbObj) && dbObj != null)
        {
            badgeEmoji = "🎯";
            try
            {
                string? jsonStr = dbObj switch
                {
                    string s => s,
                    System.Text.Json.JsonElement je => je.ValueKind == System.Text.Json.JsonValueKind.String ? je.GetString() : je.GetRawText(),
                    _ => System.Text.Json.JsonSerializer.Serialize(dbObj)
                };

                if (!string.IsNullOrWhiteSpace(jsonStr))
                {
                    detectedBoxes = System.Text.Json.JsonSerializer.Deserialize<List<PreviewDetectionBox>>(jsonStr, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
            }
            catch { }
        }

        var rootGrid = new Grid { Background = new SolidColorBrush(Color.Parse("#111318")) };

        var scrollViewer = new ScrollViewer
        {
            Background = Brushes.Transparent
        };

        var imageContainer = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(20)
        };

        var imageWrapper = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var img = new Image
        {
            Stretch = Stretch.Uniform
        };

        var overlayCanvas = new Canvas
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            IsHitTestVisible = false
        };

        Bitmap? bmp = null;
        try
        {
            if (File.Exists(context.CurrentPath))
            {
                bmp = AvaloniaImageLoader.LoadBitmap(context.CurrentPath);
                if (bmp != null)
                {
                    img.Source = bmp;
                    img.Width = bmp.PixelSize.Width;
                    img.Height = bmp.PixelSize.Height;
                    overlayCanvas.Width = bmp.PixelSize.Width;
                    overlayCanvas.Height = bmp.PixelSize.Height;
                }
            }
        }
        catch { }

        // Si hay cajas de rostros/objetos detectados, dibujarlas sobre overlayCanvas
        if (bmp != null && detectedBoxes != null && detectedBoxes.Count > 0)
        {
            int index = 1;
            foreach (var itemBox in detectedBoxes)
            {
                float left = Math.Clamp(itemBox.X1, 0f, 1f) * bmp.PixelSize.Width;
                float top = Math.Clamp(itemBox.Y1, 0f, 1f) * bmp.PixelSize.Height;
                float right = Math.Clamp(itemBox.X2, 0f, 1f) * bmp.PixelSize.Width;
                float bottom = Math.Clamp(itemBox.Y2, 0f, 1f) * bmp.PixelSize.Height;
                float boxWidth = Math.Max(10, right - left);
                float boxHeight = Math.Max(10, bottom - top);

                var boxBorder = new Border
                {
                    BorderBrush = new SolidColorBrush(Color.Parse("#00E5FF")),
                    BorderThickness = new Thickness(Math.Clamp(bmp.PixelSize.Height * 0.003, 2, 8)),
                    Background = new SolidColorBrush(Color.FromArgb(35, 0, 229, 255)),
                    CornerRadius = new CornerRadius(4),
                    Width = boxWidth,
                    Height = boxHeight
                };
                Canvas.SetLeft(boxBorder, left);
                Canvas.SetTop(boxBorder, top);

                string badgeLabel = !string.IsNullOrWhiteSpace(itemBox.Label)
                    ? $"{badgeEmoji} {itemBox.Label} ({itemBox.Score * 100:F0}%)"
                    : $"{badgeEmoji} #{index} ({itemBox.Score * 100:F0}%)";

                var labelBadge = new Border
                {
                    Background = new SolidColorBrush(Color.Parse("#E6111318")),
                    BorderBrush = new SolidColorBrush(Color.Parse("#00E5FF")),
                    BorderThickness = new Thickness(1.5),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(4, 1, 4, 1),
                    Child = new TextBlock
                    {
                        Text = badgeLabel,
                        Foreground = new SolidColorBrush(Color.Parse("#00E5FF")),
                        FontSize = Math.Clamp(bmp.PixelSize.Height * 0.02, 11, 24),
                        FontWeight = FontWeight.Bold
                    }
                };
                Canvas.SetLeft(labelBadge, left);
                Canvas.SetTop(labelBadge, Math.Max(0, top - (bmp.PixelSize.Height * 0.035)));

                overlayCanvas.Children.Add(boxBorder);
                overlayCanvas.Children.Add(labelBadge);
                index++;
            }
        }

        imageWrapper.Children.Add(img);
        imageWrapper.Children.Add(overlayCanvas);

        var scaleTransform = new ScaleTransform(1.0, 1.0);
        var rotateTransform = new RotateTransform(0);
        var transformGroup = new TransformGroup();
        transformGroup.Children.Add(scaleTransform);
        transformGroup.Children.Add(rotateTransform);
        imageWrapper.RenderTransform = transformGroup;

        // Auto-ajuste inicial si la imagen es grande
        if (bmp != null && (bmp.PixelSize.Width > 900 || bmp.PixelSize.Height > 600))
        {
            double fitScale = Math.Min(850.0 / bmp.PixelSize.Width, 550.0 / bmp.PixelSize.Height);
            if (fitScale > 0 && fitScale < 1.0)
            {
                scaleTransform.ScaleX = fitScale;
                scaleTransform.ScaleY = fitScale;
            }
        }

        imageContainer.Child = imageWrapper;
        scrollViewer.Content = imageContainer;
        rootGrid.Children.Add(scrollViewer);

        // Barra flotante inferior de controles de Zoom y Rotación
        var toolbar = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 16),
            Background = new SolidColorBrush(Color.Parse("#E61A1D24")),
            BorderBrush = new SolidColorBrush(Color.Parse("#33FFFFFF")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(12, 6, 12, 6)
        };

        var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };

        var zoomOutBtn = new Button { Content = "➖", Width = 28, Height = 28, Margin = new Thickness(2, 0, 2, 0) };
        var zoomInBtn = new Button { Content = "➕", Width = 28, Height = 28, Margin = new Thickness(2, 0, 2, 0) };
        var resetBtn = new Button { Content = "1:1", Width = 32, Height = 28, Margin = new Thickness(2, 0, 2, 0) };
        var rotateBtn = new Button { Content = "🔄", Width = 28, Height = 28, Margin = new Thickness(2, 0, 2, 0) };

        zoomOutBtn.Click += (_, _) =>
        {
            scaleTransform.ScaleX = Math.Max(0.1, scaleTransform.ScaleX - 0.15);
            scaleTransform.ScaleY = Math.Max(0.1, scaleTransform.ScaleY - 0.15);
        };

        zoomInBtn.Click += (_, _) =>
        {
            scaleTransform.ScaleX = Math.Min(5.0, scaleTransform.ScaleX + 0.15);
            scaleTransform.ScaleY = Math.Min(5.0, scaleTransform.ScaleY + 0.15);
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

        stackPanel.Children.Add(zoomOutBtn);
        stackPanel.Children.Add(resetBtn);
        stackPanel.Children.Add(zoomInBtn);
        stackPanel.Children.Add(new Separator { Margin = new Thickness(6, 0, 6, 0) });
        stackPanel.Children.Add(rotateBtn);

        toolbar.Child = stackPanel;
        rootGrid.Children.Add(toolbar);

        return Task.FromResult<Control>(rootGrid);
    }

    private class PreviewDetectionBox
    {
        public string? Label { get; set; }
        public float X1 { get; set; }
        public float Y1 { get; set; }
        public float X2 { get; set; }
        public float Y2 { get; set; }
        public float Score { get; set; }
    }
}
