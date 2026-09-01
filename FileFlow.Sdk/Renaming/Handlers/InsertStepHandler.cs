using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Sdk.Renaming.Handlers;

/// <summary>
/// Maneja la inserción de texto o variables en una posición específica.
/// </summary>
internal sealed class InsertStepHandler : IRenameStepHandler
{
    public RenameMethodType SupportedType => RenameMethodType.Insert;

    public string Execute(RenameMethodStep step, string targetText, FileItemContext item, RenameBatchContext batchContext)
    {
        string textToInsert = VariableTemplateResolver.Resolve(step.Pattern, item);
        if (string.IsNullOrEmpty(textToInsert))
        {
            return targetText;
        }

        int index = RenameIndexCalculator.CalculateInsertIndex(step.Position, step.PositionIndex, targetText.Length);
        return targetText.Insert(index, textToInsert);
    }
}
