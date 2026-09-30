using System;
using FileFlow.Sdk;

namespace FileFlow.App.Services;

/// <summary>
/// La geometría del cable entre dos puertos, en espacio de grafo y en el <see cref="Point"/> del Sdk.
///
/// <para><b>Qué dibuja</b>: una Bézier cúbica que sale del <b>ancla</b> de un socket y llega al <b>ancla</b> del
/// otro, con los dos cuellos horizontales (los sockets del lienzo miran a los lados). La forma la fijó el
/// usuario en el hito 254 después de verla en la app: la primera versión transcribía el algoritmo del control
/// <c>Connection</c> de la versión anterior —una curva retirada de las anclas y unida a ellas por dos <b>tramos rectos</b>—
/// y el resultado, en la pantalla, se leía como una <b>Z</b>: dos bajíos rectos y una ese apretada en medio,
/// que no se parece a un cable. Aquí la curva nace en el ancla, así que el trazo no tiene ningún tramo recto y
/// el cable sale del socket ya curvando.</para>
///
/// <para>Del control se conserva lo que sí era bueno: el cuello sale <b>en horizontal</b> y su largo crece
/// despacio con la distancia, con el techo <c>100 + √(25 · ancho)</c>. Lo que se añade es el tope que el
/// control no tenía: el cuello nunca pasa de la mitad del hueco entre las anclas, así que los dos cuellos no
/// se cruzan y la curva no se dobla hacia atrás (el rulo con forma de «2» que se veía al dejar dos tarjetas
/// cerca).</para>
///
/// <para><b>Por qué vive en el núcleo y no en un host</b>: es matemática pura sin framework — la misma regla del
/// <see cref="EditorViewportCalculator"/>—, y por eso está aquí: el host Uno la dibuja y el hit-testing del cable
/// consume los mismos puntos, así que un defecto de forma o de medida aquí se ve en el lienzo.</para>
/// </summary>
public static class ConnectionGeometry
{
    /// <summary>El largo base del cuello, la constante con la que el control mide su cuello.</summary>
    private const double BaseOffset = 100d;

    /// <summary>Con qué velocidad crece el cuello con la distancia, igual que la constante del control.</summary>
    private const double OffsetGrowthRate = 25d;

    /// <summary>Los cuatro puntos de una Bézier cúbica: inicio, ancla de control de salida, ancla de control de llegada y fin.</summary>
    public readonly record struct CubicBezier(Point P0, Point P1, Point P2, Point P3);

    /// <summary>La dirección en la que el cable sale de su origen: hacia adelante (del origen al destino) o hacia atrás.</summary>
    public enum FlowDirection
    {
        Forward,
        Backward
    }

    /// <summary>
    /// La curva del cable de <paramref name="source"/> a <paramref name="target"/>, con la orientación
    /// horizontal que usan los sockets del lienzo (las entradas miran a la izquierda, las salidas a la derecha).
    ///
    /// <para><b>La curva nace y muere en las anclas</b>: <c>P0</c> es el centro del socket de origen y <c>P3</c>
    /// el del destino, así que el cable <b>toca</b> sus dos sockets. Sus dos puntos de control son horizontales
    /// —el cable sale y entra del socket sin torcerse— y su largo (<c>offset</c>) es lo que hace que la forma
    /// parezca un cable y no una Z: crece despacio con la distancia (techo <c>100 + √(25 · ancho)</c>) y nunca
    /// pasa de la <b>mitad del hueco</b> entre las anclas, que es lo que impide que los cuellos se crucen y la
    /// curva se doble hacia atrás.</para>
    ///
    /// <para>Con hueco de sobra y las anclas a la misma altura, la curva degenera en una recta tirante (un
    /// cable tenso); con las anclas apiladas (sin hueco horizontal) degenera en una vertical; en cualquier
    /// diagonal queda una ese suave <b>dentro</b> del hueco.</para>
    /// </summary>
    public static CubicBezier BezierControlPoints(Point source, Point target, FlowDirection direction = FlowDirection.Forward)
    {
        double sign = direction == FlowDirection.Forward ? 1d : -1d;

        double width = Math.Abs(target.X - source.X);

        // El cuello: la mitad del hueco es su tope, y el techo del control lo mantiene corto cuando el hueco
        // es enorme (sin él, dos tarjetas lejanas dibujarían un cuello larguísimo y el cable parecería recto
        // justo en el tramo donde más se nota).
        double ceiling = BaseOffset + Math.Sqrt(width * OffsetGrowthRate);
        double offset = Math.Min(ceiling, width / 2d);

        Point exit = new(source.X + (offset * sign), source.Y);
        Point arrival = new(target.X - (offset * sign), target.Y);

        return new CubicBezier(source, exit, arrival, target);
    }

