using System.IO;
using FileFlow.Core.Engine;
using FileFlow.Sdk;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Core;

public class AutomationAndResilienceTests
{
    [Fact]
    public async Task ExecutionRetryHelper_ShouldRetryAndSucceed_WhenTransientFailureOccurs()
    {
        int attempts = 0;

        await ExecutionRetryHelper.ExecuteWithRetryAsync(async () =>
        {
            attempts++;
            if (attempts < 2)
            {
                throw new IOException("Transient lock error");
            }
            await Task.CompletedTask;
        }, maxRetries: 3, initialBackoffMs: 10);

        attempts.Should().Be(2);
    }

    [Fact]
    public async Task ExecutionRetryHelper_ShouldThrow_WhenMaxRetriesExceeded()
    {
        int attempts = 0;

        var act = async () =>
        {
            await ExecutionRetryHelper.ExecuteWithRetryAsync(async () =>
            {
                attempts++;
                throw new InvalidOperationException("Persistent error");
            }, maxRetries: 2, initialBackoffMs: 10);
        };

        await act.Should().ThrowAsync<InvalidOperationException>();
        attempts.Should().Be(3); // 1 initial + 2 retries
    }

    [Fact]
    public async Task FlowSchedulerService_ShouldEmitTriggers_WhenRunning()
    {
        using var scheduler = new FlowSchedulerService();
        scheduler.StartInterval(TimeSpan.FromMilliseconds(25));

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(2000));
        var triggers = new List<DateTime>();

        try
        {
            await foreach (var trigger in scheduler.TriggerReader.ReadAllAsync(cts.Token))
            {
                triggers.Add(trigger);
                if (triggers.Count >= 2) break;
            }
        }
        catch (OperationCanceledException) { }

        triggers.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task FolderWatcherService_ShouldDetectCreatedFile_AfterDebounce()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        try
        {
            using var watcher = new FolderWatcherService();
            watcher.Start(tempDir, filter: "*.txt", includeSubdirectories: false, debounceMs: 100);

            string sampleFile = Path.Combine(tempDir, "test_item.txt");
            await File.WriteAllTextAsync(sampleFile, "Hello World");

            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500));
            FileItemContext? detectedItem = null;

            try
            {
                await foreach (var item in watcher.ItemReader.ReadAllAsync(cts.Token))
                {
                    detectedItem = item;
                    break;
                }
            }
            catch (OperationCanceledException) { }

            detectedItem.Should().NotBeNull();
            detectedItem!.CurrentPath.Should().Be(sampleFile);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
