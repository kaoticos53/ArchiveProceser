namespace FileFlow.Plugin.Network.Transports;

/// <summary>
/// Factoría centralizada para resolver la estrategia de transporte adecuada según el protocolo especificado.
/// </summary>
public static class NetworkTransportFactory
{
    private static readonly HttpTransportStrategy _httpStrategy = new();
    private static readonly FtpTransportStrategy _ftpStrategy = new();
    private static readonly SftpTransportStrategy _sftpStrategy = new();
    private static readonly WebDavTransportStrategy _webDavStrategy = new();
    private static readonly SmbTransportStrategy _smbStrategy = new();

    /// <summary>
    /// Devuelve la estrategia correspondiente al protocolo de red o lanza NotSupportedException si no existe.
    /// </summary>
    public static INetworkTransportStrategy GetTransport(string protocol)
    {
        return (protocol?.Trim().ToUpperInvariant()) switch
        {
            "HTTP" or "HTTPS" => _httpStrategy,
            "FTP" or "FTPS" => _ftpStrategy,
            "SFTP" or "SSH" => _sftpStrategy,
            "WEBDAV" => _webDavStrategy,
            "SMB" or "UNC" => _smbStrategy,
            _ => throw new NotSupportedException($"Protocolo de red no soportado: '{protocol}'")
        };
    }
}
