using System.IO;
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

    [Fact]
    public async Task FtpUploadNode_DryRun_ShouldEmitOutWithSimulatedRemoteUrl()
    {
        // Arrange
        var node = new FtpUploadNode();
        node.Parameters["Host"] = "ftp.mycompany.com";
        node.Parameters["Port"] = 21;
        node.Parameters["RemoteDirectory"] = "/backups/test";

        string testFile = CreateTestFile("sample.txt");
        var item = new FileItemContext(testFile);

        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.SetupGet(c => c.IsDryRun).Returns(true);

        FileItemContext? emittedItem = null;
        string? emittedPort = null;

        mockContext
            .Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, it) =>
            {
                emittedPort = port;
                emittedItem = it;
            })
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object);

        // Assert
        emittedPort.Should().Be("Out");
        emittedItem.Should().NotBeNull();
        emittedItem!.Metadata.Should().ContainKey("RemoteUrl");
        emittedItem.Metadata["RemoteUrl"]?.ToString().Should().Be("ftp://ftp.mycompany.com:21/backups/test/sample.txt");
    }

    [Fact]
    public async Task SftpUploadNode_DryRun_ShouldEmitOutWithSimulatedSftpUrl()
    {
        // Arrange
        var node = new SftpUploadNode();
        node.Parameters["Host"] = "vps.linux.com";
        node.Parameters["Port"] = 22;
        node.Parameters["RemoteDirectory"] = "/var/www/uploads";

        string testFile = CreateTestFile("data.csv");
        var item = new FileItemContext(testFile);

        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.SetupGet(c => c.IsDryRun).Returns(true);

        FileItemContext? emittedItem = null;
        string? emittedPort = null;

        mockContext
            .Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, it) =>
            {
                emittedPort = port;
                emittedItem = it;
            })
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object);

        // Assert
        emittedPort.Should().Be("Out");
        emittedItem.Should().NotBeNull();
        emittedItem!.Metadata.Should().ContainKey("RemoteUrl");
        emittedItem.Metadata["RemoteUrl"]?.ToString().Should().Be("sftp://vps.linux.com:22/var/www/uploads/data.csv");
    }

    [Fact]
    public async Task SmbCopyNode_ShouldCopyFileLocallyAndEmitOutPort()
    {
        // Arrange
        string destFolder = Path.Combine(_testDir, "NasOutput");
        var node = new SmbCopyNode();
        node.Parameters["DestinationFolder"] = destFolder;
        node.Parameters["Overwrite"] = true;

        string testFile = CreateTestFile("package.zip", "ZIP content");
        var item = new FileItemContext(testFile);

        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.SetupGet(c => c.IsDryRun).Returns(false);

        FileItemContext? emittedItem = null;
        string? emittedPort = null;

        mockContext
            .Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, it) =>
            {
                emittedPort = port;
                emittedItem = it;
            })
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object);

        // Assert
        emittedPort.Should().Be("Out");
        emittedItem.Should().NotBeNull();
        string expectedDestFile = Path.Combine(destFolder, "package.zip");
        File.Exists(expectedDestFile).Should().BeTrue();
        emittedItem!.Metadata.Should().ContainKey("NetworkPath");
        emittedItem.Metadata["NetworkPath"]?.ToString().Should().Be(expectedDestFile);
    }

    [Fact]
    public async Task WebDavUploadNode_DryRun_ShouldEmitOutWithWebDavUrl()
    {
        // Arrange
        var node = new WebDavUploadNode();
        node.Parameters["ServerUrl"] = "https://nextcloud.company.com/remote.php/dav/files/admin";
        node.Parameters["RemoteDirectory"] = "/Finance/2026";

        string testFile = CreateTestFile("invoice.pdf");
        var item = new FileItemContext(testFile);

        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.SetupGet(c => c.IsDryRun).Returns(true);

        FileItemContext? emittedItem = null;
        string? emittedPort = null;

        mockContext
            .Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, it) =>
            {
                emittedPort = port;
                emittedItem = it;
            })
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object);

        // Assert
        emittedPort.Should().Be("Out");
        emittedItem.Should().NotBeNull();
        emittedItem!.Metadata.Should().ContainKey("RemoteUrl");
        emittedItem.Metadata["RemoteUrl"]?.ToString().Should().Be("https://nextcloud.company.com/remote.php/dav/files/admin/Finance/2026/invoice.pdf");
    }

    [Fact]
    public async Task RemoteDownloadNode_DryRun_ShouldSimulateDownloadAndEmitOut()
    {
        // Arrange
        string destFolder = Path.Combine(_testDir, "Downloads");
        var node = new RemoteDownloadNode();
        node.Parameters["SourceUrl"] = "https://cdn.example.com/assets/logo.png";
        node.Parameters["DestinationFolder"] = destFolder;

        var item = new FileItemContext("initial.tmp");

        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.SetupGet(c => c.IsDryRun).Returns(true);

        FileItemContext? emittedItem = null;
        string? emittedPort = null;

        mockContext
            .Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, it) =>
            {
                emittedPort = port;
                emittedItem = it;
            })
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object);

        // Assert
        emittedPort.Should().Be("Out");
        emittedItem.Should().NotBeNull();
        emittedItem!.Metadata.Should().ContainKey("SourceUrl");
        emittedItem.Metadata["SourceUrl"]?.ToString().Should().Be("https://cdn.example.com/assets/logo.png");
        emittedItem.CurrentPath.Should().Be(Path.Combine(destFolder, "logo.png"));
    }

    [Fact]
    public async Task FtpDownloadNode_DryRun_ShouldSimulateDownloadAndEmitOut()
    {
        // Arrange
        string destFolder = Path.Combine(_testDir, "FtpDownloads");
        var node = new FtpDownloadNode();
        node.Parameters["Host"] = "ftp.acme.org";
        node.Parameters["Port"] = 21;
        node.Parameters["RemoteFilePath"] = "/reports/annual_2026.csv";
        node.Parameters["DestinationFolder"] = destFolder;

        var item = new FileItemContext("trigger.tmp");

        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.SetupGet(c => c.IsDryRun).Returns(true);

        FileItemContext? emittedItem = null;
        string? emittedPort = null;

        mockContext
            .Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, it) =>
            {
                emittedPort = port;
                emittedItem = it;
            })
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object);

        // Assert
        emittedPort.Should().Be("Out");
        emittedItem.Should().NotBeNull();
        emittedItem!.Metadata.Should().ContainKey("RemoteUrl");
        emittedItem.Metadata["RemoteUrl"]?.ToString().Should().Be("ftp://ftp.acme.org:21/reports/annual_2026.csv");
        emittedItem.CurrentPath.Should().Be(Path.Combine(destFolder, "annual_2026.csv"));
        emittedItem.Metadata.Should().ContainKey("DownloadedPath");
    }

    [Fact]
    public async Task SftpDownloadNode_DryRun_ShouldSimulateDownloadAndEmitOut()
    {
        // Arrange
        string destFolder = Path.Combine(_testDir, "SftpDownloads");
        var node = new SftpDownloadNode();
        node.Parameters["Host"] = "sftp.securecorp.net";
        node.Parameters["Port"] = 2222;
        node.Parameters["Username"] = "operator";
        node.Parameters["RemoteFilePath"] = "/var/data/archive.tar.gz";
        node.Parameters["DestinationFolder"] = destFolder;

        var item = new FileItemContext("trigger.tmp");

        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.SetupGet(c => c.IsDryRun).Returns(true);

        FileItemContext? emittedItem = null;
        string? emittedPort = null;

        mockContext
            .Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, it) =>
            {
                emittedPort = port;
                emittedItem = it;
            })
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object);

        // Assert
        emittedPort.Should().Be("Out");
        emittedItem.Should().NotBeNull();
        emittedItem!.Metadata.Should().ContainKey("RemoteUrl");
        emittedItem.Metadata["RemoteUrl"]?.ToString().Should().Be("sftp://operator@sftp.securecorp.net:2222/var/data/archive.tar.gz");
        emittedItem.CurrentPath.Should().Be(Path.Combine(destFolder, "archive.tar.gz"));
        emittedItem.Metadata.Should().ContainKey("DownloadedPath");
    }
}
