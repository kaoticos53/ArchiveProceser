using FileFlow.Sdk;

namespace FileFlow.Plugin.Network.Transports;

/// <summary>
/// Contrato unificado para estrategias de transporte y transferencia de red y nube.
/// </summary>
public interface INetworkTransportStrategy
{
    /// <summary>
    /// Identificador del protocolo de transporte (ej. "HTTP", "FTP", "SFTP", "WEBDAV", "SMB").
    /// </summary>
    string ProtocolName { get; }

    /// <summary>
    /// Ejecuta la descarga de un recurso remoto hacia el sistema de archivos local.
    /// </summary>
    Task DownloadAsync(
        NetworkDownloadRequest request,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Ejecuta la subida/transferencia de un archivo local hacia un destino remoto.
    /// </summary>
    Task UploadAsync(
        NetworkUploadRequest request,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken);
}
