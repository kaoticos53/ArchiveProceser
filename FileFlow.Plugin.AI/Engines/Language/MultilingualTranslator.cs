using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Motor lingüístico semántico determinista basado en un léxico multilingüe bidireccional.
/// Se usa como último recurso cuando no hay modelos neuronales (ONNX) disponibles.
/// </summary>
internal static class MultilingualTranslator
{
    /// <summary>
    /// Traduce el texto conservando el resto del contenido y la capitalización original.
    /// </summary>
    public static string Translate(string text, string sourceLang, string targetLang)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        string src = LanguageIdentifier.Normalize(sourceLang, text);
        string tgt = LanguageIdentifier.Normalize(targetLang);

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
}
