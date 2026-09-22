namespace FileFlow.Sdk;

/// <summary>
/// Extracción segura de parámetros para nodos de flujo, evitando desbordamientos e
/// <c>InvalidCastException</c> cuando los valores provienen de <c>JsonElement</c>, la UI o cadenas.
///
/// Los cuatro métodos de conversión delegados en <see cref="ParameterValueConverter"/>, que es la
/// implementación única que también usa <c>FlowNodeBase.GetParameter&lt;T&gt;</c>. Se conservan aquí como
/// fachada para los llamantes existentes de la app y de los nodos.
/// </summary>
public static class ParameterHelper
{
    /// <summary>Convierte un parámetro a booleano. Ver el contrato en <see cref="ParameterValueConverter"/>.</summary>
    public static bool GetBoolean(object? value, bool defaultValue = false) =>
        ParameterValueConverter.ConvertTo(value, defaultValue);

    /// <summary>Convierte un parámetro a entero. Ver el contrato en <see cref="ParameterValueConverter"/>.</summary>
    public static int GetInt32(object? value, int defaultValue = 0) =>
        ParameterValueConverter.ConvertTo(value, defaultValue);

    /// <summary>Convierte un parámetro a decimal. Ver el contrato en <see cref="ParameterValueConverter"/>.</summary>
    public static double GetDouble(object? value, double defaultValue = 0.0) =>
        ParameterValueConverter.ConvertTo(value, defaultValue);

    /// <summary>Convierte un parámetro a texto. Ver el contrato en <see cref="ParameterValueConverter"/>.</summary>
    public static string GetString(object? value, string defaultValue = "") =>
        ParameterValueConverter.ConvertTo(value, defaultValue);

    /// <summary>
    /// Resuelve una plantilla de ruta de salida. Si la ruta resuelta es relativa,
    /// se ancla automáticamente bajo la Ruta Global de Salida (GlobalOutputDir) si está configurada,
    /// o bajo el directorio origen del archivo (SourceRootPath / OriginalPath / CurrentPath).
    /// </summary>
    public static string ResolveOutputPath(string targetPathPattern, FileItemContext context, string? globalOutputDir = null, string? sourceRootPath = null)
    {
        if (string.IsNullOrWhiteSpace(targetPathPattern))
        {
            targetPathPattern = "{FileName}";
        }

        string? effectiveGlobalOutputDir = globalOutputDir;
        if (string.IsNullOrWhiteSpace(effectiveGlobalOutputDir) &&
            context.Metadata.TryGetValue("GlobalOutputDir", out var godVal) && godVal != null)
        {
            effectiveGlobalOutputDir = godVal.ToString();
        }

        string resolved = TemplateEngine.VariableTemplateResolver.Resolve(targetPathPattern, context, sourceRootPath);

        if (string.IsNullOrWhiteSpace(resolved))
        {
            resolved = CrossPlatformPath.GetFileName(context.CurrentPath);
        }

        // Si ya es una ruta absoluta
        if (CrossPlatformPath.IsPathFullyQualified(resolved))
        {
            if (CrossPlatformPath.IsWindowsDrivePath(resolved) || CrossPlatformPath.IsWindowsUncPath(resolved))
            {
                return CrossPlatformPath.NormalizeWindowsPath(resolved);
            }
            string unixResolved = resolved.Replace('\\', '/');
            return OperatingSystem.IsWindows() ? resolved : Path.GetFullPath(unixResolved);
        }

        // Comprobar si el patrón original solicitaba explícitamente una ruta relativa al directorio de origen
        bool isExplicitlySourceRelative = targetPathPattern.Contains("{RelativeDir}", StringComparison.OrdinalIgnoreCase) ||
                                         targetPathPattern.Contains("{RelativeDirectory}", StringComparison.OrdinalIgnoreCase) ||
                                         targetPathPattern.Contains("{RelativePath}", StringComparison.OrdinalIgnoreCase) ||
                                         targetPathPattern.Contains("{RelativeFilePath}", StringComparison.OrdinalIgnoreCase) ||
                                         targetPathPattern.Contains("{Archive:RelativeDir}", StringComparison.OrdinalIgnoreCase) ||
                                         targetPathPattern.Contains("{Archive:OriginalArchiveRelativeDir}", StringComparison.OrdinalIgnoreCase) ||
                                         targetPathPattern.Contains("{Archive:RelativeFilePath}", StringComparison.OrdinalIgnoreCase) ||
                                         targetPathPattern.Contains("{Archive:OriginalArchiveRelativePath}", StringComparison.OrdinalIgnoreCase) ||
                                         targetPathPattern.Contains("{SourceDir}", StringComparison.OrdinalIgnoreCase) ||
                                         targetPathPattern.Contains("{OriginalDir}", StringComparison.OrdinalIgnoreCase);

        // Directorio base de origen
        string? baseDir = null;
        if (context.Metadata.TryGetValue("SourceRootPath", out var srpVal) && srpVal != null && !string.IsNullOrWhiteSpace(srpVal.ToString()))
        {
            baseDir = srpVal.ToString();
        }
        else if (!string.IsNullOrWhiteSpace(sourceRootPath))
        {
            baseDir = sourceRootPath;
        }
        else
        {
            string? itemPath = !string.IsNullOrWhiteSpace(context.OriginalPath) ? context.OriginalPath : context.CurrentPath;
            if (!string.IsNullOrWhiteSpace(itemPath))
            {
                baseDir = CrossPlatformPath.GetDirectoryName(itemPath);
            }
        }

        string finalPath;

        // 1. Si el patrón era explícitamente relativo al origen (ej. "{RelativeDir}\Output"), anclar bajo el directorio origen
        if (isExplicitlySourceRelative && !string.IsNullOrWhiteSpace(baseDir))
        {
            finalPath = CrossPlatformPath.Combine(baseDir, resolved);
        }
        // 2. Si hay GlobalOutputDir configurado, anclar bajo GlobalOutputDir
        else if (!string.IsNullOrWhiteSpace(effectiveGlobalOutputDir))
        {
            finalPath = CrossPlatformPath.Combine(effectiveGlobalOutputDir, resolved);
        }
        // 3. Fallback: anclar bajo el directorio del archivo origen
        else if (!string.IsNullOrWhiteSpace(baseDir))
        {
            finalPath = CrossPlatformPath.Combine(baseDir, resolved);
        }
        else
        {
            finalPath = resolved;
        }

        if (!OperatingSystem.IsWindows() && finalPath.StartsWith('/'))
        {
            finalPath = finalPath.Replace('\\', '/');
        }

        return finalPath;
    }

