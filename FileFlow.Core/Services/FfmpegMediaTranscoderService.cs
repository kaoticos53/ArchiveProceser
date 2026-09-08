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

        string fullArgs = $"-y -i \"{inputPath}\" {arguments} \"{outputPath}\"";

        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegExe,
            Arguments = fullArgs,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

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
}
