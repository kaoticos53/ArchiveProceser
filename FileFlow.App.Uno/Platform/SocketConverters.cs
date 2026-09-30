using System;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Sdk;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace FileFlow.App.Uno.Platform;

/// <summary>
/// La matriz de estado del <b>socket de puerto</b> de la tarjeta Uno: la decisión (qué color habla cada
/// tipo, cómo se comporta el arrastre) vive en el núcleo — <see cref="PortPalette"/>, los mismos bytes
/// que pinta el escritorio — y este fichero sólo la traduce a <see cref="Windows.UI.Color"/> y pinceles
/// de WinUI. WinUI no puede seleccionar estilos por combinación de bools del ViewModel, así que cada
/// conversor calcula el valor final.
///
/// <para>Semántica (la del escritorio): la <b>forma</b> comunica el tipo (círculo=texto, cuadrado=archivo,
/// triángulo=booleano, rombo=numérico), el <b>borde</b> el color del tipo, el <b>relleno</b> el estado
/// (libre=hueco, conectado=color del tipo), y durante un arrastre el origen brilla y el resto se atenúa.</para>
/// </summary>
public static class SocketMatrix
{
    private static Windows.UI.Color Rgb((byte R, byte G, byte B) c)
        => Microsoft.UI.ColorHelper.FromArgb(255, c.R, c.G, c.B);

    public static Windows.UI.Color TypeColor(PortTypeKind kind) => Rgb(PortPalette.TypeColor(kind));

    public static Windows.UI.Color FreeFill => Rgb(PortPalette.FreeFill);

    public static Windows.UI.Color BorderColor(PortViewModel? port)
        => port is null
            ? Microsoft.UI.Colors.Transparent
            : (port.IsDragSource ? Rgb(PortPalette.DragSourceBorder) : TypeColor(port.TypeKind));

    public static Windows.UI.Color FillColor(PortViewModel? port)
    {
        if (port is null)
        {
            return Microsoft.UI.Colors.Transparent;
        }

        if (port.IsDragSource)
        {
            return Rgb(PortPalette.DragSourceFill);
        }

        return port.IsConnected ? TypeColor(port.TypeKind) : FreeFill;
    }

    public static Windows.UI.Color TriangleFill(PortViewModel? port)
    {
        if (port is null)
        {
            return Microsoft.UI.Colors.Transparent;
        }

        if (port.IsDragSource)
        {
            return Rgb(PortPalette.DragSourceFill);
        }

        return port.IsConnected ? TypeColor(port.TypeKind) : FreeFill;
    }

    public static double Opacity(PortViewModel? port)
        => port?.IsDimmedDuringDrag == true ? 0.3 : 1.0;

    public static double LabelOpacity(PortViewModel? port)
        => port?.IsDimmedDuringDrag == true ? 0.35 : 1.0;
}

/// <summary>El color de borde/stroke del socket según tipo y estado de arrastre.</summary>
public class SocketColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language)
        => new SolidColorBrush(SocketMatrix.BorderColor(value as PortViewModel));

    public object ConvertBack(object? value, Type targetType, object? parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>El relleno del socket: libre=hueco, conectado=color del tipo, origen de arrastre=primario.</summary>
public class SocketFillConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language)
        => new SolidColorBrush(SocketMatrix.FillColor(value as PortViewModel));

    public object ConvertBack(object? value, Type targetType, object? parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>El relleno del triángulo booleano, con la misma matriz.</summary>
public class SocketTriangleFillConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language)
        => new SolidColorBrush(SocketMatrix.TriangleFill(value as PortViewModel));

    public object ConvertBack(object? value, Type targetType, object? parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>La opacidad del socket durante un arrastre (el resto de puertos se atenúa a 0.3).</summary>
public class SocketStateOpacityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language)
        => SocketMatrix.Opacity(value as PortViewModel);

    public object ConvertBack(object? value, Type targetType, object? parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>El radio de esquina del socket según su forma (círculo píldora, rombo reducido, resto 3).</summary>
public class SocketRadiusConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language)
    {
        var shape = value is PortViewModel port ? port.SocketShape : PortSocketShape.Square;
        return shape switch
        {
            PortSocketShape.Circle => new CornerRadius(999),
            PortSocketShape.Diamond => new CornerRadius(2),
            _ => new CornerRadius(3)
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>La opacidad de la etiqueta del puerto (se atenúa menos que el socket: 0.35).</summary>
public class SocketLabelOpacityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language)
        => SocketMatrix.LabelOpacity(value as PortViewModel);

    public object ConvertBack(object? value, Type targetType, object? parameter, string language)
        => throw new NotSupportedException();
}
