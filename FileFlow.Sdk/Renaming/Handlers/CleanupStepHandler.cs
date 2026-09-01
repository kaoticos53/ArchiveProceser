using System.Text;
using System.Text.RegularExpressions;

namespace FileFlow.Sdk.Renaming.Handlers;

/// <summary>
/// Maneja la limpieza, trim, colapso de espacios, desinfección de caracteres no válidos y normalización Unicode.
/// </summary>
internal sealed class CleanupStepHandler : IRenameStepHandler
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    public RenameMethodType SupportedType => RenameMethodType.TrimClean;

    public string Execute(RenameMethodStep step, string targetText, FileItemContext item, RenameBatchContext batchContext)
    {
        if (string.IsNullOrEmpty(targetText))
        {
            return targetText;
        }

        string result = targetText;

        if (step.TrimWhitespace)
        {
            result = result.Trim();
        }

        if (step.CollapseSpaces)
        {
            result = Regex.Replace(result, @"\s+", " ", RegexOptions.None, RegexTimeout);
        }

        if (step.SanitizeInvalidChars)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            if (result.IndexOfAny(invalid) >= 0)
            {
                var sb = new StringBuilder(result.Length);
                foreach (char c in result)
                {
                    sb.Append(invalid.Contains(c) ? step.InvalidCharReplacement : c);
                }
                result = sb.ToString();
            }
        }

        if (step.NormalizationMode != UnicodeNormalizationMode.None)
        {
            result = step.NormalizationMode switch
            {
                UnicodeNormalizationMode.FormC => result.Normalize(NormalizationForm.FormC),
                UnicodeNormalizationMode.FormD => result.Normalize(NormalizationForm.FormD),
                UnicodeNormalizationMode.FormKC => result.Normalize(NormalizationForm.FormKC),
                UnicodeNormalizationMode.FormKD => result.Normalize(NormalizationForm.FormKD),
                _ => result
            };
        }

        return result;
    }
}
