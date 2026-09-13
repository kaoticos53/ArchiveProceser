using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace FileFlow.App.Converters;

public class LogLevelToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is FileFlow.Sdk.LogLevel level)
        {
            return level switch
            {
                FileFlow.Sdk.LogLevel.Critical or FileFlow.Sdk.LogLevel.Error => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                FileFlow.Sdk.LogLevel.Warning => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                FileFlow.Sdk.LogLevel.Information => new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                FileFlow.Sdk.LogLevel.Debug => new SolidColorBrush(Color.FromRgb(192, 132, 252)),
                _ => new SolidColorBrush(Color.FromRgb(148, 163, 184))
            };
        }
        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
}

public class LogLevelToBadgeBackgroundConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is FileFlow.Sdk.LogLevel level)
        {
            return level switch
            {
                FileFlow.Sdk.LogLevel.Critical or FileFlow.Sdk.LogLevel.Error => new SolidColorBrush(Color.FromArgb(45, 239, 68, 68)),
                FileFlow.Sdk.LogLevel.Warning => new SolidColorBrush(Color.FromArgb(45, 245, 158, 11)),
                FileFlow.Sdk.LogLevel.Information => new SolidColorBrush(Color.FromArgb(40, 56, 189, 248)),
                FileFlow.Sdk.LogLevel.Debug => new SolidColorBrush(Color.FromArgb(40, 192, 132, 252)),
                _ => new SolidColorBrush(Color.FromArgb(30, 148, 163, 184))
            };
        }
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
}

public class LogLevelToBadgeForegroundConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is FileFlow.Sdk.LogLevel level)
        {
            return level switch
            {
                FileFlow.Sdk.LogLevel.Critical or FileFlow.Sdk.LogLevel.Error => new SolidColorBrush(Color.FromRgb(248, 113, 113)),
                FileFlow.Sdk.LogLevel.Warning => new SolidColorBrush(Color.FromRgb(251, 191, 36)),
                FileFlow.Sdk.LogLevel.Information => new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                FileFlow.Sdk.LogLevel.Debug => new SolidColorBrush(Color.FromRgb(216, 180, 254)),
                _ => new SolidColorBrush(Color.FromRgb(148, 163, 184))
            };
        }
        return Brushes.LightGray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
}

public class LogLevelToBadgeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
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

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
}

public class LoggingToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isLoggingEnabled = value is not bool b || b;
        return isLoggingEnabled 
            ? new SolidColorBrush(Color.FromRgb(6, 182, 212)) // Cyan (#06B6D4)
            : new SolidColorBrush(Color.FromArgb(80, 148, 163, 184)); // Semi-transparent Slate
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
}

public class LoggingToTooltipConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isLoggingEnabled = value is not bool b || b;
        return isLoggingEnabled
            ? FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("LoggingEnabledToolTip", "Logs: Habilitados (clic para silenciar)")
            : FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("LoggingDisabledToolTip", "Logs: Silenciados (clic para activar)");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
}

public class DurationMsToTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
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

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;
}

public class BytesToTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
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
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):F2} MB",
            _ => $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB"
        };

        return prefix + formatted;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;
}
