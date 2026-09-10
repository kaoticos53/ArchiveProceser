using System.IO;
using FileFlow.Plugin.Network.Transports;
using FileFlow.Sdk;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins.Network;

public class NetworkTransportStrategiesValidationTests
{
    [Fact]
    public async Task HttpTransportStrategy_Download_InvalidUrl_ShouldEmitError()
    {
        var strategy = new HttpTransportStrategy();
        var request = CreateDownloadRequest() with { SourceUrl = "not-a-valid-url" };
        var item = new FileItemContext("trigger.tmp");
        var (context, getEmittedPort) = CreateContext(isDryRun: false);

        await strategy.DownloadAsync(request, item, context.Object, CancellationToken.None);

        Assert.Equal("Error", getEmittedPort());
    }

    [Fact]
    public async Task HttpTransportStrategy_Upload_InvalidUrl_ShouldEmitError()
    {
        var strategy = new HttpTransportStrategy();
        var request = CreateUploadRequest() with { TargetUrl = "invalid-url" };
        var item = new FileItemContext("payload.bin");
        var (context, getEmittedPort) = CreateContext(isDryRun: false);

        await strategy.UploadAsync(request, item, context.Object, CancellationToken.None);

        Assert.Equal("Error", getEmittedPort());
    }

    [Fact]
    public async Task WebDavTransportStrategy_Download_InvalidUrl_ShouldEmitError()
    {
        var strategy = new WebDavTransportStrategy();
        var request = CreateDownloadRequest() with { ServerUrl = "" };
        var item = new FileItemContext("trigger.tmp");
        var (context, getEmittedPort) = CreateContext(isDryRun: false);

        await strategy.DownloadAsync(request, item, context.Object, CancellationToken.None);

        Assert.Equal("Error", getEmittedPort());
    }

    [Fact]
    public async Task WebDavTransportStrategy_Upload_InvalidUrl_ShouldEmitError()
    {
        var strategy = new WebDavTransportStrategy();
        var request = CreateUploadRequest() with { ServerUrl = "::::" };
        var item = new FileItemContext("payload.bin");
        var (context, getEmittedPort) = CreateContext(isDryRun: false);

        await strategy.UploadAsync(request, item, context.Object, CancellationToken.None);

        Assert.Equal("Error", getEmittedPort());
    }

    [Fact]
    public async Task SmbTransportStrategy_Download_EmptyUncPath_ShouldEmitError()
    {
        var strategy = new SmbTransportStrategy();
        var request = CreateDownloadRequest() with { UncPath = "" };
        var item = new FileItemContext("trigger.tmp");
        var (context, getEmittedPort) = CreateContext(isDryRun: false);

        await strategy.DownloadAsync(request, item, context.Object, CancellationToken.None);

        Assert.Equal("Error", getEmittedPort());
    }

    [Fact]
    public async Task SmbTransportStrategy_Upload_EmptyUncPath_ShouldEmitError()
    {
        var strategy = new SmbTransportStrategy();
        var request = CreateUploadRequest() with { UncPath = "" };
        var item = new FileItemContext("payload.bin");
        var (context, getEmittedPort) = CreateContext(isDryRun: false);

        await strategy.UploadAsync(request, item, context.Object, CancellationToken.None);

        Assert.Equal("Error", getEmittedPort());
    }

    [Fact]
    public async Task SftpTransportStrategy_Download_PrivateKeyAuthWithoutKeyFile_ShouldEmitError()
    {
        var strategy = new SftpTransportStrategy();
        var request = CreateDownloadRequest() with
        {
            Host = "sftp.local",
            Port = 22,
            Username = "user",
            AuthMethod = "PrivateKey",
            PrivateKeyPath = Path.Combine(Path.GetTempPath(), "missing_test_key.pem"),
            RemoteFilePath = "/remote/file.txt"
        };

        var item = new FileItemContext("trigger.tmp");
        var (context, getEmittedPort) = CreateContext(isDryRun: false);

        await strategy.DownloadAsync(request, item, context.Object, CancellationToken.None);

        Assert.Equal("Error", getEmittedPort());
    }

    [Fact]
    public async Task SftpTransportStrategy_Upload_PrivateKeyAuthWithoutKeyFile_ShouldEmitError()
    {
        var strategy = new SftpTransportStrategy();
        var request = CreateUploadRequest() with
        {
            Host = "sftp.local",
            Port = 22,
            Username = "user",
            AuthMethod = "PrivateKey",
            PrivateKeyPath = Path.Combine(Path.GetTempPath(), "missing_test_key_upload.pem"),
            RemoteDirectory = "/remote"
        };

        var item = new FileItemContext("payload.bin");
        var (context, getEmittedPort) = CreateContext(isDryRun: false);

        await strategy.UploadAsync(request, item, context.Object, CancellationToken.None);

        Assert.Equal("Error", getEmittedPort());
    }

    private static (Mock<IFlowExecutionContext> Context, Func<string?> GetEmittedPort) CreateContext(bool isDryRun)
    {
        string? emittedPort = null;
        var context = new Mock<IFlowExecutionContext>();
        context.SetupGet(c => c.IsDryRun).Returns(isDryRun);
        context.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, _) => emittedPort = port)
            .Returns(Task.CompletedTask);
        return (context, () => emittedPort);
    }

    private static NetworkDownloadRequest CreateDownloadRequest() => new(
        DestinationDirectory: Path.GetTempPath(),
        FileNameOverride: string.Empty,
        Overwrite: false,
        DeleteAfterDownload: false,
        SourceUrl: "https://example.com/file.bin",
        TimeoutSeconds: 30,
        Host: "localhost",
        Port: 21,
        Username: "",
        Password: "",
        RemoteFilePath: "/file.bin",
        Encryption: "None",
        PassiveMode: true,
        AuthMethod: "Password",
        PrivateKeyPath: string.Empty,
        PrivateKeyPassphrase: string.Empty,
        ServerUrl: "https://example.com/remote.php/dav/files/user/file.bin",
        UncPath: @"\\server\share\file.bin",
        Domain: string.Empty);

    private static NetworkUploadRequest CreateUploadRequest() => new(
        TargetUrl: "https://example.com/upload",
        HttpMethod: "POST",
        AuthHeader: string.Empty,
        Host: "localhost",
        Port: 21,
        Username: "",
        Password: "",
        RemoteDirectory: "/upload",
        Encryption: "None",
        PassiveMode: true,
        AuthMethod: "Password",
        PrivateKeyPath: string.Empty,
        PrivateKeyPassphrase: string.Empty,
        ServerUrl: "https://example.com/remote.php/dav/files/user",
        UncPath: @"\\server\share",
        Domain: string.Empty);
}
