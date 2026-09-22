using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Sdk;
using FileFlow.Sdk.TemplateEngine;
using Microsoft.ML.OnnxRuntime;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Fachada pública del motor de inferencia de lenguaje y procesamiento de lenguaje natural (NLP) in-process.
/// Expone traducción neuronal multilingüe con preservación de marcas de tiempo en subtítulos (.srt),
/// síntesis y extracción con modelos LLM locales y transformación dinámica de prompts de visión.
/// La implementación efectiva vive en las clases especializadas del subespacio <c>Engines/Language</c>:
/// <see cref="TranslationEngine"/>, <see cref="LlmInferenceEngine"/>, <see cref="SrtParser"/>,
/// <see cref="LanguageIdentifier"/> y <see cref="MultilingualTranslator"/>.
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

    public static Task<string> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        bool isSrt = false,
        string? modelPathOrId = null,
        CancellationToken cancellationToken = default)
        => TranslationEngine.TranslateAsync(text, sourceLanguage, targetLanguage, isSrt, modelPathOrId, cancellationToken);

    // ──────────────────────────────────────────────────────────────────────────
    // 2. Procesamiento de Texto con LLM Local
    // ──────────────────────────────────────────────────────────────────────────

    public static Task<LlmExecutionResult> GenerateLlmAsync(
        string taskType,
        string systemPrompt,
        string userPrompt,
        string outputFormat = "Markdown",
        double temperature = 0.2,
        int maxTokens = 1024,
        string? modelPathOrId = null,
        CancellationToken cancellationToken = default)
        => LlmInferenceEngine.GenerateAsync(taskType, systemPrompt, userPrompt, outputFormat, temperature, maxTokens, modelPathOrId, cancellationToken);

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
        string targetLangCode = LanguageIdentifier.Normalize(targetLanguage);
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
        => LanguageIdentifier.Normalize(lang, textSample);

    public static string DetectLanguage(string text)
        => LanguageIdentifier.Detect(text);

    public static string TranslateWithSemanticEngine(string text, string sourceLang, string targetLang)
        => MultilingualTranslator.Translate(text, sourceLang, targetLang);

    /// <summary>
    /// Libera deterministamente todas las sesiones ONNX en caché de LanguageInferenceEngine.
    /// </summary>
    public static void ClearSessionCache()
    {
        foreach (var lazy in _sessions.Values)
        {
            if (lazy.IsValueCreated)
            {
                try { lazy.Value.Dispose(); } catch { }
            }
        }
        _sessions.Clear();
    }
}
