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
    public async Task ExecuteAsync_ShouldGenerateHtmlReport_WhenFormatIsHtml()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "OpReportHtml_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        string sourceFile = Path.Combine(tempDir, "foto_vacaciones.jpg");
        await File.WriteAllTextAsync(sourceFile, "dummy image content");

        try
        {
            var node = new OperationReportNode();
            node.Parameters["ReportFormat"] = "HTML";
            node.Parameters["ReportScope"] = "Consolidated";
            node.Parameters["DestinationFolder"] = tempDir;
            node.Parameters["ReportFileName"] = "Reporte_Test";
            node.Parameters["Theme"] = "ModernDark";

            var item = new FileItemContext(sourceFile);
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

            // Act
            await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

            // Assert
            emittedOut.Should().HaveCount(1);
            emittedReport.Should().HaveCount(1);

            string expectedReportPath = Path.Combine(tempDir, "Reporte_Test.html");
            File.Exists(expectedReportPath).Should().BeTrue();

            string htmlContent = await File.ReadAllTextAsync(expectedReportPath);
            htmlContent.Should().Contain("Reporte Consolidado de Operaciones");
            htmlContent.Should().Contain("foto_vacaciones.jpg");
            htmlContent.Should().Contain("Hash SHA-256 calculado");
            htmlContent.Should().Contain("Exif:DateTaken");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldGenerateMarkdownReport_WhenFormatIsMarkdown()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "OpReportMd_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        string sourceFile = Path.Combine(tempDir, "documento.pdf");
        await File.WriteAllTextAsync(sourceFile, "dummy pdf");

        try
        {
            var node = new OperationReportNode();
            node.Parameters["ReportFormat"] = "Markdown";
            node.Parameters["ReportScope"] = "Consolidated";
            node.Parameters["DestinationFolder"] = tempDir;
            node.Parameters["ReportFileName"] = "Reporte_Md";

            var item = new FileItemContext(sourceFile);
            item.AddLog("Descomprimido desde archivo.zip");
            item.AddLog("Guardado en carpeta destino");

            var mockContext = new Mock<IFlowExecutionContext>();
            mockContext.Setup(c => c.IsDryRun).Returns(false);
            mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
                       .Returns(Task.CompletedTask);

            // Act
            await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

            // Assert
            string expectedReportPath = Path.Combine(tempDir, "Reporte_Md.md");
            File.Exists(expectedReportPath).Should().BeTrue();

            string mdContent = await File.ReadAllTextAsync(expectedReportPath);
            mdContent.Should().Contain("# 📊 Reporte Consolidado de Operaciones");
            mdContent.Should().Contain("`documento.pdf`");
            mdContent.Should().Contain("Descomprimido desde archivo.zip");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldGenerateTextReport_WhenFormatIsText()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "OpReportTxt_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        string sourceFile = Path.Combine(tempDir, "archivo.txt");
        await File.WriteAllTextAsync(sourceFile, "test text");

        try
        {
            var node = new OperationReportNode();
            node.Parameters["ReportFormat"] = "Text";
            node.Parameters["ReportScope"] = "Consolidated";
            node.Parameters["DestinationFolder"] = tempDir;
            node.Parameters["ReportFileName"] = "Reporte_Txt";

            var item = new FileItemContext(sourceFile);
            item.AddLog("Operación 1 completada");

            var mockContext = new Mock<IFlowExecutionContext>();
            mockContext.Setup(c => c.IsDryRun).Returns(false);
            mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
                       .Returns(Task.CompletedTask);

            // Act
            await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

            // Assert
            string expectedReportPath = Path.Combine(tempDir, "Reporte_Txt.txt");
            File.Exists(expectedReportPath).Should().BeTrue();

            string txtContent = await File.ReadAllTextAsync(expectedReportPath);
            txtContent.Should().Contain("FILEFLOW STUDIO");
            txtContent.Should().Contain("archivo.txt");
            txtContent.Should().Contain("Operación 1 completada");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldGenerateJsonAndCsvReports()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "OpReportJsonCsv_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        string sourceFile = Path.Combine(tempDir, "data.csv");
        await File.WriteAllTextAsync(sourceFile, "col1,col2");

        try
        {
            // JSON Test
            var jsonNode = new OperationReportNode();
            jsonNode.Parameters["ReportFormat"] = "JSON";
            jsonNode.Parameters["DestinationFolder"] = tempDir;
            jsonNode.Parameters["ReportFileName"] = "Reporte_Json";

            var item = new FileItemContext(sourceFile);
            item.AddLog("Validación completada");
            item.Metadata["CustomKey"] = "CustomVal";

            var mockContext = new Mock<IFlowExecutionContext>();
            mockContext.Setup(c => c.IsDryRun).Returns(false);
            mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>())).Returns(Task.CompletedTask);

            await jsonNode.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

            string jsonPath = Path.Combine(tempDir, "Reporte_Json.json");
            File.Exists(jsonPath).Should().BeTrue();
            string jsonContent = await File.ReadAllTextAsync(jsonPath);
            using var doc = JsonDocument.Parse(jsonContent);
            doc.RootElement.GetProperty("TotalFiles").GetInt32().Should().Be(1);

            // CSV Test
            var csvNode = new OperationReportNode();
            csvNode.Parameters["ReportFormat"] = "CSV";
            csvNode.Parameters["DestinationFolder"] = tempDir;
            csvNode.Parameters["ReportFileName"] = "Reporte_Csv";

            await csvNode.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

            string csvPath = Path.Combine(tempDir, "Reporte_Csv.csv");
            File.Exists(csvPath).Should().BeTrue();
            string csvContent = await File.ReadAllTextAsync(csvPath);
            csvContent.Should().Contain("Id,FileName,Directory,OriginalPath,FinalPath");
            csvContent.Should().Contain("data.csv");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSupportPerFileAndBothScopes()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "OpReportBoth_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        string sourceFile = Path.Combine(tempDir, "item1.bin");
        await File.WriteAllTextAsync(sourceFile, "binary item");

        try
        {
            var node = new OperationReportNode();
            node.Parameters["ReportFormat"] = "HTML";
            node.Parameters["ReportScope"] = "Both";
            node.Parameters["DestinationFolder"] = tempDir;
            node.Parameters["ReportFileName"] = "Reporte_General";

            var item = new FileItemContext(sourceFile);
            item.AddLog("Paso 1");

            var emittedReports = new List<FileItemContext>();
            var mockContext = new Mock<IFlowExecutionContext>();
            mockContext.Setup(c => c.IsDryRun).Returns(false);
            mockContext.Setup(c => c.EmitAsync("Report", It.IsAny<FileItemContext>()))
                       .Callback<string, FileItemContext>((_, r) => emittedReports.Add(r))
                       .Returns(Task.CompletedTask);
            mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
                       .Returns(Task.CompletedTask);

            // Act
            await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

            // Assert
            emittedReports.Should().HaveCount(2); // 1 individual + 1 consolidated

            string perFilePath = Path.Combine(tempDir, "item1_Report.html");
            string consolidatedPath = Path.Combine(tempDir, "Reporte_General.html");

            File.Exists(perFilePath).Should().BeTrue();
            File.Exists(consolidatedPath).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRegisterPlannedAction_WhenDryRun()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "OpReportDry_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        string sourceFile = Path.Combine(tempDir, "virtual_item.png");

        try
        {
            var node = new OperationReportNode();
            node.Parameters["ReportFormat"] = "HTML";
            node.Parameters["ReportScope"] = "Consolidated";
            node.Parameters["DestinationFolder"] = tempDir;

            var item = new FileItemContext(sourceFile);
            var plannedActions = new List<PlannedAction>();

            var mockContext = new Mock<IFlowExecutionContext>();
            mockContext.Setup(c => c.IsDryRun).Returns(true);
            mockContext.Setup(c => c.RegisterPlannedAction(It.IsAny<PlannedAction>()))
                       .Callback<PlannedAction>(a => plannedActions.Add(a));
            mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
                       .Returns(Task.CompletedTask);

            // Act
            await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

            // Assert
            plannedActions.Should().HaveCount(1);
            plannedActions[0].OperationType.Should().Be(PlannedOperationType.Custom);
            plannedActions[0].Description.Should().Contain("Simulación: Se generaría/actualizaría reporte consolidado");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldProduceOnlyOneConsolidatedReportFile_WhenMultipleFilesProcessed()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "OpReportSingleConsolidated_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            var node = new OperationReportNode();
            node.Parameters["ReportFormat"] = "HTML";
            node.Parameters["ReportScope"] = "Consolidated";
            node.Parameters["DestinationFolder"] = tempDir;
            node.Parameters["ReportFileName"] = "Reporte_Ejecucion_{Date:yyyyMMdd_HHmmss}";

            var mockContext = new Mock<IFlowExecutionContext>();
            mockContext.Setup(c => c.IsDryRun).Returns(false);
            mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
                       .Returns(Task.CompletedTask);

            // Act - Process 5 files in the same batch/execution
            string execId = Guid.NewGuid().ToString();
            for (int i = 1; i <= 5; i++)
            {
                string filePath = Path.Combine(tempDir, $"archivo_{i}.txt");
                await File.WriteAllTextAsync(filePath, $"contenido {i}");

                var item = new FileItemContext(filePath);
                item.Metadata["WorkflowExecutionId"] = execId;
                item.AddLog($"Paso {i} completado");

                await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);
            }

            // Assert - There must be EXACTLY ONE report file generated
            var reportFiles = Directory.GetFiles(tempDir, "Reporte_Ejecucion_*.html");
            reportFiles.Should().HaveCount(1, "Only one single consolidated report file should exist for the whole batch");

            string reportContent = await File.ReadAllTextAsync(reportFiles[0]);
            for (int i = 1; i <= 5; i++)
            {
                reportContent.Should().Contain($"archivo_{i}.txt");
                reportContent.Should().Contain($"Paso {i} completado");
            }
            reportContent.Should().Contain("Total Archivos");
            reportContent.Should().Contain(">5<");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldGroupOperationsByDirectory_WhenGroupByIsDirectory()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "OpReportDirGroup_" + Guid.NewGuid());
        string dirA = Path.Combine(tempDir, "Fotos_2026");
        string dirB = Path.Combine(tempDir, "Documentos_PDF");
        Directory.CreateDirectory(dirA);
        Directory.CreateDirectory(dirB);

        string fileA1 = Path.Combine(dirA, "foto1.jpg");
        string fileA2 = Path.Combine(dirA, "foto2.jpg");
        string fileB1 = Path.Combine(dirB, "manual.pdf");

        await File.WriteAllTextAsync(fileA1, "img 1");
        await File.WriteAllTextAsync(fileA2, "img 2");
        await File.WriteAllTextAsync(fileB1, "pdf 1");

        try
        {
            var node = new OperationReportNode();
            node.Parameters["ReportFormat"] = "HTML";
            node.Parameters["ReportScope"] = "Consolidated";
            node.Parameters["GroupBy"] = "Directory";
            node.Parameters["DestinationFolder"] = tempDir;
            node.Parameters["ReportFileName"] = "Reporte_Directorios";

            var mockContext = new Mock<IFlowExecutionContext>();
            mockContext.Setup(c => c.IsDryRun).Returns(false);
            mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
                       .Returns(Task.CompletedTask);

            string execId = Guid.NewGuid().ToString();

            var itemA1 = new FileItemContext(fileA1) { OriginalPath = fileA1 };
            itemA1.Metadata["WorkflowExecutionId"] = execId;
            itemA1.AddLog("Optimizado a WebP");
            await node.ExecuteAsync("In", itemA1, mockContext.Object, CancellationToken.None);

            var itemA2 = new FileItemContext(fileA2) { OriginalPath = fileA2 };
            itemA2.Metadata["WorkflowExecutionId"] = execId;
            itemA2.AddLog("Metadatos EXIF extraídos");
            await node.ExecuteAsync("In", itemA2, mockContext.Object, CancellationToken.None);

            var itemB1 = new FileItemContext(fileB1) { OriginalPath = fileB1 };
            itemB1.Metadata["WorkflowExecutionId"] = execId;
            itemB1.AddLog("Indexado y analizado");
            await node.ExecuteAsync("In", itemB1, mockContext.Object, CancellationToken.None);

            // Assert
            string expectedHtml = Path.Combine(tempDir, "Reporte_Directorios.html");
            File.Exists(expectedHtml).Should().BeTrue();

            string html = await File.ReadAllTextAsync(expectedHtml);
            html.Should().Contain("Fotos_2026");
            html.Should().Contain("Documentos_PDF");
            html.Should().Contain("2 archivos");
            html.Should().Contain("1 archivos");
            html.Should().Contain("toggleFolder(this)");
            html.Should().Contain("toggleAllFolders(true)");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
