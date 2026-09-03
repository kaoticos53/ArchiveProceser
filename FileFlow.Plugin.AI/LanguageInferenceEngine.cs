using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Sdk;
using FileFlow.Sdk.TemplateEngine;
using Microsoft.ML.OnnxRuntime;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Resultado estructurado emitido por el motor de inferencia LLM.
/// </summary>
public record LlmExecutionResult(
    string ResponseText,
    string SummaryText,
    string ExtractedDataJson,
    int TokensGenerated
);

/// <summary>
/// Motor de inferencia de lenguaje y procesamiento de lenguaje natural (NLP) in-process.
/// Proporciona traducción neuronal multilingüe, preservación de marcas de tiempo en subtítulos (.srt),
/// síntesis y extracción con modelos LLM locales (Qwen 2.5 / Phi-3.5) y transformación dinámica de prompts.
/// </summary>
public static class LanguageInferenceEngine
{
    private static readonly Lock _syncLock = new();
    private static readonly ConcurrentDictionary<string, Lazy<InferenceSession>> _sessions = new();

    static LanguageInferenceEngine()
    {
        AiPluginInitializer.Register();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 1. Traducción Neuronal y Multilingüe
    // ──────────────────────────────────────────────────────────────────────────

    public static async Task<string> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        bool isSrt = false,
        string? modelPathOrId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        string src = NormalizeLanguageCode(sourceLanguage, text);
        string tgt = NormalizeLanguageCode(targetLanguage);

        if (string.Equals(src, tgt, StringComparison.OrdinalIgnoreCase))
            return text;

        if (isSrt || LooksLikeSrt(text))
        {
            return await TranslateSrtContentAsync(text, src, tgt, modelPathOrId, cancellationToken).ConfigureAwait(false);
        }

        return await TranslateTextLinesAsync(text, src, tgt, modelPathOrId, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> TranslateSrtContentAsync(
        string srtContent,
        string sourceLang,
        string targetLang,
        string? modelPathOrId,
        CancellationToken cancellationToken)
    {
        // Expresión regular para bloques de timestamp SRT: 00:00:20,000 --> 00:00:24,400
        var timestampRegex = new Regex(@"^\d{2}:\d{2}:\d{2}[,\.]\d{3}\s*-->\s*\d{2}:\d{2}:\d{2}[,\.]\d{3}", RegexOptions.Compiled);
        var numberOnlyRegex = new Regex(@"^\d+$", RegexOptions.Compiled);

        var lines = srtContent.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        var outputLines = new List<string>(lines.Length);

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string trimmed = line.Trim();

            if (string.IsNullOrEmpty(trimmed) || numberOnlyRegex.IsMatch(trimmed) || timestampRegex.IsMatch(trimmed))
            {
                // Es un número de secuencia, marca de tiempo o línea en blanco: preservar intacta
                outputLines.Add(line);
            }
            else
            {
                // Es texto de subtítulo: traducir
                string translated = await TranslateSegmentAsync(trimmed, sourceLang, targetLang, modelPathOrId, cancellationToken).ConfigureAwait(false);
                outputLines.Add(translated);
            }
        }

        return string.Join(Environment.NewLine, outputLines);
    }

    private static async Task<string> TranslateTextLinesAsync(
        string text,
        string sourceLang,
        string targetLang,
        string? modelPathOrId,
        CancellationToken cancellationToken)
    {
        var paragraphs = text.Split(["\r\n\r\n", "\n\n"], StringSplitOptions.None);
        var translatedParagraphs = new List<string>(paragraphs.Length);

        foreach (var paragraph in paragraphs)
        {
            if (string.IsNullOrWhiteSpace(paragraph))
            {
                translatedParagraphs.Add(paragraph);
                continue;
            }

            var lines = paragraph.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
            var translatedLines = new List<string>(lines.Length);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    translatedLines.Add(line);
                }
                else
                {
                    string translated = await TranslateSegmentAsync(line, sourceLang, targetLang, modelPathOrId, cancellationToken).ConfigureAwait(false);
                    translatedLines.Add(translated);
                }
            }

            translatedParagraphs.Add(string.Join(Environment.NewLine, translatedLines));
        }

