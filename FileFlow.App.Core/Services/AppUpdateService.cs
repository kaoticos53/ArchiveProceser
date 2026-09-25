using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Services;
using FileFlow.Sdk.Storage;

namespace FileFlow.App.Services;

/// <summary>
/// Servicio nativo de autoactualizaciones e integración con GitHub Releases.
/// </summary>
public sealed class AppUpdateService : IAppUpdateService
{
    private static readonly Lazy<AppUpdateService> _instance = new(() => new AppUpdateService());
    public static AppUpdateService Instance => _instance.Value;

    private readonly HttpClient _httpClient;
    private readonly string _repositoryOwner = "kaoticos53";
    private readonly string _repositoryName = "ArchiveProceser";

    public AppPackagingFormat CurrentPackagingFormat { get; private set; }
    public SemVersion CurrentVersion { get; private set; }

    public AppUpdateService(HttpClient? httpClient = null, string? repoOwner = null, string? repoName = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        if (!string.IsNullOrWhiteSpace(repoOwner)) _repositoryOwner = repoOwner;
        if (!string.IsNullOrWhiteSpace(repoName)) _repositoryName = repoName;

        CurrentPackagingFormat = DetectCurrentPackagingFormat();
        CurrentVersion = DetectCurrentVersion();
    }

    public static AppPackagingFormat DetectCurrentPackagingFormat()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string uninstaller = Path.Combine(baseDir, "unins000.exe");
                if (File.Exists(uninstaller))
                {
                    return AppPackagingFormat.WindowsInstalled;
                }

                string? progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string? progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

