using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace FileFlow.App.Converters;

public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool boolValue = value is bool b && b;
        return !boolValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }
}

public class InverseBooleanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }
}

public class NullToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value != null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
}

public class InverseNullToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value == null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
}

public class BooleanToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool flag && flag;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool flag && flag;
    }
}

public class BooleanToGridLengthConverter : IValueConverter
{
    public double DefaultWidth { get; set; } = 360;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
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

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
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
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string? valStr = value?.ToString();
        string? paramStr = parameter?.ToString();
        return string.Equals(valStr, paramStr, StringComparison.OrdinalIgnoreCase);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
}

public class StringNotEmptyToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string? str = value?.ToString();
        return !string.IsNullOrWhiteSpace(str);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
}

public class EnumToBooleanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return false;
        string checkValue = value.ToString()!;
        string targetValue = parameter.ToString()!;
        return string.Equals(checkValue, targetValue, StringComparison.OrdinalIgnoreCase);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter != null)
        {
            if (targetType.IsEnum)
            {
                return Enum.Parse(targetType, parameter.ToString()!, true);
            }
            return parameter;
        }
        return BindingOperations.DoNothing;
    }
}

public class StringEqualsToBooleanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string? valStr = value?.ToString();
        string? paramStr = parameter?.ToString();
        return string.Equals(valStr, paramStr, StringComparison.OrdinalIgnoreCase);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
}

public class StringsEqualMultiConverter : IMultiValueConverter
{
    public object? Convert(IList<object?>? values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values == null || values.Count < 2) return false;
        string? first = values[0]?.ToString();
        string? second = values[1]?.ToString();
        return string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
    }
}

public class BooleanToBrushConverter : IValueConverter
{
    public Avalonia.Media.IBrush TrueBrush { get; set; } = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#10B981"));
    public Avalonia.Media.IBrush FalseBrush { get; set; } = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#64748B"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool flag = value is bool b && b;
        return flag ? TrueBrush : FalseBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
}

