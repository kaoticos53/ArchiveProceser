using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.FileSystem.Reporting;
using FileFlow.Sdk;
using FluentAssertions;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

public class OperationReportNodeTests
{
    [Fact]
    public async Task ExecuteAsync_And_OnWorkflowCompletedAsync_ShouldGenerateHtmlReportInMemory()
    {
        // Arrange
        var node = new OperationReportNode();
        node.Parameters["ReportFormat"] = "HTML";
        node.Parameters["ReportScope"] = "Consolidated";
        node.Parameters["ReportFileName"] = "Reporte_Test";
        node.Parameters["Theme"] = "ModernDark";

        var item = new FileItemContext(@"C:\Fotos\foto_vacaciones.jpg");
        item.AddLog("Hash SHA-256 calculado: e3b0c442...");
        item.AddLog("Renombrado a 2026-08-31_foto_vacaciones.jpg");
        item.Metadata["Exif:DateTaken"] = "2026-08-31 10:00:00";
        item.Metadata["Hash:SHA256"] = "e3b0c442...";

        var emittedOut = new List<FileItemContext>();
        var emittedReport = new List<FileItemContext>();
        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.Setup(c => c.IsDryRun).Returns(false);
        mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
                   .Callback<string, FileItemContext>((_, emItem) => emittedOut.Add(emItem))
                   .Returns(Task.CompletedTask);
        mockContext.Setup(c => c.EmitAsync("Report", It.IsAny<FileItemContext>()))
                   .Callback<string, FileItemContext>((_, emItem) => emittedReport.Add(emItem))
                   .Returns(Task.CompletedTask);

        // Act 1: Process file through node
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert 1: Out received original file, Report is not emitted yet in Consolidated scope
        emittedOut.Should().HaveCount(1);
        emittedReport.Should().BeEmpty();

        // Act 2: Complete workflow
        await node.OnWorkflowCompletedAsync(mockContext.Object, CancellationToken.None);

        // Assert 2: Consolidated report emitted in memory
        emittedReport.Should().HaveCount(1);
        var reportItem = emittedReport[0];
        reportItem.CurrentPath.Should().Be("Reporte_Test.html");
        reportItem.Metadata.Should().ContainKey("ReportContent");
        reportItem.Metadata.Should().ContainKey("VirtualContent");

        string htmlContent = reportItem.Metadata["ReportContent"]?.ToString() ?? string.Empty;
        htmlContent.Should().Contain("Reporte Consolidado de Operaciones");
        htmlContent.Should().Contain("foto_vacaciones.jpg");
        htmlContent.Should().Contain("Hash SHA-256 calculado");
        htmlContent.Should().Contain("Exif:DateTaken");
    }

    [Fact]
    public async Task ExecuteAsync_And_OnWorkflowCompletedAsync_ShouldGenerateMarkdownReportInMemory()
    {
        // Arrange
        var node = new OperationReportNode();
        node.Parameters["ReportFormat"] = "Markdown";
        node.Parameters["ReportScope"] = "Consolidated";
        node.Parameters["ReportFileName"] = "Reporte_Md";

        var item = new FileItemContext(@"C:\Docs\documento.pdf");
        item.AddLog("Descomprimido desde archivo.zip");
        item.AddLog("Guardado en carpeta destino");

        var emittedReport = new List<FileItemContext>();
        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.Setup(c => c.IsDryRun).Returns(false);
        mockContext.Setup(c => c.EmitAsync("Report", It.IsAny<FileItemContext>()))
                   .Callback<string, FileItemContext>((_, r) => emittedReport.Add(r))
                   .Returns(Task.CompletedTask);
        mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
                   .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);
        await node.OnWorkflowCompletedAsync(mockContext.Object, CancellationToken.None);

        // Assert
        emittedReport.Should().HaveCount(1);
        var reportItem = emittedReport[0];
        reportItem.CurrentPath.Should().Be("Reporte_Md.md");

