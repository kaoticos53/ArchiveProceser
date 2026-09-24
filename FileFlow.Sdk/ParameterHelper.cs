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
    ///
    /// <para><b>La salida global es una plantilla, y se resuelve antes de usarla.</b> Un flujo declara su carpeta de
    /// salida con un valor que a su vez puede llevar variables —el catálogo de ejemplos entero declara
    /// <c>{RelativeDir}</c>, y ahí «la carpeta de salida del flujo» significa la estructura del origen—, así que
    /// dejarla escrita dentro de <c>{GlobalOutputDir}</c> convertía a esa variable en la plantilla que ninguna fase
    /// posterior expandía: el patrón <c>{GlobalOutputDir}</c> la devolvía literal, el anclaje la combinaba consigo
    /// mismo (<c>{RelativeDir}\{RelativeDir}</c>) y <see cref="CrossPlatformPath.Combine"/> absolutizaba el resultado
    /// contra el <b>directorio de trabajo del proceso</b> —medido: <c>bin/Debug/net10.0/{RelativeDir}/{RelativeDir}</c>—,
    /// que es exactamente la forma del defecto del hito 204 (hito 209).</para>
    ///
    /// <para>De ahí el orden de este método: el anclaje se decide <b>antes</b> de expandir el patrón, y el valor del
    /// anclaje sale de <see cref="FlowOutputFolder"/> —la regla única, la misma que resuelve <c>{GlobalOutputDir}</c>
    /// en cualquier parámetro—, de modo que el patrón expande a un camino terminado y no a una plantilla ni a una
    /// ruta relativa. Lo que devuelve esta función nunca procede del directorio de trabajo del proceso: si la salida
    /// declarada es relativa se ancla bajo el origen, y donde no hay origen bajo la salida por defecto de los
    /// ajustes.</para>
    /// </summary>
    public static string ResolveOutputPath(string targetPathPattern, FileItemContext context, string? globalOutputDir = null, string? sourceRootPath = null)
    {
        if (string.IsNullOrWhiteSpace(targetPathPattern))
        {
            targetPathPattern = "{FileName}";
        }

        // El anclaje del origen se decide antes de expandir el patrón: la salida global se resuelve contra él.
        string? baseDir = SourceAnchor(context, sourceRootPath);

        string? effectiveGlobalOutputDir = FlowOutputFolder(context, sourceRootPath, globalOutputDir);

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
    /// Directorio de origen del archivo: contra él se ancla todo lo que el flujo declare relativo.
    /// <c>SourceRootPath</c> —lo que la fuente puso como raíz del barrido— manda sobre el argumento, y si no hay
    /// ninguno de los dos se usa la carpeta del propio archivo.
    /// </summary>
    private static string? SourceAnchor(FileItemContext context, string? sourceRootPath)
    {
        if (context.Metadata.TryGetValue("SourceRootPath", out var srpVal) && srpVal != null && !string.IsNullOrWhiteSpace(srpVal.ToString()))
        {
            return srpVal.ToString();
        }

        if (!string.IsNullOrWhiteSpace(sourceRootPath))
        {
            return sourceRootPath;
        }

        string? itemPath = !string.IsNullOrWhiteSpace(context.OriginalPath) ? context.OriginalPath : context.CurrentPath;
        return string.IsNullOrWhiteSpace(itemPath) ? null : CrossPlatformPath.GetDirectoryName(itemPath);
    }

    /// <summary>
    /// Las claves con las que viaja la carpeta de salida del flujo. Son las que el motor propaga y las que el
    /// resolutor de variables acepta: el nombre vigente y sus alias históricos.
    /// </summary>
    private static readonly string[] FlowOutputFolderKeys =
        ["GlobalOutputDir", "DefaultGlobalOutputDir", "DefaultOutputDir", "GlobalOutputPath", "DefaultOutputPath"];

    /// <summary>
    /// Guardia de reentrada: el valor declarado puede nombrarse a sí mismo (<c>"{GlobalOutputDir}/sub"</c>), y
    /// expandirlo volvería a pedir la carpeta del flujo. Con la guardia, esa referencia circular termina.
    /// </summary>
    [ThreadStatic]
    private static bool _resolvingFlowOutputFolder;

    /// <summary>
    /// <b>La carpeta de salida del flujo, terminada</b>: la que el flujo declara —o su metadata, que es lo que el
    /// motor propaga— expandida <b>una vez</b> y anclada a una ruta completa; <c>null</c> si el flujo no declara
    /// ninguna.
    ///
    /// <para><b>Esta es la regla, y sólo hay una</b>: es la misma carpeta que vale <c>{GlobalOutputDir}</c> en
    /// cualquier parámetro, la que ancla los patrones de <see cref="ResolveOutputPath"/> y la que tienen que leer los
    /// nodos que consultan la carpeta por su cuenta. Lo que el flujo declara puede ser a su vez una plantilla —el
    /// catálogo de ejemplos entero declara <c>{RelativeDir}</c>, y ahí «la salida del flujo» significa la estructura
    /// del origen—, así que quien reciba este valor recibe <b>una carpeta</b> y nunca el texto de una plantilla: sin
    /// expandirla, <c>{GlobalOutputDir}</c> devolvía la plantilla literal, el anclaje la combinaba consigo mismo y la
    /// ruta resultante acababa colgada del <b>directorio de trabajo del proceso</b> (medido en los hitos 209 y 210).
    /// </para>
    ///
    /// <para>Si lo declarado es relativo —la plantilla relativa al origen, o una carpeta escrita a mano— se ancla
    /// bajo el origen del barrido; sin origen, bajo la salida por defecto de los ajustes. Nunca bajo el directorio de
    /// trabajo del proceso: un flujo que declara <c>{RelativeDir}</c> está pidiendo la estructura del origen, no la
    /// carpeta donde corre la aplicación.</para>
    ///
    /// <para><paramref name="declaredFolder"/> es para quien tiene el valor declarado en la mano y no en la metadata
    /// (el argumento histórico de <see cref="ResolveOutputPath"/>): si llega, manda.</para>
    /// </summary>
    public static string? FlowOutputFolder(FileItemContext context, string? sourceRootPath = null, string? declaredFolder = null)
    {
        string? declared = string.IsNullOrWhiteSpace(declaredFolder)
            ? DeclaredFlowOutputFolder(context)
            : declaredFolder;

        if (string.IsNullOrWhiteSpace(declared))
        {
            return null;
        }

        if (_resolvingFlowOutputFolder)
        {
            return null;
        }

        string expanded;
        _resolvingFlowOutputFolder = true;
        try
        {
            expanded = TemplateEngine.VariableTemplateResolver.Resolve(declared, context, sourceRootPath);
        }
        finally
        {
            _resolvingFlowOutputFolder = false;
        }

        if (CrossPlatformPath.IsPathFullyQualified(expanded))
        {
            if (CrossPlatformPath.IsWindowsDrivePath(expanded) || CrossPlatformPath.IsWindowsUncPath(expanded))
            {
                return CrossPlatformPath.NormalizeWindowsPath(expanded);
            }
            string unixExpanded = expanded.Replace('\\', '/');
            return OperatingSystem.IsWindows() ? expanded : Path.GetFullPath(unixExpanded);
        }

        // Declarado en relativo. Se ancla bajo el origen del barrido —`{RelativeDir}` de un archivo en la raíz es
        // vacío, y eso significa el origen mismo, no «sin carpeta»— y, sin origen, bajo la salida por defecto.
        string? anchor = SourceAnchor(context, sourceRootPath);
        if (string.IsNullOrWhiteSpace(anchor))
        {
            return Storage.AppPaths.DefaultGlobalOutputDir;
        }

        return string.IsNullOrWhiteSpace(expanded) ? anchor : CrossPlatformPath.Combine(anchor, expanded);
    }

    /// <summary>Lo que el flujo declaró como su salida, tal cual, o <c>null</c> si no declaró nada.</summary>
    private static string? DeclaredFlowOutputFolder(FileItemContext context)
    {
        foreach (string key in FlowOutputFolderKeys)
        {
            if (context.Metadata.TryGetValue(key, out var value) && value != null && !string.IsNullOrWhiteSpace(value.ToString()))
            {
                return value.ToString();
            }
        }

        return null;
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
