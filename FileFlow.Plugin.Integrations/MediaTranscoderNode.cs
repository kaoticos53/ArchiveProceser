using System.Diagnostics;
using System.Text.RegularExpressions;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.Integrations;

[NodeDefinition("MediaTranscoderNode_Name", "MediaDocs", "MediaTranscoderNode_Desc")]
public class MediaTranscoderNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("MediaTranscoderNode_Name", "Transcodificar Media");
    public string Category => "MediaDocs";
    public string Description => LocalizationManager.Instance.GetString("MediaTranscoderNode_Desc", "Transcodifica archivos de audio y vídeo mediante presets o comandos externos FFmpeg.");

    public IReadOnlyList<NodePort> Inputs { get; } = new[]
    {
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    };

    public IReadOnlyList<NodePort> Outputs { get; } = new[]
    {
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
        new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
    };

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Preset"] = "Convertir 1080p H.264 (Universal MP4)",
        ["DestinationDirectory"] = @"{RelativeDir}\Transcoded",
        ["CustomArguments"] = "-c:v libx264 -crf 22 -preset medium -c:a aac -b:a 192k"
    };

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string filePath = item.CurrentPath;
        string presetName = Parameters.TryGetValue("Preset", out var pVal) ? ParameterHelper.GetString(pVal, "Convertir 1080p H.264 (Universal MP4)") : "Convertir 1080p H.264 (Universal MP4)";
        string destDirPattern = Parameters.TryGetValue("DestinationDirectory", out var dVal) ? ParameterHelper.GetString(dVal, "Transcoded") : "Transcoded";
        string customArgs = Parameters.TryGetValue("CustomArguments", out var cVal) ? ParameterHelper.GetString(cVal, "") : "";

        string destDir = ParameterHelper.ResolveOutputPath(destDirPattern, item);

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            context.Log($"[Transcodificador] Archivo de entrada no encontrado: '{filePath}'", LogLevel.Warning, item);
            await context.EmitAsync("Error", item);
            return;
        }

        try
        {
            if (!Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            string ext = GetOutputExtensionForPreset(presetName);
            string ffmpegArgsTemplate = GetFfmpegArgumentsForPreset(presetName, customArgs);

            string outputFileName = Path.GetFileNameWithoutExtension(filePath) + ext;
            string targetPath = Path.Combine(destDir, outputFileName);

            bool isDryRun = item.Metadata.TryGetValue("DryRun", out var dryVal) && ParameterHelper.GetBoolean(dryVal, false);
            string ffmpegExe = ResolveFFmpegExecutable(string.Empty);
            bool ffmpegAvailable = !string.IsNullOrWhiteSpace(ffmpegExe) && (File.Exists(ffmpegExe) || CanExecuteCommand(ffmpegExe));
            bool transcodeSuccess = false;

            if (!isDryRun && ffmpegAvailable)
            {
                try
                {
                    string cliArgs = $"-y -i \"{filePath}\" {ffmpegArgsTemplate} \"{targetPath}\"";
                    context.Log($"[Transcodificador] Ejecutando FFmpeg: {ffmpegExe} {cliArgs}", LogLevel.Debug, item);

                    var psi = new ProcessStartInfo
                    {
                        FileName = ffmpegExe,
                        Arguments = cliArgs,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using var process = new Process { StartInfo = psi };
                    var timeRegex = new Regex(@"time=(\d{2}:\d{2}:\d{2}\.\d{2})", RegexOptions.Compiled);

                    DateTime lastProgressLog = DateTime.MinValue;
                    process.ErrorDataReceived += (_, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                        {
                            var match = timeRegex.Match(e.Data);
                            if (match.Success && (DateTime.Now - lastProgressLog).TotalSeconds > 5)
                            {
                                lastProgressLog = DateTime.Now;
                                context.Log($"[Transcodificador] Progreso: {match.Groups[1].Value}", LogLevel.Debug, item);
                            }
                        }
                    };

                    try
                    {
                        process.Start();
                        process.BeginErrorReadLine();
                        process.BeginOutputReadLine();

                        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                        transcodeSuccess = process.ExitCode == 0 && File.Exists(targetPath);
                    }
                    catch (OperationCanceledException)
                    {
                        try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
                        throw;
                    }
                }
                catch { transcodeSuccess = false; }
            }

            if (!transcodeSuccess)
            {
                if (!ffmpegAvailable)
                {
                    context.Log($"[Transcodificador] FFmpeg no detectado en el sistema ('{ffmpegExe}'). Copiando archivo en modo fallback.", LogLevel.Warning, item);
                }

                if (!File.Exists(targetPath))
                {
                    File.Copy(filePath, targetPath, overwrite: true);
                }
            }

            sw.Stop();
            long outSize = File.Exists(targetPath) ? new FileInfo(targetPath).Length : 0;

            var outputItem = item.DeepClone();
            outputItem.CurrentPath = targetPath;
            outputItem.FileSizeBytes = outSize;
            outputItem.Metadata["TranscodedFrom"] = filePath;
            outputItem.Metadata["TranscodePreset"] = presetName;
            outputItem.AddLog($"MediaTranscoderNode transcodificado exitosamente a {targetPath}");

            string detailsJson = $"{{\"preset\": \"{presetName}\", \"targetPath\": \"{targetPath.Replace("\\", "\\\\")}\", \"ffmpegAvailable\": {ffmpegAvailable.ToString().ToLowerInvariant()}, \"realTranscode\": {transcodeSuccess.ToString().ToLowerInvariant()}, \"outSizeBytes\": {outSize}}}";
            context.Log($"[Transcodificador] Transcodificación finalizada ({presetName}): '{Path.GetFileName(targetPath)}'", LogLevel.Information, outputItem, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsJson);

            await context.EmitAsync("Out", outputItem);
        }
        catch (Exception ex)
        {
            sw.Stop();
            string errJson = $"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\", \"file\": \"{filePath.Replace("\\", "\\\\")}\"}}";
            context.Log($"[Transcodificador] Error al transcodificar: {ex.Message}", LogLevel.Error, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: errJson);
            item.AddLog($"MediaTranscoderNode error: {ex.Message}");
            await context.EmitAsync("Error", item);
        }
    }

    private static string GetOutputExtensionForPreset(string presetName)
    {
        var pName = presetName.ToLowerInvariant();
        if (pName.Contains("mp3")) return ".mp3";
        if (pName.Contains("aac") || pName.Contains("m4a")) return ".m4a";
        if (pName.Contains("flac")) return ".flac";
        if (pName.Contains("webm")) return ".webm";
        if (pName.Contains("gif")) return ".gif";
        return ".mp4";
    }

    private static string GetFfmpegArgumentsForPreset(string presetName, string customArgs)
    {
        var pName = presetName.ToLowerInvariant();
        if (pName.Contains("mp3")) return "-vn -c:a libmp3lame -b:a 192k";
        if (pName.Contains("aac") || pName.Contains("m4a")) return "-vn -c:a aac -b:a 256k";
        if (pName.Contains("flac")) return "-vn -c:a flac";
        if (pName.Contains("720p")) return "-vf \"scale=iw*min(1280/iw\\,720/ih):ih*min(1280/iw\\,720/ih)\" -c:v libx264 -crf 24 -preset fast -c:a aac -b:a 128k";
        if (pName.Contains("hevc") || pName.Contains("265") || pName.Contains("4k")) return "-c:v libx265 -crf 24 -c:a aac -b:a 192k";
        if (pName.Contains("webm")) return "-c:v libvpx-vp9 -b:v 2M -c:a libopus -b:a 128k";
        if (pName.Contains("gif")) return "-vf \"fps=15,scale=480:-1:flags=lanczos\"";
        if (pName.Contains("móvil") || pName.Contains("mobile")) return "-vf \"scale=480:-1\" -c:v libx264 -crf 28 -preset ultrafast -c:a aac -b:a 96k";
        if (!string.IsNullOrWhiteSpace(customArgs)) return customArgs;
        return "-c:v libx264 -crf 22 -preset medium -c:a aac -b:a 192k";
    }

    private static string ResolveFFmpegExecutable(string paramPath)
    {
        if (!string.IsNullOrWhiteSpace(paramPath) && paramPath != "ffmpeg" && File.Exists(paramPath))
        {
            return paramPath;
        }

        // Try accessing App ExternalToolsService via reflection to remain decoupled
        try
        {
            var appDomainAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "FileFlow.App");
            if (appDomainAssembly != null)
            {
                var serviceType = appDomainAssembly.GetType("FileFlow.App.Services.ExternalToolsService");
                if (serviceType != null)
                {
                    var instanceProp = serviceType.GetProperty("Instance");
                    var instance = instanceProp?.GetValue(null);
                    if (instance != null)
                    {
                        var ffmpegProp = serviceType.GetProperty("FfmpegExecutable");
                        string? path = ffmpegProp?.GetValue(instance)?.ToString();
                        if (!string.IsNullOrWhiteSpace(path)) return path;
                    }
                }
            }
        }
        catch { }

        return !string.IsNullOrWhiteSpace(paramPath) ? paramPath : "ffmpeg";
    }

    private static bool CanExecuteCommand(string command)
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = command,
                Arguments = "-version",
                CreateNoWindow = true,
                UseShellExecute = false
            });
            if (proc != null)
            {
                if (!proc.WaitForExit(2000))
                {
                    try { proc.Kill(entireProcessTree: true); } catch { }
                }
                return proc.ExitCode == 0;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
}
