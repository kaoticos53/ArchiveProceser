using System;
using FileFlow.Sdk;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace FileFlow.App.Uno.Platform;

/// <summary>
/// Los conversores de presentación de la tarjeta de nodo, con <b>los mismos valores</b> que sus hermanos
/// de la versión anterior (<c>FileFlow.App/Converters</c>): el mapa estado de ejecución → color, el LED del
/// breakpoint, y los formatos de duración y bytes. La semántica se mide en los conversores del host original;
/// aquí sólo cambia el tipo de pincel del framework. Cuando la fase 3.5 centralice la presentación,
/// la mitad portable de estas tablas viajará al núcleo como ya hizo la geometría.
/// </summary>
public static class NodeCardConverters
{
    // Los mismos RGB que NodeExecutionStatusToBrushConverter y BreakpointToBrushConverter de la versión anterior.
    public static Windows.UI.Color StatusColor(NodeExecutionStatus status) => status switch
    {
        NodeExecutionStatus.Running => FromRgb(0xA8, 0x55, 0xF7),        // Purple
        NodeExecutionStatus.Completed => FromRgb(0x10, 0xB9, 0x81),      // Emerald
        NodeExecutionStatus.PausedOnError or NodeExecutionStatus.Faulted => FromRgb(0xEF, 0x44, 0x44), // Red
        NodeExecutionStatus.PausedAtBreakpoint => FromRgb(0xF5, 0x9E, 0x0B), // Amber
        _ => Microsoft.UI.Colors.Transparent
    };

    public static Windows.UI.Color BreakpointColor(bool hasBreakpoint) => hasBreakpoint
        ? FromRgb(0xEF, 0x44, 0x44)                                       // Bright Red
        : FromArgb(80, 0x94, 0xA3, 0xB8);                                 // Semi-transparent Slate

    /// <summary>El mismo formato que DurationMsToTextConverter: µs/ms/s/min con las mismas cotas.</summary>
    public static string FormatDuration(double ms) => ms switch
    {
        < 0 => string.Empty,
        < 1.0 => $"{ms * 1000:F0} µs",
        < 1_000.0 => $"{ms:F1} ms",
        < 60_000.0 => $"{ms / 1000.0:F2} s",
        _ => $"{ms / 60_000.0:F1} min"
    };

    /// <summary>El mismo formato que BytesToTextConverter: B/KB/MB/GB con las mismas cotas.</summary>
    public static string FormatBytes(long bytes) => bytes switch
    {
        < 0 => string.Empty,
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):F2} MB",
        _ => $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB"
    };

    private static Windows.UI.Color FromRgb(byte r, byte g, byte b) => FromArgb(255, r, g, b);

    private static Windows.UI.Color FromArgb(byte a, byte r, byte g, byte b)
        => Microsoft.UI.ColorHelper.FromArgb(a, r, g, b);
}

/// <summary>Estado de ejecución del nodo → pincel del LED de la línea de contexto.</summary>
public class NodeExecutionStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => new SolidColorBrush(value is NodeExecutionStatus status
            ? NodeCardConverters.StatusColor(status)
            : Microsoft.UI.Colors.Transparent);

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>HasBreakpoint → pincel del LED circular del breakpoint.</summary>
public class BreakpointToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => new SolidColorBrush(NodeCardConverters.BreakpointColor(value is bool b && b));

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value;
}

/// <summary>IsLoggingEnabled → pincel del LED cuadrado de logging (cian cuando está activo).</summary>
public class LoggingToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => new SolidColorBrush(value is bool b && b
            ? Microsoft.UI.ColorHelper.FromArgb(255, 0x06, 0xB6, 0xD4)          // Cyan
            : Microsoft.UI.ColorHelper.FromArgb(80, 0x94, 0xA3, 0xB8));          // Semi-transparent Slate

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value;
}

/// <summary>RollingLatencyMs (double) → texto compacto de duración.</summary>
public class DurationMsToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => NodeCardConverters.FormatDuration(value is double d ? d : -1);

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value;
}

/// <summary>Bytes atribuidos (long) → texto compacto de tamaño.</summary>
public class BytesToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => NodeCardConverters.FormatBytes(value is long l ? l : -1);

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value;
}
