namespace FileFlow.App.Services;

/// <summary>
/// Configuración de herramientas externas en la capa de UI.
/// </summary>
public class ExternalToolsConfig : FileFlow.Core.Services.ExternalToolsConfig
{
}

/// <summary>
/// Adaptador de herramientas externas en FileFlow.App que delega al servicio desacoplado de FileFlow.Core.
/// </summary>
public class ExternalToolsService : FileFlow.Core.Services.ExternalToolsService
{
    private static readonly Lazy<ExternalToolsService> _appInstance = new(() => new ExternalToolsService());
    public static new ExternalToolsService Instance => _appInstance.Value;
}
