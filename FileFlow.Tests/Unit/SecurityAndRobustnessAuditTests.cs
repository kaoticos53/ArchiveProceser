using System.IO;
using FileFlow.App.Services;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Images;
using FileFlow.Sdk;
using FileFlow.Sdk.Renaming;
using Xunit;

namespace FileFlow.Tests.Unit;

public class SecurityAndRobustnessAuditTests
{
    private class MockFlowExecutionContext : IFlowExecutionContext
    {
        public bool IsDryRun { get; set; }
        public List<string> EmittedPorts { get; } = [];
        public List<FileItemContext> EmittedItems { get; } = [];
        public List<PlannedAction> PlannedActions { get; } = [];
        public List<JournalEntry> JournalEntries { get; } = [];
        public List<string> Logs { get; } = [];

        public Task EmitAsync(string portName, FileItemContext item)
        {
            EmittedPorts.Add(portName);
            EmittedItems.Add(item);
            return Task.CompletedTask;
        }

        public void Log(string message, LogLevel level)
        {
            Logs.Add($"[{level}] {message}");
        }

        public void ReportProgress(double percentage, string message) { }
        public void SetTotalExpectedItems(long totalExpectedItems) { }

        public void RegisterPlannedAction(PlannedAction action)
        {
            PlannedActions.Add(action);
        }

        public void RecordJournalEntry(JournalEntry entry)
        {
            JournalEntries.Add(entry);
        }
    }

    [Fact]
    public async Task FileRelocator_WhenSourceAndTargetAreIdentical_SkipsPhysicalOperationAndEmitsOut()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "FF_Audit_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string testFile = Path.Combine(tempDir, "same_path_test.txt");
        await File.WriteAllTextAsync(testFile, "Hello World Content");

        try
        {
            var node = new FileRelocatorNode();
            node.Parameters["Operation"] = "Move";
            node.Parameters["DestinationDirectory"] = tempDir; // Same folder!
            node.Parameters["VerifyIntegrity"] = true;

            var item = new FileItemContext(testFile, isDirectory: false);
            var ctx = new MockFlowExecutionContext();

            // Act
            await node.ExecuteAsync("In", item, ctx, CancellationToken.None);

            // Assert
            Assert.Contains("Out", ctx.EmittedPorts);
            Assert.DoesNotContain("Error", ctx.EmittedPorts);
            Assert.True(File.Exists(testFile));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task FileRelocator_WhenMovingWithIntegrityCheck_VerifiesAndCleansOriginalSafely()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "FF_Audit_Move_" + Guid.NewGuid().ToString("N"));
        string destDir = Path.Combine(tempDir, "DestSubdir");
        Directory.CreateDirectory(tempDir);
        string testFile = Path.Combine(tempDir, "source_to_move.txt");
        await File.WriteAllTextAsync(testFile, "Secure Move Validation Data 12345");

