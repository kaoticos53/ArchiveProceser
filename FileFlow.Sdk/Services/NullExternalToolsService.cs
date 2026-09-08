namespace FileFlow.Sdk.Services;

/// <summary>
/// Implementación por defecto y fallback seguro para <see cref="IExternalToolsService"/>.
/// </summary>
public class NullExternalToolsService : IExternalToolsService
{
    private static readonly Lazy<NullExternalToolsService> _instance = new(() => new NullExternalToolsService());
    public static NullExternalToolsService Instance => _instance.Value;

    public string ResolveToolPath(string toolName) => toolName;
    public bool IsToolAvailable(string toolName) => false;
}
