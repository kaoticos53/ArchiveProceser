namespace FileFlow.Sdk.Services;

/// <summary>
/// Implementación nula / no-op del servicio de actualización para tests y entornos deshabilitados.
/// </summary>
public sealed class NullAppUpdateService : IAppUpdateService
{
    public static NullAppUpdateService Instance { get; } = new();

    public AppPackagingFormat CurrentPackagingFormat => AppPackagingFormat.Unknown;

    public SemVersion CurrentVersion => new(1, 0, 0);

    public Task<UpdateCheckResult> CheckForUpdatesAsync(UpdateChannel channel, bool force = false, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(UpdateCheckResult.NoUpdate());
    }

    public Task<string> DownloadAndPrepareUpdateAsync(AppUpdateInfo updateInfo, IProgress<UpdateProgressReport>? progress = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(string.Empty);
    }

    public Task ApplyUpdateAndRestartAsync(AppUpdateInfo updateInfo, string downloadedFilePath)
    {
        return Task.CompletedTask;
    }
}