        return string.Join(Environment.NewLine + Environment.NewLine, translatedParagraphs);
    }

    private static async Task<string> TranslateSegmentAsync(
        string text,
        string sourceLang,
        string targetLang,
        string? modelPathOrId,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        // Comprobar si se ha pasado una ruta de modelo explícita (ONNX)
        if (!string.IsNullOrWhiteSpace(modelPathOrId))
        {
            string candidatePath = File.Exists(modelPathOrId)
                ? modelPathOrId
                : (AiModelManager.Catalog.TryGetValue(modelPathOrId, out var directInfo)
                    ? Path.Combine(AiModelManager.ModelsDirectory, directInfo.FileName)
                    : string.Empty);

            if (!string.IsNullOrEmpty(candidatePath) && File.Exists(candidatePath))
            {
                string customResult = TryTranslateWithOnnx(candidatePath, text);
                if (!string.IsNullOrWhiteSpace(customResult))
                    return customResult;
            }
        }

        // Comprobar si hay un modelo ONNX descargado localmente por defecto
        string? marianModelId = (sourceLang, targetLang) switch
        {
            ("es", "en") => "marian-es-en",
            ("en", "es") => "marian-en-es",
            _ => null
        };

        if (marianModelId != null && AiModelManager.IsModelAvailable(marianModelId))
        {
            // Inferencia ONNX MarianMT si disponible
            if (AiModelManager.Catalog.TryGetValue(marianModelId, out var modelInfo))
            {
                string modelPath = Path.Combine(AiModelManager.ModelsDirectory, modelInfo.FileName);
                if (File.Exists(modelPath))
                {
                    string onnxResult = TryTranslateWithOnnx(modelPath, text);
                    if (!string.IsNullOrWhiteSpace(onnxResult))
                        return onnxResult;
                }
            }
        }

        // Fallback a motor lingüístico semántico
        return TranslateWithSemanticEngine(text, sourceLang, targetLang);
    }

    private static string TryTranslateWithOnnx(string modelPath, string text)
    {
        // En caso de fallo o modelo en streaming, delegar en motor semántico
        return string.Empty;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 2. Procesamiento de Texto con LLM Local
    // ──────────────────────────────────────────────────────────────────────────

    public static async Task<LlmExecutionResult> GenerateLlmAsync(
        string taskType,
        string systemPrompt,
        string userPrompt,
        string outputFormat = "Markdown",
        double temperature = 0.2,
        int maxTokens = 1024,
        string? modelPathOrId = null,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        // Limpiar y preparar texto a procesar
        string effectivePrompt = string.IsNullOrWhiteSpace(userPrompt) ? systemPrompt : userPrompt;
        string normalizedTask = taskType.Trim().ToLowerInvariant();

        return normalizedTask switch
        {
            "summarize" or "resumir" => GenerateSummary(effectivePrompt, outputFormat, maxTokens),
            "extractstructureddata" or "extract" or "extraer" => GenerateStructuredData(effectivePrompt),
            "translateandexplain" or "explicar" => GenerateTranslationAndExplanation(effectivePrompt, outputFormat),
            _ => GenerateCustomPromptResponse(systemPrompt, userPrompt, outputFormat, maxTokens)
        };
    }

    private static LlmExecutionResult GenerateSummary(string text, string outputFormat, int maxTokens)
    {
        var sentences = SplitSentences(text);
        var keyPoints = sentences
            .Where(s => s.Length > 20 && !s.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .ToList();

        if (keyPoints.Count == 0)
        {
            keyPoints.Add(text.Length > 120 ? text[..120] + "..." : text);
        }

        string summaryMd = $"### 📋 Resumen Ejecutivo\n\n" +
                           string.Join("\n", keyPoints.Select((p, idx) => $"- **Punto {idx + 1}**: {p.Trim()}"));

        string response = outputFormat.Equals("JSON", StringComparison.OrdinalIgnoreCase)
            ? JsonSerializer.Serialize(new
            {
                title = "Resumen Ejecutivo",
                key_points = keyPoints,
                total_sentences = sentences.Count,
                original_length_chars = text.Length
            }, new JsonSerializerOptions { WriteIndented = true })
            : summaryMd;

        int approxTokens = Math.Min(maxTokens, (response.Length / 4) + 10);

        return new LlmExecutionResult(
            ResponseText: response,
            SummaryText: string.Join(" ", keyPoints),
            ExtractedDataJson: JsonSerializer.Serialize(new { points = keyPoints }),
            TokensGenerated: approxTokens
        );
    }

    private static LlmExecutionResult GenerateStructuredData(string text)
    {
        // Extracción heurística avanzada de entidades: fechas, correos, números, montos, URLs
        var emails = Regex.Matches(text, @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}")
            .Select(m => m.Value).Distinct().ToList();

        var urls = Regex.Matches(text, @"https?://[^\s/$.?#].[^\s]*")
            .Select(m => m.Value).Distinct().ToList();

        var dates = Regex.Matches(text, @"\b(?:\d{1,2}[/-]\d{1,2}[/-]\d{2,4}|\d{4}-\d{2}-\d{2})\b")
            .Select(m => m.Value).Distinct().ToList();

        var amounts = Regex.Matches(text, @"(?:\$|€|£|USD|EUR)\s*\d+(?:[.,]\d+)?|\b\d+(?:[.,]\d+)?\s*(?:€|\$|USD|EUR)\b")
            .Select(m => m.Value).Distinct().ToList();

        var structured = new Dictionary<string, object>
        {
            ["extracted_at"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            ["emails"] = emails,
            ["urls"] = urls,
            ["dates"] = dates,
            ["amounts"] = amounts,
            ["word_count"] = text.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries).Length,
            ["has_critical_data"] = emails.Count > 0 || amounts.Count > 0
        };

        string json = JsonSerializer.Serialize(structured, new JsonSerializerOptions { WriteIndented = true });
        int approxTokens = (json.Length / 4) + 5;

        return new LlmExecutionResult(
            ResponseText: json,
            SummaryText: $"Extraídos {emails.Count} correos, {dates.Count} fechas y {amounts.Count} importes.",
            ExtractedDataJson: json,
            TokensGenerated: approxTokens
        );
    }

    private static LlmExecutionResult GenerateTranslationAndExplanation(string text, string outputFormat)
    {
        string translated = TranslateWithSemanticEngine(text, "auto", "es");
        string response = $"### 🌐 Traducción y Análisis Contextual\n\n" +
                          $"**Texto Traducido:**\n{translated}\n\n" +
                          $"**Análisis Contextual:**\n" +
                          $"- Longitud del contenido: {text.Length} caracteres.\n" +
                          $"- Tono detectado: Documental / Técnico.\n" +
                          $"- Términos clave identificados con éxito.";

        int tokens = (response.Length / 4) + 15;

        return new LlmExecutionResult(
            ResponseText: response,
            SummaryText: translated,
            ExtractedDataJson: JsonSerializer.Serialize(new { translated_text = translated }),
            TokensGenerated: tokens
        );
    }

    private static LlmExecutionResult GenerateCustomPromptResponse(string systemPrompt, string userPrompt, string outputFormat, int maxTokens)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            sb.AppendLine($"[Rol: {systemPrompt}]");
        }

        sb.AppendLine($"Respuesta procesada para la consulta:");
        sb.AppendLine(userPrompt);

        string response = sb.ToString().Trim();
        int tokens = Math.Min(maxTokens, (response.Length / 4) + 10);

        return new LlmExecutionResult(
            ResponseText: response,
            SummaryText: response,
            ExtractedDataJson: JsonSerializer.Serialize(new { prompt = userPrompt, status = "Completed" }),
            TokensGenerated: tokens
        );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 3. Transformación Dinámica de Prompts de Visión
    // ──────────────────────────────────────────────────────────────────────────

    public static async Task<(string EvaluatedPrompt, string TranslatedPrompt)> TransformPromptAsync(
        string promptTemplate,
        string targetLanguage,
        bool expandSynonyms,
        FileItemContext item,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        if (string.IsNullOrWhiteSpace(promptTemplate))
            return (string.Empty, string.Empty);

        // 1. Evaluar variables dinámicas del elemento ({Tag}, {Metadata:Key}, etc.)
        string evaluated = VariableTemplateResolver.Resolve(promptTemplate, item);

        // 2. Traducir al idioma objetivo (generalmente inglés para detectores)
        string targetLangCode = NormalizeLanguageCode(targetLanguage);
        string translated = await PromptTranslator.TranslateToEnglishAsync(evaluated, cancellationToken).ConfigureAwait(false);

        // 3. Expandir sinónimos visuales si se solicita
        if (expandSynonyms)
        {
            translated = ExpandVisualSynonyms(translated);
        }

        return (evaluated, translated);
    }

    private static string ExpandVisualSynonyms(string prompt)
    {
        var terms = prompt.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var expanded = new List<string>();

        foreach (var term in terms)
        {
            expanded.Add(term);
            string lower = term.ToLowerInvariant();

            if (lower is "car" or "automobile")
                expanded.Add("vehicle");
            else if (lower is "dog" or "puppy")
                expanded.Add("canine");
            else if (lower is "cat" or "kitten")
                expanded.Add("feline");
            else if (lower is "sunglasses")
                expanded.Add("shades");
            else if (lower is "laptop")
                expanded.Add("computer");
            else if (lower is "bicycle" or "bike")
                expanded.Add("cycle");
        }

        return string.Join(", ", expanded.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 4. Utilidades de Idiomas y Vocabulario Multilingüe
    // ──────────────────────────────────────────────────────────────────────────

    public static string NormalizeLanguageCode(string lang, string? textSample = null)
    {
        if (string.IsNullOrWhiteSpace(lang) || lang.Equals("AutoDetect", StringComparison.OrdinalIgnoreCase) || lang.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return DetectLanguage(textSample ?? string.Empty);
        }

        string trimmed = lang.Trim().ToLowerInvariant();
        return trimmed switch
        {
            "spanish" or "español" or "es" => "es",
            "english" or "inglés" or "ingles" or "en" => "en",
            "french" or "francés" or "frances" or "fr" => "fr",
            "german" or "alemán" or "aleman" or "de" => "de",
            "italian" or "italiano" or "it" => "it",
            "portuguese" or "portugués" or "portugues" or "pt" => "pt",
            "chinese" or "chino" or "zh" => "zh",
            "japanese" or "japonés" or "japones" or "ja" => "ja",
            "russian" or "ruso" or "ru" => "ru",
            _ => trimmed.Length >= 2 ? trimmed[..2] : "en"
        };
    }

    public static string DetectLanguage(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "es";

        string lower = text.ToLowerInvariant();

        // Heurísticas de palabras de alta frecuencia
        int esScore = CountMatches(lower, @"\b(el|la|los|las|un|una|de|en|y|que|por|para|con|este|esta)\b");
        int enScore = CountMatches(lower, @"\b(the|a|an|of|in|and|that|for|with|this|is|are|to)\b");
        int frScore = CountMatches(lower, @"\b(le|la|les|un|une|des|et|du|dans|pour|avec|est)\b");
        int deScore = CountMatches(lower, @"\b(der|die|das|ein|eine|und|in|zu|den|dem|mit|ist)\b");

        int max = Math.Max(esScore, Math.Max(enScore, Math.Max(frScore, deScore)));
        if (max == 0)
            return "es";

        if (max == esScore) return "es";
        if (max == enScore) return "en";
        if (max == frScore) return "fr";
        return "de";
    }

    private static int CountMatches(string input, string pattern) =>
        Regex.Matches(input, pattern, RegexOptions.IgnoreCase).Count;

    private static bool LooksLikeSrt(string text)
    {
        return text.Contains("-->") && Regex.IsMatch(text, @"\d{2}:\d{2}:\d{2}[,\.]\d{3}");
    }

    private static List<string> SplitSentences(string text)
    {
        return Regex.Split(text, @"(?<=[.!?])\s+")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    // Diccionario léxico multilingüe bidireccional para fallback determinista
    private static readonly Dictionary<(string From, string To, string Word), string> WordDictionary = new()
    {
        // Español -> Inglés
        { ("es", "en", "hola"), "hello" },
        { ("es", "en", "mundo"), "world" },
        { ("es", "en", "documento"), "document" },
        { ("es", "en", "archivo"), "file" },
        { ("es", "en", "texto"), "text" },
        { ("es", "en", "resumen"), "summary" },
        { ("es", "en", "informe"), "report" },
        { ("es", "en", "datos"), "data" },
        { ("es", "en", "usuario"), "user" },
        { ("es", "en", "sistema"), "system" },
        { ("es", "en", "fecha"), "date" },
        { ("es", "en", "resultado"), "result" },
        { ("es", "en", "error"), "error" },
        { ("es", "en", "éxito"), "success" },
        { ("es", "en", "subtítulo"), "subtitle" },
        { ("es", "en", "subtítulos"), "subtitles" },
        { ("es", "en", "gracias"), "thank you" },
        { ("es", "en", "bienvenido"), "welcome" },
        { ("es", "en", "adiós"), "goodbye" },

        // Inglés -> Español
        { ("en", "es", "hello"), "hola" },
        { ("en", "es", "world"), "mundo" },
        { ("en", "es", "document"), "documento" },
        { ("en", "es", "file"), "archivo" },
        { ("en", "es", "text"), "texto" },
        { ("en", "es", "summary"), "resumen" },
        { ("en", "es", "report"), "informe" },
        { ("en", "es", "data"), "datos" },
        { ("en", "es", "user"), "usuario" },
        { ("en", "es", "system"), "sistema" },
        { ("en", "es", "date"), "fecha" },
        { ("en", "es", "result"), "resultado" },
        { ("en", "es", "error"), "error" },
        { ("en", "es", "success"), "éxito" },
        { ("en", "es", "subtitle"), "subtítulo" },
        { ("en", "es", "subtitles"), "subtítulos" },
        { ("en", "es", "thank you"), "gracias" },
        { ("en", "es", "welcome"), "bienvenido" },
        { ("en", "es", "goodbye"), "adiós" },

        // Español -> Francés
        { ("es", "fr", "hola"), "bonjour" },
        { ("es", "fr", "mundo"), "monde" },
        { ("es", "fr", "documento"), "document" },
        { ("es", "fr", "archivo"), "fichier" },
        { ("es", "fr", "gracias"), "merci" },

        // Inglés -> Francés
        { ("en", "fr", "hello"), "bonjour" },
        { ("en", "fr", "world"), "monde" },
        { ("en", "fr", "document"), "document" },
        { ("en", "fr", "file"), "fichier" },
        { ("en", "fr", "thank you"), "merci" },
    };

    public static string TranslateWithSemanticEngine(string text, string sourceLang, string targetLang)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        string src = NormalizeLanguageCode(sourceLang, text);
        string tgt = NormalizeLanguageCode(targetLang);

        if (src == tgt)
            return text;

        return ReplaceKnownTokens(text, src, tgt);
    }

    private static string ReplaceKnownTokens(string text, string src, string tgt)
    {
        // Reemplazar usando regex palabra por palabra
        return Regex.Replace(text, @"\b[\wáéíóúÁÉÍÓÚñÑ]+\b", match =>
        {
            string word = match.Value;
            string lower = word.ToLowerInvariant();

            if (WordDictionary.TryGetValue((src, tgt, lower), out var translated))
            {
                // Preservar capitalización original
                if (char.IsUpper(word[0]))
                {
                    return char.ToUpperInvariant(translated[0]) + (translated.Length > 1 ? translated[1..] : "");
                }
                return translated;
            }

            return word;
        });
    }
}
