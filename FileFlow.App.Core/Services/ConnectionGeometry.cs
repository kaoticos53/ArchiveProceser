using System;
using FileFlow.Sdk;

namespace FileFlow.App.Services;

/// <summary>
/// La geometría del cable entre dos puertos, en espacio de grafo y en el <see cref="Point"/> del Sdk.
///
/// <para><b>Qué reproduce</b>: el algoritmo exacto del control <c>Connection</c> de Nodify —una curva de
/// Bézier cúbica con las anclas de control que salen de <c>Spacing</c> y la distancia entre extremos—, medido
/// de su fuente (hito 216). El host Avalonia lo pinta porque Nodify lo dibuja; el host Uno lo necesitará para
/// <b>dibujar el mismo cable</b> (fase 3.1 del plan Uno) y para el <b>hit-testing</b> del menú contextual del
/// cable (fase 3.3): dos hosts, una sola geometría, y las pruebas que fijan que es la misma.</para>
///
/// <para><b>Por qué vive en el núcleo y no en un host</b>: es matemática pura sin framework — la misma regla
/// del <see cref="EditorViewportCalculator"/>. Y por eso está aquí, cualquier divergencia entre lo que pinta
/// el escritorio y lo que pinte el host Uno sería un defecto del producto y no una decisión de plataforma.</para>
/// </summary>
public static class ConnectionGeometry
{
    /// <summary>El ancho base del cuello de la curva, igual que la constante privada del control.</summary>
    private const double BaseOffset = 100d;

    /// <summary>Con qué velocidad crece el cuello con la distancia, igual que la constante privada del control.</summary>
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
    /// Las anclas de control de la curva que Nodify dibuja de <paramref name="source"/> a <paramref name="target"/>,
    /// con la orientación horizontal que usan los sockets del lienzo (entradas a la izquierda, salidas a la derecha).
    ///
    /// <para>Es el algoritmo de <c>Connection.GetBezierControlPoints</c> transcrito y probado: el cuello sale
    /// de <paramref name="spacing"/>, se suaviza cuando los extremos están cerca (no más de 100) y crece con la
    /// distancia (no más de 100 + √(ancho · 25)).</para>
    /// </summary>
    public static CubicBezier BezierControlPoints(Point source, Point target, double spacing = 45, FlowDirection direction = FlowDirection.Forward)
    {
        double sign = direction == FlowDirection.Forward ? 1d : -1d;

        Point start = new(source.X + (spacing * sign), source.Y);
        Point end = new(target.X - (spacing * sign), target.Y);

        double width = Math.Abs(target.X - source.X);
        double height = Math.Abs(target.Y - source.Y);

        // Suaviza la curva cuando la distancia es menor que el cuello base (los nodos pegados no hacen rulos).
        double smooth = Math.Min(BaseOffset, height);

        // El cuello nunca es menor que la mitad de la distancia horizontal, y crece despacio con ella.
        double offset = Math.Max(smooth, width / 2d);
        offset = Math.Min(BaseOffset + Math.Sqrt(width * OffsetGrowthRate), offset);

        Point exit = new(start.X + (offset * sign), start.Y);
        Point arrival = new(end.X - (offset * sign), end.Y);

        return new CubicBezier(start, exit, arrival, end);
    }

    /// <summary>
    /// El punto de la curva en <paramref name="t"/> (0 = origen, 1 = destino): lo que necesita el hit-testing
    /// para saber si el puntero pasó por encima del cable, y lo que un host sin Nodify necesita para dibujarla
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

    /// <summary>El cable de referencia con el que se declara y se prueba el estilo del lienzo.</summary>
    public const double DefaultSpacing = 45d;
}
