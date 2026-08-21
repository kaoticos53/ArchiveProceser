using System.Text.Json;

namespace FileFlow.Sdk;

/// <summary>
/// Proporciona conversión y extracción segura de parámetros para nodos de flujo,
/// evitando desbordamientos e InvalidCastException cuando los valores provienen de JsonElement, WPF UI o cadenas.
/// </summary>
public static class ParameterHelper
{
    public static bool GetBoolean(object? value, bool defaultValue = false)
    {
        if (value == null) return defaultValue;

        if (value is bool b) return b;

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => bool.TryParse(element.GetString(), out var parsed) ? parsed : defaultValue,
                JsonValueKind.Number => element.TryGetInt64(out var num) && num != 0,
                _ => defaultValue
            };
        }

        if (value is string s)
        {
            return bool.TryParse(s, out var parsed) ? parsed : defaultValue;
        }

        try
        {
            return Convert.ToBoolean(value);
        }
        catch
        {
            return defaultValue;
        }
    }

    public static int GetInt32(object? value, int defaultValue = 0)
    {
        if (value == null) return defaultValue;

        if (value is int i) return i;
        if (value is long l) return (int)l;
        if (value is double d) return (int)d;

        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var parsedInt))
            {
                return parsedInt;
            }
            if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out var parsedStr))
            {
                return parsedStr;
            }
        }

        if (value is string s)
        {
            return int.TryParse(s, out var parsed) ? parsed : defaultValue;
        }

        try
        {
            return Convert.ToInt32(value);
        }
        catch
        {
            return defaultValue;
        }
    }

    public static string GetString(object? value, string defaultValue = "")
    {
        if (value == null) return defaultValue;

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? defaultValue,
                JsonValueKind.Null => defaultValue,
                JsonValueKind.Undefined => defaultValue,
                _ => element.GetRawText()
            };
        }

        return value.ToString() ?? defaultValue;
    }
}
