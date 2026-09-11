using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FileFlow.Sdk.Serialization;

/// <summary>
/// Opciones y utilidades centralizadas de serialización JSON y decodificación de caracteres unicode
/// para evitar secuencias de escape agresivas (como \u0060 para backtick o \u0022 para comillas).
/// </summary>
public static partial class JsonDefaults
{
    /// <summary>
    /// Opciones JSON relajadas compactas sin escape agresivo de HTML ni caracteres unicode.
    /// </summary>
    public static readonly JsonSerializerOptions RelaxedOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    /// <summary>
    /// Opciones JSON relajadas con indentación multilínea legible.
    /// </summary>
    public static readonly JsonSerializerOptions RelaxedIndentedOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    [GeneratedRegex(@"\\u([0-9a-fA-F]{4})", RegexOptions.Compiled)]
    private static partial Regex UnicodeEscapeRegex();

    /// <summary>
    /// Desescapa secuencias unicode del tipo \uXXXX (ej. \u0060 -> `, \u0022 -> ", \u00e1 -> á)
    /// preservando rutas de archivo de Windows intactas.
    /// </summary>
    public static string UnescapeUnicode(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        if (!input.Contains(@"\u", StringComparison.OrdinalIgnoreCase)) return input;

        return UnicodeEscapeRegex().Replace(input, m =>
        {
            if (ushort.TryParse(m.Groups[1].ValueSpan, System.Globalization.NumberStyles.HexNumber, null, out ushort codePoint))
            {
                return ((char)codePoint).ToString();
            }
            return m.Value;
        });
    }

    /// <summary>
    /// Serializa un objeto a JSON garantizando que no se escapen caracteres unicode ni HTML
    /// (como comillas, acentos, símbolos matemáticos o backticks).
    /// </summary>
    public static string SerializeRelaxed(object? value, bool indented = false)
    {
        if (value == null) return "null";
        return JsonSerializer.Serialize(value, indented ? RelaxedIndentedOptions : RelaxedOptions);
    }

    /// <summary>
    /// Formatea un texto o payload JSON para su presentación visual legible en la interfaz de usuario,
    /// desescapando secuencias unicode y aplicando indentación si el texto es un JSON estructurado válido.
    /// </summary>
    public static string FormatDetailsForDisplay(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        // 1. Si es un documento JSON válido, parsearlo y serializarlo con opciones relajadas e indentadas.
        // Esto transforma automáticamente secuencias \u0060 en `, \u0022 en \", acentos UTF-8, etc.
        try
        {
            using var doc = JsonDocument.Parse(text);
            string formatted = JsonSerializer.Serialize(doc.RootElement, RelaxedIndentedOptions);
            return UnescapeUnicode(formatted);
        }
        catch
        {
            // 2. Si no es JSON sintácticamente estricto (ej. texto markdown, log plano), desescapar directamente.
            return UnescapeUnicode(text);
        }
    }
}
