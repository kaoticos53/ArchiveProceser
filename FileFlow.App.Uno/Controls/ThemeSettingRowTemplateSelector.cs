using FileFlow.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileFlow.App.Uno.Controls;

/// <summary>
/// Elige la plantilla de una fila del EDITOR DE TEMAS por el TIPO de su view model.
///
/// <para>Es la pieza que el escritorio resuelve con sus <c>DataTemplates</c> por tipo (Avalonia los
/// selecciona solo); WinUI no elige plantilla por tipo de dato, así que el host lo declara explícitamente.
/// <b>No conoce ningún ajuste concreto</b>: mira la clase de la fila, que es lo que el catálogo del núcleo
/// decide, y devuelve una de las tres plantillas. Añadir un ajuste al catálogo no toca este fichero.</para>
/// </summary>
public sealed partial class ThemeSettingRowTemplateSelector : DataTemplateSelector
{
    /// <summary>Plantilla de las filas de color (hex + muestra).</summary>
    public DataTemplate? ColorTemplate { get; set; }

    /// <summary>Plantilla de las filas numéricas (deslizador con su rango y su paso).</summary>
    public DataTemplate? NumberTemplate { get; set; }

    /// <summary>Plantilla de las filas de elección (desplegable de opciones).</summary>
    public DataTemplate? ChoiceTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item) => item switch
    {
        ThemeColorRowViewModel => ColorTemplate,
        ThemeChoiceRowViewModel => ChoiceTemplate,
        ThemeNumberRowViewModel => NumberTemplate,
        _ => null,
    };

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) =>
        SelectTemplateCore(item);
}
