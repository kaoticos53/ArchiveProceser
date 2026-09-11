using System.IO;

namespace FileFlow.Sdk.Storage;

/// <summary>
/// Proveedor centralizado de rutas del sistema de archivos para FileFlow Studio.
/// Soporta modo instalado estándar (%AppData%/FileFlow/), modo portable autónomo (data/ en la carpeta del ejecutable)
/// y migración transparente de versiones heredadas.
/// </summary>
public static class AppPaths
{
    private static readonly string DefaultAppDataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FileFlow");
    private static readonly string AppBaseDirectory = AppContext.BaseDirectory;
    private static string? _customDataDirectory;
    private static readonly Lock _lock = new();

    /// <summary>
    /// Indica si la aplicación se está ejecutando en modo portable autónomo.
    /// Se activa automáticamente si existe un archivo 'portable.dat', '.portable' o una carpeta 'data' junto al ejecutable,
    /// o mediante la variable de entorno FILEFLOW_PORTABLE=1.
    /// </summary>
    public static bool IsPortableMode
    {
        get
        {
            if (!string.IsNullOrEmpty(_customDataDirectory)) return true;

            string envPortable = Environment.GetEnvironmentVariable("FILEFLOW_PORTABLE") ?? string.Empty;
            if (envPortable.Equals("1", StringComparison.OrdinalIgnoreCase) || envPortable.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return File.Exists(Path.Combine(AppBaseDirectory, "portable.dat")) ||
                   File.Exists(Path.Combine(AppBaseDirectory, ".portable")) ||
                   Directory.Exists(Path.Combine(AppBaseDirectory, "data"));
        }
    }

    /// <summary>
    /// Permite forzar un directorio raíz de datos personalizado (útil para pruebas unitarias, perfiles y CLI).
    /// </summary>
    public static void SetCustomDataDirectory(string? customPath)
    {
        lock (_lock)
        {
            _customDataDirectory = string.IsNullOrWhiteSpace(customPath) ? null : customPath;
        }
    }

    /// <summary>
    /// Directorio raíz de datos de usuario (Modo Portable: AppBaseDir/data, Modo Instalado: %AppData%/FileFlow/).
    /// </summary>
    public static string RootDirectory
    {
        get
        {
            lock (_lock)
            {
                if (!string.IsNullOrEmpty(_customDataDirectory))
                {
                    return _customDataDirectory;
                }

                if (IsPortableMode)
                {
                    return Path.Combine(AppBaseDirectory, "data");
                }

                return DefaultAppDataRoot;
            }
        }
    }

    // Subcarpetas estructuradas
    public static string ConfigDirectory => Path.Combine(RootDirectory, "config");
    public static string ThemesDirectory => Path.Combine(RootDirectory, "themes");
    public static string PresetsDirectory => Path.Combine(RootDirectory, "presets");
    public static string SamplesDirectory => Path.Combine(RootDirectory, "samples");
    public static string ScriptsDirectory => Path.Combine(RootDirectory, "scripts");
    public static string LogsDirectory => Path.Combine(RootDirectory, "logs");

    /// <summary>
    /// Ruta de salida global por defecto utilizada por los flujos y variables del sistema.
    /// (Modo Portable: AppBaseDir/data/output, Modo Instalado: %USERPROFILE%/Documents/FileFlowStudio/Output).
    /// </summary>
    public static string DefaultGlobalOutputDir
    {
        get
        {
            if (IsPortableMode)
            {
                return Path.Combine(RootDirectory, "output");
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FileFlowStudio", "Output");
        }
    }

    /// <summary>
    /// Ruta de trabajo temporal por defecto para archivos intermedios generados por los nodos.
    /// (Modo Portable: AppBaseDir/data/temp, Modo Instalado: %TEMP%/FileFlowStudio/Temp).
    /// </summary>
    public static string DefaultTempDirectory
    {
        get
        {
            if (IsPortableMode)
            {
                return Path.Combine(RootDirectory, "temp");
            }
            return Path.Combine(Path.GetTempPath(), "FileFlowStudio", "Temp");
        }
    }

    /// <summary>
    /// Directorio que alberga los espacios de trabajo temporales acotados por ejecución (Runs/{ExecutionId}/).
    /// </summary>
    public static string RunsDirectory => Path.Combine(DefaultTempDirectory, "Runs");

    /// <summary>
    /// Limpia de forma segura y recursiva directorios y archivos temporales residuales o abandonados de ejecuciones anteriores.
    /// </summary>
    /// <param name="maxAge">Antigüedad mínima para considerar un temporal como abandonado (por defecto 2 horas).</param>
    /// <returns>Número total de bytes liberados en el disco.</returns>
    public static long CleanupStaleTempDirectories(TimeSpan? maxAge = null)
    {
        TimeSpan effectiveMaxAge = maxAge ?? TimeSpan.FromHours(2);
        DateTime thresholdUtc = DateTime.UtcNow - effectiveMaxAge;
        long totalBytesFreed = 0;

        string[] candidateRoots =
        [
            RunsDirectory,
            Path.Combine(DefaultTempDirectory, "intermediate"),
            Path.Combine(Path.GetTempPath(), "FileFlow_Sessions"),
            Path.Combine(Path.GetTempPath(), "FileFlow_MockData")
        ];

        foreach (var root in candidateRoots)
        {
            if (!Directory.Exists(root)) continue;

            try
            {
                // Limpieza de subdirectorios
                foreach (var subDir in Directory.GetDirectories(root))
                {
                    try
                    {
                        var dirInfo = new DirectoryInfo(subDir);
                        if (dirInfo.LastWriteTimeUtc < thresholdUtc || dirInfo.CreationTimeUtc < thresholdUtc)
                        {
                            long dirSize = GetDirectorySize(subDir);
                            Directory.Delete(subDir, recursive: true);
                            totalBytesFreed += dirSize;
                        }
                    }
                    catch
                    {
                        // Resistencia ante archivos bloqueados o en uso
                    }
                }

                // Limpieza de archivos sueltos
                foreach (var file in Directory.GetFiles(root))
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.LastWriteTimeUtc < thresholdUtc || fileInfo.CreationTimeUtc < thresholdUtc)
                        {
                            long size = fileInfo.Length;
                            File.Delete(file);
                            totalBytesFreed += size;
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        return totalBytesFreed;
    }

    private static long GetDirectorySize(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return 0;
        try
        {
            return Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories)
                .Sum(f => {
                    try { return new FileInfo(f).Length; } catch { return 0L; }
                });
        }
        catch
        {
            return 0;
        }
    }

    // Ficheros estándar de configuración del usuario
    public static string UserPreferencesFile => Path.Combine(ConfigDirectory, "user_preferences.json");
    public static string ExternalToolsFile => Path.Combine(ConfigDirectory, "external_tools.json");
    public static string CustomThemesFile => Path.Combine(ThemesDirectory, "custom_themes.json");
    public static string RenamerPresetsFile => Path.Combine(PresetsDirectory, "renamer_presets.json");
    public static string MediaPresetsFile => Path.Combine(PresetsDirectory, "media_presets.json");
    public static string RegexLibraryFile => Path.Combine(PresetsDirectory, "regex_library.json");
    public static string RenamerSamplesFile => Path.Combine(SamplesDirectory, "renamer_samples.json");
    public static string CrashLogFile => Path.Combine(LogsDirectory, "crash.log");

    /// <summary>
    /// Resuelve una ruta que puede ser absoluta o relativa a la carpeta del ejecutable de la aplicación.
    /// Útil para herramientas portables como tools\ffmpeg\ffmpeg.exe.
    /// </summary>
    public static string ResolveApplicationPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        if (Path.IsPathRooted(path)) return path;

        return Path.GetFullPath(Path.Combine(AppBaseDirectory, path));
    }

    /// <summary>
    /// Garantiza la existencia de toda la jerarquía de directorios de datos y realiza
    /// la migración automática de cualquier fichero ubicado en carpetas heredadas.
    /// </summary>
    public static void EnsureDirectories()
    {
        try
        {
            Directory.CreateDirectory(RootDirectory);
            Directory.CreateDirectory(ConfigDirectory);
            Directory.CreateDirectory(ThemesDirectory);
            Directory.CreateDirectory(PresetsDirectory);
            Directory.CreateDirectory(SamplesDirectory);
            Directory.CreateDirectory(ScriptsDirectory);
            Directory.CreateDirectory(LogsDirectory);
            Directory.CreateDirectory(DefaultTempDirectory);

            if (!IsPortableMode)
            {
                MigrateLegacyLocations();
            }
        }
        catch
        {
            // Resistencia ante entornos con restricciones de permisos temporales
        }
    }

    /// <summary>
    /// Migra de forma no destructiva ficheros existentes en %AppData%/FileFlowStudio/ o en la raíz de %AppData%/FileFlow/.
    /// </summary>
    private static void MigrateLegacyLocations()
    {
        try
        {
            string baseAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            // 1. Migración desde la carpeta heredada %AppData%/FileFlowStudio/
            string legacyDir = Path.Combine(baseAppData, "FileFlowStudio");
            if (Directory.Exists(legacyDir))
            {
                MigrateFile(Path.Combine(legacyDir, "user_preferences.json"), UserPreferencesFile);
                MigrateFile(Path.Combine(legacyDir, "external_tools.json"), ExternalToolsFile);
                MigrateFile(Path.Combine(legacyDir, "media_presets.json"), MediaPresetsFile);
                MigrateFile(Path.Combine(legacyDir, "crash.log"), CrashLogFile);
            }

            // 2. Migración desde la raíz plana de %AppData%/FileFlow/ hacia las nuevas subcarpetas
            MigrateFile(Path.Combine(RootDirectory, "user_preferences.json"), UserPreferencesFile);
            MigrateFile(Path.Combine(RootDirectory, "external_tools.json"), ExternalToolsFile);
            MigrateFile(Path.Combine(RootDirectory, "custom_themes.json"), CustomThemesFile);
            MigrateFile(Path.Combine(RootDirectory, "renamer_presets.json"), RenamerPresetsFile);
            MigrateFile(Path.Combine(RootDirectory, "media_presets.json"), MediaPresetsFile);
            MigrateFile(Path.Combine(RootDirectory, "regex_library.json"), RegexLibraryFile);
            MigrateFile(Path.Combine(RootDirectory, "renamer_samples.json"), RenamerSamplesFile);
            MigrateFile(Path.Combine(RootDirectory, "crash.log"), CrashLogFile);

            // 3. Migración de scripts desde %AppData%/FileFlow/Scripts/ (PascalCase) a scripts/
            string oldScriptsDir = Path.Combine(RootDirectory, "Scripts");
            if (Directory.Exists(oldScriptsDir) && !string.Equals(oldScriptsDir, ScriptsDirectory, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var file in Directory.GetFiles(oldScriptsDir, "*.ffscript"))
                {
                    string dest = Path.Combine(ScriptsDirectory, Path.GetFileName(file));
                    if (!File.Exists(dest))
                    {
                        File.Copy(file, dest, true);
                    }
                }
            }
        }
        catch
        {
            // Migración no bloqueante
        }
    }

    private static void MigrateFile(string sourcePath, string targetPath)
    {
        if (File.Exists(sourcePath) && !File.Exists(targetPath))
        {
            try
            {
                string? destDir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }
                File.Copy(sourcePath, targetPath, false);
            }
            catch
            {
                // Ignorar fallos de I/O en copia preventiva
            }
        }
    }
}
