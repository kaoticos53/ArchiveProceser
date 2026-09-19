using System.Text.RegularExpressions;

namespace FileFlow.Sdk.Services;

/// <summary>
/// Canal de actualización de la aplicación.
/// </summary>
public enum UpdateChannel
{
    /// <summary>
    /// Canal de versiones finales estables.
    /// </summary>
    Stable,

    /// <summary>
    /// Canal de pre-lanzamientos y versiones preliminares (Beta / Release Candidates).
    /// </summary>
    Beta
}

/// <summary>
/// Formato de empaquetado y entorno de ejecución detectado de la aplicación.
/// </summary>
public enum AppPackagingFormat
{
    /// <summary>
    /// Instalador tradicional de Windows (Inno Setup).
    /// </summary>
    WindowsInstalled,

    /// <summary>
    /// Paquete portable autónomo de Windows (.zip).
    /// </summary>
    WindowsPortable,

    /// <summary>
    /// Ejecutable único universal de Linux (AppImage).
    /// </summary>
    LinuxAppImage,

    /// <summary>
    /// Paquete sandbox autónomo de Linux (Flatpak).
    /// </summary>
    LinuxFlatpak,

    /// <summary>
    /// Paquete de instalación nativa para Debian/Ubuntu (.deb).
    /// </summary>
    LinuxDebPackage,

    /// <summary>
    /// Bundle universal o tarball portable de Linux (.tar.gz).
    /// </summary>
    LinuxGenericTarball,

    /// <summary>
    /// Formato o entorno desconocido.
    /// </summary>
    Unknown
}

/// <summary>
/// Información de un archivo descargable adjunto a un lanzamiento en GitHub.
/// </summary>
public sealed record AppReleaseAssetInfo(
    string Name,
    string DownloadUrl,
    long SizeBytes,
    string Sha256Hash);

/// <summary>
/// Información completa de una versión o actualización disponible.
/// </summary>
public sealed record AppUpdateInfo(
    string VersionTag,
    SemVersion Version,
    string Title,
    string ReleaseNotesMarkdown,
    DateTime PublishedAtUtc,
    bool IsPrerelease,
    AppReleaseAssetInfo? MatchedAsset,
    string HtmlUrl);

/// <summary>
/// Resultado de una comprobación de actualizaciones.
/// </summary>
public sealed record UpdateCheckResult(
    bool UpdateAvailable,
    AppUpdateInfo? UpdateInfo,
    string? ErrorMessage = null)
{
    public static UpdateCheckResult NoUpdate() => new(false, null);
    public static UpdateCheckResult Available(AppUpdateInfo info) => new(true, info);
    public static UpdateCheckResult Error(string message) => new(false, null, message);
}

/// <summary>
/// Reporte de progreso de descarga y verificación de actualización.
/// </summary>
public sealed record UpdateProgressReport(
    long BytesDownloaded,
    long TotalBytes,
    double Percentage,
    double SpeedBytesPerSec,
    string StatusMessage);

/// <summary>
/// Representación SemVer 2.0 ligera para comparación de versiones en SDK.
/// </summary>
public sealed record SemVersion(int Major, int Minor, int Patch, string PreRelease = "", int BuildNumber = 0) : IComparable<SemVersion>
{
    public static SemVersion Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new SemVersion(0, 0, 0);

        string clean = raw.Trim().TrimStart('v', 'V');
        
        // Extraer build metadata si existe (ej. +build.3588)
        int build = 0;
        int plusIdx = clean.IndexOf('+');
        if (plusIdx >= 0)
        {
            string buildPart = clean[(plusIdx + 1)..];
            clean = clean[..plusIdx];
            var buildMatch = Regex.Match(buildPart, @"\d+");
            if (buildMatch.Success && int.TryParse(buildMatch.Value, out int b))
            {
                build = b;
            }
        }

        // Extraer pre-release si existe (ej. -beta.1)
        string pre = string.Empty;
        int dashIdx = clean.IndexOf('-');
        if (dashIdx >= 0)
        {
            pre = clean[(dashIdx + 1)..];
            clean = clean[..dashIdx];
        }

        string[] parts = clean.Split('.', StringSplitOptions.RemoveEmptyEntries);
        int maj = parts.Length > 0 && int.TryParse(parts[0], out int p0) ? p0 : 0;
        int min = parts.Length > 1 && int.TryParse(parts[1], out int p1) ? p1 : 0;
        int pat = parts.Length > 2 && int.TryParse(parts[2], out int p2) ? p2 : 0;

        return new SemVersion(maj, min, pat, pre, build);
    }

    public int CompareTo(SemVersion? other)
    {
        if (other is null) return 1;

        int c = Major.CompareTo(other.Major);
        if (c != 0) return c;

        c = Minor.CompareTo(other.Minor);
        if (c != 0) return c;

        c = Patch.CompareTo(other.Patch);
        if (c != 0) return c;

        // SemVer: Versión sin pre-release tiene mayor precedencia que con pre-release
        if (string.IsNullOrEmpty(PreRelease) && !string.IsNullOrEmpty(other.PreRelease)) return 1;
        if (!string.IsNullOrEmpty(PreRelease) && string.IsNullOrEmpty(other.PreRelease)) return -1;

        if (!string.IsNullOrEmpty(PreRelease) && !string.IsNullOrEmpty(other.PreRelease))
        {
            c = string.Compare(PreRelease, other.PreRelease, StringComparison.OrdinalIgnoreCase);
            if (c != 0) return c;
        }

        return BuildNumber.CompareTo(other.BuildNumber);
    }

    public static bool operator >(SemVersion left, SemVersion right) => left.CompareTo(right) > 0;
    public static bool operator <(SemVersion left, SemVersion right) => left.CompareTo(right) < 0;
    public static bool operator >=(SemVersion left, SemVersion right) => left.CompareTo(right) >= 0;
    public static bool operator <=(SemVersion left, SemVersion right) => left.CompareTo(right) <= 0;

    public override string ToString()
    {
        string pre = !string.IsNullOrEmpty(PreRelease) ? $"-{PreRelease}" : "";
        string bld = BuildNumber > 0 ? $"+build.{BuildNumber}" : "";
        return $"{Major}.{Minor}.{Patch}{pre}{bld}";
    }
}

/// <summary>
/// Contrato del servicio de autoactualización de la aplicación.
/// </summary>
public interface IAppUpdateService
{
    private static IAppUpdateService _instance = NullAppUpdateService.Instance;

    public static IAppUpdateService Instance
    {
        get => _instance;
        set => _instance = value ?? NullAppUpdateService.Instance;
    }

    /// <summary>
    /// Obtiene el formato de empaquetado del entorno de ejecución actual.
    /// </summary>
    AppPackagingFormat CurrentPackagingFormat { get; }

    /// <summary>
    /// Obtiene la versión actual de la aplicación.
    /// </summary>
    SemVersion CurrentVersion { get; }

    /// <summary>
    /// Comprueba si hay actualizaciones disponibles en el repositorio oficial de GitHub.
    /// </summary>
    Task<UpdateCheckResult> CheckForUpdatesAsync(UpdateChannel channel, bool force = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Descarga y prepara el paquete de actualización, verificando su suma SHA-256.
    /// </summary>
    Task<string> DownloadAndPrepareUpdateAsync(AppUpdateInfo updateInfo, IProgress<UpdateProgressReport>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aplica la actualización descargada y reinicia la aplicación según el entorno detectado.
    /// </summary>
    Task ApplyUpdateAndRestartAsync(AppUpdateInfo updateInfo, string downloadedFilePath);
}

