using System;

namespace FileFlow.Sdk;

/// <summary>
/// Tamaño 2D en coordenadas de lienzo, neutro de framework. Pareja del <see cref="Point"/> de Sdk:
/// los ViewModels del editor hablan en estas unidades y cada host proyecta hacia su tipo visual en el borde.
/// </summary>
public readonly record struct Size(double Width, double Height)
{
    /// <summary>Tamaño vacío (0, 0).</summary>
    public static Size Empty => default;

    /// <summary>Indica si el tamaño tiene área positiva (un tamaño sin medir no restringe cálculos de encuadre).</summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public override string ToString() => FormattableString.Invariant($"{Width:0.###}x{Height:0.###}");
}
