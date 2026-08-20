using FileFlow.Plugin.FileSystem;
using FileFlow.Sdk;
using FluentAssertions;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

public class VariableInjectorNodeTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldInjectMultipleCustomVariablesIntoItemMetadata()
    {
        // Arrange
        var node = new VariableInjectorNode();
        node.Parameters.Clear();
        node.Parameters["CustomCategory"] = "{FileNameNoExt}_processed";
        node.Parameters["CustomYear"] = "{Year(DateNow)}";

        var item = new FileItemContext(@"C:\Photos\vacation.jpg", isDirectory: false);
        var mockContext = new Mock<IFlowExecutionContext>();

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        item.Metadata.Should().ContainKey("CustomCategory");
        item.Metadata["CustomCategory"].Should().Be("vacation_processed");
        item.Metadata.Should().ContainKey("CustomYear");
        item.Metadata["CustomYear"].Should().Be(DateTime.Now.Year.ToString());
        mockContext.Verify(c => c.EmitAsync("Out", item), Times.Once);
    }
}
