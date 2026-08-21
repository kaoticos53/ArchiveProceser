using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Plugin.Images;
using FileFlow.Sdk;
using FluentAssertions;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

public class ImageOptimizerNodeTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldEmitError_WhenInputFileDoesNotExist()
    {
        // Arrange
        string nonExistentFile = @"C:\FakeImage_" + Guid.NewGuid() + ".jpg";
        var node = new ImageOptimizerNode();
        var item = new FileItemContext(nonExistentFile, isDirectory: false);

        var emittedErrors = new List<FileItemContext>();
        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.Setup(c => c.EmitAsync("Error", It.IsAny<FileItemContext>()))
                   .Callback<string, FileItemContext>((port, emItem) => emittedErrors.Add(emItem))
                   .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        emittedErrors.Should().HaveCount(1);
    }
}
