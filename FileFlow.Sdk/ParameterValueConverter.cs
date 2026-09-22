using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FileFlow.Sdk;

/// <summary>
/// Implementación única de la conversión de valores de parámetros a tipos .NET, tolerante con las tres
/// formas en que un parámetro llega a un nodo: valores de la UI, <see cref="JsonElement"/> (perfiles
/// guardados) y cadenas escritas a mano.
///
/// Es la base compartida de <see cref="ParameterHelper"/> y de <c>FlowNodeBase.GetParameter&lt;T&gt;</c>:
/// ambas rutas delegan aquí, de modo que leer un parámetro con el ayudante tipado o con el helper
/// estático es, por construcción, lo mismo. Antes de esta clase no lo era —<c>GetParameter&lt;T&gt;</c> usaba
/// <c>Convert.ChangeType</c>, que no entiende <see cref="JsonElement"/> ni los números embebidos en
/// texto— y por eso el ayudante tipado tenía cero llamadas en los nodos.
///
/// <para><b>Contrato fijado por tests</b> (<c>ParameterValueConverterTests</c>):</para>
/// <list type="bullet">
/// <item><b>Invariante de cultura.</b> Todo parseo usa <see cref="CultureInfo.InvariantCulture"/>: el
/// valor de un parámetro no puede cambiar según el idioma del sistema. Los separadores de miles no se
/// aceptan (ni <c>"1,000"</c> ni <c>"1.000"</c> parsean), que es lo que .NET hace de todos modos.</item>
/// <item><b>Números embebidos.</b> Si la cadena no es un número, se extrae el primer número que
/// contenga: <c>"12abc"</c> → 12. Por eso <c>"50%"</c> → 50 y <c>"50%"</c> como double → 50.0: el signo
/// de porcentaje se ignora como unidad, NO se divide entre 100 (los nodos esperan el número tal cual).</item>
/// <item><b>Unidades de tiempo, sólo para enteros.</b> <c>"250ms"</c> → 250, <c>"3s"</c> → 3,
/// <c>"2m"</c> → 120, <c>"1h"</c> → 3600. En un destino decimal no se aplican (<c>"2m"</c> → 2.0).</item>
/// <item><b>Sin desbordamientos silenciosos.</b> Un valor fuera del rango del tipo destino devuelve el
/// valor por defecto, en lugar de envolverse o saturarse.</item>
/// <item><b>Enteros desde JSON.</b> Un número JSON sólo vale como entero si es integral
/// (<c>42</c> sí, <c>42.9</c> no: devuelve el valor por defecto), igual que antes.</item>
/// <item><b>Texto JSON.</b> <c>GetString</c> de un objeto o array JSON devuelve su texto JSON literal,
/// no el valor por defecto.</item>
/// </list>
/// </summary>
public static class ParameterValueConverter
{
    private static readonly Regex NumberExtractionRegex = new(@"[-+]?\d+(?:[\.,]\d+)?", RegexOptions.Compiled);

    /// <summary>
    /// Convierte <paramref name="value"/> a <typeparamref name="T"/>, devolviendo
    /// <paramref name="defaultValue"/> cuando el valor es nulo o no es interpretable como ese tipo.
    /// </summary>
    public static T ConvertTo<T>(object? value, T defaultValue = default!)
    {
        object? converted = ConvertTo(value, typeof(T), defaultValue);

        return converted is T typed ? typed : defaultValue;
    }

    /// <summary>
    /// Convierte <paramref name="value"/> a <paramref name="targetType"/>, devolviendo
    /// <paramref name="defaultValue"/> cuando el valor es nulo o no es interpretable como ese tipo.
    /// Acepta tipos anulables (<c>int?</c>, <c>bool?</c>) y enums.
    /// </summary>
    public static object? ConvertTo(object? value, Type targetType, object? defaultValue = null)
    {
        ArgumentNullException.ThrowIfNull(targetType);

        Type target = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (value is not null && target.IsInstanceOfType(value))
        {
            return value;
        }

        if (target == typeof(string))
        {
            return ToText(value, AsText(defaultValue));
        }

        if (target == typeof(bool))
        {
            return ToBoolean(value, defaultValue is bool flag && flag);
        }

        if (target.IsEnum)
        {
            return ToEnum(value, target, defaultValue);
        }

        if (IsIntegralType(target))
        {
            return ToIntegral(value, target, defaultValue);
        }

        if (IsFloatingPointType(target))
        {
            return ToFloatingPoint(value, target, defaultValue);
        }

        // Tipos fuera de la tabla (Guid, DateTime, Uri…): conversión estándar invariante, sin sorpresas.
        if (value is null)
        {
            return defaultValue;
        }

