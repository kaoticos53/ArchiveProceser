using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Plugin.AI;
using FileFlow.Sdk;
using FluentAssertions;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.AI;

public class LocalLlmProcessorNodeTests : IDisposable
{
    private readonly string _tempDir;

    public LocalLlmProcessorNodeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FileFlow_LlmTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task ExecuteAsync_SummarizeTask_ShouldInjectSummaryAndResponseMetadata()
    {
        // Arrange
        string docPath = Path.Combine(_tempDir, "informe.md");
        await File.WriteAllTextAsync(docPath, "Este es un informe sobre el rendimiento del sistema. La velocidad de procesamiento aumentó un 40%. Todos los componentes operan dentro de los límites esperados.");

        var item = new FileItemContext(docPath);
        var mockContext = new Mock<IFlowExecutionContext>();

        var node = new LocalLlmProcessorNode();
        node.Parameters["TaskType"] = "Summarize";
        node.Parameters["OutputFormat"] = "Markdown";

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("Processed", item), Times.Once);
        item.Metadata.Should().ContainKey("AI:LlmResponse");
        item.Metadata.Should().ContainKey("AI:Summary");
        item.Metadata.Should().ContainKey("AI:TokensGenerated");

        string response = item.Metadata["AI:LlmResponse"]?.ToString() ?? string.Empty;
        response.Should().Contain("Resumen");
    }

    [Fact]
    public async Task ExecuteAsync_ExtractStructuredData_ShouldGenerateValidJsonWithEntities()
    {
        // Arrange
        string text = "Factura emitida por contacto@empresa.com el 2026-05-15 por un importe total de 1250 € para el cliente.";
        string docPath = Path.Combine(_tempDir, "factura.txt");
        await File.WriteAllTextAsync(docPath, text);

        var item = new FileItemContext(docPath);
        var mockContext = new Mock<IFlowExecutionContext>();

        var node = new LocalLlmProcessorNode();
        node.Parameters["TaskType"] = "ExtractStructuredData";
        node.Parameters["OutputFormat"] = "JSON";

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("Processed", item), Times.Once);
        item.Metadata.Should().ContainKey("AI:ExtractedDataJson");

        string json = item.Metadata["AI:ExtractedDataJson"]?.ToString() ?? string.Empty;
        using var jsonDoc = JsonDocument.Parse(json);
        jsonDoc.RootElement.TryGetProperty("emails", out var emails).Should().BeTrue();
        emails.GetArrayLength().Should().BeGreaterThan(0);
        emails[0].GetString().Should().Be("contacto@empresa.com");
    }

    [Fact]
    public async Task ExecuteAsync_WithTemplateVariables_ShouldResolveMetadataInPrompt()
    {
        // Arrange
        var item = new FileItemContext(Path.Combine(_tempDir, "doc.txt"))
        {
            Metadata = { ["Ocr:Text"] = "Reunión de balance general y objetivos del trimestre." }
        };
        var mockContext = new Mock<IFlowExecutionContext>();

        var node = new LocalLlmProcessorNode();
        node.Parameters["TaskType"] = "Summarize";
        node.Parameters["UserPrompt"] = "Analiza el contenido OCR: {Ocr:Text}";

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("Processed", item), Times.Once);
        item.Metadata.Should().ContainKey("AI:LlmResponse");
    }

    [Fact]
    public async Task ExecuteAsync_WhenSaveAsNewFile_ShouldCreateOutputFileOnDisk()
    {
        // Arrange
        string docPath = Path.Combine(_tempDir, "documento.txt");
        await File.WriteAllTextAsync(docPath, "Contenido a resumir y guardar como informe.");

        var item = new FileItemContext(docPath);
        var mockContext = new Mock<IFlowExecutionContext>();

        var node = new LocalLlmProcessorNode();
        node.Parameters["TaskType"] = "Summarize";
        node.Parameters["OutputFormat"] = "Markdown";
        node.Parameters["SaveAsNewFile"] = true;

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("Processed", item), Times.Once);
        string expectedSaved = Path.Combine(_tempDir, "documento_analisis.md");
        File.Exists(expectedSaved).Should().BeTrue();
    }
}
