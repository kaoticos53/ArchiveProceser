using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace FileFlow.Plugin.AI.Utilities;

/// <summary>
/// Utilidad especializada para aplanar y extraer metadatos a partir de respuestas estructuradas o JSON de modelos VLM.
/// Desanida objetos, procesa arrays, y desempaqueta strings que contengan JSONs serializados (anti-doble serialización).
/// </summary>
public static class JsonMetadataFlattener
{
    /// <summary>
    /// Aplana un JSON completo (o string que contenga JSON) e inyecta las claves y valores resultantes en el diccionario destino.
    /// </summary>
    /// <param name="rawJsonOrText">Texto plano o JSON devuelto por el modelo.</param>
    /// <param name="targetMetadata">Diccionario de metadatos del elemento donde inyectar las variables.</param>
    /// <param name="prefix">Prefijo opcional para nombres de metadatos (p. ej. "AI:Vlm:").</param>
    /// <returns>Diccionario con todas las variables extraídas y sus valores formateados.</returns>
    public static Dictionary<string, string> FlattenAndInject(
        string? rawJsonOrText,
        IDictionary<string, object?> targetMetadata,
        string prefix = "AI:Vlm:")
    {
        ArgumentNullException.ThrowIfNull(targetMetadata);

        var extracted = Flatten(rawJsonOrText);
        foreach (var kvp in extracted)
        {
            // 1. Inyectar con nombre canónico plano (ej. "numero_factura" o "cliente_nombre")
            targetMetadata[kvp.Key] = kvp.Value;

            // 2. Inyectar también con prefijo de IA si se especificó (ej. "AI:Vlm:numero_factura")
            if (!string.IsNullOrWhiteSpace(prefix))
            {
                targetMetadata[$"{prefix}{kvp.Key}"] = kvp.Value;
            }

            // 3. Casos especiales conocidos para etiquetas y motivos
            if (kvp.Key.Contains("etiqueta", StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.Contains("tag", StringComparison.OrdinalIgnoreCase))
            {
                targetMetadata["AI:VlmTags"] = kvp.Value;
            }
            else if (kvp.Key.Contains("motivo", StringComparison.OrdinalIgnoreCase) ||
                     kvp.Key.Contains("reason", StringComparison.OrdinalIgnoreCase))
            {
                targetMetadata["AI:VlmReason"] = kvp.Value;
            }
        }

        return extracted;
    }

    /// <summary>
    /// Extrae un diccionario plano clave-valor a partir de un texto o JSON.
    /// Si una propiedad contiene un JSON anidado o un string con JSON escapado, lo desempaqueta recursivamente.
    /// </summary>
    public static Dictionary<string, string> Flatten(string? rawJsonOrText)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(rawJsonOrText))
        {
            return result;
        }

        string trimmed = rawJsonOrText.Trim();

        // Si empieza y termina con llaves/corchetes, o contiene un bloque JSON, intentamos parsear
        string? candidateJson = UnwrapEmbeddedJson(trimmed);
        if (candidateJson == null)
        {
            return result;
        }

        try
        {
            using var doc = JsonDocument.Parse(candidateJson);
            FlattenElement(string.Empty, doc.RootElement, result);
        }
        catch
        {
            // No es un JSON válido o no se pudo parsear
        }

