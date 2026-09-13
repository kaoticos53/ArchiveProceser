using System;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FileFlow.App.Models;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Views;

public partial class LogView : UserControl
{
    private LogViewModel? _viewModel;

    public LogView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        KeyDown += OnDataGridKeyDown;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _viewModel = DataContext as LogViewModel;
    }

    private void OnDataGridKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.C && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            CopySelectedLogsToClipboard();
            e.Handled = true;
        }
    }

    private void CopySelectedLogsToClipboard()
    {
        var logDataGrid = this.FindControl<ListBox>("LogListBox");
        if (logDataGrid == null) return;

        var selectedItems = logDataGrid.SelectedItems;
        if (selectedItems == null || selectedItems.Count == 0) return;

        var sb = new StringBuilder();
        foreach (var item in selectedItems)
        {
            if (item is FileFlow.Sdk.Telemetry.StructuredLogRecord rec)
            {
                sb.AppendLine(rec.FormattedLine);
            }
            else if (item is LogEntry entry)
            {
                sb.AppendLine($"[{entry.Timestamp:HH:mm:ss}] [{entry.Level}] {entry.Message}");
            }
        }

        if (sb.Length > 0)
        {
            LogViewModel.SafeSetClipboardText(sb.ToString());
        }
    }
}
