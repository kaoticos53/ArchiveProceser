using System;
using System.ComponentModel;
using FileFlow.App.Services;
using FileFlow.App.Uno.Platform;
using FileFlow.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileFlow.App.Uno.Controls;

/// <summary>
/// El adaptador de lectura que la tarjeta del lienzo Uno consume por cada <see cref="NodeViewModel"/> del
/// núcleo: expone el título, la categoría y la descripción, y la <see cref="Position"/> —el punto del
/// framework, proyectado con <see cref="UnoPointConverter"/> (la regla del hito 217: ninguna lectura directa
/// de <c>Location.X/.Y</c>, el binding pasa por el conversor)—.
///
/// <para><b>Modo lectura</b>: suscribe los cambios del nodo para refrescar (título, posición, ancho) y no
/// escribe nada de vuelta; el arrastre y la edición llegan en las fases 3.2/3.3.</para>
/// </summary>
public sealed class NodeCardViewModel : INotifyPropertyChanged
{
    private readonly NodeViewModel _node;

    public NodeCardViewModel(NodeViewModel node)
    {
        _node = node ?? throw new ArgumentNullException(nameof(node));
        _node.PropertyChanged += OnNodePropertyChanged;
    }

    public NodeViewModel Node => _node;

    public string Title => _node.Title;

    public string Category => _node.Category;

    public string Description => _node.Description;

    /// <summary>
    /// La posición de la tarjeta en el lienzo, ya proyectada: el code-behind la aplica con el conversor,
    /// como exige la guardia de geometría (los enlaces de WinUI no traducen puntos por sí solos).
    /// </summary>
    public Windows.Foundation.Point Position => ProjectLocation();

    /// <summary>El ancho medido o de referencia de la tarjeta, para el encuadre.</summary>
    public double Width => _node.Width > 0 ? _node.Width : 220;

    /// <summary>
    /// La proyección del punto del grafo, pasando por <b>el mismo conversor</b> que el XAML cita
    /// ({StaticResource UnoPointConverter}): el aplicador de posiciones y el enlace comparten pieza,
    /// así que la guardia defiende las dos mitades con la misma regla.
    /// </summary>
    private Windows.Foundation.Point ProjectLocation()
    {
        var converter = UnoPointConverter.Instance;
        return (Windows.Foundation.Point)converter.Convert(_node.Location, typeof(Windows.Foundation.Point), null!, "en-US");
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NodeViewModel.Location) or nameof(NodeViewModel.Title)
            or nameof(NodeViewModel.Category) or nameof(NodeViewModel.Description) or nameof(NodeViewModel.Width))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
