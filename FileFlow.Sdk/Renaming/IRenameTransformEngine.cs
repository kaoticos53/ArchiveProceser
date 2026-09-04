namespace FileFlow.Sdk.Renaming;

/// <summary>
/// Contrato del motor de ejecución de transformaciones de renombrado acumulativo.
/// </summary>
public interface IRenameTransformEngine
{
    /// <summary>
    /// Aplica una secuencia ordenada de pasos de transformación sobre el nombre de archivo indicado.
    /// </summary>
    /// <param name="currentFileName">Nombre de archivo de entrada (ej. "foto.jpg").</param>
    /// <param name="item">Contexto del archivo con metadatos y rutas.</param>
    /// <param name="steps">Lista de pasos de transformación a aplicar.</param>
    /// <param name="batchContext">Contexto de lote para contadores y secuencias.</param>
    /// <returns>Resultado con el nombre transformado y trazas diagnósticas.</returns>
    RenameResult Transform(
        string currentFileName,
        FileItemContext item,
        IReadOnlyList<RenameMethodStep> steps,
        RenameBatchContext batchContext,
        bool recordTraces = true);
}