    /// <summary>
    /// El punto de la curva en <paramref name="t"/> (0 = origen, 1 = destino): lo que necesita el hit-testing
    /// para saber si el puntero pasó por encima del cable, y lo que el host necesita para dibujarla
    /// por tramos.
    /// </summary>
    public static Point Interpolate(in CubicBezier curve, double t)
    {
        double u = 1d - t;
        double w0 = u * u * u;
        double w1 = 3d * t * u * u;
        double w2 = 3d * t * t * u;
        double w3 = t * t * t;

        return new Point(
            (curve.P0.X * w0) + (curve.P1.X * w1) + (curve.P2.X * w2) + (curve.P3.X * w3),
            (curve.P0.Y * w0) + (curve.P1.Y * w1) + (curve.P2.Y * w2) + (curve.P3.Y * w3));
    }

    /// <summary>
    /// La tangente de la curva en <paramref name="t"/>: hacia dónde apunta el cable en ese punto (la punta de
    /// una flecha direccional se dibuja contra este vector).
    /// </summary>
    public static Point Tangent(in CubicBezier curve, double t)
    {
        double u = 1d - t;
        double x =
            (3d * u * u * (curve.P1.X - curve.P0.X)) +
            (6d * t * u * (curve.P2.X - curve.P1.X)) +
            (3d * t * t * (curve.P3.X - curve.P2.X));
        double y =
            (3d * u * u * (curve.P1.Y - curve.P0.Y)) +
            (6d * t * u * (curve.P2.Y - curve.P1.Y)) +
            (3d * t * t * (curve.P3.Y - curve.P2.Y));

        return new Point(x, y);
    }

    /// <summary>
    /// La distancia del punto <paramref name="candidate"/> al cable, muestreando la curva: el hit-testing del
    /// menú contextual del cable (fase 3.3) pregunta esto con la tolerancia del trazo. El muestreo es uniforme
    /// porque una tolerancia de clic no necesita la distancia exacta, sí no falsar ni el dentro ni el fuera.
    /// </summary>
    public static double DistanceTo(in CubicBezier curve, Point candidate, int samples = 32)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(samples, 2);

        double best = double.MaxValue;

        for (int i = 0; i <= samples; i++)
        {
            Point point = Interpolate(curve, (double)i / samples);
            double dx = point.X - candidate.X;
            double dy = point.Y - candidate.Y;
            double distance = (dx * dx) + (dy * dy);

            if (distance < best)
            {
                best = distance;
            }
        }

        return Math.Sqrt(best);
    }

    /// <summary>
    /// El trazado del cable entre dos anclas, listo para dibujar: la Bézier que las une, con sus dos cuellos.
    /// </summary>
    /// <param name="Source">El ancla de salida: el centro dibujado del socket de origen.</param>
    /// <param name="Target">El ancla de llegada: el centro dibujado del socket de destino.</param>
    /// <param name="Curve">La Bézier, que ya empieza y termina en las anclas.</param>
    public readonly record struct WirePath(Point Source, Point Target, CubicBezier Curve)
    {
        /// <summary>El cuello de salida: el primer punto de control, a la derecha del ancla de origen.</summary>
        public Point Exit => Curve.P1;

        /// <summary>El cuello de llegada: el último punto de control, a la izquierda del ancla de destino.</summary>
        public Point Arrival => Curve.P2;

        /// <summary>
        /// Las cuatro paradas del trazo, en orden: <b>ancla de salida, cuello de salida, cuello de llegada y
        /// ancla de destino</b>. Son los cuatro puntos de una Bézier cúbica y el host las dibuja como UNA curva:
        /// no hay ningún tramo recto que unir, y por eso el cable sale del socket ya curvando en vez de mostrar
        /// el bajío recto que se leía como una Z.
        ///
        /// <para><b>El primero y el último son las anclas</b>, y eso es contrato: un host que abriera la figura
        /// en el cuello de salida y la cerrara en el de llegada dibujaría un cable separado de sus dos sockets.</para>
        /// </summary>
        public IReadOnlyList<Point> Trace => [Source, Exit, Arrival, Target];
    }

    /// <summary>El trazado del cable entre dos anclas, del que sale y al que llega.</summary>
    public static WirePath BuildWire(
        Point source,
        Point target,
        FlowDirection direction = FlowDirection.Forward)
        => new(source, target, BezierControlPoints(source, target, direction));
}
