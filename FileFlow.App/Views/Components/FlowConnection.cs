using System;
using Avalonia;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using FileFlow.App.Services;
using AvaloniaPoint = Avalonia.Point;

namespace FileFlow.App.Views.Components;

/// <summary>
/// El cable del lienzo del <b>escritorio</b>: una Bézier cúbica entre dos anclas, con la geometría
/// <b>compartida del núcleo</b> (<see cref="ConnectionGeometry"/>, la misma que dibuja el host Uno).
///
/// <para><b>Por qué no lo dibuja el control de conexión de Nodify.</b> El control trae su propia curva
/// —una Bézier retirada de las anclas y unida a ellas por dos <b>tramos rectos</b>, con el cuello
/// <c>Spacing</c> fijo—, así que el mismo flujo se veía distinto en los dos hosts: el escritorio dibujaba
/// una ese apretada con dos bajíos rectos (una <b>Z</b>) y el host Uno un cable que nace curvando en el
/// socket. Aquí el control sólo pone el <b>envoltorio de Avalonia</b> (un <see cref="Shape"/> cuyo
/// <c>DefiningGeometry</c> sale del trazador compartido); la forma la decide el núcleo, y por eso un
/// defecto de forma se ve (o se mide) en los dos hosts a la vez.</para>
///
/// <para><b>Lo que sí se conserva del control</b>, porque era bueno: el cuello sale en horizontal, crece
/// despacio con la distancia y nunca pasa de la mitad del hueco entre las anclas (lo que impide que los
/// dos cuellos se crucen y el cable se doble hacia atrás). Y lo que el lienzo usaba del control
/// —<c>Stroke</c>, <c>StrokeThickness</c>, <c>StrokeDashArray</c>, <c>Cursor</c>, las clases por familia
/// de tipo y el menú contextual— sigue funcionando igual: es un <see cref="Shape"/>, y el color de cada
/// familia de tipo lo siguen poniendo los estilos del tema.</para>
/// </summary>
public class FlowConnection : Shape
{
    /// <summary>El ancla de salida: el centro dibujado del socket de origen, en unidades del lienzo.</summary>
    public static readonly StyledProperty<AvaloniaPoint> SourceProperty =
        AvaloniaProperty.Register<FlowConnection, AvaloniaPoint>(nameof(Source));

    /// <summary>El ancla de llegada: el centro dibujado del socket de destino.</summary>
    public static readonly StyledProperty<AvaloniaPoint> TargetProperty =
        AvaloniaProperty.Register<FlowConnection, AvaloniaPoint>(nameof(Target));

    /// <summary>
    /// De qué lado sale el cable: hacia adelante (del origen al destino) o hacia atrás. Es el flag del
    /// núcleo, no el del control de Nodify: la forma la decide <see cref="ConnectionGeometry"/>, así que
    /// su vocabulario es el que manda aquí. Un cable en curso que se arrastra <b>desde una entrada</b> va
    /// hacia atrás.
    /// </summary>
    public static readonly StyledProperty<ConnectionGeometry.FlowDirection> DirectionProperty =
        AvaloniaProperty.Register<FlowConnection, ConnectionGeometry.FlowDirection>(nameof(Direction));

    static FlowConnection()
    {
        // Las anclas y la dirección son la geometría entera: al cambiar cualquiera, la figura se rehace
        // (es el mismo contrato que `Data` en un `Path`, pero imposible de sustituir desde fuera).
        AffectsGeometry<FlowConnection>(SourceProperty, TargetProperty, DirectionProperty);
    }

    public AvaloniaPoint Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public AvaloniaPoint Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    public ConnectionGeometry.FlowDirection Direction
    {
        get => GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    /// <summary>
    /// La figura del cable: <b>una sola Bézier</b>, del ancla de salida al ancla de destino, con los dos
    /// cuellos como puntos de control. Los puntos se piden al núcleo en unidades <c>Sdk.Point</c> y se
    /// proyectan con la única traducción permitida (<see cref="SdkPointProjection"/>).
    /// </summary>
    protected override Geometry? CreateDefiningGeometry()
    {
        ConnectionGeometry.WirePath wire = ConnectionGeometry.BuildWire(
            Source.ToSdk(),
            Target.ToSdk(),
            Direction);

        var figure = new PathFigure
        {
            StartPoint = wire.Source.ToAvalonia(),
            IsFilled = false,
            IsClosed = false
        };

        figure.Segments!.Add(new BezierSegment
        {
            Point1 = wire.Exit.ToAvalonia(),
            Point2 = wire.Arrival.ToAvalonia(),
            Point3 = wire.Target.ToAvalonia()
        });

        return new PathGeometry { Figures = new PathFigures { figure } };
    }
}
