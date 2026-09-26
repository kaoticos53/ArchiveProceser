using System;
using System.Collections.Concurrent;
using System.Security;
using Material.Icons;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;

namespace FileFlow.App.Uno.Platform;

/// <summary>
/// La iconografía del lienzo Uno con <b>los mismos datos</b> que el escritorio: el paquete
/// <c>Material.Icons</c> (ya referencia del núcleo, donde vive el enum <see cref="MaterialIconKind"/> que
/// exponen los ViewModels) guarda cada icono como <b>path data SVG</b>, no como font — así que la
/// mitigación del plan («el font Material Design Icons como recurso del host») se ajusta a lo medido:
/// ningún paquete nuevo ni TTF, sólo este conversor que resuelve el path del icono y lo envuelve en la
/// <see cref="StreamGeometry"/> que un <see cref="PathIcon"/> de WinUI pinta.
///
/// <para><b>Cómo pinta WinUI</b>: el <see cref="PathIcon"/> estira su geometría al tamaño del control
/// (estiramiento uniforme, como el <c>MaterialIcon</c> de Avalonia ajusta al suyo), así que los datos en
/// su rejilla de diseño de 24×24 entran tal cual — sin transformaciones. El color es el
/// <c>Foreground</c> del control, igual que en el escritorio.</para>
///
/// <para><b>Cómo se parsea</b>: WinUI no expone <c>Geometry.Parse</c> (el mini-lenguaje de path lo
/// procesa el propio parser XAML), así que el parseo va por <see cref="XamlReader.Load"/> de un fragmento
/// mínimo — el patrón canónico de la plataforma. El string del path se resuelve una vez por icono
/// (cacheado); la geometría se construye al consumir, en el hilo de la UI (los tipos de XAML de WinUI
/// tienen afinidad de hilo y el binding siempre evalúa en ella).</para>
///
/// <para>Es un <see cref="IValueConverter"/> para que el XAML lo cante contra el binding
/// <c>{Binding Icon}</c> del ViewModel: el mismo modo de consumo que el escritorio
/// (<c>materialIcons:MaterialIcon Kind="{Binding Icon}"</c>), sin código por tarjeta.</para>
/// </summary>
public class MaterialIconKindToGeometryConverter : IValueConverter
{
    /// <summary>El path de cada icono, resuelto una vez: los datos son inmutables y el cache es seguro entre hilos.</summary>
    private static readonly ConcurrentDictionary<MaterialIconKind, string> PathDataCache = new();

    /// <summary>La instancia que el XAML del host cita (StaticResource sobre este singleton).</summary>
    public static readonly MaterialIconKindToGeometryConverter Instance = new();

