using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FileFlow.App.Converters;

public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool boolValue = value is bool b && b;
        return boolValue ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility v && v != Visibility.Visible;
    }
}

public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value != null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => DependencyProperty.UnsetValue;
}

public class InverseNullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value == null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => DependencyProperty.UnsetValue;
}

public class NodeExecutionStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is FileFlow.Sdk.NodeExecutionStatus status)
        {
            return status switch
            {
                FileFlow.Sdk.NodeExecutionStatus.Running => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(168, 85, 247)), // Purple / Morado procesando
                FileFlow.Sdk.NodeExecutionStatus.Completed => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129)), // Emerald / Verde completado con éxito
                FileFlow.Sdk.NodeExecutionStatus.PausedOnError or FileFlow.Sdk.NodeExecutionStatus.Faulted => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)), // Red / Rojo error
                FileFlow.Sdk.NodeExecutionStatus.PausedAtBreakpoint => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11)), // Amber / Ámbar pausa
                _ => System.Windows.Media.Brushes.Transparent
            };
        }
        return System.Windows.Media.Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => DependencyProperty.UnsetValue;
}

public class DiffChangeTypeToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string changeType)
        {
            return changeType switch
            {
                "Added" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129)), // Emerald
                "Modified" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11)), // Amber
                "Removed" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)), // Red
                _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184)) // Slate
            };
        }
        return System.Windows.Media.Brushes.White;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => DependencyProperty.UnsetValue;
}

public class BreakpointToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool hasBreakpoint = value is bool b && b;
        return hasBreakpoint 
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)) // Bright Red
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(80, 148, 163, 184)); // Semi-transparent Slate
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => DependencyProperty.UnsetValue;
}

public class BooleanToGridLengthConverter : IValueConverter
{
    public double DefaultWidth { get; set; } = 360;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isVisible = value is bool b && b;
        if (!isVisible)
        {
            return new GridLength(0, GridUnitType.Pixel);
        }

        if (parameter != null && double.TryParse(parameter.ToString(), out double parsedWidth))
        {
            return new GridLength(parsedWidth, GridUnitType.Pixel);
        }

        return new GridLength(DefaultWidth, GridUnitType.Pixel);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is GridLength gl)
        {
            return gl.Value > 0;
        }
        return false;
    }
}

public class InputOutputBulletConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isInput = value is bool b && b;
        return isInput ? "🔵 " : "🟢 ";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => DependencyProperty.UnsetValue;
}

public class InputOutputBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isInput = value is bool b && b;
        return isInput 
            ? Application.Current?.FindResource("AccentCyanBrush") ?? System.Windows.Media.Brushes.Cyan
            : Application.Current?.FindResource("AccentSuccessBrush") ?? System.Windows.Media.Brushes.LimeGreen;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => DependencyProperty.UnsetValue;
}

public class StringEqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string? valStr = value?.ToString();
        string? paramStr = parameter?.ToString();

        if (string.Equals(valStr, paramStr, StringComparison.OrdinalIgnoreCase))
        {
            return Visibility.Visible;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => DependencyProperty.UnsetValue;
}

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

public class StringNotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string? str = value?.ToString();
        return !string.IsNullOrWhiteSpace(str) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => DependencyProperty.UnsetValue;
}

public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool b = value is bool flag && flag;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility v && v == Visibility.Visible;
    }
}


