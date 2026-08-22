using System.IO;
using FileFlow.Core.Engine;
using FileFlow.Sdk;
using FluentAssertions;
using Xunit;


namespace FileFlow.Tests.Unit.Core;

public class DryRunAndJournalTests
{
    [Fact]
    public async Task ExecutionJournal_Rollback_RestoresMovedFile()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "FileFlowTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string sourceFile = Path.Combine(tempDir, "source.txt");
            string targetFile = Path.Combine(tempDir, "target.txt");
            await File.WriteAllTextAsync(sourceFile, "Hello FileFlow");

            // Perform Move
            File.Move(sourceFile, targetFile);

            var journal = new ExecutionJournalService();
            journal.Record(new JournalEntry(
                Guid.NewGuid(),
                "node_1",
                JournalOperationType.Moved,
                sourceFile,
                targetFile
            ));

            journal.Entries.Should().HaveCount(1);
            File.Exists(targetFile).Should().BeTrue();
            File.Exists(sourceFile).Should().BeFalse();

            // Rollback
            int undone = await journal.RollbackAsync();

            undone.Should().Be(1);
            File.Exists(sourceFile).Should().BeTrue();
            File.Exists(targetFile).Should().BeFalse();
            string content = await File.ReadAllTextAsync(sourceFile);
            content.Should().Be("Hello FileFlow");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void WorkflowExecutor_RegisterPlannedAction_StoresInList()
    {
        var executor = new WorkflowExecutor { IsDryRun = true };
        var action = new PlannedAction(
            Guid.NewGuid(),
            "node_rename",
            "Advanced Renamer",
            PlannedOperationType.Rename,
            @"C:\test\old.txt",
            @"C:\test\new.txt",
            "Rename file"
        );

        executor.RegisterPlannedAction(action);

        executor.PlannedActions.Should().ContainSingle();
        executor.PlannedActions[0].SourcePath.Should().Be(@"C:\test\old.txt");
        executor.PlannedActions[0].DestinationPath.Should().Be(@"C:\test\new.txt");
    }
}