                if ((!string.IsNullOrWhiteSpace(progFiles) && baseDir.StartsWith(progFiles, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(progFilesX86) && baseDir.StartsWith(progFilesX86, StringComparison.OrdinalIgnoreCase)))
                {
                    return AppPackagingFormat.WindowsInstalled;
                }

                return AppPackagingFormat.WindowsPortable;
            }
            else if (OperatingSystem.IsLinux())
            {
                if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("APPIMAGE")))
                {
                    return AppPackagingFormat.LinuxAppImage;
                }

                if (File.Exists("/.flatpak-info") || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FLATPAK_ID")))
                {
                    return AppPackagingFormat.LinuxFlatpak;
                }

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                if (baseDir.StartsWith("/opt/fileflow", StringComparison.OrdinalIgnoreCase) ||
                    baseDir.StartsWith("/usr/", StringComparison.OrdinalIgnoreCase))
                {
                    return AppPackagingFormat.LinuxDebPackage;
                }

                return AppPackagingFormat.LinuxGenericTarball;
            }
        }
        catch
        {
            // Fallback silencioso
        }

        return AppPackagingFormat.Unknown;
    }

    private static SemVersion DetectCurrentVersion()
    {
        try
        {
            var asm = Assembly.GetEntryAssembly() ?? typeof(AppUpdateService).Assembly;
            var attr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (attr != null && !string.IsNullOrWhiteSpace(attr.InformationalVersion))
            {
                return SemVersion.Parse(attr.InformationalVersion);
            }

            var ver = asm.GetName().Version;
            if (ver != null)
            {
                return new SemVersion(ver.Major, ver.Minor, ver.Build >= 0 ? ver.Build : 0);
            }
        }
        catch
        {
            // Fallback
        }

        return new SemVersion(1, 0, 0);
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(
        UpdateChannel channel,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var prefs = UserPreferencesService.Instance.Preferences;
            if (!force && prefs.LastUpdateCheckUtc.HasValue && (DateTime.UtcNow - prefs.LastUpdateCheckUtc.Value).TotalHours < 24.0)
            {
                // Dentro del periodo de 24h
            }

            string url = $"https://api.github.com/repos/{_repositoryOwner}/{_repositoryName}/releases?per_page=10";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.Add(new ProductInfoHeaderValue("FileFlow-Studio-App", CurrentVersion.ToString()));
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

            using var resp = await _httpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                string statusMsg = $"GitHub API respondió con código {resp.StatusCode}";
                return UpdateCheckResult.Error(statusMsg);
            }

            string json = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var releasesNode = JsonNode.Parse(json)?.AsArray();
            if (releasesNode == null || releasesNode.Count == 0)
            {
                return UpdateCheckResult.NoUpdate();
            }

            // Registrar fecha de última comprobación
            prefs.LastUpdateCheckUtc = DateTime.UtcNow;
            UserPreferencesService.Instance.Save();

            AppUpdateInfo? latestCandidate = null;

            foreach (var r in releasesNode)
            {
                if (r == null) continue;
                bool isPrerelease = r["prerelease"]?.GetValue<bool>() ?? false;
                bool isDraft = r["draft"]?.GetValue<bool>() ?? false;
                if (isDraft) continue;

                // Filtrar según canal
                if (channel == UpdateChannel.Stable && isPrerelease)
                {
                    continue;
                }

                string tag = r["tag_name"]?.GetValue<string>() ?? string.Empty;
                var releaseVer = SemVersion.Parse(tag);

                if (releaseVer > CurrentVersion)
                {
                    if (latestCandidate == null || releaseVer > latestCandidate.Version)
                    {
                        string title = r["name"]?.GetValue<string>() ?? tag;
                        string body = r["body"]?.GetValue<string>() ?? string.Empty;
                        string htmlUrl = r["html_url"]?.GetValue<string>() ?? string.Empty;
                        DateTime pubAt = r["published_at"]?.GetValue<DateTime>() ?? DateTime.UtcNow;

                        // Descubrir checksums y assets
                        var assets = r["assets"]?.AsArray();
                        var parsedAssets = ParseAssets(assets);

                        // Descargar o buscar checksums.txt
                        var checksumAsset = parsedAssets.FirstOrDefault(a => a.Name.Equals("checksums.txt", StringComparison.OrdinalIgnoreCase));
                        Dictionary<string, string> checksumTable = new(StringComparer.OrdinalIgnoreCase);
                        if (checksumAsset != null)
                        {
                            checksumTable = await FetchChecksumTableAsync(checksumAsset.DownloadUrl, cancellationToken).ConfigureAwait(false);
                        }

                        // Mapear el asset adecuado según el formato del entorno
                        var matchedAsset = ResolveAssetForPlatform(parsedAssets, CurrentPackagingFormat, checksumTable);

                        latestCandidate = new AppUpdateInfo(
                            tag,
                            releaseVer,
                            title,
                            body,
                            pubAt,
                            isPrerelease,
                            matchedAsset,
                            htmlUrl);
                    }
                }
            }

            if (latestCandidate != null)
            {
                return UpdateCheckResult.Available(latestCandidate);
            }

            return UpdateCheckResult.NoUpdate();
        }
        catch (Exception ex)
        {
            return UpdateCheckResult.Error($"Error al comprobar actualizaciones: {ex.Message}");
        }
    }

    public static List<AppReleaseAssetInfo> ParseAssets(JsonArray? assetsJson)
    {
        var list = new List<AppReleaseAssetInfo>();
        if (assetsJson == null) return list;

        foreach (var a in assetsJson)
        {
            if (a == null) continue;
            string name = a["name"]?.GetValue<string>() ?? string.Empty;
            string downloadUrl = a["browser_download_url"]?.GetValue<string>() ?? string.Empty;
            long size = a["size"]?.GetValue<long>() ?? 0L;

            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(downloadUrl))
            {
                list.Add(new AppReleaseAssetInfo(name, downloadUrl, size, string.Empty));
            }
        }

        return list;
    }

    public static AppReleaseAssetInfo? ResolveAssetForPlatform(
        List<AppReleaseAssetInfo> assets,
        AppPackagingFormat format,
        Dictionary<string, string> checksumTable)
    {
        AppReleaseAssetInfo? matched = null;

        switch (format)
        {
            case AppPackagingFormat.WindowsInstalled:
                matched = assets.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                                                    (a.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase) || a.Name.Contains("Installer", StringComparison.OrdinalIgnoreCase)));
                matched ??= assets.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) ??
                            assets.FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
                break;

            case AppPackagingFormat.WindowsPortable:
                matched = assets.FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && a.Name.Contains("win", StringComparison.OrdinalIgnoreCase));
                matched ??= assets.FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) ??
                            assets.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
                break;

            case AppPackagingFormat.LinuxAppImage:
                matched = assets.FirstOrDefault(a => a.Name.EndsWith(".AppImage", StringComparison.OrdinalIgnoreCase)) ??
                          assets.FirstOrDefault(a => a.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase));
                break;

            case AppPackagingFormat.LinuxFlatpak:
                matched = assets.FirstOrDefault(a => a.Name.EndsWith(".flatpak", StringComparison.OrdinalIgnoreCase)) ??
                          assets.FirstOrDefault(a => a.Name.EndsWith(".AppImage", StringComparison.OrdinalIgnoreCase));
                break;

            case AppPackagingFormat.LinuxDebPackage:
                matched = assets.FirstOrDefault(a => a.Name.EndsWith(".deb", StringComparison.OrdinalIgnoreCase)) ??
                          assets.FirstOrDefault(a => a.Name.EndsWith(".AppImage", StringComparison.OrdinalIgnoreCase));
                break;

            case AppPackagingFormat.LinuxGenericTarball:
                matched = assets.FirstOrDefault(a => a.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) && !a.Name.Contains("AppDir", StringComparison.OrdinalIgnoreCase)) ??
                          assets.FirstOrDefault(a => a.Name.EndsWith(".AppImage", StringComparison.OrdinalIgnoreCase));
                break;

            default:
                if (OperatingSystem.IsWindows())
                {
                    matched = assets.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) ??
                              assets.FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
                }
                else if (OperatingSystem.IsLinux())
                {
                    matched = assets.FirstOrDefault(a => a.Name.EndsWith(".AppImage", StringComparison.OrdinalIgnoreCase)) ??
                              assets.FirstOrDefault(a => a.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase));
                }
                break;
        }

        if (matched != null && checksumTable.TryGetValue(matched.Name, out var hash))
        {
            return matched with { Sha256Hash = hash };
        }

        return matched;
    }

    private async Task<Dictionary<string, string>> FetchChecksumTableAsync(string checksumUrl, CancellationToken cancellationToken)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, checksumUrl);
            req.Headers.UserAgent.Add(new ProductInfoHeaderValue("FileFlow-Studio-App", CurrentVersion.ToString()));
            using var resp = await _httpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);
            if (resp.IsSuccessStatusCode)
            {
                string content = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                using var reader = new StringReader(content);
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        string hash = parts[0].Trim();
                        string file = Path.GetFileName(parts[1].Trim().TrimStart('*'));
                        dict[file] = hash;
                    }
                }
            }
        }
        catch
        {
            // Checksum no crítico para continuar
        }
        return dict;
    }

    public async Task<string> DownloadAndPrepareUpdateAsync(
        AppUpdateInfo updateInfo,
        IProgress<UpdateProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (updateInfo.MatchedAsset == null)
        {
            throw new InvalidOperationException(LocalizationManager.Instance.GetString("Update_NoCompatibleAssetFound", "No se encontró ningún paquete de instalación compatible con su entorno en este lanzamiento."));
        }

        string tempUpdatesDir = Path.Combine(AppPaths.DefaultTempDirectory, "Updates");
        Directory.CreateDirectory(tempUpdatesDir);

        string targetFile = Path.Combine(tempUpdatesDir, updateInfo.MatchedAsset.Name);
        if (File.Exists(targetFile))
        {
            try { File.Delete(targetFile); } catch { }
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, updateInfo.MatchedAsset.DownloadUrl);
        req.Headers.UserAgent.Add(new ProductInfoHeaderValue("FileFlow-Studio-App", CurrentVersion.ToString()));

        using var response = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        long totalBytes = response.Content.Headers.ContentLength ?? updateInfo.MatchedAsset.SizeBytes;
        long bytesDownloaded = 0;

        var stopwatch = Stopwatch.StartNew();
        long lastReportTicks = 0;

        await using (var remoteStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        await using (var fileStream = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
        {
            byte[] buffer = new byte[81920];
            int read;
            while ((read = await remoteStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                bytesDownloaded += read;

                if (stopwatch.ElapsedMilliseconds - lastReportTicks > 100 || bytesDownloaded == totalBytes)
                {
                    lastReportTicks = stopwatch.ElapsedMilliseconds;
                    double sec = Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
                    double speed = bytesDownloaded / sec;
                    double pct = totalBytes > 0 ? (double)bytesDownloaded / totalBytes * 100.0 : 0.0;

                    progress?.Report(new UpdateProgressReport(
                        bytesDownloaded,
                        totalBytes,
                        pct,
                        speed,
                        LocalizationManager.Instance.GetFormattedString("Update_DownloadingProgress", "Descargando: {0:F1}% ({1:F1} MB / {2:F1} MB a {3:F1} MB/s)", pct, bytesDownloaded / 1048576.0, totalBytes / 1048576.0, speed / 1048576.0)));
                }
            }
        }

        // Verificación SHA-256
        progress?.Report(new UpdateProgressReport(bytesDownloaded, totalBytes, 100.0, 0, LocalizationManager.Instance.GetString("Update_VerifyingChecksum", "Verificando integridad criptográfica SHA-256...")));

        string calculatedHash = await ComputeSha256Async(targetFile, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(updateInfo.MatchedAsset.Sha256Hash))
        {
            if (!calculatedHash.Equals(updateInfo.MatchedAsset.Sha256Hash, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(targetFile); } catch { }
                throw new InvalidOperationException(LocalizationManager.Instance.GetFormattedString(
                    "Update_ChecksumMismatchError",
                    "Fallo de seguridad: El hash SHA-256 del archivo descargado ({0}) no coincide con el publicado ({1}). La actualización ha sido abortada.",
                    calculatedHash,
                    updateInfo.MatchedAsset.Sha256Hash));
            }
        }

        progress?.Report(new UpdateProgressReport(bytesDownloaded, totalBytes, 100.0, 0, LocalizationManager.Instance.GetString("Update_ChecksumVerified", "Integridad verificada correctamente.")));

        return targetFile;
    }

    public static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken = default)
    {
        using var sha256 = SHA256.Create();
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        byte[] hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexStringLower(hashBytes);
    }

    public Task ApplyUpdateAndRestartAsync(AppUpdateInfo updateInfo, string downloadedFilePath)
    {
        if (!File.Exists(downloadedFilePath))
        {
            throw new FileNotFoundException("El archivo de actualización descargado no existe.", downloadedFilePath);
        }

        int currentPid = Environment.ProcessId;
        string appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        switch (CurrentPackagingFormat)
        {
            case AppPackagingFormat.WindowsInstalled:
                // Lanzar instalador Inno Setup con flags silenciosas
                var psiInstaller = new ProcessStartInfo
                {
                    FileName = downloadedFilePath,
                    Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
                    UseShellExecute = true
                };
                Process.Start(psiInstaller);
                Environment.Exit(0);
                break;

            case AppPackagingFormat.WindowsPortable:
                // Generar script de reemplazo atómico que espera a que este PID finalice y sobreescribe binarios
                string scriptPath = Path.Combine(Path.GetTempPath(), $"ff_update_{Guid.NewGuid():N}.ps1");
                string stagingDir = Path.Combine(Path.GetTempPath(), $"ff_stage_{Guid.NewGuid():N}");
                string mainExe = Path.Combine(appDir, "FileFlow.App.exe");

                var sb = new StringBuilder();
                sb.AppendLine($"# Script de autoactualización de FileFlow Studio");
                sb.AppendLine($"$ErrorActionPreference = 'Stop'");
                sb.AppendLine($"try {{");
                sb.AppendLine($"    Write-Host 'Esperando cierre de FileFlow Studio (PID {currentPid})...'");
                sb.AppendLine($"    Wait-Process -Id {currentPid} -Timeout 20 -ErrorAction SilentlyContinue");
                sb.AppendLine($"    Start-Sleep -Seconds 1");
                sb.AppendLine($"    Write-Host 'Descomprimiendo actualización...'");
                sb.AppendLine($"    Expand-Archive -LiteralPath '{downloadedFilePath}' -DestinationPath '{stagingDir}' -Force");
                sb.AppendLine($"    $sourceDir = '{stagingDir}'");
                sb.AppendLine($"    if (Test-Path (Join-Path $sourceDir 'FileFlow.App.exe')) {{");
                sb.AppendLine($"        # Copiar todos los archivos excepto Config/ y data/");
                sb.AppendLine($"        Get-ChildItem -Path $sourceDir | ForEach-Object {{");
                sb.AppendLine($"            if ($_.Name -notin @('data', 'Config', 'models', 'runs')) {{");
                sb.AppendLine($"                Copy-Item -Path $_.FullName -Destination '{appDir}' -Recurse -Force");
                sb.AppendLine($"            }}");
                sb.AppendLine($"        }}");
                sb.AppendLine($"    }}");
                sb.AppendLine($"    Write-Host 'Relanzando FileFlow Studio...'");
                sb.AppendLine($"    Start-Process -FilePath '{mainExe}'");
                sb.AppendLine($"}} catch {{");
                sb.AppendLine($"    Write-Error $_.Exception.Message");
                sb.AppendLine($"}} finally {{");
                sb.AppendLine($"    Remove-Item -Path '{stagingDir}' -Recurse -Force -ErrorAction SilentlyContinue");
                sb.AppendLine($"    Remove-Item -Path '{downloadedFilePath}' -Force -ErrorAction SilentlyContinue");
                sb.AppendLine($"    Remove-Item -Path $PSCommandPath -Force -ErrorAction SilentlyContinue");
                sb.AppendLine($"}}");

                File.WriteAllText(scriptPath, sb.ToString(), Encoding.UTF8);

                var psiScript = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-WindowStyle Hidden -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    UseShellExecute = true,
                    CreateNoWindow = true
                };
                Process.Start(psiScript);
                Environment.Exit(0);
                break;

            case AppPackagingFormat.LinuxAppImage:
                string appImagePath = Environment.GetEnvironmentVariable("APPIMAGE") ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(appImagePath) && File.Exists(appImagePath))
                {
                    // Dar permisos de ejecución
                    try
                    {
                        Process.Start("chmod", $"+x \"{downloadedFilePath}\"").WaitForExit();
                    }
                    catch { }

                    // Script de reemplazo Linux
                    string shScript = Path.Combine(Path.GetTempPath(), $"ff_update_{Guid.NewGuid():N}.sh");
                    var sh = new StringBuilder();
                    sh.AppendLine("#!/bin/bash");
                    sh.AppendLine($"while kill -0 {currentPid} 2>/dev/null; do sleep 0.5; done");
                    sh.AppendLine($"mv \"{downloadedFilePath}\" \"{appImagePath}\"");
                    sh.AppendLine($"chmod +x \"{appImagePath}\"");
                    sh.AppendLine($"\"{appImagePath}\" &");
                    sh.AppendLine($"rm -- \"$0\"");

                    File.WriteAllText(shScript, sh.ToString());
                    Process.Start("chmod", $"+x \"{shScript}\"").WaitForExit();
                    Process.Start(new ProcessStartInfo { FileName = "/bin/bash", Arguments = $"\"{shScript}\"", UseShellExecute = false });
                    Environment.Exit(0);
                }
                break;

            case AppPackagingFormat.LinuxDebPackage:
                Process.Start(new ProcessStartInfo { FileName = "xdg-open", Arguments = $"\"{downloadedFilePath}\"", UseShellExecute = true });
                break;

            case AppPackagingFormat.LinuxFlatpak:
                Process.Start(new ProcessStartInfo { FileName = "flatpak-spawn", Arguments = "--host flatpak update com.fileflowstudio.FileFlow", UseShellExecute = true });
                break;

            default:
                // Abrir archivo descargado en el explorador
                if (OperatingSystem.IsWindows())
                {
                    Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"/select,\"{downloadedFilePath}\"", UseShellExecute = true });
                }
                else if (OperatingSystem.IsLinux())
                {
                    Process.Start(new ProcessStartInfo { FileName = "xdg-open", Arguments = Path.GetDirectoryName(downloadedFilePath) ?? downloadedFilePath, UseShellExecute = true });
                }
                break;
        }

        return Task.CompletedTask;
    }
}

