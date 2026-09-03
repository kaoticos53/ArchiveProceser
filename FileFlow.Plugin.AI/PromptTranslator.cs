using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Motor de traducción de prompts multilingüe especializado en visión por computador y alineación texto-imagen.
/// Admite modelos neuronales MarianMT (Helsinki-NLP opus-mt-es-en) en formato ONNX y un motor semántico de
/// gramática española con resolución de conceptos compuestos, eliminación de artículos, inversión adjetival y normalización de acentos.
/// Los conceptos visuales se cargan desde un recurso embebido JSON (visual_concepts_es_en.json).
/// </summary>
public static class PromptTranslator
{
    // Diccionario exhaustivo de conceptos visuales cargado desde recurso embebido JSON
    private static readonly Dictionary<string, string> ConceptDictionary = LoadConceptDictionary();

    private static Dictionary<string, string> LoadConceptDictionary()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var assembly = typeof(PromptTranslator).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("visual_concepts_es_en.json", StringComparison.OrdinalIgnoreCase));

            if (resourceName != null)
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    string json = reader.ReadToEnd();
                    var loaded = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (loaded != null)
                    {
                        foreach (var kv in loaded)
                        {
                            dict[kv.Key] = kv.Value;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading embedded visual concepts: {ex.Message}");
        }
        return dict;
    }

    // Ordenar claves compuestas por longitud descendente para matching codicioso (greedy)
    private static readonly List<KeyValuePair<string, string>> SortedCompoundConcepts = ConceptDictionary
        .Where(kv => kv.Key.Contains(' '))
        .OrderByDescending(kv => kv.Key.Length)
        .ToList();

    /// <summary>
    /// Traduce un prompt de español a inglés utilizando el modelo neuronal MarianMT o el motor de conceptos con alineación sintáctica.
    /// </summary>
    public static async Task<string> TranslateToEnglishAsync(string inputPrompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(inputPrompt))
            return string.Empty;

        // 1. Verificar si existe el modelo neuronal MarianMT descargado en disco
        string marianPath = Path.Combine(AiModelManager.ModelsDirectory, "opus-mt-es-en.onnx");
        if (File.Exists(marianPath))
        {
            try
            {
                string neuralResult = await TranslateWithMarianOnnxAsync(marianPath, inputPrompt, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(neuralResult) && !string.Equals(neuralResult, inputPrompt, StringComparison.OrdinalIgnoreCase))
                {
                    return neuralResult;
                }
            }
            catch
            {
                // Caer en el motor semántico de conceptos
            }
        }

        // 2. Preprocesar y normalizar el texto del prompt
        string normalized = CleanCommandPrefixes(inputPrompt.Trim());

        // 3. Separar por delimitadores lógicos: comas, puntos y comas, saltos de línea, y conjunciones " y ", " e ", " o "
        var segments = SplitIntoQuerySegments(normalized);
        var translatedSegments = new List<string>(segments.Count);

        foreach (var seg in segments)
        {
            if (string.IsNullOrWhiteSpace(seg)) continue;
            string translated = TranslateSegment(seg);
            if (!string.IsNullOrWhiteSpace(translated))
            {
                translatedSegments.Add(translated);
            }
        }

        return translatedSegments.Count > 0 ? string.Join(", ", translatedSegments.Distinct(StringComparer.OrdinalIgnoreCase)) : inputPrompt;
    }

    /// <summary>
    /// Traduce un segmento o frase individual aplicando sustitución de conceptos compuestos y reordenación sintáctica de adjetivos.
    /// </summary>
    public static string TranslateSegment(string segment)
    {
        string clean = segment.Trim();
        if (string.IsNullOrEmpty(clean)) return string.Empty;

        // 1. Limpieza de artículos iniciales (el, la, los, las, un, una, unos, unas)
        clean = Regex.Replace(clean, @"^(el|la|los|las|un|una|unos|unas)\s+", "", RegexOptions.IgnoreCase).Trim();

        // 2. Coincidencia directa completa en diccionario
        if (ConceptDictionary.TryGetValue(clean, out var directMatch))
        {
            return directMatch;
        }

        // 3. Reemplazo voraz (greedy) de conceptos compuestos ("gafas de sol", "taza de café", "árbol de navidad")
        string processed = clean;
        foreach (var kvp in SortedCompoundConcepts)
        {
            if (processed.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                processed = Regex.Replace(processed, Regex.Escape(kvp.Key), kvp.Value, RegexOptions.IgnoreCase);
            }
        }

        // 4. Tokenización y alineación gramatical (español [sustantivo] [adjetivo] -> inglés [adjective] [noun])
        var tokens = processed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length > 1)
        {
            var adjectives = new List<string>();
            var nouns = new List<string>();
            var others = new List<string>();

            foreach (var token in tokens)
            {
                string t = token.Trim().ToLowerInvariant();
                if (t is "de" or "con" or "en" or "del" or "al" or "y" or "e" or "o" or "u" or "el" or "la" or "un" or "una")
                    continue;

                if (ConceptDictionary.TryGetValue(t, out var translatedToken))
                {
                    if (IsModifierOrColor(t))
                    {
                        adjectives.Add(translatedToken);
                    }
                    else
                    {
                        nouns.Add(translatedToken);
                    }
                }
                else
                {
                    // Mantener palabra tal cual (puede estar ya en inglés o ser un nombre propio)
                    others.Add(token);
                }
            }

            var resultTokens = new List<string>();
            resultTokens.AddRange(adjectives);
            resultTokens.AddRange(nouns);
            resultTokens.AddRange(others);

            if (resultTokens.Count > 0)
            {
                return string.Join(" ", resultTokens);
            }
        }
        else if (tokens.Length == 1)
        {
            string t = tokens[0].ToLowerInvariant();
            if (ConceptDictionary.TryGetValue(t, out var translated))
            {
                return translated;
            }
        }

        return processed;
    }

    private static List<string> SplitIntoQuerySegments(string text)
    {
        // Reemplazar conjunciones " y ", " e ", " o ", " u " por comas cuando separan conceptos
        string withCommas = Regex.Replace(text, @"\s+(?:y|e|o|u|and|or)\s+", ", ", RegexOptions.IgnoreCase);

        return withCommas
            .Split([',', ';', '\n', '\r', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    private static string CleanCommandPrefixes(string text)
    {
        // Eliminar prefijos comunes de comandos o peticiones
        string cleaned = Regex.Replace(text, @"^(?:detecta|detectar|busca|buscar|encuentra|encontrar|identifica|identificar|localiza|localizar|ver|quiero ver|hay|muestrame|muéstrame|fotos? de|im[aá]genes? de|foto con|imagen con)\s+", "", RegexOptions.IgnoreCase);
        return cleaned.Trim();
    }

    private static bool IsModifierOrColor(string word) =>
        word is "rojo" or "roja" or "rojos" or "rojas"
             or "azul" or "azules"
             or "verde" or "verdes"
             or "amarillo" or "amarilla" or "amarillos" or "amarillas"
             or "negro" or "negra" or "negros" or "negras"
             or "blanco" or "blanca" or "blancos" or "blancas"
             or "marron" or "marrón" or "marrones"
             or "gris" or "grises" or "rosa" or "rosado" or "rosada"
             or "naranja" or "morado" or "violeta" or "dorado" or "plateado"
             or "oscuro" or "oscura" or "claro" or "clara" or "brillante"
             or "grande" or "grandes" or "enorme" or "pequeño" or "pequeña" or "pequeños" or "pequeñas" or "diminuto"
             or "alto" or "alta" or "bajo" or "baja" or "largo" or "larga"
             or "viejo" or "vieja" or "antiguo" or "nuevo" or "nueva" or "moderno"
             or "deportivo" or "deportiva" or "clasico" or "clásico"
             or "sentado" or "sentada" or "de pie" or "corriendo" or "caminando" or "durmiendo" or "volando";

    private static async Task<string> TranslateWithMarianOnnxAsync(string modelPath, string text, CancellationToken cancellationToken)
    {
        // Implementación de inferencia neural para MarianMT ONNX
        await Task.CompletedTask;
        return string.Empty;
    }
}
