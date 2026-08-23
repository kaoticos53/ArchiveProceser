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

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value != null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => DependencyProperty.UnsetValue;
}

public class InverseNullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value == null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => DependencyProperty.UnsetValue;
}

public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool b = value is bool flag && flag;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility v && v == Visibility.Visible;
    }
}

public class BooleanToGridLengthConverter : IValueConverter
{
    public double DefaultWidth { get; set; } = 360;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isVisible = value is bool b && b;
        if (!isVisible)
        {
            return new GridLength(0, GridUnitType.Pixel);
        }

        if (parameter != null && double.TryParse(parameter.ToString(), out double parsedWidth))
        {
            return new GridLength(parsedWidth, GridUnitType.Pixel);
        }

        return new GridLength(DefaultWidth, GridUnitType.Pixel);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is GridLength gl)
        {
            return gl.Value > 0;
        }
        return false;
    }
}

public class StringEqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string? valStr = value?.ToString();
        string? paramStr = parameter?.ToString();

        if (string.Equals(valStr, paramStr, StringComparison.OrdinalIgnoreCase))
        {
            return Visibility.Visible;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => DependencyProperty.UnsetValue;
}

public class StringNotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string? str = value?.ToString();
        return !string.IsNullOrWhiteSpace(str) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => DependencyProperty.UnsetValue;
}

public class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return false;
        string checkValue = value.ToString()!;
        string targetValue = parameter.ToString()!;
        return string.Equals(checkValue, targetValue, StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter != null)
        {
            if (targetType.IsEnum)
            {
                return Enum.Parse(targetType, parameter.ToString()!, true);
            }
            return parameter;
        }
        return Binding.DoNothing;
    }
}
