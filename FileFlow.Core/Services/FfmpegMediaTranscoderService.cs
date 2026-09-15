using System.Diagnostics;
using System.IO;
using FileFlow.Sdk.Services;

namespace FileFlow.Core.Services;

/// <summary>
/// Motor de transcodificación multimedia basado en FFmpeg desacoplado del sistema operativo.
/// </summary>
public class FfmpegMediaTranscoderService : IMediaTranscoderService
{
    private readonly IExternalToolsService _toolsService;

    public FfmpegMediaTranscoderService(IExternalToolsService? toolsService = null)
    {
        _toolsService = toolsService ?? ExternalToolsService.Instance;
    }

    public bool IsAvailable() => _toolsService.IsToolAvailable("ffmpeg");
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
        _toolsService.IsToolAvailableAsync("ffmpeg", cancellationToken);

    public async Task<bool> TranscodeAsync(
        string inputPath,
        string outputPath,
        string arguments,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath))
        {
            return false;
        }

        string ffmpegExe = _toolsService.FfmpegExecutable;
        if (string.IsNullOrWhiteSpace(ffmpegExe))
        {
            return false;
        }

        string? destDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(destDir) && !Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegExe,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(inputPath);

        if (!string.IsNullOrWhiteSpace(arguments))
        {
            foreach (var token in TokenizeArguments(arguments))
            {
                startInfo.ArgumentList.Add(token);
            }
        }

        startInfo.ArgumentList.Add(outputPath);

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();

            // Esperar asíncronamente con soporte para cancelación
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return process.ExitCode == 0 && File.Exists(outputPath);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch { }
            throw;
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<string> TokenizeArguments(string args)
    {
        if (string.IsNullOrWhiteSpace(args)) yield break;

        var sb = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < args.Length; i++)
        {
            char c = args[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (sb.Length > 0)
                {
                    yield return sb.ToString();
                    sb.Clear();
                }
            }
            else
            {
                sb.Append(c);
            }
        }

        if (sb.Length > 0)
        {
            yield return sb.ToString();
        }
    }
}
