using System.Globalization;
using System.Text;

namespace FileFlow.Sdk.Renaming.Handlers;

/// <summary>
/// Maneja la conversión de mayúsculas y minúsculas (Lowercase, Uppercase, TitleCase, SentenceCase, CapitalizeFirst).
/// </summary>
internal sealed class CaseStepHandler : IRenameStepHandler
{
    public RenameMethodType SupportedType => RenameMethodType.CaseConversion;

    public string Execute(RenameMethodStep step, string targetText, FileItemContext item, RenameBatchContext batchContext)
    {
        if (string.IsNullOrEmpty(targetText))
        {
            return targetText;
        }

        return step.CaseType switch
        {
            CaseTransformType.Lowercase => targetText.ToLowerInvariant(),
            CaseTransformType.Uppercase => targetText.ToUpperInvariant(),
            CaseTransformType.TitleCase => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(targetText.ToLowerInvariant()),
            CaseTransformType.CapitalizeFirst => char.ToUpperInvariant(targetText[0]) + targetText.Substring(1),
            CaseTransformType.SentenceCase => ToSentenceCase(targetText),
            _ => targetText
        };
    }

    private static string ToSentenceCase(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        var sb = new StringBuilder(text.Length);
        bool capitalizeNext = true;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (char.IsLetter(c))
            {
                sb.Append(capitalizeNext ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
                capitalizeNext = false;
            }
            else
            {
                sb.Append(c);
                if (c == '.' || c == '!' || c == '?')
                {
                    capitalizeNext = true;
                }
            }
        }

        return sb.ToString();
    }
}
