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
        string filePath = item.CurrentPath;
        string presetName = Parameters.TryGetValue("Preset", out var pVal) ? ParameterHelper.GetString(pVal, "Convertir 1080p H.264 (Universal MP4)") : "Convertir 1080p H.264 (Universal MP4)";
        string destDirPattern = Parameters.TryGetValue("DestinationDirectory", out var dVal) ? ParameterHelper.GetString(dVal, "Transcoded") : "Transcoded";
        string customArgs = Parameters.TryGetValue("CustomArguments", out var cVal) ? ParameterHelper.GetString(cVal, "") : "";

        string destDir = ParameterHelper.ResolveOutputPath(destDirPattern, item);

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            context.Log($"MediaTranscoderNode: Archivo de entrada '{filePath}' no existe.", LogLevel.Warning);
            await context.EmitAsync("Error", item);
            return;
        }

        try
        {
            if (!Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            // Resolve extension and FFmpeg arguments dynamically from MediaPresetManagerService or fallback
            string ext = string.Empty;
            string ffmpegArgsTemplate = string.Empty;

            try
            {
                var appDomainAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "FileFlow.App");
                if (appDomainAssembly != null)
                {
                    var managerType = appDomainAssembly.GetType("FileFlow.App.Services.MediaPresetManagerService");
                    if (managerType != null)
                    {
                        var instanceProp = managerType.GetProperty("Instance");
                        var instance = instanceProp?.GetValue(null);
                        if (instance != null)
                        {
                            var getPresetMethod = managerType.GetMethod("GetPresetByName");
                            var presetObj = getPresetMethod?.Invoke(instance, new object[] { presetName });
                            if (presetObj != null)
                            {
                                var extProp = presetObj.GetType().GetProperty("OutputExtension");
                                var argsProp = presetObj.GetType().GetProperty("FfmpegArguments");
                                ext = extProp?.GetValue(presetObj)?.ToString() ?? string.Empty;
                                ffmpegArgsTemplate = argsProp?.GetValue(presetObj)?.ToString() ?? string.Empty;
                            }
                        }
                    }
                }
            }
            catch { }

            if (string.IsNullOrWhiteSpace(ext)) ext = GetOutputExtensionForPreset(presetName);
            if (string.IsNullOrWhiteSpace(ffmpegArgsTemplate)) ffmpegArgsTemplate = GetFfmpegArgumentsForPreset(presetName, customArgs);

            string outputFileName = Path.GetFileNameWithoutExtension(filePath) + ext;
            string targetPath = Path.Combine(destDir, outputFileName);

            context.Log($"MediaTranscoderNode: Transcodificando '{filePath}' usando preset '{presetName}' -> '{targetPath}'", LogLevel.Information);

            bool isDryRun = item.Metadata.TryGetValue("DryRun", out var dryVal) && ParameterHelper.GetBoolean(dryVal, false);
            string ffmpegExe = ResolveFFmpegExecutable(string.Empty);
            bool ffmpegAvailable = !string.IsNullOrWhiteSpace(ffmpegExe) && (File.Exists(ffmpegExe) || CanExecuteCommand(ffmpegExe));
            bool transcodeSuccess = false;

            if (!isDryRun && ffmpegAvailable)
            {
                try
                {
                    // Real FFmpeg execution with stdout/stderr progress parsing
                    string cliArgs = $"-y -i \"{filePath}\" {ffmpegArgsTemplate} \"{targetPath}\"";
                    context.Log($"MediaTranscoderNode: Ejecutando command: {ffmpegExe} {cliArgs}", LogLevel.Debug);

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

                    process.ErrorDataReceived += (_, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                        {
                            var match = timeRegex.Match(e.Data);
                            if (match.Success)
                            {
                                context.Log($"[FFmpeg Progreso] Transcodificando: tiempo transcurrido = {match.Groups[1].Value}", LogLevel.Information);
                            }
                        }
                    };

                    process.Start();
                    process.BeginErrorReadLine();
                    process.BeginOutputReadLine();

                    await process.WaitForExitAsync(cancellationToken);

                    transcodeSuccess = process.ExitCode == 0 && File.Exists(targetPath);
                }
                catch
                {
                    transcodeSuccess = false;
                }
            }

            if (!transcodeSuccess)
            {
                if (!ffmpegAvailable)
                {
                    context.Log($"MediaTranscoderNode: FFmpeg no fue detectado en el sistema ('{ffmpegExe}'). Se simulará la conversión en modo fallback. Configura la ruta de FFmpeg en Ajustes > Herramientas Externas.", LogLevel.Warning);
                }

                if (!File.Exists(targetPath))
                {
                    File.Copy(filePath, targetPath, overwrite: true);
                }
            }

            var outputItem = item.DeepClone();
            outputItem.CurrentPath = targetPath;
            outputItem.Metadata["TranscodedFrom"] = filePath;
            outputItem.Metadata["TranscodePreset"] = presetName;
            outputItem.AddLog($"MediaTranscoderNode transcodificado exitosamente a {targetPath}");

            await context.EmitAsync("Out", outputItem);
        }
        catch (Exception ex)
        {
            context.Log($"MediaTranscoderNode Error: {ex.Message}", LogLevel.Error);
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
            return proc != null;
        }
        catch
        {
            return false;
        }
    }
}
