using System;

namespace FileFlow.Sdk;

/// <summary>
/// Punto 2D en coordenadas de lienzo, neutro de framework. Los ViewModels del editor hablan en
/// <see cref="Point"/> y cada host proyecta hacia su tipo visual (Avalonia.Point, Windows.Foundation.Point)
/// en el borde, de modo que la lógica del lienzo pueda vivir en un ensamblado sin UI.
/// </summary>
public readonly record struct Point(double X, double Y)
{
    /// <summary>El origen del lienzo (0, 0).</summary>
    public static Point Zero => default;

    public static Point operator +(Point a, Point b) => new(a.X + b.X, a.Y + b.Y);
    public static Point operator -(Point a, Point b) => new(a.X - b.X, a.Y - b.Y);

    /// <summary>Distancia euclídea hasta otro punto del lienzo.</summary>
    public double DistanceTo(Point other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public override string ToString() => FormattableString.Invariant($"{X:0.###}, {Y:0.###}");
}
