namespace FileFlow.Sdk.Renaming.Handlers;

/// <summary>
/// Contrato interno para la ejecución de un paso específico de transformación de renombrado.
/// </summary>
internal interface IRenameStepHandler
{
    /// <summary>
    /// Tipo de método de renombrado soportado por este handler.
    /// </summary>
    RenameMethodType SupportedType { get; }

    /// <summary>
    /// Ejecuta la transformación sobre el texto objetivo.
    /// </summary>
    string Execute(RenameMethodStep step, string targetText, FileItemContext item, RenameBatchContext batchContext);
}
