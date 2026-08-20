using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FileFlow.App.Converters;

public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool boolValue = value is bool b && b;
        return boolValue ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility v && v != Visibility.Visible;
    }
}

public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }
}
