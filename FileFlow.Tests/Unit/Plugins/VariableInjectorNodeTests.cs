using FileFlow.Plugin.FileSystem;
using FileFlow.Sdk;
using FluentAssertions;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

public class VariableInjectorNodeTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldInjectCustomVariableIntoItemMetadata()
    {
        // Arrange
        var node = new VariableInjectorNode();
        node.Parameters["VariableName"] = "CustomCategory";
        node.Parameters["ExpressionValue"] = "{FileNameNoExt}_processed";

        var item = new FileItemContext(@"C:\Photos\vacation.jpg", isDirectory: false);
        var mockContext = new Mock<IFlowExecutionContext>();

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        item.Metadata.Should().ContainKey("CustomCategory");
        item.Metadata["CustomCategory"].Should().Be("vacation_processed");
        mockContext.Verify(c => c.EmitAsync("Out", item), Times.Once);
    }
}
