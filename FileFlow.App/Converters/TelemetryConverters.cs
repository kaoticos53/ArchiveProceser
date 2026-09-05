using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FileFlow.App.Converters;

public class LogLevelToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is FileFlow.Sdk.LogLevel level)
        {
            return level switch
            {
                FileFlow.Sdk.LogLevel.Critical or FileFlow.Sdk.LogLevel.Error =>
                    Application.Current?.TryFindResource("AccentErrorBrush") ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)),
                FileFlow.Sdk.LogLevel.Warning =>
                    Application.Current?.TryFindResource("AccentWarningBrush") ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11)),
                FileFlow.Sdk.LogLevel.Information =>
                    Application.Current?.TryFindResource("AccentCyanBrush") ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248)),
                FileFlow.Sdk.LogLevel.Debug =>
                    Application.Current?.TryFindResource("AccentPurpleBrush") ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(192, 132, 252)),
                _ => Application.Current?.TryFindResource("TextSecondaryBrush") ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184))
            };
        }
        return System.Windows.Media.Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => DependencyProperty.UnsetValue;
}

public class LogLevelToBadgeBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is FileFlow.Sdk.LogLevel level)
        {
            return level switch
            {
                FileFlow.Sdk.LogLevel.Critical or FileFlow.Sdk.LogLevel.Error =>
                    new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(45, 239, 68, 68)),
                FileFlow.Sdk.LogLevel.Warning =>
                    new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(45, 245, 158, 11)),
                FileFlow.Sdk.LogLevel.Information =>
                    new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 56, 189, 248)),
                FileFlow.Sdk.LogLevel.Debug =>
                    new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 192, 132, 252)),
                _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(30, 148, 163, 184))
            };
        }
        return System.Windows.Media.Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => DependencyProperty.UnsetValue;
}

public class LogLevelToBadgeForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is FileFlow.Sdk.LogLevel level)
        {
            return level switch
            {
                FileFlow.Sdk.LogLevel.Critical or FileFlow.Sdk.LogLevel.Error =>
                    new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 113, 113)),
                FileFlow.Sdk.LogLevel.Warning =>
                    new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(251, 191, 36)),
                FileFlow.Sdk.LogLevel.Information =>
                    new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248)),
                FileFlow.Sdk.LogLevel.Debug =>
                    new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(216, 180, 254)),
                _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184))
            };
        }
        return System.Windows.Media.Brushes.LightGray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => DependencyProperty.UnsetValue;
}

public class LogLevelToBadgeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is FileFlow.Sdk.LogLevel level)
        {
            return level switch
            {
                FileFlow.Sdk.LogLevel.Critical => "CRITICAL",
                FileFlow.Sdk.LogLevel.Error => "ERROR",
                FileFlow.Sdk.LogLevel.Warning => "WARN",
                FileFlow.Sdk.LogLevel.Information => "INFO",
                FileFlow.Sdk.LogLevel.Debug => "DEBUG",
                FileFlow.Sdk.LogLevel.Trace => "TRACE",
                _ => level.ToString().ToUpperInvariant()
            };
        }
        return "LOG";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => DependencyProperty.UnsetValue;
}

public class LoggingToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isLoggingEnabled = value is not bool b || b;
        return isLoggingEnabled 
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(6, 182, 212)) // Cyan brillante (#06B6D4)
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(80, 148, 163, 184)); // Semi-transparent Slate
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => DependencyProperty.UnsetValue;
}

public class LoggingToTooltipConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isLoggingEnabled = value is not bool b || b;
        return isLoggingEnabled
            ? FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("LoggingEnabledToolTip", "Logs: Habilitados (clic para silenciar)")
            : FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("LoggingDisabledToolTip", "Logs: Silenciados (clic para activar)");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => DependencyProperty.UnsetValue;
}

/// <summary>
/// Converts a numeric duration value (in milliseconds, as double or long) to a
/// human-readable string with automatic unit scaling: µs → ms → s → min.
/// An optional ConverterParameter string is prepended as a prefix (e.g. "⚡ ").
/// </summary>
public class DurationMsToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double ms = value switch
        {
            double d => d,
            float f  => f,
            long l   => l,
            int i    => i,
            _        => -1
        };

        if (ms < 0) return string.Empty;

        string prefix = parameter is string p ? p : string.Empty;

        string formatted = ms switch
        {
            < 1.0     => $"{ms * 1000:F0} µs",
            < 1_000.0 => $"{ms:F1} ms",
            < 60_000.0 => $"{ms / 1000.0:F2} s",
            _         => $"{ms / 60_000.0:F1} min"
        };

        return prefix + formatted;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

/// <summary>
/// Converts a byte count (long or int) to a human-readable size string with
/// automatic unit scaling: B → KB → MB → GB.
/// An optional ConverterParameter string is prepended as a prefix (e.g. "💾 ").
/// </summary>
public class BytesToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        long bytes = value switch
        {
            long l => l,
            int i  => i,
            _      => -1
        };

        if (bytes < 0) return string.Empty;

        string prefix = parameter is string p ? p : string.Empty;

        string formatted = bytes switch
        {
            >= 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024 * 1024):F2} GB",
            >= 1024L * 1024        => $"{bytes / (1024.0 * 1024):F1} MB",
            >= 1024L               => $"{bytes / 1024.0:F0} KB",
            _                      => $"{bytes} B"
        };

        return prefix + formatted;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}
