using System.IO;
using FileFlow.App.ViewModels;
using FileFlow.Plugin.Network;
using FileFlow.Sdk;
using FluentAssertions;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins.Network;

public class NetworkNodesTests
{
    private readonly string _testDir;

    public NetworkNodesTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "FileFlow_Network_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    private string CreateTestFile(string fileName = "document.pdf", string content = "Sample test payload")
    {
        string filePath = Path.Combine(_testDir, fileName);
        File.WriteAllText(filePath, content);
        return filePath;
    }

    [Fact]
    public void NetworkTemplateHelper_ShouldResolveDateTimeAndFileNameTokens()
    {
        // Arrange
        string testFile = CreateTestFile("report_2026.pdf");
        var item = new FileItemContext(testFile)
        {
            Metadata = { ["CustomTag"] = "Invoices" }
        };

        // Act
        string resolved = NetworkTemplateHelper.ResolveRemotePath("/backups/{Year}/{Month}/{CustomTag}/{FileName}", item);

        // Assert
        resolved.Should().Contain(DateTime.Now.Year.ToString("D4"));
        resolved.Should().Contain(DateTime.Now.Month.ToString("D2"));
        resolved.Should().Contain("Invoices");
        resolved.Should().EndWith("report_2026.pdf");
    }

    #region NetworkDownloadNode Tests (5 Protocolos Simétricos)

    [Fact]
    public async Task NetworkDownloadNode_Http_DryRun_ShouldSimulateAndEmitOut()
    {
        // Arrange
        string destFolder = Path.Combine(_testDir, "Downloads_Http");
        var node = new NetworkDownloadNode();
        node.Parameters["Protocol"] = "HTTP";
        node.Parameters["SourceUrl"] = "https://cdn.example.com/assets/logo.png";
        node.Parameters["DestinationFolder"] = destFolder;

        var item = new FileItemContext("trigger.tmp");
        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.SetupGet(c => c.IsDryRun).Returns(true);

        FileItemContext? emitted = null;
        string? port = null;
        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((p, it) => { port = p; emitted = it; })
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object);

