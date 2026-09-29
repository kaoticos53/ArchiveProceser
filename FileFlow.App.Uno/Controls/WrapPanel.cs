using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace FileFlow.App.Uno.Controls;

/// <summary>
/// El panel de las tiras que no caben en una línea (hito 272): reparte a sus hijos en filas del ancho
/// disponible, con la separación que declare <see cref="Spacing"/>.
///
/// <para><b>Por qué existe</b>: dos tiras del host se declaraban en un <see cref="StackPanel"/>
/// horizontal —las quince categorías del cajón y las seis secciones de Ajustes— y un <c>StackPanel</c> no
/// parte la línea: lo que sobraba se recortaba contra el borde y quedaba <b>fuera del alcance del
/// ratón</b> (medido: once chips y la pestaña «Actualizaciones» con rectángulo vacío, sin scroll con el
/// que alcanzarlos y con la rueda sin efecto). El escritorio resuelve su filtro de categorías con un
/// desplegable, que siempre cabe; el host conserva las chips, pero partidas en filas para que TODAS se
/// puedan pulsar.</para>
///
/// <para>Panel propio y no <c>ItemsWrapGrid</c>: aquél es un panel de elementos VIRTUALIZADOS —pensado
/// para los contenedores de un <c>ListViewBase</c>— y aquí los hijos son fijos (chips y botones de
/// sección ya materializados); lo único que hace falta es medir y repartir filas, que es lo que este
/// archivo escribe y lo que su sonda mide.</para>
/// </summary>
public sealed class WrapPanel : Panel
{
    /// <summary>La separación horizontal y vertical entre hijos, en píxeles lógicos.</summary>
    public static readonly DependencyProperty SpacingProperty =
        DependencyProperty.Register(nameof(Spacing), typeof(double), typeof(WrapPanel), new PropertyMetadata(0d));

    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double limit = double.IsFinite(availableSize.Width) ? availableSize.Width : double.PositiveInfinity;
        double line = 0, lineHeight = 0, used = 0, height = 0;
        bool first = true;

        foreach (var child in Children)
        {
            child.Measure(new Size(limit, double.PositiveInfinity));
            Size size = child.DesiredSize;

            if (!first && line + Spacing + size.Width > limit)
            {
                used = Math.Max(used, line);
                height += lineHeight + Spacing;
                line = size.Width;
                lineHeight = size.Height;
            }
            else
            {
                line = first ? size.Width : line + Spacing + size.Width;
                lineHeight = Math.Max(lineHeight, size.Height);
            }

            first = false;
        }

        if (!first)
        {
            used = Math.Max(used, line);
            height += lineHeight;
        }

        return new Size(double.IsFinite(limit) ? Math.Min(used, limit) : used, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double limit = finalSize.Width;
        double x = 0, y = 0, lineHeight = 0;
        bool first = true;

        foreach (var child in Children)
        {
            Size size = child.DesiredSize;
            if (!first && x + Spacing + size.Width > limit)
            {
                x = 0;
                y += lineHeight + Spacing;
                lineHeight = 0;
                first = true;
            }

            double left = first ? 0 : x + Spacing;
            child.Arrange(new Rect(left, y, size.Width, size.Height));
            x = left + size.Width;
            lineHeight = Math.Max(lineHeight, size.Height);
            first = false;
        }

        return finalSize;
    }
}
