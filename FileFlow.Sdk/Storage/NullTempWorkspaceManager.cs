namespace FileFlow.Sdk.Storage;

/// <summary>
/// Implementación nula/fallback de ITempWorkspaceManager para contextos aislados, tests o modo virtual.
/// </summary>
public sealed class NullTempWorkspaceManager : ITempWorkspaceManager
{
    public static readonly NullTempWorkspaceManager Instance = new();

    public string ExecutionTempDirectory => AppPaths.DefaultTempDirectory;

    public string CreateSubdirectory(string purpose)
    {
        string dir = Path.Combine(AppPaths.DefaultTempDirectory, string.IsNullOrWhiteSpace(purpose) ? "misc" : purpose);
        try
        {
            Directory.CreateDirectory(dir);
        }
        catch { }
        return dir;
    }

    public void RegisterTemporaryFile(string filePath) { }

    public void RegisterTemporaryDirectory(string directoryPath) { }

    public Task<long> CleanupExecutionWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(0L);
    }
}
