using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Sdk.Renaming.Handlers;

/// <summary>
/// Maneja la asignación de un nuevo nombre completo basado en plantillas y variables.
/// </summary>
internal sealed class NewNameStepHandler : IRenameStepHandler
{
    public RenameMethodType SupportedType => RenameMethodType.NewName;

    public string Execute(RenameMethodStep step, string targetText, FileItemContext item, RenameBatchContext batchContext)
    {
        return VariableTemplateResolver.Resolve(step.Pattern, item);
    }
}
