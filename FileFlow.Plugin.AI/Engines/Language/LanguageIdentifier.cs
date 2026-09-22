using System;
using System.Text.RegularExpressions;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Identificación y normalización de códigos de idioma a partir de nombres de idioma
/// o de heurísticas de vocabulario de alta frecuencia.
/// </summary>
internal static class LanguageIdentifier
{
    /// <summary>
    /// Normaliza un nombre o código de idioma ("Spanish", "español", "es") a su código ISO de dos letras.
    /// Cuando se solicita autodetección, delega en <see cref="Detect"/> usando la muestra de texto dada.
    /// </summary>
    public static string Normalize(string lang, string? textSample = null)
    {
        if (string.IsNullOrWhiteSpace(lang) || lang.Equals("AutoDetect", StringComparison.OrdinalIgnoreCase) || lang.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return Detect(textSample ?? string.Empty);
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

    /// <summary>
    /// Detecta heurísticamente el idioma del texto comparando palabras funcionales de alta frecuencia.
    /// </summary>
    public static string Detect(string text)
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
}