    /// <summary>
    /// La geometría de un icono fijo (los glifos que la tarjeta pinta siempre: consola, alerta,
    /// contadores): el adaptador de la tarjeta los expone como propiedades para que el XAML los enlace
    /// sin un <c>Source</c> auto-referencial.
    /// </summary>
    public static Microsoft.UI.Xaml.Media.Geometry ToGeometry(MaterialIconKind kind)
        => (Microsoft.UI.Xaml.Media.Geometry)Instance.Convert(kind, typeof(Microsoft.UI.Xaml.Media.Geometry), null, "en-US")
           ?? Microsoft.UI.Xaml.Media.Geometry.Empty;

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not MaterialIconKind kind)
        {
            return null;
        }

        string pathData = PathDataCache.GetOrAdd(kind, MaterialIconDataProvider.GetData);
        if (string.IsNullOrEmpty(pathData))
        {
            return null;
        }

        // El mini-lenguaje de path sólo contiene letras, dígitos, puntos, comas, guiones y espacios;
        // el escape es cinturón de seguridad contra datos futuros del paquete.
        var fragment = (Microsoft.UI.Xaml.Shapes.Path)XamlReader.Load(
            "<Path xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" " +
            "Data=\"" + SecurityElement.Escape(pathData) + "\" />");

        // La geometría que el parser produce por la propiedad Data NO es asignable a Path.Data en este
        // host (el sondeo midió ArgumentException «Value does not fall within the expected range» hasta
        // sobre un Path recién creado), pero sus figuras sí se leen. Se clona figura a figura en una
        // PathGeometry construida por código — la misma receta que ya pintan los cables del lienzo — y
        // esa clon es la que se devuelve. Sin clon, un binding a este valor moriría en silencio o un
        // asignador directo reventaría en runtime.
        return CloneAsCodeBuiltGeometry(fragment.Data);
    }

    /// <summary>Una geometría equivalente construida por código, asignable a Path.Data (medido por el sondeo).</summary>
    private static Microsoft.UI.Xaml.Media.Geometry CloneAsCodeBuiltGeometry(Microsoft.UI.Xaml.Media.Geometry? parsed)
    {
        if (parsed is not Microsoft.UI.Xaml.Media.PathGeometry source)
        {
            return new Microsoft.UI.Xaml.Media.PathGeometry();
        }

        var clone = new Microsoft.UI.Xaml.Media.PathGeometry();
        foreach (var figure in source.Figures)
        {
            var copied = new Microsoft.UI.Xaml.Media.PathFigure
            {
                StartPoint = figure.StartPoint,
                IsClosed = figure.IsClosed,
                IsFilled = figure.IsFilled
            };

            foreach (var segment in figure.Segments)
            {
                Microsoft.UI.Xaml.Media.PathSegment? copy = segment switch
                {
                    Microsoft.UI.Xaml.Media.LineSegment line => new Microsoft.UI.Xaml.Media.LineSegment { Point = line.Point },
                    Microsoft.UI.Xaml.Media.PolyLineSegment polyLine => CopyPoly(polyLine.Points),
                    Microsoft.UI.Xaml.Media.BezierSegment bezier => new Microsoft.UI.Xaml.Media.BezierSegment
                    {
                        Point1 = bezier.Point1, Point2 = bezier.Point2, Point3 = bezier.Point3
                    },
                    Microsoft.UI.Xaml.Media.PolyBezierSegment polyBezier => CopyPolyAsBezier(polyBezier.Points),
                    Microsoft.UI.Xaml.Media.QuadraticBezierSegment quadratic => new Microsoft.UI.Xaml.Media.QuadraticBezierSegment
                    {
                        Point1 = quadratic.Point1, Point2 = quadratic.Point2
                    },
                    Microsoft.UI.Xaml.Media.PolyQuadraticBezierSegment polyQuadratic => CopyPolyAsQuadratic(polyQuadratic.Points),
                    Microsoft.UI.Xaml.Media.ArcSegment arc => new Microsoft.UI.Xaml.Media.ArcSegment
                    {
                        Point = arc.Point,
                        Size = arc.Size,
                        RotationAngle = arc.RotationAngle,
                        IsLargeArc = arc.IsLargeArc,
                        SweepDirection = arc.SweepDirection
                    },
                    _ => null // segmento que no se sabe clonar: mejor un hueco que un crash
                };

                if (copy is not null)
                {
                    copied.Segments.Add(copy);
                }
            }

            clone.Figures.Add(copied);
        }

        return clone;
    }

    private static Microsoft.UI.Xaml.Media.PolyLineSegment CopyPoly(Microsoft.UI.Xaml.Media.PointCollection points)
    {
        var copy = new Microsoft.UI.Xaml.Media.PolyLineSegment();
        foreach (var point in points)
        {
            copy.Points.Add(point);
        }

        return copy;
    }

    private static Microsoft.UI.Xaml.Media.PolyBezierSegment CopyPolyAsBezier(Microsoft.UI.Xaml.Media.PointCollection points)
    {
        var copy = new Microsoft.UI.Xaml.Media.PolyBezierSegment();
        foreach (var point in points)
        {
            copy.Points.Add(point);
        }

        return copy;
    }

    private static Microsoft.UI.Xaml.Media.PolyQuadraticBezierSegment CopyPolyAsQuadratic(Microsoft.UI.Xaml.Media.PointCollection points)
    {
        var copy = new Microsoft.UI.Xaml.Media.PolyQuadraticBezierSegment();
        foreach (var point in points)
        {
            copy.Points.Add(point);
        }

        return copy;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException("La geometría del icono no vuelve a convertirse en Kind.");
}