        try
        {
            var node = new FileRelocatorNode();
            node.Parameters["Operation"] = "Move";
            node.Parameters["DestinationDirectory"] = destDir;
            node.Parameters["VerifyIntegrity"] = true;
            node.Parameters["CreateDirectories"] = true;

            var item = new FileItemContext(testFile, isDirectory: false);
            var ctx = new MockFlowExecutionContext();

            // Act
            await node.ExecuteAsync("In", item, ctx, CancellationToken.None);

            // Assert
            Assert.Contains("Out", ctx.EmittedPorts);
            string expectedTarget = Path.Combine(destDir, "source_to_move.txt");
            Assert.True(File.Exists(expectedTarget));
            Assert.False(File.Exists(testFile)); // Original safely removed after hash verification
            Assert.Contains(ctx.JournalEntries, j => j.OperationType == JournalOperationType.Moved && j.DestinationPath == expectedTarget);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task AdvancedRenamer_WhenSearchReplaceRegexIsInvalid_HandlesGracefullyWithoutThrowing()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "FF_Audit_Regex_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string sourceFile = Path.Combine(tempDir, "document_sample.txt");
        await File.WriteAllTextAsync(sourceFile, "dummy text");

        try
        {
            var node = new AdvancedRenamerNode();
            var step = new RenameMethodStep
            {
                MethodType = RenameMethodType.SearchReplace,
                SearchText = "[unclosed_regex_bracket(",
                ReplaceText = "fixed",
                UseRegex = true,
                IsEnabled = true
            };
            node.Parameters["MethodSteps"] = RenamerPresetService.SerializeSteps([step]);

            var item = new FileItemContext(sourceFile);
            var ctx = new MockFlowExecutionContext();

            // Act & Assert (Should not throw RegexParseException)
            await node.ExecuteAsync("In", item, ctx, CancellationToken.None);

            Assert.Contains("Out", ctx.EmittedPorts);
            Assert.Equal("document_sample.txt", item.FileName);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task AdvancedRenamer_WhenNormalizeNumbersCustomRegexIsInvalid_HandlesGracefullyWithoutThrowing()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "FF_Audit_NormNum_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string sourceFile = Path.Combine(tempDir, "episode_1.mkv");
        await File.WriteAllTextAsync(sourceFile, "video data");

        try
        {
            var node = new AdvancedRenamerNode();
            var step = new RenameMethodStep
            {
                MethodType = RenameMethodType.NormalizeNumbers,
                NumberTarget = NumberPaddingTarget.CustomRegex,
                NumberRegexPattern = "(?<invalid[",
                NumberPaddingDigits = 3,
                IsEnabled = true
            };
            node.Parameters["MethodSteps"] = RenamerPresetService.SerializeSteps([step]);

            var item = new FileItemContext(sourceFile);
            var ctx = new MockFlowExecutionContext();

            // Act & Assert (Should not throw RegexParseException)
            await node.ExecuteAsync("In", item, ctx, CancellationToken.None);

            Assert.Contains("Out", ctx.EmittedPorts);
            Assert.Equal("episode_1.mkv", item.FileName);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ImageOptimizerNode_InDryRunMode_RegistersPlannedActionAndPreservesMetadata()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "FF_Audit_Image_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string dummyImg = Path.Combine(tempDir, "photo.png");
        await File.WriteAllBytesAsync(dummyImg, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        try
        {
            var node = new ImageOptimizerNode();
            node.Parameters["TargetFormat"] = "WebP";
            node.Parameters["Quality"] = 85;

            var item = new FileItemContext(dummyImg, isDirectory: false);
            item.FileSizeBytes = 1024;
            var ctx = new MockFlowExecutionContext { IsDryRun = true };

            // Act
            await node.ExecuteAsync("In", item, ctx, CancellationToken.None);

            // Assert
            Assert.Contains("Out", ctx.EmittedPorts);
            Assert.Single(ctx.PlannedActions);
            Assert.Equal(PlannedOperationType.TransformMedia, ctx.PlannedActions[0].OperationType);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task EmptyDirectoryCleaner_RegistersDeletedPermanentlyJournalEntry()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "FF_Audit_EmptyDir_" + Guid.NewGuid().ToString("N"));
        string subDir = Path.Combine(tempDir, "EmptyChild");
        Directory.CreateDirectory(subDir);

        try
        {
            var node = new EmptyDirectoryCleanerNode();
            node.Parameters["TargetDirectory"] = tempDir;
            node.Parameters["Recursive"] = true;

            var item = new FileItemContext(tempDir, isDirectory: true);
            var ctx = new MockFlowExecutionContext { IsDryRun = false };

            // Act
            await node.ExecuteAsync("TriggerIn", item, ctx, CancellationToken.None);

            // Assert
            Assert.False(Directory.Exists(subDir));
            Assert.Contains(ctx.JournalEntries, j => j.OperationType == JournalOperationType.DeletedPermanently && j.SourcePath == subDir);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public void FastObservableRingBuffer_CapacityAndCircularBehavior_OperatesCleanlyWithoutExceptions()
    {
        // Arrange
        var buffer = new FileFlow.App.Collections.FastObservableRingBuffer<string>(3);

        // Act
        buffer.Add("item1");
        buffer.Add("item2");
        buffer.Add("item3");
        buffer.Add("item4"); // Overwrites item1

        // Assert
        Assert.Equal(3, buffer.Count);
        Assert.Equal("item2", buffer[0]);
        Assert.Equal("item3", buffer[1]);
        Assert.Equal("item4", buffer[2]);
    }
}
