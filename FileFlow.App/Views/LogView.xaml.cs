using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FileFlow.App.Models;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Views;

public partial class LogView : UserControl
{
    private LogViewModel? _viewModel;
    private ScrollViewer? _scrollViewer;

    public LogView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        LogDataGrid.PreviewKeyDown += OnDataGridPreviewKeyDown;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _scrollViewer ??= FindVisualChild<ScrollViewer>(LogDataGrid);
        if (_scrollViewer != null)
        {
            _scrollViewer.ScrollChanged -= OnScrollChanged;
            _scrollViewer.ScrollChanged += OnScrollChanged;
        }
    }

    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_scrollViewer == null || _viewModel == null) return;

        // No forzar modo en vivo si el usuario tiene un filtro de búsqueda o de nivel activo
        if (_viewModel.ActiveFilter != LogFilterLevel.All || !string.IsNullOrWhiteSpace(_viewModel.SearchFilter))
        {
            return;
        }

        // Ignorar cambios generados por redimensionamiento o vaciado de la lista
        if (e.ExtentHeightChange != 0) return;

        if (Math.Abs(e.VerticalChange) > 0.001 && _scrollViewer.ScrollableHeight > 5)
        {
            bool isAtBottom = _scrollViewer.VerticalOffset >= _scrollViewer.ScrollableHeight - 5;
            if (_viewModel.IsLiveMode != isAtBottom)
            {
                _viewModel.IsLiveMode = isAtBottom;
            }
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_scrollViewer != null)
        {
            _scrollViewer.ScrollChanged -= OnScrollChanged;
        }

        if (_viewModel != null)
        {
            _viewModel.OnLogBatchAdded -= HandleLogBatchAdded;
            _viewModel.OnLogsCleared -= HandleLogsCleared;
            _viewModel.OnFilterChanged -= HandleFilterChanged;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.OnLogBatchAdded -= HandleLogBatchAdded;
            _viewModel.OnLogsCleared -= HandleLogsCleared;
            _viewModel.OnFilterChanged -= HandleFilterChanged;
        }

        _viewModel = e.NewValue as LogViewModel;

        if (_viewModel != null)
        {
            _viewModel.OnLogBatchAdded += HandleLogBatchAdded;
            _viewModel.OnLogsCleared += HandleLogsCleared;
            _viewModel.OnFilterChanged += HandleFilterChanged;
        }
    }

    private void HandleLogBatchAdded()
    {
        if (_viewModel?.IsLiveMode != true) return;

        bool autoScroll = UserPreferencesService.Instance?.Preferences?.AutoScrollConsole ?? true;
        if (!autoScroll) return;

        if (LogDataGrid.SelectedItems.Count > 1) return;

        Dispatcher.InvokeAsync(() =>
        {
            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollToEnd();
            }
            else if (LogDataGrid.Items.Count > 0)
            {
                LogDataGrid.ScrollIntoView(LogDataGrid.Items[^1]);
            }
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void HandleLogsCleared()
    {
        // No-op
    }

    private void HandleFilterChanged()
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollToTop();
            }
            else if (LogDataGrid.Items.Count > 0)
            {
                LogDataGrid.ScrollIntoView(LogDataGrid.Items[0]);
            }
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void OnDataGridPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            CopySelectedLogsToClipboard();
            e.Handled = true;
        }
    }

    private void CopySelectedLogsToClipboard()
    {
        var selectedItems = LogDataGrid.SelectedItems;
        if (selectedItems.Count == 0) return;

        var sb = new StringBuilder();
        foreach (var item in selectedItems)
        {
            if (item is FileFlow.Sdk.Telemetry.StructuredLogRecord rec)
            {
                string node = !string.IsNullOrWhiteSpace(rec.NodeName) ? $"[{rec.NodeName}] " : "";
                string file = !string.IsNullOrWhiteSpace(rec.FileName) ? $"[{rec.FileName}] " : "";
                sb.AppendLine($"[{rec.Timestamp:HH:mm:ss.fff}] [{rec.Level}] {node}{file}{rec.Message}");
            }
            else if (item is LogEntry entry)
            {
                sb.AppendLine($"[{entry.Timestamp:HH:mm:ss}] [{entry.Level}] {entry.Message}");
            }
        }

        if (sb.Length > 0)
        {
            try
            {
                Clipboard.SetText(sb.ToString());
            }
            catch
            {
                // Manejo resiliente si el portapapeles de Windows está bloqueado temporalmente por otro proceso
            }
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) return null;

        int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childrenCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
            {
                return typedChild;
            }

            var result = FindVisualChild<T>(child);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}
