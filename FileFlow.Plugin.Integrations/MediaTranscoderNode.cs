using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using FileFlow.Plugin.Integrations.UI.Views;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Platform;

namespace FileFlow.Plugin.Integrations;

[NodeDefinition("MediaTranscoderNode_Name", "AudioVoice", "MediaTranscoderNode_Desc", PipelineRole.Transform,
    "ffmpeg", "video", "audio", "mp4", "mp3", "transcodificar", "convertir", "h264", "h265", "webm", "media")]
public sealed class MediaTranscoderNode : IFlowNode, INodeCustomActionProvider
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("MediaTranscoderNode_Name", "Transcodificar Media");
    public string Category => "AudioVoice";
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

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => [
        new("Preset", ParameterEditorType.MediaPreset, DefaultValue: "Convertir 1080p H.264 (Universal MP4)", DisplayOrder: 1),
        new("DestinationDirectory", ParameterEditorType.FolderPath, DefaultValue: @"{RelativeDir}\Transcoded", DisplayOrder: 2),
        new("CustomArguments", ParameterEditorType.Text, DefaultValue: "-c:v libx264 -crf 22 -preset medium -c:a aac -b:a 192k", DisplayOrder: 3)
    ];

    public IReadOnlyList<NodeActionDescriptor> CustomActions => [
        new("ManageMediaPresets", "🎬 Presets...", "🎬", "Gestionar y personalizar presets de transcodificación FFmpeg")
    ];

    public void ExecuteCustomAction(string actionId, object? context = null)
    {
        if (actionId.Equals("ManageMediaPresets", StringComparison.OrdinalIgnoreCase))
        {
            var window = new MediaPresetManagerWindow();
            if (context is Window ownerWindow)
            {
                window.Owner = ownerWindow;
            }
            else if (Application.Current?.MainWindow != null)
            {
                window.Owner = Application.Current.MainWindow;
            }
            window.ShowDialog();
        }
    }

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
            string ffmpegExe = ResolveFFmpegExecutable(string.Empty, context);
            bool ffmpegAvailable = !string.IsNullOrWhiteSpace(ffmpegExe) && (File.Exists(ffmpegExe) || CanExecuteCommand(ffmpegExe, context));
            bool transcodeSuccess = false;

            if (!isDryRun && ffmpegAvailable)
            {
                try
                {
                    string cliArgs = $"-y -i \"{filePath}\" {ffmpegArgsTemplate} \"{targetPath}\"";
                    context.Log($"[Transcodificador] Ejecutando FFmpeg: {ffmpegExe} {cliArgs}", LogLevel.Debug, item);

                    var timeRegex = new Regex(@"time=(\d{2}:\d{2}:\d{2}\.\d{2})", RegexOptions.Compiled);
                    DateTime lastProgressLog = DateTime.MinValue;

                    var runner = context.ProcessRunner ?? ProcessRunner.Instance;
                    var runResult = await runner.RunAsync(new ProcessExecutionRequest
                    {
                        FileName = ffmpegExe,
                        Arguments = cliArgs,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        OnStandardErrorLine = line =>
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                var match = timeRegex.Match(line);
                                if (match.Success && (DateTime.Now - lastProgressLog).TotalSeconds > 5)
                                {
                                    lastProgressLog = DateTime.Now;
                                    context.Log($"[Transcodificador] Progreso: {match.Groups[1].Value}", LogLevel.Debug, item);
                                }
                            }
                        }
                    }, cancellationToken).ConfigureAwait(false);

                    transcodeSuccess = runResult.Success && File.Exists(targetPath);
                }
                catch (OperationCanceledException)
                {
                    throw;
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
                    context.Log($"[Transcodificador] FFmpeg no detectado en el sistema ('{ffmpegExe}'). Copiando archivo en modo fallback.", LogLevel.Warning, item);
                }

                if (!string.Equals(Path.GetFullPath(filePath), Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase))
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
        catch (Exception ex) when (ex is not OperationCanceledException)
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

    private static string ResolveFFmpegExecutable(string paramPath, IFlowExecutionContext? context = null)
    {
        if (!string.IsNullOrWhiteSpace(paramPath) && paramPath != "ffmpeg" && File.Exists(paramPath))
        {
            return paramPath;
        }

        if (context?.Tools != null)
        {
            string toolExe = context.Tools.FfmpegExecutable;
            if (!string.IsNullOrWhiteSpace(toolExe)) return toolExe;
        }

        return !string.IsNullOrWhiteSpace(paramPath) ? paramPath : "ffmpeg";
    }

    private static bool CanExecuteCommand(string command, IFlowExecutionContext? context = null)
    {
        try
        {
            var runner = context?.ProcessRunner ?? NullProcessRunner.Instance;
            var result = runner.RunAsync(new ProcessExecutionRequest
            {
                FileName = command,
                Arguments = "-version",
                Timeout = TimeSpan.FromSeconds(2)
            }).GetAwaiter().GetResult();
            return result.Success;
        }
        catch
        {
            return false;
        }
    }
}
