using System.Text.RegularExpressions;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Sdk.Renaming.Handlers;

/// <summary>
/// Maneja la eliminación de caracteres por posición o por coincidencia de texto/regex.
/// </summary>
internal sealed class RemoveStepHandler : IRenameStepHandler
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    public RenameMethodType SupportedType => RenameMethodType.Remove;

    public string Execute(RenameMethodStep step, string targetText, FileItemContext item, RenameBatchContext batchContext)
    {
        if (string.IsNullOrEmpty(targetText))
        {
            return targetText;
        }

        if (!string.IsNullOrEmpty(step.SearchText) || !string.IsNullOrEmpty(step.Pattern))
        {
            string search = !string.IsNullOrEmpty(step.SearchText) ? step.SearchText : step.Pattern;
            string searchPattern = VariableTemplateResolver.Resolve(search, item);
            if (string.IsNullOrEmpty(searchPattern))
            {
                return targetText;
            }

            if (step.UseRegex)
            {
                var options = step.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
                var regex = new Regex(searchPattern, options, RegexTimeout);
                return regex.Replace(targetText, string.Empty);
            }
            return targetText.Replace(searchPattern, string.Empty, step.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
        }

        int count = Math.Max(1, step.CharacterCount);
        int startIndex = RenameIndexCalculator.CalculateRemoveStartIndex(step.Position, step.PositionIndex, targetText.Length, count);

        if (startIndex < 0 || startIndex >= targetText.Length)
        {
            return targetText;
        }

        int actualCount = Math.Min(count, targetText.Length - startIndex);
        return targetText.Remove(startIndex, actualCount);
    }
}
