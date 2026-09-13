using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FileFlow.App.Preview.Helpers;

namespace FileFlow.App.Preview.Controls;

public partial class ImageCompareSliderControl : UserControl
{
    private bool _isDragging;
    private double _splitRatio = 0.5;

    public ImageCompareSliderControl()
    {
        InitializeComponent();
        AddHandler(PointerMovedEvent, RootGrid_PointerMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, RootGrid_PointerReleased, RoutingStrategies.Tunnel);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public void LoadImages(string originalPath, string processedPath)
    {
        if (File.Exists(originalPath))
        {
            try
            {
                var bmpOrig = AvaloniaImageLoader.LoadBitmap(originalPath);
                var originalImage = this.FindControl<Image>("OriginalImage");
                var originalBadgeText = this.FindControl<TextBlock>("OriginalBadgeText");
                if (bmpOrig != null && originalImage != null)
                {
                    originalImage.Source = bmpOrig;
                    long origSize = new FileInfo(originalPath).Length;
                    if (originalBadgeText != null)
                    {
                        originalBadgeText.Text = $"Original ({origSize / 1024.0:F1} KB)";
                    }
                }
            }
            catch { }
        }

        if (File.Exists(processedPath))
        {
            try
            {
                var bmpProc = AvaloniaImageLoader.LoadBitmap(processedPath);
                var processedImage = this.FindControl<Image>("ProcessedImage");
                var processedBadgeText = this.FindControl<TextBlock>("ProcessedBadgeText");
                if (bmpProc != null && processedImage != null)
                {
                    processedImage.Source = bmpProc;
                    long procSize = new FileInfo(processedPath).Length;
                    if (processedBadgeText != null)
                    {
                        processedBadgeText.Text = $"Procesado ({procSize / 1024.0:F1} KB)";
                    }
                }
            }
            catch { }
        }

        UpdateSplitter();
    }

    private void SliderHandle_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _isDragging = true;
        e.Handled = true;
    }

    private void RootGrid_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDragging && Bounds.Width > 0)
        {
            Point pos = e.GetPosition(this);
            _splitRatio = Math.Clamp(pos.X / Bounds.Width, 0.05, 0.95);
            UpdateSplitter();
        }
    }

    private void RootGrid_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
    }

    private void UpdateSplitter()
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0) return;

        double splitX = Bounds.Width * _splitRatio;
        var sliderHandle = this.FindControl<Control>("SliderHandle");
        if (sliderHandle != null)
        {
            sliderHandle.Margin = new Thickness(splitX - 18, (Bounds.Height / 2) - 18, 0, 0);
        }
    }
}