        // Assert
        port.Should().Be("Out");
        emitted.Should().NotBeNull();
        emitted!.Metadata.Should().ContainKey("DownloadedPath");
        emitted.CurrentPath.Should().Be(Path.Combine(destFolder, "logo.png"));
    }

    [Fact]
    public async Task NetworkDownloadNode_Ftp_DryRun_ShouldSimulateAndEmitOut()
    {
        // Arrange
        string destFolder = Path.Combine(_testDir, "Downloads_Ftp");
        var node = new NetworkDownloadNode();
        node.Parameters["Protocol"] = "FTP";
        node.Parameters["Host"] = "ftp.acme.org";
        node.Parameters["Port"] = 21;
        node.Parameters["RemoteFilePath"] = "/reports/annual.csv";
        node.Parameters["DestinationFolder"] = destFolder;

        var item = new FileItemContext("trigger.tmp");
        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.SetupGet(c => c.IsDryRun).Returns(true);

        FileItemContext? emitted = null;
        string? port = null;
        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((p, it) => { port = p; emitted = it; })
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object);

        // Assert
        port.Should().Be("Out");
        emitted.Should().NotBeNull();
        emitted!.Metadata["RemoteUrl"]?.ToString().Should().Be("ftp://ftp.acme.org:21/reports/annual.csv");
        emitted.CurrentPath.Should().Be(Path.Combine(destFolder, "annual.csv"));
    }

    [Fact]
    public async Task NetworkDownloadNode_Sftp_DryRun_ShouldSimulateAndEmitOut()
    {
        // Arrange
        string destFolder = Path.Combine(_testDir, "Downloads_Sftp");
        var node = new NetworkDownloadNode();
        node.Parameters["Protocol"] = "SFTP";
        node.Parameters["Host"] = "sftp.corp.net";
        node.Parameters["Port"] = 22;
        node.Parameters["Username"] = "admin";
        node.Parameters["RemoteFilePath"] = "/var/data/dump.sql";
        node.Parameters["DestinationFolder"] = destFolder;

        var item = new FileItemContext("trigger.tmp");
        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.SetupGet(c => c.IsDryRun).Returns(true);

        FileItemContext? emitted = null;
        string? port = null;
        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((p, it) => { port = p; emitted = it; })
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object);

        // Assert
        port.Should().Be("Out");
        emitted.Should().NotBeNull();
        emitted!.Metadata["RemoteUrl"]?.ToString().Should().Be("sftp://admin@sftp.corp.net:22/var/data/dump.sql");
        emitted.CurrentPath.Should().Be(Path.Combine(destFolder, "dump.sql"));
    }

    [Fact]
    public async Task NetworkDownloadNode_WebDav_DryRun_ShouldSimulateAndEmitOut()
    {
        // Arrange
        string destFolder = Path.Combine(_testDir, "Downloads_WebDav");
        var node = new NetworkDownloadNode();
        node.Parameters["Protocol"] = "WebDAV";
        node.Parameters["ServerUrl"] = "https://cloud.company.com/remote.php/dav/files/user/invoices/inv_01.pdf";
        node.Parameters["DestinationFolder"] = destFolder;

        var item = new FileItemContext("trigger.tmp");
        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.SetupGet(c => c.IsDryRun).Returns(true);

        FileItemContext? emitted = null;
        string? port = null;
        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((p, it) => { port = p; emitted = it; })
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object);

        // Assert
        port.Should().Be("Out");
        emitted.Should().NotBeNull();
        emitted!.CurrentPath.Should().Be(Path.Combine(destFolder, "inv_01.pdf"));
    }

    [Fact]
    public async Task NetworkDownloadNode_Smb_DryRun_ShouldSimulateAndEmitOut()
    {
        // Arrange
        string destFolder = Path.Combine(_testDir, "Downloads_Smb");
        var node = new NetworkDownloadNode();
        node.Parameters["Protocol"] = "SMB";
        node.Parameters["UncPath"] = @"\\server\share\data.zip";
        node.Parameters["DestinationFolder"] = destFolder;

        var item = new FileItemContext("trigger.tmp");
        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.SetupGet(c => c.IsDryRun).Returns(true);

        FileItemContext? emitted = null;
        string? port = null;
        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((p, it) => { port = p; emitted = it; })
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object);

        // Assert
        port.Should().Be("Out");
        emitted.Should().NotBeNull();
        emitted!.CurrentPath.Should().Be(Path.Combine(destFolder, "data.zip"));
    }

    #endregion

    #region NetworkUploadNode Tests (5 Protocolos Simétricos)

    [Fact]
    public async Task NetworkUploadNode_Http_DryRun_ShouldSimulateAndEmitOut()
    {
        // Arrange
        var node = new NetworkUploadNode();
        node.Parameters["Protocol"] = "HTTP";
        node.Parameters["TargetUrl"] = "https://api.example.com/v1/ingest";
        node.Parameters["HttpMethod"] = "POST";

        string testFile = CreateTestFile("payload.json", "{}");
        var item = new FileItemContext(testFile);

        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.SetupGet(c => c.IsDryRun).Returns(true);

        FileItemContext? emitted = null;
        string? port = null;
        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((p, it) => { port = p; emitted = it; })
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object);

        // Assert
        port.Should().Be("Out");
        emitted.Should().NotBeNull();
        emitted!.Metadata["RemoteUrl"]?.ToString().Should().Be("https://api.example.com/v1/ingest");
    }

    [Fact]
    public async Task NetworkUploadNode_Ftp_DryRun_ShouldSimulateAndEmitOut()
    {
        // Arrange
        var node = new NetworkUploadNode();
        node.Parameters["Protocol"] = "FTP";
        node.Parameters["Host"] = "ftp.acme.org";
        node.Parameters["Port"] = 21;
        node.Parameters["RemoteDirectory"] = "/inbox";

        string testFile = CreateTestFile("sales.csv", "1,2,3");
        var item = new FileItemContext(testFile);

        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.SetupGet(c => c.IsDryRun).Returns(true);

        FileItemContext? emitted = null;
        string? port = null;
        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((p, it) => { port = p; emitted = it; })
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object);

        // Assert
        port.Should().Be("Out");
        emitted.Should().NotBeNull();
        emitted!.Metadata["RemoteUrl"]?.ToString().Should().Be("ftp://ftp.acme.org:21/inbox/sales.csv");
    }

    [Fact]
    public async Task NetworkUploadNode_Sftp_DryRun_ShouldSimulateAndEmitOut()
    {
        // Arrange
        var node = new NetworkUploadNode();
        node.Parameters["Protocol"] = "SFTP";
        node.Parameters["Host"] = "vps.server.com";
        node.Parameters["Port"] = 22;
        node.Parameters["Username"] = "deploy";
        node.Parameters["RemoteDirectory"] = "/var/www/uploads";

        string testFile = CreateTestFile("site.tar.gz", "archive data");
        var item = new FileItemContext(testFile);

        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.SetupGet(c => c.IsDryRun).Returns(true);

        FileItemContext? emitted = null;
        string? port = null;
        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((p, it) => { port = p; emitted = it; })
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object);

        // Assert
        port.Should().Be("Out");
        emitted.Should().NotBeNull();
        emitted!.Metadata["RemoteUrl"]?.ToString().Should().Be("sftp://deploy@vps.server.com:22/var/www/uploads/site.tar.gz");
    }

    [Fact]
    public async Task NetworkUploadNode_WebDav_DryRun_ShouldSimulateAndEmitOut()
    {
        // Arrange
        var node = new NetworkUploadNode();
        node.Parameters["Protocol"] = "WebDAV";
        node.Parameters["ServerUrl"] = "https://nextcloud.company.com/remote.php/dav/files/admin/Finance";

        string testFile = CreateTestFile("invoice.pdf", "pdf data");
        var item = new FileItemContext(testFile);

        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.SetupGet(c => c.IsDryRun).Returns(true);

        FileItemContext? emitted = null;
        string? port = null;
        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((p, it) => { port = p; emitted = it; })
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object);

        // Assert
        port.Should().Be("Out");
        emitted.Should().NotBeNull();
        emitted!.Metadata["RemoteUrl"]?.ToString().Should().Be("https://nextcloud.company.com/remote.php/dav/files/admin/Finance/invoice.pdf");
    }

    [Fact]
    public async Task NetworkUploadNode_Smb_DryRun_ShouldSimulateAndEmitOut()
    {
        // Arrange
        var node = new NetworkUploadNode();
        node.Parameters["Protocol"] = "SMB";
        node.Parameters["UncPath"] = @"\\nas\backup";

        string testFile = CreateTestFile("system.bak", "backup payload");
        var item = new FileItemContext(testFile);

        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.SetupGet(c => c.IsDryRun).Returns(true);

        FileItemContext? emitted = null;
        string? port = null;
        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((p, it) => { port = p; emitted = it; })
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object);

        // Assert
        port.Should().Be("Out");
        emitted.Should().NotBeNull();
        emitted!.Metadata["RemoteUrl"]?.ToString().Should().EndWith("system.bak");
    }

    #endregion

    #region Reactividad y Parámetros Dinámicos (DependsOn)

    [Fact]
    public void NetworkDownloadNode_ParameterVisibility_ShouldChangeDynamicallyWithProtocol()
    {
        // Arrange
        var node = new NetworkDownloadNode();
        var nodeVm = new NodeViewModel(node, new System.Windows.Point(0, 0));

        var protocolParam = nodeVm.Parameters.First(p => p.Key == "Protocol");
        var sourceUrlParam = nodeVm.Parameters.First(p => p.Key == "SourceUrl");
        var hostParam = nodeVm.Parameters.First(p => p.Key == "Host");
        var privateKeyParam = nodeVm.Parameters.First(p => p.Key == "PrivateKeyPath");

        // 1. Con Protocol = HTTP (Default)
        protocolParam.Value = "HTTP";
        sourceUrlParam.IsVisible.Should().BeTrue("SourceUrl must be visible for HTTP");
        hostParam.IsVisible.Should().BeFalse("Host must be hidden for HTTP");
        privateKeyParam.IsVisible.Should().BeFalse("PrivateKeyPath must be hidden for HTTP");

        // 2. Con Protocol = FTP
        protocolParam.Value = "FTP";
        sourceUrlParam.IsVisible.Should().BeFalse("SourceUrl must be hidden for FTP");
        hostParam.IsVisible.Should().BeTrue("Host must be visible for FTP");
        privateKeyParam.IsVisible.Should().BeFalse("PrivateKeyPath must be hidden for FTP");

        // 3. Con Protocol = SFTP
        protocolParam.Value = "SFTP";
        sourceUrlParam.IsVisible.Should().BeFalse("SourceUrl must be hidden for SFTP");
        hostParam.IsVisible.Should().BeTrue("Host must be visible for SFTP");
        privateKeyParam.IsVisible.Should().BeTrue("PrivateKeyPath must be visible for SFTP");
    }

    #endregion
}
