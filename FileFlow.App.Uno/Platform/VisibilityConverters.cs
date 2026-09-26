using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace FileFlow.App.Uno.Platform;

/// <summary>
/// WinUI no convierte <c>bool</c> a <see cref="Visibility"/> por sí solo (el escritorio lo hace con su
/// propia coerción de <c>IsVisible</c>): los bindings de la tarjeta usan estos dos conversores — uno para
/// «visible cuando true» y el inverso para «visible cuando false» (el <c>!IsVisible</c> del escritorio).
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool b && b ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is Visibility v && v == Visibility.Visible;
}

/// <summary>El inverso: el equivalente del <c>!IsVisible</c> del editor de escritorio.</summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool b && b ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is Visibility v && v == Visibility.Collapsed;
}