        try
        {
            return System.Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException or ArgumentException)
        {
            return defaultValue;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Texto
    // ─────────────────────────────────────────────────────────────────────────────

    private static string ToText(object? value, string defaultValue)
    {
        if (value is null)
        {
            return defaultValue;
        }

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? defaultValue,
                JsonValueKind.Null or JsonValueKind.Undefined => defaultValue,
                _ => element.GetRawText()
            };
        }

        return value.ToString() ?? defaultValue;
    }

    private static string AsText(object? defaultValue) =>
        defaultValue switch
        {
            null => string.Empty,
            string text => text,
            _ => defaultValue.ToString() ?? string.Empty
        };

    // ─────────────────────────────────────────────────────────────────────────────
    // Booleano
    // ─────────────────────────────────────────────────────────────────────────────

    private static bool ToBoolean(object? value, bool defaultValue)
    {
        if (value is null)
        {
            return defaultValue;
        }

        if (value is bool flag)
        {
            return flag;
        }

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => bool.TryParse(element.GetString(), out bool parsed) ? parsed : defaultValue,
                JsonValueKind.Number => element.TryGetInt64(out long number) && number != 0,
                _ => defaultValue
            };
        }

        if (value is string text)
        {
            return bool.TryParse(text, out bool parsed) ? parsed : defaultValue;
        }

        try
        {
            return System.Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException or ArgumentException)
        {
            return defaultValue;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Enum
    // ─────────────────────────────────────────────────────────────────────────────

    private static object? ToEnum(object? value, Type target, object? defaultValue)
    {
        if (value is null)
        {
            return defaultValue;
        }

        if (value is JsonElement element)
        {
            value = element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number when element.TryGetInt64(out long number) => number,
                _ => null
            };
        }

        if (value is null)
        {
            return defaultValue;
        }

        if (value is string text)
        {
            // Un texto numérico ("2") se trata como valor, y "99" en un enum de tres miembros se rechaza
            // igual que un número fuera de rango. Los nombres, incluidos los combinados de un enum de
            // banderas ("Read, Write"), se aceptan tal cual.
            if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long textNumber))
            {
                return Enum.TryParse(target, text, ignoreCase: true, out object? parsed) ? parsed : defaultValue;
            }

            value = textNumber;
        }

        if (IsNumericValue(value))
        {
            long numeric = System.Convert.ToInt64(value, CultureInfo.InvariantCulture);
            object numericEnum = Enum.ToObject(target, numeric);

            return Enum.IsDefined(target, numericEnum) ? numericEnum : defaultValue;
        }

        return defaultValue;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Enteros
    // ─────────────────────────────────────────────────────────────────────────────

    private static object? ToIntegral(object? value, Type target, object? defaultValue)
    {
        if (value is null)
        {
            return defaultValue;
        }

        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number)
            {
                // Sólo los números JSON integrales valen como entero: 42 sí, 42.9 devuelve el defecto.
                return element.TryGetInt64(out long jsonInteger)
                    ? FitIntegral(jsonInteger, target, defaultValue)
                    : defaultValue;
            }

            if (element.ValueKind != JsonValueKind.String)
            {
                return defaultValue;
            }

            // El texto JSON se resuelve con la misma tubería de cadenas que un valor de la UI.
            return ConvertTo(element.GetString(), target, defaultValue);
        }

        if (value is string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return defaultValue;
            }

            string trimmed = text.Trim();

            // 1. Número entero invariante ("42", "-7", "  42  ").
            if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long direct))
            {
                return FitIntegral(direct, target, defaultValue);
            }

            // 2. Unidades de tiempo: "250ms" → 250, "3s" → 3, "2m" → 120, "1h" → 3600.
            if (TryParseTimeUnit(trimmed, out double units))
            {
                return FitIntegral(units, target, defaultValue);
            }

            // 3. Primer número embebido en texto libre: "50%" → 50, "12abc" → 12, "1,5" → 1.
            Match match = NumberExtractionRegex.Match(trimmed);
            if (match.Success && double.TryParse(
                    match.Value.Replace(',', '.'),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double embedded))
            {
                return FitIntegral(embedded, target, defaultValue);
            }

            return defaultValue;
        }

        if (IsNumericValue(value))
        {
            return value is float or double or decimal
                ? FitIntegral(System.Convert.ToDouble(value, CultureInfo.InvariantCulture), target, defaultValue)
                : FitIntegral(System.Convert.ToInt64(value, CultureInfo.InvariantCulture), target, defaultValue);
        }

        try
        {
            return System.Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException or ArgumentException)
        {
            return defaultValue;
        }
    }

    private static bool TryParseTimeUnit(string trimmed, out double value)
    {
        value = 0;

        double multiplier = trimmed.EndsWith("ms", StringComparison.OrdinalIgnoreCase) ? 1
            : trimmed.EndsWith("s", StringComparison.OrdinalIgnoreCase) ? 1
            : trimmed.EndsWith("m", StringComparison.OrdinalIgnoreCase) ? 60
            : trimmed.EndsWith("h", StringComparison.OrdinalIgnoreCase) ? 3600
            : 0;

        if (multiplier == 0)
        {
            return false;
        }

        Match match = NumberExtractionRegex.Match(trimmed);

        if (!match.Success || !double.TryParse(
                match.Value.Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double parsed))
        {
            return false;
        }

        value = parsed * multiplier;

        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Decimales
    // ─────────────────────────────────────────────────────────────────────────────

    private static object? ToFloatingPoint(object? value, Type target, object? defaultValue)
    {
        if (value is null)
        {
            return defaultValue;
        }

        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number)
            {
                return element.TryGetDouble(out double jsonNumber)
                    ? FitFloatingPoint(jsonNumber, target, defaultValue)
                    : defaultValue;
            }

            if (element.ValueKind != JsonValueKind.String)
            {
                return defaultValue;
            }

            // El texto JSON se resuelve con la misma tubería de cadenas que un valor de la UI.
            return ConvertTo(element.GetString(), target, defaultValue);
        }

        if (value is string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return defaultValue;
            }

            // 1. Número decimal invariante ("0.7", "1e3").
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double direct))
            {
                return FitFloatingPoint(direct, target, defaultValue);
            }

            // 2. Primer número embebido: "50%" → 50, "0,7" → 0.7. Sin unidades de tiempo (a diferencia
            //    del destino entero): "2m" como double vale 2, no 120.
            Match match = NumberExtractionRegex.Match(text.Trim());
            if (match.Success && double.TryParse(
                    match.Value.Replace(',', '.'),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double embedded))
            {
                return FitFloatingPoint(embedded, target, defaultValue);
            }

            return defaultValue;
        }

        if (IsNumericValue(value))
        {
            return FitFloatingPoint(System.Convert.ToDouble(value, CultureInfo.InvariantCulture), target, defaultValue);
        }

        try
        {
            return System.Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException or ArgumentException)
        {
            return defaultValue;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Ajuste al tipo destino
    // ─────────────────────────────────────────────────────────────────────────────

    private static object? FitIntegral(long value, Type target, object? defaultValue) => target switch
    {
        _ when target == typeof(long) => value,
        _ when target == typeof(int) && value is >= int.MinValue and <= int.MaxValue => (int)value,
        _ when target == typeof(short) && value is >= short.MinValue and <= short.MaxValue => (short)value,
        _ when target == typeof(byte) && value is >= byte.MinValue and <= byte.MaxValue => (byte)value,
        _ when target == typeof(sbyte) && value is >= sbyte.MinValue and <= sbyte.MaxValue => (sbyte)value,
        _ when target == typeof(ushort) && value is >= ushort.MinValue and <= ushort.MaxValue => (ushort)value,
        _ when target == typeof(uint) && value is >= uint.MinValue and <= uint.MaxValue => (uint)value,
        _ when target == typeof(ulong) && value >= 0 => (ulong)value,
        _ => defaultValue
    };

    private static object? FitIntegral(double value, Type target, object? defaultValue)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < long.MinValue || value > long.MaxValue)
        {
            return defaultValue;
        }

        return FitIntegral((long)value, target, defaultValue);
    }

    private static object? FitFloatingPoint(double value, Type target, object? defaultValue)
    {
        if (target == typeof(double))
        {
            return value;
        }

        if (target == typeof(float))
        {
            return value is >= -float.MaxValue and <= float.MaxValue ? (float)value : defaultValue;
        }

        if (target == typeof(decimal))
        {
            return double.IsNaN(value) || double.IsInfinity(value) || value < (double)decimal.MinValue || value > (double)decimal.MaxValue
                ? defaultValue
                : (decimal)value;
        }

        return defaultValue;
    }

    private static bool IsIntegralType(Type type) =>
        type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte)
        || type == typeof(sbyte) || type == typeof(ushort) || type == typeof(uint) || type == typeof(ulong);

    private static bool IsFloatingPointType(Type type) =>
        type == typeof(double) || type == typeof(float) || type == typeof(decimal);

    private static bool IsNumericValue(object? value) =>
        value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;
}
