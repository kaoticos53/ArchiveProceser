namespace FileFlow.Plugin.Network.Transports;

/// <summary>
/// Parámetros encapsulados y tipados para operaciones de descarga remota.
/// </summary>
public sealed record NetworkDownloadRequest(
    string DestinationDirectory,
    string FileNameOverride,
    bool Overwrite,
    bool DeleteAfterDownload,
    string SourceUrl,
    int TimeoutSeconds,
    string Host,
    int Port,
    string Username,
    string Password,
    string RemoteFilePath,
    string Encryption,
    bool PassiveMode,
    string AuthMethod,
    string PrivateKeyPath,
    string PrivateKeyPassphrase,
    string ServerUrl,
    string UncPath,
    string Domain
);

/// <summary>
/// Parámetros encapsulados y tipados para operaciones de subida o transferencia remota.
/// </summary>
public sealed record NetworkUploadRequest(
    string TargetUrl,
    string HttpMethod,
    string AuthHeader,
    string Host,
    int Port,
    string Username,
    string Password,
    string RemoteDirectory,
    string Encryption,
    bool PassiveMode,
    string AuthMethod,
    string PrivateKeyPath,
    string PrivateKeyPassphrase,
    string ServerUrl,
    string UncPath,
    string Domain
);
