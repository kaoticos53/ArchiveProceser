using System.IO;
using FileFlow.Core.Engine;
using FileFlow.Sdk.Storage;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Sdk;

public class TempWorkspaceManagerTests
{
    [Fact]
    public async Task NullTempWorkspaceManager_ShouldProvideSafeNoOpDefaults()
    {
        // Arrange
        var nullManager = NullTempWorkspaceManager.Instance;

        // Act & Assert
        nullManager.ExecutionTempDirectory.Should().NotBeNullOrWhiteSpace();
        nullManager.CreateSubdirectory("test").Should().NotBeNullOrWhiteSpace();

        var act1 = () => nullManager.RegisterTemporaryFile("dummy.tmp");
        var act2 = () => nullManager.RegisterTemporaryDirectory("dummy_dir");
        var freed = await nullManager.CleanupExecutionWorkspaceAsync();

        act1.Should().NotThrow();
        act2.Should().NotThrow();
        freed.Should().Be(0);
    }

    [Fact]
    public void WorkflowWorkspaceManager_ShouldCreateScopedDirectoryAndCleanupOnDispose()
    {
        // Arrange
        string baseTemp = Path.Combine(Path.GetTempPath(), "FileFlow_Test_Runs_" + Guid.NewGuid().ToString("N"));
        string runId = "TestRun_" + Guid.NewGuid().ToString("N");
        string workspacePath;

        using (var workspace = new WorkflowWorkspaceManager(runId, baseTemp))
        {
            workspacePath = workspace.ExecutionTempDirectory;
            Directory.Exists(workspacePath).Should().BeTrue();
            workspacePath.Should().Contain(runId);

            // Create intermediate subdirectory and file
            string subDir = workspace.CreateSubdirectory("intermediate");
            Directory.Exists(subDir).Should().BeTrue();

            string tempFile = Path.Combine(subDir, "test.png");
            File.WriteAllText(tempFile, "dummy image content");
            File.Exists(tempFile).Should().BeTrue();

            // Create an external temp directory and register it
            string externalDir = Path.Combine(Path.GetTempPath(), "FileFlow_External_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(externalDir);
            string externalFile = Path.Combine(externalDir, "item.dat");
            File.WriteAllText(externalFile, "test data");
            workspace.RegisterTemporaryDirectory(externalDir);

            // Workspace and external should exist before dispose
            Directory.Exists(workspacePath).Should().BeTrue();
            Directory.Exists(externalDir).Should().BeTrue();
        }

        // Assert after dispose: workspace and registered external folder should be purged
        Directory.Exists(workspacePath).Should().BeFalse();
    }

    [Fact]
    public void WorkflowWorkspaceManager_RegisterFile_ShouldDeleteRegisteredIndividualFile()
    {
        // Arrange
        string baseTemp = Path.Combine(Path.GetTempPath(), "FileFlow_Test_Runs_" + Guid.NewGuid().ToString("N"));
        string externalFile = Path.Combine(Path.GetTempPath(), "FileFlow_ExtFile_" + Guid.NewGuid().ToString("N") + ".tmp");
        File.WriteAllText(externalFile, "temp content");

        using (var workspace = new WorkflowWorkspaceManager("run_files", baseTemp))
        {
            workspace.RegisterTemporaryFile(externalFile);
            File.Exists(externalFile).Should().BeTrue();
        }

        // Assert
        File.Exists(externalFile).Should().BeFalse();
    }
}
