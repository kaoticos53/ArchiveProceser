using System.IO;
using System.Windows;
using FileFlow.App.ViewModels;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

public class AdvancedRenamerEditorViewModelTests : IDisposable
{
    private readonly string _testDir;

    public AdvancedRenamerEditorViewModelTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "FileFlow_RenamerPreview_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public void Constructor_WithoutFolderSource_ShouldUseSyntheticSamplesAndDefaultDescription()
    {
        // Arrange
        var loader = new PluginLoader();
        var editor = new EditorViewModel(loader);
        var renamerNode = new NodeViewModel(new AdvancedRenamerNode(), new Point(0, 0))
        {
            ParentEditor = editor
        };
        editor.Nodes.Add(renamerNode);

        // Act
        var vm = new AdvancedRenamerEditorViewModel(renamerNode);

        // Assert
        vm.PreviewItems.Should().NotBeEmpty();
        vm.PreviewItems.Count.Should().Be(9);
        vm.PreviewSourceDescription.Should().Contain("sintéticas");
    }

    [Fact]
    public void Constructor_WithConnectedFolderSource_ShouldLoadRealFilesInLivePreview()
    {
        // Arrange
        for (int i = 1; i <= 5; i++)
        {
            File.WriteAllText(Path.Combine(_testDir, $"video_cap_{i}.mp4"), "content");
        }

        var loader = new PluginLoader();
        var editor = new EditorViewModel(loader);

        var folderNode = new NodeViewModel(new FolderSourceNode(), new Point(0, 0))
        {
            ParentEditor = editor
        };
        folderNode.NodeInstance.Parameters["SourcePath"] = _testDir;
        folderNode.Parameters.First(p => p.Key == "SourcePath").Value = _testDir;

        var renamerNode = new NodeViewModel(new AdvancedRenamerNode(), new Point(200, 0))
        {
            ParentEditor = editor
        };

        editor.Nodes.Add(folderNode);
        editor.Nodes.Add(renamerNode);

        var outPort = folderNode.OutputPorts.First();
        var inPort = renamerNode.InputPorts.First();
        editor.Connections.Add(new ConnectionViewModel(outPort, inPort));

        // Act
        var vm = new AdvancedRenamerEditorViewModel(renamerNode);

        // Assert
        vm.PreviewItems.Should().HaveCount(5);
        vm.PreviewItems.Should().Contain(p => p.OriginalName == "video_cap_1.mp4");
        vm.PreviewItems.Should().Contain(p => p.OriginalName == "video_cap_5.mp4");
        vm.PreviewSourceDescription.Should().Contain("5 archivo(s) real(es)");
    }

    [Fact]
    public void Constructor_WithFolderSourceOver100Files_ShouldCapAt100Items()
    {
        // Arrange
        for (int i = 1; i <= 120; i++)
        {
            File.WriteAllText(Path.Combine(_testDir, $"item_{i:D3}.txt"), "content");
        }

        var loader = new PluginLoader();
        var editor = new EditorViewModel(loader);

        var folderNode = new NodeViewModel(new FolderSourceNode(), new Point(0, 0))
        {
            ParentEditor = editor
        };
        folderNode.NodeInstance.Parameters["SourcePath"] = _testDir;
        folderNode.Parameters.First(p => p.Key == "SourcePath").Value = _testDir;

        var renamerNode = new NodeViewModel(new AdvancedRenamerNode(), new Point(200, 0))
        {
            ParentEditor = editor
        };

        editor.Nodes.Add(folderNode);
        editor.Nodes.Add(renamerNode);

        var outPort = folderNode.OutputPorts.First();
        var inPort = renamerNode.InputPorts.First();
        editor.Connections.Add(new ConnectionViewModel(outPort, inPort));

        // Act
        var vm = new AdvancedRenamerEditorViewModel(renamerNode);

        // Assert
        vm.PreviewItems.Should().HaveCount(100);
        vm.PreviewSourceDescription.Should().Contain("100 archivo(s) real(es)");
    }
}
