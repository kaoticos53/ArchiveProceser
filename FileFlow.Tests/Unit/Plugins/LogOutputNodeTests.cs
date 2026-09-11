using FileFlow.Plugin.FileSystem;
using FileFlow.Sdk;
using FluentAssertions;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

public class LogOutputNodeTests
{
    [Fact]
    public void ParameterDescriptors_ShouldContainCustomMessageWithMultiLineTextEditor()
    {
        // Arrange
        var node = new LogOutputNode();

        // Act
        var descriptors = node.ParameterDescriptors;

        // Assert
        descriptors.Should().NotBeNull();
        var customMsgDesc = descriptors.FirstOrDefault(d => d.Key == "CustomMessage");
        customMsgDesc.Should().NotBeNull();
        customMsgDesc!.EditorType.Should().Be(ParameterEditorType.MultiLineText);
        customMsgDesc.DisplayOrder.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithCustomMessage_ShouldResolveVariablesAndLogCustomMessage()
    {
        // Arrange
        var node = new LogOutputNode();
        node.Parameters["CustomMessage"] = "Processing file '{FileName}' with category '{AI:Category}' (Size: {SizeKb} KB)";
        node.Parameters["LogLevel"] = "Warning";

        var item = new FileItemContext(@"C:\Data\document.pdf", isDirectory: false)
        {
            FileSizeBytes = 2048
        };
        item.Metadata["AI:Category"] = "Invoices";

        string? loggedMessage = null;
        LogLevel? loggedLevel = null;
        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext
            .Setup(c => c.Log(It.IsAny<string>(), It.IsAny<LogLevel>(), It.IsAny<FileItemContext>(), It.IsAny<double>(), It.IsAny<string?>()))
            .Callback<string, LogLevel, FileItemContext?, double, string?>((msg, lvl, _, _, _) =>
            {
                loggedMessage = msg;
                loggedLevel = lvl;
            });

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        loggedMessage.Should().Be("Processing file 'document.pdf' with category 'Invoices' (Size: 2.0 KB)");
        loggedLevel.Should().Be(LogLevel.Warning);
        item.ExecutionLog.Should().Contain(l => l.Contains("LogOutputNode: Processing file 'document.pdf' with category 'Invoices'"));
        mockContext.Verify(c => c.EmitAsync("Out", item), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyCustomMessage_ShouldUseDefaultSummary()
    {
        // Arrange
        var node = new LogOutputNode();
        node.Parameters["CustomMessage"] = string.Empty;
        node.Parameters["LogLevel"] = "Information";

        var item = new FileItemContext(@"C:\Data\image.png", isDirectory: false)
        {
            FileSizeBytes = 1048576 // 1 MB
        };
        item.Tags.Add("Photo");

        string? loggedMessage = null;
        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext
            .Setup(c => c.Log(It.IsAny<string>(), It.IsAny<LogLevel>(), It.IsAny<FileItemContext>(), It.IsAny<double>(), It.IsAny<string?>()))
            .Callback<string, LogLevel, FileItemContext?, double, string?>((msg, _, _, _, _) => loggedMessage = msg);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        loggedMessage.Should().Contain("🔍 Inspección: image.png");
        loggedMessage.Should().Contain("1 tags");
        item.ExecutionLog.Should().Contain(l => l.Contains("LogOutputNode inspeccionó estado (image.png)"));
        mockContext.Verify(c => c.EmitAsync("Out", item), Times.Once);
    }
}
