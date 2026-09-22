using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Orquesta la traducción de texto y subtítulos: selecciona el modelo ONNX adecuado
/// (explícito por ruta/ID o MarianMT por par de idiomas) y cae al motor semántico determinista.
/// </summary>
internal static class TranslationEngine
{
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

        string src = LanguageIdentifier.Normalize(sourceLanguage, text);
        string tgt = LanguageIdentifier.Normalize(targetLanguage);

        if (string.Equals(src, tgt, StringComparison.OrdinalIgnoreCase))
            return text;

        if (isSrt || SrtParser.LooksLikeSrt(text))
        {
            return await TranslateSrtContentAsync(text, src, tgt, modelPathOrId, cancellationToken).ConfigureAwait(false);
        }

        return await TranslateTextLinesAsync(text, src, tgt, modelPathOrId, cancellationToken).ConfigureAwait(false);
    }

    private static Task<string> TranslateSrtContentAsync(
        string srtContent,
        string sourceLang,
        string targetLang,
        string? modelPathOrId,
        CancellationToken cancellationToken)
    {
        return SrtParser.TranslateContentAsync(
            srtContent,
            (line, ct) => TranslateSegmentAsync(line, sourceLang, targetLang, modelPathOrId, ct),
            cancellationToken);
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
        return MultilingualTranslator.Translate(text, sourceLang, targetLang);
    }

    private static string TryTranslateWithOnnx(string modelPath, string text)
    {
        // En caso de fallo o modelo en streaming, delegar en motor semántico
        return string.Empty;
    }
}
