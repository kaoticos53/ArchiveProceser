using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using FileFlow.App.Models;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Sdk;

namespace FileFlow.App.Views;

public partial class LogView : UserControl
{
    private LogViewModel? _viewModel;

    public LogView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ThemeManager.Instance.ThemeChanged += OnThemeChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ThemeManager.Instance.ThemeChanged -= OnThemeChanged;
    }

    private void OnThemeChanged(AppTheme theme)
    {
        Dispatcher.InvokeAsync(RebuildDocument);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.OnLogAdded -= HandleLogAdded;
            _viewModel.OnLogsCleared -= HandleLogsCleared;
            _viewModel.OnFilterChanged -= HandleFilterChanged;
        }

        _viewModel = e.NewValue as LogViewModel;

        if (_viewModel != null)
        {
            _viewModel.OnLogAdded += HandleLogAdded;
            _viewModel.OnLogsCleared += HandleLogsCleared;
            _viewModel.OnFilterChanged += HandleFilterChanged;
            RebuildDocument();
        }
    }

    private void HandleLogAdded(LogEntry entry)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (ShouldShowEntry(entry))
            {
                AppendLogParagraph(entry);
                if (LogConsoleRichTextBox.Selection.IsEmpty)
                {
                    LogConsoleRichTextBox.ScrollToEnd();
                }
            }
        });
    }

    private void HandleLogsCleared()
    {
        Dispatcher.InvokeAsync(() =>
        {
            LogConsoleRichTextBox.Document.Blocks.Clear();
        });
    }

    private void HandleFilterChanged()
    {
        Dispatcher.InvokeAsync(() =>
        {
            RebuildDocument();
        });
    }

    private bool ShouldShowEntry(LogEntry entry)
    {
        if (_viewModel == null) return true;
        return _viewModel.ActiveFilter switch
        {
            LogFilterLevel.ErrorsOnly => entry.Level == LogLevel.Error || entry.Level == LogLevel.Critical,
            LogFilterLevel.WarningsOnly => entry.Level == LogLevel.Warning,
            LogFilterLevel.InfoOnly => entry.Level == LogLevel.Information,
            _ => true
        };
    }

    private void RebuildDocument()
    {
        LogConsoleRichTextBox.Document.Blocks.Clear();
        if (_viewModel == null) return;

        foreach (var entry in _viewModel.Logs)
        {
            if (ShouldShowEntry(entry))
            {
                AppendLogParagraph(entry);
            }
        }
        LogConsoleRichTextBox.ScrollToEnd();
    }

    private void AppendLogParagraph(LogEntry entry)
    {
        var p = new Paragraph { Margin = new Thickness(0, 1, 0, 1) };

        // Timestamp Run
        p.Inlines.Add(new Run($"[{entry.Timestamp:HH:mm:ss}] ")
        {
            Foreground = GetThemeBrush("TextSecondaryBrush", "#64748B")
        });

        // Level Run
        var (levelText, levelBrush, isBold) = GetLevelStyle(entry.Level);
        p.Inlines.Add(new Run($"[{levelText}] ")
        {
            Foreground = levelBrush,
            FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal
        });

        // Message Run
        p.Inlines.Add(new Run(entry.Message)
        {
            Foreground = GetThemeBrush("TextPrimaryBrush", "#F1F5F9")
        });

        LogConsoleRichTextBox.Document.Blocks.Add(p);
    }

    private (string Text, Brush Brush, bool Bold) GetLevelStyle(LogLevel level)
    {
        return level switch
        {
            LogLevel.Critical => ("CRITICAL", GetThemeBrush("AccentErrorBrush", "#EF4444"), true),
            LogLevel.Error => ("ERROR", GetThemeBrush("AccentErrorBrush", "#EF4444"), true),
            LogLevel.Warning => ("WARNING", GetThemeBrush("AccentWarningBrush", "#F59E0B"), true),
            LogLevel.Information => ("INFO", GetThemeBrush("AccentCyanBrush", "#38BDF8"), false),
            LogLevel.Debug => ("DEBUG", GetThemeBrush("AccentPurpleBrush", "#C084FC"), false),
            LogLevel.Trace => ("TRACE", GetThemeBrush("TextSecondaryBrush", "#94A3B8"), false),
            _ => (level.ToString().ToUpperInvariant(), GetThemeBrush("TextPrimaryBrush", "#F1F5F9"), false)
        };
    }

    private Brush GetThemeBrush(string resourceKey, string fallbackHex)
    {
        if (TryFindResource(resourceKey) is Brush b) return b;
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(fallbackHex));
    }
}
