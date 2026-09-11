using System.Diagnostics;
using System.IO;

namespace FileFlow.Plugin.Archives.Services;

/// <summary>
/// Puente de ejecución desacoplado con la utilidad de línea de comandos de 7-Zip (7z.exe).
/// Soporta la totalidad de formatos (RAR5, CBR, CB7, 7z LZMA2, ZIPX/ZSTD, ZIP64, archivos sólidos y split volumes).
/// </summary>
public static class SevenZipCliRunner
{
    private static readonly string[] Standard7zPaths =
    [
        @"C:\Program Files\7-Zip\7z.exe",
        @"C:\Program Files (x86)\7-Zip\7z.exe",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\7-Zip\7z.exe")
    ];

    private static string? s_cachedSevenZipPath;
    private static bool s_cacheChecked;
    private static readonly System.Threading.Lock s_cacheLock = new();

    /// <summary>
    /// Busca la ruta del ejecutable 7z.exe en el sistema o valida la ruta personalizada proporcionada.
    /// </summary>
    public static string? FindSevenZipExecutable(string? customPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            string expandedCustom = Environment.ExpandEnvironmentVariables(customPath.Trim());
            if (File.Exists(expandedCustom))
            {
                return expandedCustom;
            }
        }

        lock (s_cacheLock)
        {
            if (s_cacheChecked && s_cachedSevenZipPath != null && File.Exists(s_cachedSevenZipPath))
            {
                return s_cachedSevenZipPath;
            }

            foreach (var standardPath in Standard7zPaths)
            {
                if (File.Exists(standardPath))
                {
                    s_cachedSevenZipPath = standardPath;
                    s_cacheChecked = true;
                    return standardPath;
                }
            }

            // Búsqueda en PATH de entorno
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var dir in paths)
                {
                    try
                    {
                        var candidate = Path.Combine(dir, "7z.exe");
                        if (File.Exists(candidate))
                        {
                            s_cachedSevenZipPath = candidate;
                            s_cacheChecked = true;
                            return candidate;
                        }

                        var candidateZa = Path.Combine(dir, "7za.exe");
                        if (File.Exists(candidateZa))
                        {
                            s_cachedSevenZipPath = candidateZa;
                            s_cacheChecked = true;
                            return candidateZa;
                        }
                    }
                    catch { }
                }
            }

            s_cacheChecked = true;
            s_cachedSevenZipPath = null;
            return null;
        }
    }

    public static bool IsAvailable(string? customPath = null)
    {
        return FindSevenZipExecutable(customPath) != null;
    }

    /// <summary>
    /// Extrae un archivo comprimido utilizando 7-Zip CLI de forma asíncrona.
    /// </summary>
    public static async Task<ArchiveExtractionResult> ExtractAsync(
        string archivePath,
        string targetDir,
        string? password = null,
        string? customSevenZipPath = null,
        CancellationToken cancellationToken = default)
    {
        string? exePath = FindSevenZipExecutable(customSevenZipPath);
        if (string.IsNullOrEmpty(exePath))
        {
            return new ArchiveExtractionResult(
                Success: false,
                ArchivePath: archivePath,
                DestinationDirectory: targetDir,
                ExtractedFiles: [],
                TotalEntriesCount: 0,
                EngineUsed: "SevenZipCLI",
                ValidPasswordUsed: null,
                ErrorMessage: "El ejecutable 7z.exe no fue encontrado en las rutas estándar de Windows ni en el PATH del sistema.",
                Warnings: []);
        }

        string fullTargetDir = Path.GetFullPath(targetDir);
        string fullTargetDirWithSep = Path.TrimEndingDirectorySeparator(fullTargetDir) + Path.DirectorySeparatorChar;

        if (!Directory.Exists(fullTargetDir))
        {
            Directory.CreateDirectory(fullTargetDir);
        }

        // Argumentos:
        // x: extraer con rutas completas
        // -y: asumir 'Yes' a todas las preguntas (sobrescribir)
        // -o"{targetDir}": carpeta de salida (sin espacio entre -o y la ruta)
        // -p"{password}": contraseña
        // -bso1 -bse2: redirigir stdout/stderr de forma limpia
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add("x");
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add($"-o{fullTargetDir}");
        if (!string.IsNullOrEmpty(password))
        {
            psi.ArgumentList.Add($"-p{password}");
        }
        else
        {
            psi.ArgumentList.Add("-p-"); // No solicitar contraseña de forma interactiva
        }
        psi.ArgumentList.Add(archivePath);

        var warnings = new List<string>();
        string stdout = string.Empty;
        string stderr = string.Empty;
        int exitCode = -1;

        try
        {
            using var process = new Process { StartInfo = psi };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            stdout = await stdoutTask;
            stderr = await stderrTask;
            exitCode = process.ExitCode;

            // Escaneo de archivos extraídos en targetDir
            var extractedFiles = Directory.Exists(fullTargetDir)
                ? Directory.GetFiles(fullTargetDir, "*.*", SearchOption.AllDirectories).ToList()
                : [];

            // Validación post-extracción de seguridad anti Zip-Slip
            foreach (var file in extractedFiles)
            {
                string fullFilePath = Path.GetFullPath(file);
                if (!fullFilePath.StartsWith(fullTargetDirWithSep, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(fullFilePath, fullTargetDir, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(file); } catch { }
                    throw new System.Security.SecurityException($"Zip Slip detectado tras extracción por 7z! Fichero fuera de límites: '{file}'");
                }
            }

            // Códigos de salida de 7-Zip:
            // 0: Éxito normal
            // 1: Advertencia (ej. ficheros bloqueados por otro proceso, pero extracción realizada)
            // 2: Error fatal (contraseña incorrecta, archivo corrupto o formato no soportado)
            // 7: Error de argumentos en línea de comandos
            // 8: Memoria insuficiente
            // 255: Cancelado por usuario
            if (exitCode == 0 || (exitCode == 1 && extractedFiles.Count > 0))
            {
                if (exitCode == 1)
                {
                    warnings.Add($"7-Zip finalizó con advertencias (código 1). Mensaje: {stderr.Trim()}");
                }

                return new ArchiveExtractionResult(
                    Success: true,
                    ArchivePath: archivePath,
                    DestinationDirectory: fullTargetDir,
                    ExtractedFiles: extractedFiles,
                    TotalEntriesCount: extractedFiles.Count,
                    EngineUsed: "SevenZipCLI",
                    ValidPasswordUsed: password,
                    ErrorMessage: null,
                    Warnings: warnings);
            }
            else
            {
                string combinedErr = !string.IsNullOrWhiteSpace(stderr) ? stderr.Trim() : stdout.Trim();
                if (string.IsNullOrWhiteSpace(combinedErr))
                {
                    combinedErr = $"7-Zip falló con código de salida {exitCode}.";
                }

                return new ArchiveExtractionResult(
                    Success: false,
                    ArchivePath: archivePath,
                    DestinationDirectory: fullTargetDir,
                    ExtractedFiles: extractedFiles,
                    TotalEntriesCount: extractedFiles.Count,
                    EngineUsed: "SevenZipCLI",
                    ValidPasswordUsed: null,
                    ErrorMessage: combinedErr,
                    Warnings: warnings);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ArchiveExtractionResult(
                Success: false,
                ArchivePath: archivePath,
                DestinationDirectory: fullTargetDir,
                ExtractedFiles: [],
                TotalEntriesCount: 0,
                EngineUsed: "SevenZipCLI",
                ValidPasswordUsed: null,
                ErrorMessage: ex.Message,
                Warnings: warnings);
        }
    }
}
