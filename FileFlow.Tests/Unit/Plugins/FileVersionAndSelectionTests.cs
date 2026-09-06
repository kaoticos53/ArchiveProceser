using System.IO;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Logic;
using FileFlow.Sdk;
using FileFlow.Sdk.TemplateEngine;
using FluentAssertions;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

public class FileVersionAndSelectionTests : IDisposable
{
    private readonly string _testDir;

    public FileVersionAndSelectionTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "FileFlow_VersionTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup failures in tests
        }
    }

    [Fact]
    public void FileItemContext_VersionRegistration_PopulatesMetadataAndResolves()
    {
        string origPath = Path.Combine(_testDir, "photo.jpg");
        File.WriteAllBytes(origPath, new byte[2000]);

        var item = new FileItemContext(origPath);
        item.FileVersions.Should().ContainKey("Original");
        item.GetVersionPath("Original").Should().Be(origPath);

        string optPath = Path.Combine(_testDir, "photo_opt.webp");
        File.WriteAllBytes(optPath, new byte[1000]);

        item.RegisterVersion("Optimized", optPath);

        item.GetVersionPath("Optimized").Should().Be(optPath);
        item.Metadata["File:Optimized"].Should().Be(optPath);
        item.Metadata["FileSize:Optimized"].Should().Be("1000");

        // SystemVariablesResolver template checks
        VariableTemplateResolver.Resolve("{File:Optimized}", item).Should().Be(optPath);
        VariableTemplateResolver.Resolve("{File:Original}", item).Should().Be(origPath);
        VariableTemplateResolver.Resolve("{FileSize:Optimized}", item).Should().Be("1000");
    }

    [Fact]
    public async Task BestVersionSelectorNode_SelectsSmallestSize_WhenOptimizedIsSmaller()
    {
        string origFile = Path.Combine(_testDir, "orig.jpg");
        string optFile = Path.Combine(_testDir, "opt.webp");

        File.WriteAllBytes(origFile, new byte[5000]);
        File.WriteAllBytes(optFile, new byte[2000]);

        var item = new FileItemContext(origFile);
        item.CurrentPath = optFile; // Transformed to opt.webp
        item.RegisterVersion("Optimized", optFile);

        var node = new BestVersionSelectorNode();
        node.Parameters["CandidateA"] = "{CurrentPath}";
        node.Parameters["CandidateB"] = "{OriginalPath}";
        node.Parameters["Criterion"] = "SmallestSize";
        node.Parameters["DiscardLoser"] = true;

        var contextMock = new Mock<IFlowExecutionContext>();
        var emittedPorts = new List<string>();

        contextMock.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, _) => emittedPorts.Add(port))
            .Returns(Task.CompletedTask);

        await node.ExecuteAsync("In", item, contextMock.Object, CancellationToken.None);

        item.CurrentPath.Should().Be(optFile);
        item.Metadata["SelectedVersion"].Should().Be("CandidateA");
        emittedPorts.Should().Contain("Out");
        emittedPorts.Should().Contain("WonA");
        File.Exists(optFile).Should().BeTrue();
        // Original must NEVER be purged
        File.Exists(origFile).Should().BeTrue();
    }

    [Fact]
    public async Task BestVersionSelectorNode_SelectsOriginal_WhenOptimizedIsLarger_AndAutoPurgesOptimized()
    {
        string origFile = Path.Combine(_testDir, "orig.jpg");
        string optFile = Path.Combine(_testDir, "opt_bad.png"); // Larger than original!

        File.WriteAllBytes(origFile, new byte[3000]);
        File.WriteAllBytes(optFile, new byte[8000]);

        var item = new FileItemContext(origFile);
        item.CurrentPath = optFile;
        item.RegisterVersion("Optimized", optFile);

        var node = new BestVersionSelectorNode();
        node.Parameters["CandidateA"] = "{CurrentPath}";
        node.Parameters["CandidateB"] = "{OriginalPath}";
        node.Parameters["Criterion"] = "SmallestSize";
        node.Parameters["DiscardLoser"] = true; // "perfecto con la autopurga"

        var contextMock = new Mock<IFlowExecutionContext>();
        var emittedPorts = new List<string>();

        contextMock.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, _) => emittedPorts.Add(port))
            .Returns(Task.CompletedTask);

        await node.ExecuteAsync("In", item, contextMock.Object, CancellationToken.None);

        // Candidate B (Original) should win!
        item.CurrentPath.Should().Be(origFile);
        item.Metadata["SelectedVersion"].Should().Be("CandidateB");
        emittedPorts.Should().Contain("Out");
        emittedPorts.Should().Contain("WonB");

        // Autopurge: Candidate A (intermediate optFile) was discarded and purged from disk!
        File.Exists(optFile).Should().BeFalse();
        // Original must remain intact
        File.Exists(origFile).Should().BeTrue();
    }

    [Fact]
    public async Task VersionRouterNode_RoutesToFalse_AndActivatesOriginal_WhenOptimizedIsLarger()
    {
        string origFile = Path.Combine(_testDir, "input.jpg");
        string optFile = Path.Combine(_testDir, "output.webp");

        File.WriteAllBytes(origFile, new byte[4000]);
        File.WriteAllBytes(optFile, new byte[6000]); // Larger

        var item = new FileItemContext(origFile);
        item.CurrentPath = optFile;
        item.RegisterVersion("Optimized", optFile);

        var node = new VersionRouterNode();
        node.Parameters["Property"] = "FileSize:Optimized";
        node.Parameters["Operator"] = "<";
        node.Parameters["ComparisonValue"] = "{FileSize:Original}";
        node.Parameters["TrueFile"] = "{CurrentPath}";
        node.Parameters["FalseFile"] = "{OriginalPath}";
        node.Parameters["PurgeUnselectedTemps"] = true;

        var contextMock = new Mock<IFlowExecutionContext>();
        string? emittedPort = null;

        contextMock.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, _) => emittedPort = port)
            .Returns(Task.CompletedTask);

        await node.ExecuteAsync("In", item, contextMock.Object, CancellationToken.None);

        emittedPort.Should().Be("False");
        item.CurrentPath.Should().Be(origFile);
        // Optimized was unselected and purged
        File.Exists(optFile).Should().BeFalse();
        File.Exists(origFile).Should().BeTrue();
    }

    [Fact]
    public async Task FileRelocatorNode_RelocatesSourcePath_AndCleansUpIntermediateSource()
    {
        string origFile = Path.Combine(_testDir, "source_original.jpg");
        string intermediateFile = Path.Combine(_testDir, "temp_render.png");
        string targetDir = Path.Combine(_testDir, "Dest");

        File.WriteAllBytes(origFile, new byte[1000]);
        File.WriteAllBytes(intermediateFile, new byte[1500]);

        var item = new FileItemContext(origFile);
        item.RegisterVersion("Render", intermediateFile);

        var relocator = new FileRelocatorNode();
        relocator.Parameters["SourcePath"] = "{File:Render}";
        relocator.Parameters["DestinationDirectory"] = targetDir;
        relocator.Parameters["Operation"] = "Copy";
        relocator.Parameters["CleanupSource"] = true;

        var contextMock = new Mock<IFlowExecutionContext>();
        await relocator.ExecuteAsync("In", item, contextMock.Object, CancellationToken.None);

        string expectedTarget = Path.Combine(targetDir, "temp_render.png");
        File.Exists(expectedTarget).Should().BeTrue();
        // Intermediate file cleaned up
        File.Exists(intermediateFile).Should().BeFalse();
        // Original intact
        File.Exists(origFile).Should().BeTrue();
    }

    [Fact]
    public async Task SwitchActiveFileNode_SwitchesCurrentPath_AndCanDeletePreviousIntermediate()
    {
        string origFile = Path.Combine(_testDir, "orig.txt");
        string tempFile = Path.Combine(_testDir, "temp.txt");

        File.WriteAllText(origFile, "Original content");
        File.WriteAllText(tempFile, "Temporary content");

        var item = new FileItemContext(origFile);
        item.CurrentPath = tempFile;

        var node = new SwitchActiveFileNode();
        node.Parameters["TargetFile"] = "{OriginalPath}";
        node.Parameters["DeleteCurrentFileFirst"] = true;

        var contextMock = new Mock<IFlowExecutionContext>();
        await node.ExecuteAsync("In", item, contextMock.Object, CancellationToken.None);

        item.CurrentPath.Should().Be(origFile);
        File.Exists(tempFile).Should().BeFalse();
        File.Exists(origFile).Should().BeTrue();
    }

    [Fact]
    public async Task FileForkNode_EmitsParallelBranchesForOriginalAndCurrent()
    {
        string origFile = Path.Combine(_testDir, "source.raw");
        string optFile = Path.Combine(_testDir, "processed.png");

        File.WriteAllBytes(origFile, new byte[100]);
        File.WriteAllBytes(optFile, new byte[50]);

        var item = new FileItemContext(origFile);
        item.CurrentPath = optFile;

        var node = new FileForkNode();
        node.Parameters["ForkOriginal"] = true;
        node.Parameters["ForkCurrent"] = true;

        var emittedItems = new Dictionary<string, FileItemContext>();
        var contextMock = new Mock<IFlowExecutionContext>();

        contextMock.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, itm) => emittedItems[port] = itm)
            .Returns(Task.CompletedTask);

        await node.ExecuteAsync("In", item, contextMock.Object, CancellationToken.None);

        emittedItems.Should().ContainKey("Original");
        emittedItems["Original"].CurrentPath.Should().Be(origFile);

        emittedItems.Should().ContainKey("Current");
        emittedItems["Current"].CurrentPath.Should().Be(optFile);
    }

    [Fact]
    public async Task IntermediateCleanupNode_PurgesIntermediateFiles_PreservingOriginal()
    {
        string origFile = Path.Combine(_testDir, "orig.jpg");
        string temp1 = Path.Combine(_testDir, "temp1.tmp");
        string temp2 = Path.Combine(_testDir, "temp2.tmp");

        File.WriteAllBytes(origFile, new byte[100]);
        File.WriteAllBytes(temp1, new byte[200]);
        File.WriteAllBytes(temp2, new byte[300]);

        var item = new FileItemContext(origFile);
        item.RegisterVersion("Step1", temp1);
        item.RegisterVersion("Step2", temp2);

        var node = new IntermediateCleanupNode();
        node.Parameters["KeepOriginal"] = true;
        node.Parameters["KeepCurrent"] = true;

        var contextMock = new Mock<IFlowExecutionContext>();
        await node.ExecuteAsync("In", item, contextMock.Object, CancellationToken.None);

        File.Exists(temp1).Should().BeFalse();
        File.Exists(temp2).Should().BeFalse();
        File.Exists(origFile).Should().BeTrue();
    }
}
