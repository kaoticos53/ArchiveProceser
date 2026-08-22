using System.IO;
using FileFlow.Plugin.Hashing;
using FileFlow.Plugin.Logic;
using FileFlow.Sdk;
using FluentAssertions;
using Moq;
using Xunit;


namespace FileFlow.Tests.Unit.Plugins;

public class LogicAndHashingNodesTests
{
    [Fact]
    public async Task SwitchCaseNode_RoutesCorrectlyByExtension()
    {
        var node = new SwitchCaseNode();
        node.SetCases([
            new SwitchCaseRule("Imagenes", "jpg;jpeg;png;webp"),
            new SwitchCaseRule("Videos", "mp4;mkv;avi;mov")
        ]);

        var contextMock = new Mock<IFlowExecutionContext>();
        string? emittedPort = null;

        contextMock.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, _) => emittedPort = port)
            .Returns(Task.CompletedTask);

        var item = new FileItemContext(@"C:\media\sample.mp4");

        await node.ExecuteAsync("In", item, contextMock.Object, CancellationToken.None);

        emittedPort.Should().Be("Videos");
        node.Outputs.Select(o => o.Name).Should().Contain(["Imagenes", "Videos", "Default"]);
    }

    [Fact]
    public async Task SwitchCaseNode_RoutesCorrectlyBySmartSizeRanges()
    {
        var node = new SwitchCaseNode();
        node.Parameters["Expression"] = "{SizeMB}";
        node.SetCases([
            new SwitchCaseRule("Small", "< 10 MB"),
            new SwitchCaseRule("Medium", "10 MB..1 GB"),
            new SwitchCaseRule("Large", ">= 1 GB")
        ]);

        var contextMock = new Mock<IFlowExecutionContext>();
        string? emittedPort = null;
        contextMock.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, _) => emittedPort = port)
            .Returns(Task.CompletedTask);

        var item = new FileItemContext(@"C:\files\video.mp4")
        {
            FileSizeBytes = 50L * 1024 * 1024
        };

        await node.ExecuteAsync("In", item, contextMock.Object, CancellationToken.None);

        emittedPort.Should().Be("Medium");
    }

    [Fact]
    public async Task SwitchCaseNode_RoutesToDefaultWhenNoMatch()
    {
        var node = new SwitchCaseNode();
        node.SetCases([
            new SwitchCaseRule("Imagenes", "jpg;png")
        ]);

        var contextMock = new Mock<IFlowExecutionContext>();
        string? emittedPort = null;

        contextMock.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, _) => emittedPort = port)
            .Returns(Task.CompletedTask);

        var item = new FileItemContext(@"C:\docs\readme.txt");

        await node.ExecuteAsync("In", item, contextMock.Object, CancellationToken.None);

        emittedPort.Should().Be("Default");
    }


    [Fact]
    public async Task ExpressionFilterNode_EvaluatesNumericSize()
    {
        var node = new ExpressionFilterNode();
        node.Parameters["Property"] = "SizeMB";
        node.Parameters["Operator"] = ">";
        node.Parameters["ComparisonValue"] = "5";

        var contextMock = new Mock<IFlowExecutionContext>();
        string? emittedPort = null;

        contextMock.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, _) => emittedPort = port)
            .Returns(Task.CompletedTask);

        var item = new FileItemContext(@"C:\files\bigfile.zip")
        {
            FileSizeBytes = 10 * 1024 * 1024 // 10MB
        };

        await node.ExecuteAsync("In", item, contextMock.Object, CancellationToken.None);

        emittedPort.Should().Be("True");
    }

    [Fact]
    public async Task ExpressionFilterNode_EvaluatesAlphanumericWithUnits()
    {
        var node = new ExpressionFilterNode();
        node.Parameters["Property"] = "CustomSize";
        node.Parameters["Operator"] = "<";
        node.Parameters["ComparisonValue"] = "10 MB";

        var contextMock = new Mock<IFlowExecutionContext>();
        string? emittedPort = null;

        contextMock.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, _) => emittedPort = port)
            .Returns(Task.CompletedTask);

        var item = new FileItemContext(@"C:\files\photo.jpg");
        item.Metadata["CustomSize"] = "4.25 MB";

        await node.ExecuteAsync("In", item, contextMock.Object, CancellationToken.None);

        emittedPort.Should().Be("True");
    }

    [Fact]
    public async Task BatchBufferNode_FlushesWhenBatchSizeReached()
    {
        var node = new BatchBufferNode();
        node.Parameters["BatchSize"] = 3;

        var emittedItems = new List<string>();
        var contextMock = new Mock<IFlowExecutionContext>();
        contextMock.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, item) => emittedItems.Add($"{port}:{item.CurrentPath}"))
            .Returns(Task.CompletedTask);

        await node.ExecuteAsync("ItemIn", new FileItemContext("file1.txt"), contextMock.Object, CancellationToken.None);
        await node.ExecuteAsync("ItemIn", new FileItemContext("file2.txt"), contextMock.Object, CancellationToken.None);
        emittedItems.Should().BeEmpty(); // not yet 3 items

        await node.ExecuteAsync("ItemIn", new FileItemContext("file3.txt"), contextMock.Object, CancellationToken.None);
        emittedItems.Should().HaveCount(4); // 3 items + 1 BatchCompleted
    }

    [Fact]
    public async Task DeduplicationFilterNode_IdentifiesDuplicates()
    {
        var node = new DeduplicationFilterNode();
        var contextMock = new Mock<IFlowExecutionContext>();
        var emittedPorts = new List<string>();

        contextMock.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, _) => emittedPorts.Add(port))
            .Returns(Task.CompletedTask);

        string tempFile1 = Path.GetTempFileName();
        string tempFile2 = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile1, "Same content");
        await File.WriteAllTextAsync(tempFile2, "Same content");

        try
        {
            var item1 = new FileItemContext(tempFile1);
            var item2 = new FileItemContext(tempFile2);

            await node.ExecuteAsync("In", item1, contextMock.Object, CancellationToken.None);
            await node.ExecuteAsync("In", item2, contextMock.Object, CancellationToken.None);

            emittedPorts.Should().Equal(["Unique", "Duplicate"]);
        }
        finally
        {
            if (File.Exists(tempFile1)) File.Delete(tempFile1);
            if (File.Exists(tempFile2)) File.Delete(tempFile2);
        }
    }
}
