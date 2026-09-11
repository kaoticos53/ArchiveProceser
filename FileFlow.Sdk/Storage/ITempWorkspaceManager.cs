namespace FileFlow.Sdk.Storage;

/// <summary>
/// Contrato del gestor de ciclo de vida de archivos y carpetas temporales para ejecuciones de flujos.
/// Proporciona un espacio de trabajo acotado y aislado por ejecución y garantiza la purga determinista de disco.
/// </summary>
public interface ITempWorkspaceManager
{
    /// <summary>
    /// Directorio raíz temporal dedicado exclusivamente a la ejecución actual (ej. %TEMP%/FileFlowStudio/Runs/{ExecutionId}/).
    /// </summary>
    string ExecutionTempDirectory { get; }

    /// <summary>
    /// Crea y registra una subcarpeta estructurada dentro del espacio de trabajo de la ejecución actual.
    /// </summary>
    /// <param name="purpose">Nombre descriptivo o categoría de la subcarpeta (ej. "intermediate", "opt", "audio").</param>
    /// <returns>Ruta absoluta a la subcarpeta creada.</returns>
    string CreateSubdirectory(string purpose);

    /// <summary>
    /// Registra un archivo temporal específico para ser eliminado automáticamente al concluir la ejecución del flujo.
    /// </summary>
    void RegisterTemporaryFile(string filePath);

    /// <summary>
    /// Registra un directorio temporal para ser eliminado recursivamente al concluir la ejecución del flujo.
    /// </summary>
    void RegisterTemporaryDirectory(string directoryPath);

    /// <summary>
    /// Ejecuta la limpieza completa de todo el espacio de trabajo de la ejecución y los recursos temporales registrados.
    /// </summary>
    /// <returns>Número total de bytes liberados en el disco.</returns>
    Task<long> CleanupExecutionWorkspaceAsync(CancellationToken cancellationToken = default);
}