        string mdContent = reportItem.Metadata["ReportContent"]?.ToString() ?? string.Empty;
        mdContent.Should().Contain("# 📊 Reporte Consolidado de Operaciones");
        mdContent.Should().Contain("`documento.pdf`");
        mdContent.Should().Contain("Descomprimido desde archivo.zip");
    }

    [Fact]
    public async Task ExecuteAsync_And_OnWorkflowCompletedAsync_ShouldGenerateTextReportInMemory()
    {
        // Arrange
        var node = new OperationReportNode();
        node.Parameters["ReportFormat"] = "Text";
        node.Parameters["ReportScope"] = "Consolidated";
        node.Parameters["ReportFileName"] = "Reporte_Txt";

        var item = new FileItemContext(@"C:\Logs\archivo.txt");
        item.AddLog("Operación 1 completada");

        var emittedReport = new List<FileItemContext>();
        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.Setup(c => c.IsDryRun).Returns(false);
        mockContext.Setup(c => c.EmitAsync("Report", It.IsAny<FileItemContext>()))
                   .Callback<string, FileItemContext>((_, r) => emittedReport.Add(r))
                   .Returns(Task.CompletedTask);
        mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
                   .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);
        await node.OnWorkflowCompletedAsync(mockContext.Object, CancellationToken.None);

        // Assert
        emittedReport.Should().HaveCount(1);
        var reportItem = emittedReport[0];
        reportItem.CurrentPath.Should().Be("Reporte_Txt.txt");

        string txtContent = reportItem.Metadata["ReportContent"]?.ToString() ?? string.Empty;
        txtContent.Should().Contain("FILEFLOW STUDIO");
        txtContent.Should().Contain("archivo.txt");
        txtContent.Should().Contain("Operación 1 completada");
    }

    [Fact]
    public async Task ExecuteAsync_And_OnWorkflowCompletedAsync_ShouldGenerateJsonAndCsvReportsInMemory()
    {
        // Arrange
        var jsonNode = new OperationReportNode();
        jsonNode.Parameters["ReportFormat"] = "JSON";
        jsonNode.Parameters["ReportFileName"] = "Reporte_Json";

        var csvNode = new OperationReportNode();
        csvNode.Parameters["ReportFormat"] = "CSV";
        csvNode.Parameters["ReportFileName"] = "Reporte_Csv";

        var item = new FileItemContext(@"C:\Data\data.csv");
        item.AddLog("Validación completada");
        item.Metadata["CustomKey"] = "CustomVal";

        var jsonReports = new List<FileItemContext>();
        var csvReports = new List<FileItemContext>();

        var jsonContext = new Mock<IFlowExecutionContext>();
        jsonContext.Setup(c => c.IsDryRun).Returns(false);
        jsonContext.Setup(c => c.EmitAsync("Report", It.IsAny<FileItemContext>()))
                   .Callback<string, FileItemContext>((_, r) => jsonReports.Add(r))
                   .Returns(Task.CompletedTask);
        jsonContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>())).Returns(Task.CompletedTask);

        var csvContext = new Mock<IFlowExecutionContext>();
        csvContext.Setup(c => c.IsDryRun).Returns(false);
        csvContext.Setup(c => c.EmitAsync("Report", It.IsAny<FileItemContext>()))
                  .Callback<string, FileItemContext>((_, r) => csvReports.Add(r))
                  .Returns(Task.CompletedTask);
        csvContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>())).Returns(Task.CompletedTask);

        // Act
        await jsonNode.ExecuteAsync("In", item, jsonContext.Object, CancellationToken.None);
        await jsonNode.OnWorkflowCompletedAsync(jsonContext.Object, CancellationToken.None);

        await csvNode.ExecuteAsync("In", item, csvContext.Object, CancellationToken.None);
        await csvNode.OnWorkflowCompletedAsync(csvContext.Object, CancellationToken.None);

        // Assert JSON
        jsonReports.Should().HaveCount(1);
        string jsonContent = jsonReports[0].Metadata["ReportContent"]?.ToString() ?? string.Empty;
        using var doc = JsonDocument.Parse(jsonContent);
        doc.RootElement.GetProperty("TotalFiles").GetInt32().Should().Be(1);

        // Assert CSV
        csvReports.Should().HaveCount(1);
        string csvContent = csvReports[0].Metadata["ReportContent"]?.ToString() ?? string.Empty;
        csvContent.Should().Contain("Id,FileName,Directory,OriginalPath,FinalPath");
        csvContent.Should().Contain("data.csv");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSupportPerFileAndBothScopes()
    {
        // Arrange
        var node = new OperationReportNode();
        node.Parameters["ReportFormat"] = "HTML";
        node.Parameters["ReportScope"] = "Both";
        node.Parameters["ReportFileName"] = "Reporte_General";

        var item = new FileItemContext(@"C:\Items\item1.bin");
        item.AddLog("Paso 1");

        var emittedReports = new List<FileItemContext>();
        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.Setup(c => c.IsDryRun).Returns(false);
        mockContext.Setup(c => c.EmitAsync("Report", It.IsAny<FileItemContext>()))
                   .Callback<string, FileItemContext>((_, r) => emittedReports.Add(r))
                   .Returns(Task.CompletedTask);
        mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
                   .Returns(Task.CompletedTask);

        // Act 1: Process item (PerFile part emits immediately)
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);
        emittedReports.Should().HaveCount(1);
        emittedReports[0].CurrentPath.Should().Be("item1_Report.html");

        // Act 2: Complete workflow (Consolidated part emits on completion)
        await node.OnWorkflowCompletedAsync(mockContext.Object, CancellationToken.None);
        emittedReports.Should().HaveCount(2);
        emittedReports[1].CurrentPath.Should().Be("Reporte_General.html");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRegisterPlannedAction_WhenDryRun()
    {
        // Arrange
        var node = new OperationReportNode();
        node.Parameters["ReportFormat"] = "HTML";
        node.Parameters["ReportScope"] = "Consolidated";

        var item = new FileItemContext(@"C:\Items\virtual_item.png");
        var plannedActions = new List<PlannedAction>();

        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.Setup(c => c.IsDryRun).Returns(true);
        mockContext.Setup(c => c.RegisterPlannedAction(It.IsAny<PlannedAction>()))
                   .Callback<PlannedAction>(a => plannedActions.Add(a));
        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
                   .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);
        await node.OnWorkflowCompletedAsync(mockContext.Object, CancellationToken.None);

        // Assert
        plannedActions.Should().HaveCount(1);
        plannedActions[0].OperationType.Should().Be(PlannedOperationType.Custom);
        plannedActions[0].Description.Should().Contain("Simulación: Se generaría reporte consolidado en memoria");
    }

    [Fact]
    public async Task DestinationSinkNode_ShouldSaveInMemoryReportFileToDisk()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "OpReportSinkIntegration_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            var reportNode = new OperationReportNode();
            reportNode.Parameters["ReportFormat"] = "HTML";
            reportNode.Parameters["ReportFileName"] = "Reporte_Guardado";

            var sinkNode = new DestinationSinkNode();
            sinkNode.Parameters["DestinationRoot"] = tempDir;

            var item1 = new FileItemContext(@"C:\Origen\archivo1.txt");
            item1.AddLog("Paso A completado");
            var item2 = new FileItemContext(@"C:\Origen\archivo2.txt");
            item2.AddLog("Paso B completado");

            FileItemContext? emittedReportItem = null;
            var reportContextMock = new Mock<IFlowExecutionContext>();
            reportContextMock.Setup(c => c.IsDryRun).Returns(false);
            reportContextMock.Setup(c => c.EmitAsync("Report", It.IsAny<FileItemContext>()))
                             .Callback<string, FileItemContext>((_, r) => emittedReportItem = r)
                             .Returns(Task.CompletedTask);
            reportContextMock.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
                             .Returns(Task.CompletedTask);

            // Act 1: Process items in ReportNode
            await reportNode.ExecuteAsync("In", item1, reportContextMock.Object, CancellationToken.None);
            await reportNode.ExecuteAsync("In", item2, reportContextMock.Object, CancellationToken.None);
            await reportNode.OnWorkflowCompletedAsync(reportContextMock.Object, CancellationToken.None);

            emittedReportItem.Should().NotBeNull();

            // Act 2: Feed in-memory report directly into DestinationSinkNode
            var sinkContextMock = new Mock<IFlowExecutionContext>();
            sinkContextMock.Setup(c => c.IsDryRun).Returns(false);
            sinkContextMock.Setup(c => c.EmitAsync("Done", It.IsAny<FileItemContext>())).Returns(Task.CompletedTask);

            await sinkNode.ExecuteAsync("In", emittedReportItem!, sinkContextMock.Object, CancellationToken.None);

            // Assert: DestinationSinkNode successfully created the physical HTML file on disk from memory!
            string expectedDiskFile = Path.Combine(tempDir, "Reporte_Guardado.html");
            File.Exists(expectedDiskFile).Should().BeTrue();

            string diskContent = await File.ReadAllTextAsync(expectedDiskFile);
            diskContent.Should().Contain("Reporte Consolidado de Operaciones");
            diskContent.Should().Contain("archivo1.txt");
            diskContent.Should().Contain("archivo2.txt");
            diskContent.Should().Contain("Paso A completado");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHandleConcurrentExecutionWithoutErrors()
    {
        // Arrange
        var node = new OperationReportNode();
        node.Parameters["ReportFormat"] = "HTML";
        node.Parameters["ReportScope"] = "Consolidated";
        node.Parameters["ReportFileName"] = "Reporte_Concurrente";

        var emittedReports = new List<FileItemContext>();
        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.Setup(c => c.IsDryRun).Returns(false);
        mockContext.Setup(c => c.EmitAsync("Report", It.IsAny<FileItemContext>()))
                   .Callback<string, FileItemContext>((_, r) => emittedReports.Add(r))
                   .Returns(Task.CompletedTask);
        mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
                   .Returns(Task.CompletedTask);

        string execId = Guid.NewGuid().ToString();
        const int fileCount = 20;

        // Act - Execute 20 concurrent parallel tasks on the SAME node instance
        var tasks = new List<Task>();
        for (int i = 0; i < fileCount; i++)
        {
            int idx = i;
            var item = new FileItemContext($@"C:\Parallel\archivo_{idx}.txt");
            item.Metadata["WorkflowExecutionId"] = execId;
            item.AddLog($"Paso concurrente {idx}");

            tasks.Add(Task.Run(async () =>
            {
                await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);
            }));
        }

        await Task.WhenAll(tasks);
        await node.OnWorkflowCompletedAsync(mockContext.Object, CancellationToken.None);

        // Assert - Consolidated report in memory with 20 items
        emittedReports.Should().HaveCount(1);
        var report = emittedReports[0];
        string html = report.Metadata["ReportContent"]?.ToString() ?? string.Empty;
        html.Should().Contain("Reporte Consolidado de Operaciones");
        html.Should().Contain($">{fileCount}<");
    }
}
