using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FileFlow.App.Preview.Controls;

public partial class ImageCompareSliderControl : UserControl
{
    private bool _isDragging;
    private double _splitRatio = 0.5; // 0.0 (todo original) a 1.0 (todo procesado)

    public ImageCompareSliderControl()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateSplitter();
        SizeChanged += (_, _) => UpdateSplitter();
        MouseMove += RootGrid_MouseMove;
        MouseLeftButtonUp += RootGrid_MouseUp;
    }

    public void LoadImages(string originalPath, string processedPath)
    {
        if (File.Exists(originalPath))
        {
            try
            {
                var bmpOrig = new BitmapImage();
                bmpOrig.BeginInit();
                bmpOrig.CacheOption = BitmapCacheOption.OnLoad;
                bmpOrig.UriSource = new Uri(originalPath);
                bmpOrig.EndInit();
                bmpOrig.Freeze();
                OriginalImage.Source = bmpOrig;

                long origSize = new FileInfo(originalPath).Length;
                OriginalBadgeText.Text = $"Original ({origSize / 1024.0:F1} KB)";
            }
            catch { }
        }

        if (File.Exists(processedPath))
        {
            try
            {
                var bmpProc = new BitmapImage();
                bmpProc.BeginInit();
                bmpProc.CacheOption = BitmapCacheOption.OnLoad;
                bmpProc.UriSource = new Uri(processedPath);
                bmpProc.EndInit();
                bmpProc.Freeze();
                ProcessedImage.Source = bmpProc;

                long procSize = new FileInfo(processedPath).Length;
                ProcessedBadgeText.Text = $"Procesado ({procSize / 1024.0:F1} KB)";
            }
            catch { }
        }

        UpdateSplitter();
    }

    private void SliderHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        SliderHandle.CaptureMouse();
        e.Handled = true;
    }

    private void RootGrid_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging && ActualWidth > 0)
        {
            Point pos = e.GetPosition(this);
            _splitRatio = Math.Clamp(pos.X / ActualWidth, 0.05, 0.95);
            UpdateSplitter();
        }
    }

    private void RootGrid_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            SliderHandle.ReleaseMouseCapture();
        }
    }

    private void UpdateSplitter()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        double splitX = ActualWidth * _splitRatio;

        // Posicionar línea
        SplitterLine.X1 = splitX;
        SplitterLine.Y1 = 0;
        SplitterLine.X2 = splitX;
        SplitterLine.Y2 = ActualHeight;

        // Posicionar Handle
        Canvas.SetLeft(SliderHandle, splitX - (SliderHandle.ActualWidth / 2));
        Canvas.SetTop(SliderHandle, (ActualHeight / 2) - (SliderHandle.ActualHeight / 2));
        SliderHandle.Margin = new Thickness(splitX - 18, (ActualHeight / 2) - 18, 0, 0);

        // Recortar la imagen procesada (revelar solo desde splitX hacia la derecha)
        ProcessedClipGeometry.Rect = new Rect(splitX, 0, Math.Max(0, ActualWidth - splitX), ActualHeight);
    }
}
