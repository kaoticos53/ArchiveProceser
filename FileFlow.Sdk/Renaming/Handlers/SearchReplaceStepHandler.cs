using System.Text.RegularExpressions;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Sdk.Renaming.Handlers;

/// <summary>
/// Maneja la búsqueda y reemplazo de texto o expresiones regulares con resolución de variables.
/// </summary>
internal sealed class SearchReplaceStepHandler : IRenameStepHandler
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    public RenameMethodType SupportedType => RenameMethodType.SearchReplace;

    public string Execute(RenameMethodStep step, string targetText, FileItemContext item, RenameBatchContext batchContext)
    {
        if (string.IsNullOrEmpty(step.SearchText))
        {
            return targetText;
        }

        string searchPattern = VariableTemplateResolver.Resolve(step.SearchText, item);
        if (string.IsNullOrEmpty(searchPattern))
        {
            return targetText;
        }

        if (step.UseRegex)
        {
            var options = step.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
            var regex = new Regex(searchPattern, options, RegexTimeout);
            return VariableTemplateResolver.ApplyRegexReplacement(regex, targetText, step.ReplaceText ?? string.Empty, item, step.ReplaceAll);
        }

        string replacePattern = VariableTemplateResolver.Resolve(step.ReplaceText ?? string.Empty, item);
        var comparison = step.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if (step.ReplaceAll)
        {
            return targetText.Replace(searchPattern, replacePattern, comparison);
        }

        int idx = targetText.IndexOf(searchPattern, comparison);
        if (idx >= 0)
        {
            return string.Concat(targetText.AsSpan(0, idx), replacePattern, targetText.AsSpan(idx + searchPattern.Length));
        }

        return targetText;
    }
}
