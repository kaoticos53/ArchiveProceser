using FileFlow.Core.Engine;
using FileFlow.Sdk;
using FileFlow.Sdk.Telemetry;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit;

public class StructuredLogContextTests
{
    [Fact]
    public void WorkflowExecutionContext_WhenLoggingWithoutExplicitPath_ShouldAutoBindFromCurrentItem()
    {
        var executor = new WorkflowExecutor();
        var item = new FileItemContext
        {
            CurrentPath = @"C:\Photos\Sunset.jpg",
            OriginalPath = @"C:\Photos\Sunset.jpg",
            FileSizeBytes = 4_500_000
        };

        StructuredLogRecord? emittedRecord = null;
        executor.StructuredLogEmitted += record => emittedRecord = record;

        var context = new WorkflowExecutionContext("Node_A", executor, CancellationToken.None, item);

        // Act - Invoke simple log call without passing path or item
        context.Log("Transformación completada con éxito.", LogLevel.Information);

        // Assert
        emittedRecord.Should().NotBeNull();
        emittedRecord!.Message.Should().Be("Transformación completada con éxito.");
        emittedRecord.FilePath.Should().Be(@"C:\Photos\Sunset.jpg");
        emittedRecord.FileName.Should().Be("Sunset.jpg");
        emittedRecord.ItemId.Should().Be(item.Id.ToString());
        emittedRecord.FileSizeBytes.Should().Be(4_500_000);
        emittedRecord.FormattedFileSize.Should().Be("4.29 MB");
        emittedRecord.ShortItemId.Should().Be(item.Id.ToString()[..8]);
    }

    [Fact]
    public void WorkflowExecutionContext_WhenLoggingWithDetailsJson_ShouldAttachPayload()
    {
        var executor = new WorkflowExecutor();
        var item = new FileItemContext
        {
            CurrentPath = @"C:\Docs\Report.pdf",
            FileSizeBytes = 120_000
        };

        StructuredLogRecord? emittedRecord = null;
        executor.StructuredLogEmitted += record => emittedRecord = record;

        var context = new WorkflowExecutionContext("Node_Inspector", executor, CancellationToken.None, item);

        string jsonPayload = "{\"pages\": 12, \"encrypted\": false}";
        context.Log("Inspección de documento", LogLevel.Information, item, durationMs: 4.5, detailsJson: jsonPayload);

        emittedRecord.Should().NotBeNull();
        emittedRecord!.HasDetails.Should().BeTrue();
        emittedRecord.DetailsJson.Should().Be(jsonPayload);
        emittedRecord.DurationMs.Should().Be(4.5);
        emittedRecord.FileName.Should().Be("Report.pdf");
    }

    [Fact]
    public void WorkflowExecutionContext_WhenNodeLoggingIsDisabled_ShouldNotEmitLogs()
    {
        var executor = new WorkflowExecutor();
        executor.SetNodeLoggingEnabled("Node_Silenced", false);

        var item = new FileItemContext(@"C:\Docs\Secret.pdf");
        StructuredLogRecord? emittedRecord = null;
        executor.StructuredLogEmitted += record => emittedRecord = record;

        var context = new WorkflowExecutionContext("Node_Silenced", executor, CancellationToken.None, item);

        // Act
        context.Log("Este log no debería emitirse.", LogLevel.Information);

        // Assert
        emittedRecord.Should().BeNull();
    }

    [Fact]
    public void WorkflowExecutionContext_WhenNodeLoggingIsReEnabled_ShouldEmitLogsNormally()
    {
        var executor = new WorkflowExecutor();
        executor.SetNodeLoggingEnabled("Node_A", false);

        var item = new FileItemContext(@"C:\Docs\Public.pdf");
        StructuredLogRecord? emittedRecord = null;
        executor.StructuredLogEmitted += record => emittedRecord = record;

        var context = new WorkflowExecutionContext("Node_A", executor, CancellationToken.None, item);

        // Act 1: Silenciado
        context.Log("Mensaje silenciado", LogLevel.Information);
        emittedRecord.Should().BeNull();

        // Act 2: Reactivado
        executor.SetNodeLoggingEnabled("Node_A", true);
        context.Log("Mensaje activo", LogLevel.Information);

        // Assert
        emittedRecord.Should().NotBeNull();
        emittedRecord!.Message.Should().Be("Mensaje activo");
    }
}
