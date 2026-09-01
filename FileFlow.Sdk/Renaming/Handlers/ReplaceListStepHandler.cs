using System.Text.RegularExpressions;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Sdk.Renaming.Handlers;

/// <summary>
/// Maneja la sustitución acumulativa mediante tabla/lista de pares búsqueda y reemplazo.
/// </summary>
internal sealed class ReplaceListStepHandler : IRenameStepHandler
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    public RenameMethodType SupportedType => RenameMethodType.ReplaceList;

    public string Execute(RenameMethodStep step, string targetText, FileItemContext item, RenameBatchContext batchContext)
    {
        if (step.ReplaceList == null || step.ReplaceList.Count == 0)
        {
            return targetText;
        }

        string result = targetText;
        foreach (var entry in step.ReplaceList)
        {
            if (string.IsNullOrEmpty(entry.Find)) continue;

            string findText = VariableTemplateResolver.Resolve(entry.Find, item);
            if (string.IsNullOrEmpty(findText)) continue;

            if (entry.UseRegex)
            {
                var options = entry.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
                var regex = new Regex(findText, options, RegexTimeout);
                result = VariableTemplateResolver.ApplyRegexReplacement(regex, result, entry.ReplaceWith ?? string.Empty, item, replaceAll: true);
            }
            else
            {
                string replaceWith = VariableTemplateResolver.Resolve(entry.ReplaceWith ?? string.Empty, item);
                result = result.Replace(findText, replaceWith, entry.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
            }
        }

        return result;
    }
}
