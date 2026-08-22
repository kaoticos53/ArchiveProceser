using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Integrations;
using FileFlow.Sdk;
using FluentAssertions;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

public class QASecurityAuditFixesTests
{
    [Fact]
    public async Task WebhookNotificationNode_WithInvalidUrlScheme_ShouldEmitToFailedPort()
    {
        var node = new WebhookNotificationNode();
        node.Parameters["Url"] = "ftp://invalid-scheme.com/test";

        var item = new FileItemContext("C:\\test.txt");
        var contextMock = new Mock<IFlowExecutionContext>();
        string? emittedPort = null;

        contextMock
            .Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, _) => emittedPort = port)
            .Returns(Task.CompletedTask);

        await node.ExecuteAsync("In", item, contextMock.Object, CancellationToken.None);

        emittedPort.Should().Be("Failed");
    }

    [Fact]
    public async Task VariableInjectorNode_WithInvalidOrSpacesOnlyKey_ShouldIgnoreInjection()
    {
        var node = new VariableInjectorNode();
        node.Parameters["   "] = "Value1";
        node.Parameters["---"] = "Value2";
        node.Parameters["ValidKey"] = "ValidValue";

        var item = new FileItemContext("C:\\test.txt");
        var contextMock = new Mock<IFlowExecutionContext>();

        await node.ExecuteAsync("In", item, contextMock.Object, CancellationToken.None);

        item.Metadata.Should().ContainKey("ValidKey");
        item.Metadata.Should().NotContainKey(" ");
        item.Metadata.Should().NotContainKey("___");
    }
}