    /// <summary>
    /// Resuelve el directorio de salida para un nodo que genera archivos intermedios.
    /// Si outputPattern no está especificado o está en blanco, genera automáticamente una subcarpeta aleatoria
    /// dentro del directorio temporal de trabajo (context.TemporaryDirectory) para prevenir colisiones entre nodos.
    /// </summary>
    public static string ResolveIntermediateOutputDir(string? outputPattern, FileItemContext item, IFlowExecutionContext context)
    {
        if (!string.IsNullOrWhiteSpace(outputPattern))
        {
            return ResolveOutputPath(outputPattern, item);
        }

        // Si el archivo forma parte de una sesión de descompresión (ej. ArchiveFanOut), usar la carpeta de la sesión
        if (item.Metadata.TryGetValue("Archive:WorkingFolder", out var wfObj) && wfObj != null && !string.IsNullOrWhiteSpace(wfObj.ToString()))
        {
            string sessionFolder = wfObj.ToString()!;
            if (Directory.Exists(sessionFolder))
            {
                string? currentDir = Path.GetDirectoryName(item.CurrentPath);
                if (!string.IsNullOrEmpty(currentDir) && currentDir.StartsWith(sessionFolder, StringComparison.OrdinalIgnoreCase))
                {
                    return currentDir;
                }
                return sessionFolder;
            }
        }

        // Utilizar el espacio de trabajo acotado de la ejecución actual
        if (context.TempWorkspace != null)
        {
            return context.TempWorkspace.CreateSubdirectory("intermediate");
        }

        string tempBase = !string.IsNullOrWhiteSpace(context.TemporaryDirectory)
            ? context.TemporaryDirectory
            : (item.Metadata.TryGetValue("TemporaryDirectory", out var tdVal) && tdVal != null && !string.IsNullOrWhiteSpace(tdVal.ToString())
                ? tdVal.ToString()!
                : Storage.AppPaths.DefaultTempDirectory);

        string resolvedDir = Path.Combine(tempBase, "intermediate");
        try
        {
            Directory.CreateDirectory(resolvedDir);
        }
        catch
        {
            // Resistencia ante permisos o entornos restringidos
        }

        return resolvedDir;
    }
}
