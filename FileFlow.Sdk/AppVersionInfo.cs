using System.Reflection;

namespace FileFlow.Sdk;

/// <summary>
/// Provee información estructurada sobre la versión SemVer 2.0 de FileFlow Studio.
/// </summary>
public static class AppVersionInfo
{
    public static string InformationalVersion { get; }
    public static string DisplayVersion { get; }
    public static Version AssemblyVersion { get; }
    public static int Major { get; }
    public static int Minor { get; }
    public static int Patch { get; }
    public static string PreRelease { get; } = string.Empty;
    public static string BuildMetadata { get; } = string.Empty;

    static AppVersionInfo()
    {
        var assembly = typeof(AppVersionInfo).Assembly;
        var infoAttr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        
        string rawVersion = infoAttr?.InformationalVersion ?? "1.0.0+build.1";

        // Limpiar cualquier hash de git si el SDK lo inyectara adicionalmente
        InformationalVersion = rawVersion;
        DisplayVersion = $"v{InformationalVersion}";

        AssemblyVersion = assembly.GetName().Version ?? new Version(1, 0, 0, 0);

        // Parsear SemVer: MAJOR.MINOR.PATCH[-PRERELEASE]+build.METADATA
        try
        {
            string working = rawVersion;
            
            // 1. Extraer Build Metadata (+...)
            int plusIndex = working.IndexOf('+');
            if (plusIndex >= 0)
            {
                BuildMetadata = working[(plusIndex + 1)..];
                working = working[..plusIndex];
            }

            // 2. Extraer Pre-Release (-...)
            int dashIndex = working.IndexOf('-');
            if (dashIndex >= 0)
            {
                PreRelease = working[(dashIndex + 1)..];
                working = working[..dashIndex];
            }

            // 3. Extraer Major, Minor, Patch
            string[] parts = working.Split('.');
            if (parts.Length > 0 && int.TryParse(parts[0], out int maj)) Major = maj;
            if (parts.Length > 1 && int.TryParse(parts[1], out int min)) Minor = min;
            if (parts.Length > 2 && int.TryParse(parts[2], out int pat)) Patch = pat;
        }
        catch
        {
            Major = AssemblyVersion.Major;
            Minor = AssemblyVersion.Minor;
            Patch = AssemblyVersion.Build >= 0 ? AssemblyVersion.Build : 0;
        }
    }
}
