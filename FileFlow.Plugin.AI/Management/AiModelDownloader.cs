using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Motor desacoplado de descarga HTTP resiliente de modelos de IA con soporte de espejos múltiples, reporte de progreso y validación de integridad.
/// </summary>
public static class AiModelDownloader
{
    private static readonly Lock _fileLock = new();
    private static readonly ConcurrentDictionary<string, Task<string?>> _activeDownloads = new();

    /// <summary>
    /// Último error detallado producido durante la descarga o verificación de un modelo de IA.
    /// </summary>
    public static string? LastError { get; private set; }

    private static readonly HttpClient _httpClient = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            ConnectTimeout = TimeSpan.FromSeconds(30),
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
            AutomaticDecompression = DecompressionMethods.All
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(30)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 FileFlowStudio/1.0");
        client.DefaultRequestHeaders.Add("Accept", "*/*");
        return client;
    }

    /// <summary>
    /// Descarga un modelo con soporte para reporte de progreso numérico (0.0 a 100.0) y mensajes de estado.
    /// </summary>
    public static async Task<string?> DownloadModelWithProgressAsync(
        string modelId,
        IProgress<double>? progress = null,
        Action<string>? statusLogger = null,
        CancellationToken cancellationToken = default)
    {
        if (!AiModelManager.Catalog.TryGetValue(modelId, out var info))
        {
            statusLogger?.Invoke($"Modelo desconocido: '{modelId}'");
            return null;
        }

        string targetPath = AiModelManager.GetModelPath(info.FileName);

        if (AiModelManager.IsModelAvailable(modelId))
        {
            LastError = null;
            progress?.Report(100.0);
            return targetPath;
        }

        bool isPrimary = false;
        var downloadTask = _activeDownloads.GetOrAdd(modelId, _ =>
        {
            isPrimary = true;
            return DownloadModelInternalAsync(modelId, info, targetPath, progress, statusLogger, cancellationToken);
        });

        if (!isPrimary)
        {
            statusLogger?.Invoke($"Descarga de '{info.Description}' ya en curso, sincronizando...");
        }

        try
        {
            string? result = await downloadTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (result != null)
            {
                progress?.Report(100.0);
            }
            return result;
        }
        finally
        {
            if (isPrimary)
            {
                _activeDownloads.TryRemove(modelId, out _);
            }
        }
    }

    private static async Task<string?> DownloadModelInternalAsync(
        string modelId,
        AiModelInfo info,
        string targetPath,
        IProgress<double>? progress,
        Action<string>? statusLogger,
        CancellationToken cancellationToken)
    {
        try
        {
            LastError = null;
            var urls = AiModelUrlConfig.GetConfiguredUrls(modelId);
            if (urls.Count == 0)
            {
                LastError = $"No hay URLs configuradas para el modelo '{modelId}'.";
                statusLogger?.Invoke($"❌ {LastError}");
                return null;
            }

            string? parentDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(parentDir))
                Directory.CreateDirectory(parentDir);

            string tempPath = targetPath + ".downloading";
            var errors = new List<string>();

            for (int mirrorIndex = 0; mirrorIndex < urls.Count; mirrorIndex++)
            {
                string currentUrl = urls[mirrorIndex];
                string mirrorLabel = urls.Count > 1 ? $" (espejo {mirrorIndex + 1}/{urls.Count})" : string.Empty;

                statusLogger?.Invoke($"⬇️ Conectando{mirrorLabel} para descargar: {info.FriendlyName}...");
                progress?.Report(0.0);

                try
                {
                    using (var response = await _httpClient.GetAsync(
                        currentUrl,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            string failMsg = $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase}) desde {currentUrl}";
                            errors.Add(failMsg);
                            if (mirrorIndex < urls.Count - 1)
                            {
                                statusLogger?.Invoke($"⚠️ {failMsg}. Conmutando al siguiente espejo...");
                            }
                            continue;
                        }

                        long? totalBytes = response.Content.Headers.ContentLength;
                        await using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
                        await using (var fileStream = new FileStream(
                            tempPath,
                            FileMode.Create,
                            FileAccess.Write,
                            FileShare.None,
                            bufferSize: 81920,
                            useAsync: true))
                        {
                            var buffer = new byte[81920];
                            long totalRead = 0;
                            int bytesRead;
                            DateTime lastReport = DateTime.UtcNow;

                            while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                            {
                                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                                totalRead += bytesRead;

                                if ((DateTime.UtcNow - lastReport).TotalMilliseconds >= 200)
                                {
                                    lastReport = DateTime.UtcNow;
                                    if (totalBytes.HasValue && totalBytes.Value > 0)
                                    {
                                        double pct = (double)totalRead / totalBytes.Value * 100.0;
                                        progress?.Report(pct);
                                        statusLogger?.Invoke($"⬇️ Descargando {info.FriendlyName}: {pct:F1}% ({totalRead / 1_048_576.0:F1} / {totalBytes.Value / 1_048_576.0:F1} MB)");
                                    }
                                    else
                                    {
                                        statusLogger?.Invoke($"⬇️ Descargando {info.FriendlyName}: {totalRead / 1_048_576.0:F1} MB");
                                    }
                                }
                            }
                        }
                    }

                    var fi = new FileInfo(tempPath);
                    if (!fi.Exists || fi.Length < info.MinSizeBytes)
                    {
                        CleanupTemp(tempPath);
                        string failMsg = $"Descarga incompleta de {info.FileName} desde {currentUrl}: recibidos {(fi.Exists ? fi.Length : 0):N0} bytes, esperados >= {info.MinSizeBytes:N0} bytes.";
                        errors.Add(failMsg);
                        if (mirrorIndex < urls.Count - 1)
                        {
                            statusLogger?.Invoke($"⚠️ {failMsg}. Conmutando al siguiente espejo...");
                        }
                        continue;
                    }

                    lock (_fileLock)
                    {
                        if (File.Exists(targetPath))
                        {
                            try
                            {
                                File.Delete(targetPath);
                            }
                            catch (Exception delEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"[AiModelDownloader] Target delete before move failed: {delEx.Message}");
                            }
                        }

                        string? destDir = Path.GetDirectoryName(targetPath);
                        if (!string.IsNullOrWhiteSpace(destDir))
                            Directory.CreateDirectory(destDir);

                        File.Move(tempPath, targetPath, overwrite: true);
                    }

                    LastError = null;
                    progress?.Report(100.0);
                    statusLogger?.Invoke($"✅ Modelo descargado correctamente: {info.FileName} ({fi.Length / 1_048_576.0:F1} MB)");
                    return targetPath;
                }
                catch (OperationCanceledException)
                {
                    CleanupTemp(tempPath);
                    throw;
                }
                catch (Exception ex)
                {
                    CleanupTemp(tempPath);
                    string failMsg = $"Excepción al conectar con {currentUrl}: {ex.Message}";
                    errors.Add(failMsg);
                    if (mirrorIndex < urls.Count - 1)
                    {
                        statusLogger?.Invoke($"⚠️ {failMsg}. Conmutando al siguiente espejo...");
                    }
                }
            }

            LastError = $"Fallaron todos los espejos configurados ({urls.Count}) para '{info.FriendlyName}':\n" +
                        string.Join("\n", errors.Select(e => " • " + e));
            statusLogger?.Invoke($"❌ {LastError}");
            return null;
        }
        catch (OperationCanceledException)
        {
            CleanupTemp(targetPath + ".downloading");
            LastError = "Descarga cancelada por el usuario.";
            throw;
        }
        catch (Exception ex)
        {
            CleanupTemp(targetPath + ".downloading");
            LastError = $"Error descargando '{modelId}': {ex.Message}";
            statusLogger?.Invoke($"❌ {LastError}");
            return null;
        }
    }

    private static void CleanupTemp(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AiModelDownloader] Cleanup temp failed: {ex.Message}");
        }
    }
}
