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
    private static readonly ConcurrentDictionary<string, bool> _downloadInProgress = new();

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

        // Evitar descargas concurrentes del mismo modelo
        if (!_downloadInProgress.TryAdd(modelId, true))
        {
            statusLogger?.Invoke($"Descarga de '{info.Description}' ya en curso...");
            for (int i = 0; i < 600; i++)
            {
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                if (AiModelManager.IsModelAvailable(modelId))
                {
                    LastError = null;
                    progress?.Report(100.0);
                    return targetPath;
                }
                if (!_downloadInProgress.ContainsKey(modelId)) break;
            }
            return AiModelManager.IsModelAvailable(modelId) ? targetPath : null;
        }

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
                            string statusDetails = $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase})";
                            string failMsg = $"Fallo en enlace {currentUrl}: {statusDetails}";
                            errors.Add(failMsg);
                            if (mirrorIndex < urls.Count - 1)
                            {
                                statusLogger?.Invoke($"⚠️ {failMsg}. Conmutando al siguiente espejo...");
                            }
                            continue;
                        }

                        long? totalBytes = response.Content.Headers.ContentLength;
                        long totalRead = 0;
                        int lastReportedPercent = -1;

                        await using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
                        await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                        {
                            var buffer = new byte[81920];
                            int bytesRead;

                            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                            {
                                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                                totalRead += bytesRead;

                                if (totalBytes.HasValue && totalBytes.Value > 0)
                                {
                                    double percent = (double)totalRead * 100.0 / totalBytes.Value;
                                    progress?.Report(percent);

                                    int intPercent = (int)percent;
                                    if (intPercent != lastReportedPercent && intPercent % 10 == 0)
                                    {
                                        lastReportedPercent = intPercent;
                                        statusLogger?.Invoke($"Descargando {Path.GetFileName(info.FileName)}{mirrorLabel}: {intPercent}% ({totalRead / 1_048_576.0:F1} / {totalBytes.Value / 1_048_576.0:F1} MB)");
                                    }
                                }
                            }

                            await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                        }
                    }

                    // Una vez cerrado el fileStream en Windows, verificar tamaño y mover
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
                            try { File.Delete(targetPath); } catch { }
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

            // Si se agotaron todas las URLs sin éxito
            LastError = $"Fallaron todos los espejos configurados ({urls.Count}) para '{info.FriendlyName}':\n" +
                        string.Join("\n", errors.Select(e => " • " + e));
            statusLogger?.Invoke($"❌ {LastError}");
            return null;
        }
        catch (OperationCanceledException)
        {
            CleanupTemp(AiModelManager.GetModelPath(info.FileName) + ".downloading");
            LastError = "Descarga cancelada por el usuario.";
            throw;
        }
        catch (Exception ex)
        {
            CleanupTemp(AiModelManager.GetModelPath(info.FileName) + ".downloading");
            LastError = $"Error descargando '{modelId}': {ex.Message}";
            statusLogger?.Invoke($"❌ {LastError}");
            return null;
        }
        finally
        {
            _downloadInProgress.TryRemove(modelId, out _);
        }
    }

    private static void CleanupTemp(string tempPath)
    {
        try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
    }
}
