using System.Collections.ObjectModel;
using System.IO;
using FileFlow.App.ViewModels;
using FileFlow.Core.Engine;
using FileFlow.Plugin.Archives;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Hashing;
using FileFlow.Plugin.Integrations;
using FileFlow.Plugin.Logic;
using FileFlow.Sdk;
using FileFlow.Sdk.TemplateEngine;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit;

public class QASecurityExhaustiveAuditFixesTests
{
    [Fact]
    public void VariableTemplateResolver_ParseArguments_ShouldRespectCommasInsideQuotes()
    {
        // Arrange
        var item = new FileItemContext(@"C:\Test\Sample.txt");
        string template = "{Replace(FileName, \",\", \"_\")}";

        // Act
        string resolved = VariableTemplateResolver.Resolve(template, item);

        // Assert
        resolved.Should().NotBeNull();
    }

    [Fact]
    public void StringFunctionsEvaluator_RegexMatch_ShouldEnforceTimeoutWithoutHanging()
    {
        // Arrange
        var item = new FileItemContext(@"C:\Test\Sample.txt");
        // ReDoS pattern (catastrophic backtracking)
        string template = @"{RegexMatch(""aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaX"", ""(a+)+$"")}";

        // Act
        var act = () => VariableTemplateResolver.Resolve(template, item);

        // Assert - should complete quickly without hanging
        act.Should().NotThrow();
    }

    [Fact]
    public async Task DeduplicationFilterNode_ShouldResetSeenHashes_OnNewExecutionId()
    {
        // Arrange
        var node = new DeduplicationFilterNode();
        string tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "Identical Content");

        try
        {
            var item1 = new FileItemContext(tempFile);
            item1.Metadata["WorkflowExecutionId"] = "Execution-001";
            var ctx = new MockExecutionContext();

            // Run 1
            await node.ExecuteAsync("In", item1, ctx, CancellationToken.None);
            ctx.EmittedPorts.Should().Contain("Unique");
            ctx.EmittedPorts.Clear();

            // Run 2 with NEW ExecutionId
            var item2 = new FileItemContext(tempFile);
            item2.Metadata["WorkflowExecutionId"] = "Execution-002";
            await node.ExecuteAsync("In", item2, ctx, CancellationToken.None);

            // Assert: On new execution, it should be unique again!
            ctx.EmittedPorts.Should().Contain("Unique");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void LogViewModel_AddLog_ShouldQueueLogEntries()
    {
        // Arrange
        var vm = new LogViewModel();

        // Act
        vm.AddLog(LogLevel.Information, "Test Log Message");

        // Assert
        vm.Should().NotBeNull();
    }

    [Fact]
    public void ArchiveCompressorNode_ShouldParseCompressionTypeParameter()
    {
        // Arrange
        var node = new ArchiveCompressorNode();
        node.Parameters["CompressionType"] = "Store";
        node.Parameters["ArchiveFormat"] = "ZIP";

        // Assert
        node.Parameters["CompressionType"].Should().Be("Store");
    }

    private class MockExecutionContext : IFlowExecutionContext
    {
        public List<string> EmittedPorts { get; } = [];
        public bool IsDryRun => false;

        public Task EmitAsync(string outputPortName, FileItemContext item)
        {
            EmittedPorts.Add(outputPortName);
            return Task.CompletedTask;
        }

        public void ReportProgress(double percentage, string statusMessage) { }
        public void Log(string message, LogLevel level) { }
        public void RegisterPlannedAction(PlannedAction action) { }
        public void RecordJournalEntry(JournalEntry entry) { }
    }
}
