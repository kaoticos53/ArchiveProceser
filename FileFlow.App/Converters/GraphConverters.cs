using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace FileFlow.App.Converters;

public class NodeExecutionStatusToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is FileFlow.Sdk.NodeExecutionStatus status)
        {
            return status switch
            {
                FileFlow.Sdk.NodeExecutionStatus.Running => new SolidColorBrush(Color.FromRgb(168, 85, 247)), // Purple
                FileFlow.Sdk.NodeExecutionStatus.Completed => new SolidColorBrush(Color.FromRgb(16, 185, 129)), // Emerald
                FileFlow.Sdk.NodeExecutionStatus.PausedOnError or FileFlow.Sdk.NodeExecutionStatus.Faulted => new SolidColorBrush(Color.FromRgb(239, 68, 68)), // Red
                FileFlow.Sdk.NodeExecutionStatus.PausedAtBreakpoint => new SolidColorBrush(Color.FromRgb(245, 158, 11)), // Amber
                _ => Brushes.Transparent
            };
        }
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
}

public class DiffChangeTypeToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string changeType)
        {
            return changeType switch
            {
                "Added" => new SolidColorBrush(Color.FromRgb(16, 185, 129)), // Emerald
                "Modified" => new SolidColorBrush(Color.FromRgb(245, 158, 11)), // Amber
                "Removed" => new SolidColorBrush(Color.FromRgb(239, 68, 68)), // Red
                _ => new SolidColorBrush(Color.FromRgb(148, 163, 184)) // Slate
            };
        }
        return Brushes.White;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
}

public class BreakpointToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool hasBreakpoint = value is bool b && b;
        return hasBreakpoint 
            ? new SolidColorBrush(Color.FromRgb(239, 68, 68)) // Bright Red
            : new SolidColorBrush(Color.FromArgb(80, 148, 163, 184)); // Semi-transparent Slate
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
}

public class InputOutputBulletConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isInput = value is bool b && b;
        return isInput ? "🔵 " : "🟢 ";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
}

public class InputOutputBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isInput = value is bool b && b;
        return isInput 
            ? Brushes.Cyan
            : Brushes.LimeGreen;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
}