        return result;
    }

    private static void FlattenElement(string currentPath, JsonElement element, Dictionary<string, string> accumulator)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    string nextPath = string.IsNullOrEmpty(currentPath) ? prop.Name : $"{currentPath}_{prop.Name}";
                    string dotPath = string.IsNullOrEmpty(currentPath) ? prop.Name : $"{currentPath}.{prop.Name}";

                    // Si el valor es una cadena, verificar si contiene otro JSON serializado dentro
                    if (prop.Value.ValueKind == JsonValueKind.String)
                    {
                        string strVal = prop.Value.GetString() ?? string.Empty;
                        string? innerJson = UnwrapEmbeddedJson(strVal);
                        if (!string.IsNullOrWhiteSpace(innerJson) && (innerJson.StartsWith('{') || innerJson.StartsWith('[')))
                        {
                            try
                            {
                                using var innerDoc = JsonDocument.Parse(innerJson);
                                if (innerDoc.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                                {
                                    FlattenElement(nextPath, innerDoc.RootElement, accumulator);
                                    continue;
                                }
                            }
                            catch
                            {
                                // Continuar como string normal
                            }
                        }

                        accumulator[nextPath] = strVal;
                        if (!string.Equals(nextPath, dotPath, StringComparison.Ordinal))
                        {
                            accumulator[dotPath] = strVal;
                        }
                    }
                    else if (prop.Value.ValueKind == JsonValueKind.Object)
                    {
                        FlattenElement(nextPath, prop.Value, accumulator);
                    }
                    else if (prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        // Si es array de primitivos, unir con comas
                        var items = new List<string>();
                        bool allPrimitives = true;
                        int idx = 0;

                        foreach (var item in prop.Value.EnumerateArray())
                        {
                            if (item.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                            {
                                allPrimitives = false;
                                FlattenElement($"{nextPath}_{idx}", item, accumulator);
                            }
                            else
                            {
                                items.Add(item.ToString());
                            }
                            idx++;
                        }

                        if (allPrimitives)
                        {
                            string joined = string.Join(", ", items.Where(s => !string.IsNullOrWhiteSpace(s)));
                            accumulator[nextPath] = joined;
                            if (!string.Equals(nextPath, dotPath, StringComparison.Ordinal))
                            {
                                accumulator[dotPath] = joined;
                            }
                        }
                    }
                    else
                    {
                        string val = element.ValueKind == JsonValueKind.Null ? string.Empty : prop.Value.ToString();
                        accumulator[nextPath] = val;
                        if (!string.Equals(nextPath, dotPath, StringComparison.Ordinal))
                        {
                            accumulator[dotPath] = val;
                        }
                    }
                }
                break;

            case JsonValueKind.Array:
                var list = new List<string>();
                int arrIdx = 0;
                foreach (var item in element.EnumerateArray())
                {
                    if (item.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    {
                        FlattenElement(string.IsNullOrEmpty(currentPath) ? $"item_{arrIdx}" : $"{currentPath}_{arrIdx}", item, accumulator);
                    }
                    else
                    {
                        list.Add(item.ToString());
                    }
                    arrIdx++;
                }

                if (!string.IsNullOrEmpty(currentPath) && list.Count > 0)
                {
                    accumulator[currentPath] = string.Join(", ", list);
                }
                break;

            default:
                if (!string.IsNullOrEmpty(currentPath))
                {
                    accumulator[currentPath] = element.ToString();
                }
                break;
        }
    }

    /// <summary>
    /// Desempaqueta bloques de código markdown (```json ... ```) o strings que contienen JSONs embebidos.
    /// </summary>
    public static string? UnwrapEmbeddedJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        string trimmed = text.Trim();

        // 1. Limpieza de bloques markdown
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            int firstNewline = trimmed.IndexOf('\n');
            int lastBackticks = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewline >= 0 && lastBackticks > firstNewline)
            {
                trimmed = trimmed.Substring(firstNewline + 1, lastBackticks - firstNewline - 1).Trim();
            }
        }

        // 2. Si ya es un JSON válido
        if ((trimmed.StartsWith('{') && trimmed.EndsWith('}')) || (trimmed.StartsWith('[') && trimmed.EndsWith(']')))
        {
            if (IsValidJson(trimmed)) return trimmed;
        }

        // 3. Buscar subcadena entre la primera '{' y la última '}'
        int startBrace = trimmed.IndexOf('{');
        int endBrace = trimmed.LastIndexOf('}');
        if (startBrace >= 0 && endBrace > startBrace)
        {
            string sub = trimmed.Substring(startBrace, endBrace - startBrace + 1);
            if (IsValidJson(sub)) return sub;
        }

        return null;
    }

    private static bool IsValidJson(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
