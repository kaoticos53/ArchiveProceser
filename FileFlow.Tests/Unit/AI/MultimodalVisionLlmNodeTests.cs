using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Plugin.AI;
using FileFlow.Sdk;
using FluentAssertions;
using Moq;
using Moq.Protected;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace FileFlow.Tests.Unit.AI;

public class MultimodalVisionLlmNodeTests : IDisposable
{
    private readonly string _tempDir;

    public MultimodalVisionLlmNodeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FileFlow_VlmTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    private static void FillImage(Image<Rgba32> img, Color color)
    {
        var px = color.ToPixel<Rgba32>();
        img.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                accessor.GetRowSpan(y).Fill(px);
            }
        });
    }

    [Fact]
    public void MultimodalVisionLlmNode_ShouldHaveExpectedPortsAndParameters()
    {
        // Arrange & Act
        var node = new MultimodalVisionLlmNode();

        // Assert ports
        node.Inputs.Should().ContainSingle(p => p.Name == "In");
        node.Outputs.Select(p => p.Name).Should().Contain(["Out", "Structured", "Error"]);

        // Assert parameters
        node.Parameters.Should().ContainKey("Provider");
        node.Parameters.Should().ContainKey("EndpointUrl");
        node.Parameters.Should().ContainKey("ModelName");
        node.Parameters.Should().ContainKey("TaskPreset");
        node.Parameters.Should().ContainKey("MaxImageDimension");
        node.Parameters.Should().ContainKey("Temperature");
        node.Parameters.Should().ContainKey("MaxTokens");
        node.Parameters.Should().ContainKey("SaveAsNewFile");
        node.Parameters.Should().ContainKey("TimeoutSeconds");
    }

    [Fact]
    public void VlmEngine_PrepareImageAsBase64Jpeg_ShouldScaleDownOversizedImage()
    {
        // Arrange: 2000 x 1000 image, maxDimension 1000
        using var largeImg = new Image<Rgb24>(2000, 1000);

        // Act
        string base64Uri = MultimodalVlmClientEngine.PrepareImageAsBase64Jpeg(largeImg, maxDimension: 1000);

        // Assert
        base64Uri.Should().StartWith("data:image/jpeg;base64,");
        string base64Data = base64Uri.Substring("data:image/jpeg;base64,".Length);
        byte[] bytes = Convert.FromBase64String(base64Data);

        using var decoded = Image.Load<Rgb24>(bytes);
        decoded.Width.Should().Be(1000);
        decoded.Height.Should().Be(500);
    }

    [Fact]
    public void VlmEngine_TryExtractValidJson_ShouldCleanMarkdownCodeBlocks()
    {
        // Arrange
        string rawMarkdown = "Aquí tienes los datos extraídos:\n```json\n{\n  \"factura_numero\": \"F-2024-001\",\n  \"total\": 199.99\n}\n```\nEspero que te sirva.";

        // Act
        string? json = MultimodalVlmClientEngine.TryExtractValidJson(rawMarkdown);

        // Assert
        json.Should().NotBeNull();
        json.Should().Contain("F-2024-001");
        json.Should().NotContain("```");
    }

    [Fact]
    public void VlmEngine_GetPresetPrompts_ShouldReturnNonEmptyPromptsForEveryPreset()
    {
        foreach (VlmTaskPreset preset in Enum.GetValues<VlmTaskPreset>())
        {
            var (sys, user) = MultimodalVlmClientEngine.GetPresetPrompts(preset, "Español");
            sys.Should().NotBeNullOrWhiteSpace($"Preset {preset} must have a system prompt");
            user.Should().NotBeNullOrWhiteSpace($"Preset {preset} must have a user prompt");
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingFile_ShouldEmitError()
    {
        // Arrange
        var node = new MultimodalVisionLlmNode();
        var item = new FileItemContext(Path.Combine(_tempDir, "missing_image.jpg"));
        var mockContext = new Mock<IFlowExecutionContext>();

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("Error", item), Times.Once);
        mockContext.Verify(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithSuccessfulVlmResponse_ShouldEnrichMetadataAndEmitOutAndStructured()
    {
        // Arrange: Generate sample invoice image
        string imgPath = Path.Combine(_tempDir, "sample_invoice.png");
        using (var img = new Image<Rgba32>(300, 300))
        {
            FillImage(img, Color.White);
            await img.SaveAsPngAsync(imgPath);
        }

        string mockResponseBody = """
        {
          "id": "chatcmpl-123",
          "choices": [
            {
              "message": {
                "role": "assistant",
                "content": "```json\n{\n  \"emisor\": \"Acme Corp\",\n  \"importe\": 350.50\n}\n```"
              },
              "finish_reason": "stop"
            }
          ],
          "usage": {
            "prompt_tokens": 100,
            "completion_tokens": 40,
            "total_tokens": 140
          }
        }
        """;

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(mockResponseBody, Encoding.UTF8, "application/json")
            });

        var customClient = new HttpClient(mockHandler.Object);

        var node = new MultimodalVisionLlmNode
        {
            CustomHttpClient = customClient
        };
        node.Parameters["TaskPreset"] = "ExtractInvoiceReceiptJson";

        var item = new FileItemContext(imgPath);
        var mockContext = new Mock<IFlowExecutionContext>();

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("Out", item), Times.Once);
        mockContext.Verify(c => c.EmitAsync("Structured", item), Times.Once);
        mockContext.Verify(c => c.EmitAsync("Error", It.IsAny<FileItemContext>()), Times.Never);

        item.Metadata.Should().ContainKey("AI:VlmResponse");
        item.Metadata.Should().ContainKey("AI:VlmJson");
        item.Metadata["AI:VlmJson"]?.ToString().Should().Contain("Acme Corp");
        Convert.ToInt32(item.Metadata["AI:VlmTokens"]).Should().Be(140);
    }

    [Fact]
    public async Task ExecuteAsync_WhenServerThrowsHttpError_ShouldEmitError()
    {
        // Arrange
        string imgPath = Path.Combine(_tempDir, "sample.png");
        using (var img = new Image<Rgba32>(200, 200))
        {
            FillImage(img, Color.Gray);
            await img.SaveAsPngAsync(imgPath);
        }

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused (LM Studio not running)"));

        var node = new MultimodalVisionLlmNode
        {
            CustomHttpClient = new HttpClient(mockHandler.Object)
        };

        var item = new FileItemContext(imgPath);
        var mockContext = new Mock<IFlowExecutionContext>();

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("Error", item), Times.Once);
        mockContext.Verify(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSaveAsNewFileIsTrue_ShouldWriteResultFile()
    {
        // Arrange
        string imgPath = Path.Combine(_tempDir, "receipt.png");
        using (var img = new Image<Rgba32>(200, 200))
        {
            FillImage(img, Color.White);
            await img.SaveAsPngAsync(imgPath);
        }

        string mockResponseBody = """
        {
          "choices": [
            {
              "message": {
                "role": "assistant",
                "content": "{\"ticket_id\": \"T-999\", \"total\": 12.50}"
              }
            }
          ]
        }
        """;

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(mockResponseBody, Encoding.UTF8, "application/json")
            });

        var node = new MultimodalVisionLlmNode
        {
            CustomHttpClient = new HttpClient(mockHandler.Object)
        };
        node.Parameters["SaveAsNewFile"] = true;

        var item = new FileItemContext(imgPath);
        var mockContext = new Mock<IFlowExecutionContext>();

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert: should have created receipt_vlm.json
        string expectedOutputFile = Path.Combine(_tempDir, "receipt_vlm.json");
        File.Exists(expectedOutputFile).Should().BeTrue();
        string content = await File.ReadAllTextAsync(expectedOutputFile);
        content.Should().Contain("T-999");
    }

    [Theory]
    [InlineData("Internal Engine (In-Process)", typeof(InProcessVlmAdapter))]
    [InlineData("LM Studio (localhost:1234)", typeof(OpenAiCompatibleVlmAdapter))]
    [InlineData("Ollama (localhost:11434)", typeof(OpenAiCompatibleVlmAdapter))]
    [InlineData("Custom Endpoint", typeof(OpenAiCompatibleVlmAdapter))]
    [InlineData(null, typeof(OpenAiCompatibleVlmAdapter))]
    public void VlmAdapterFactory_ShouldResolveCorrectAdapterType(string? provider, Type expectedType)
    {
        var adapter = VlmAdapterFactory.Create(provider);
        adapter.Should().BeOfType(expectedType);
    }

    [Fact]
    public async Task ExecuteAsync_WithInProcessProvider_ExtractInvoiceReceiptJson_ShouldProduceStructuredJsonWithoutNetwork()
    {
        // Arrange
        string imgPath = Path.Combine(_tempDir, "factura_test.png");
        using (var img = new Image<Rgba32>(300, 300))
        {
            FillImage(img, Color.White);
            await img.SaveAsPngAsync(imgPath);
        }

        var node = new MultimodalVisionLlmNode();
        node.Parameters["Provider"] = VlmAdapterFactory.ProviderInProcess;
        node.Parameters["TaskPreset"] = "ExtractInvoiceReceiptJson";

        var item = new FileItemContext(imgPath);
        item.Metadata["Ocr:Text"] = "FACTURA FAC-2024-999 Fecha: 2024-06-15 Total: 720.50 EUR";

        var mockContext = new Mock<IFlowExecutionContext>();

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("Out", item), Times.Once);
        mockContext.Verify(c => c.EmitAsync("Structured", item), Times.Once);
        mockContext.Verify(c => c.EmitAsync("Error", It.IsAny<FileItemContext>()), Times.Never);

        item.Metadata.Should().ContainKey("AI:VlmJson");
        item.Metadata.Should().ContainKey("AI:VlmCategory");
        item.Metadata.Should().ContainKey("AI:VlmProvider");

        string json = item.Metadata["AI:VlmJson"]?.ToString() ?? string.Empty;
        json.Should().Contain("FAC-2024-999");
        json.Should().Contain("720.50");
        json.Should().Contain("FileFlow In-Process");
        item.Metadata["AI:VlmCategory"]?.ToString().Should().Be("Factura_Recibo");
        item.Metadata["AI:VlmProvider"]?.ToString().Should().Be("Internal Engine (In-Process)");
    }

    [Fact]
    public async Task ExecuteAsync_WithInProcessProvider_ClassifyAndTag_ShouldProduceCategoryAndTags()
    {
        // Arrange
        string imgPath = Path.Combine(_tempDir, "document_sample.png");
        using (var img = new Image<Rgba32>(200, 300))
        {
            FillImage(img, Color.White);
            await img.SaveAsPngAsync(imgPath);
        }

        var node = new MultimodalVisionLlmNode();
        node.Parameters["Provider"] = VlmAdapterFactory.ProviderInProcess;
        node.Parameters["TaskPreset"] = "ClassifyAndTag";

        var item = new FileItemContext(imgPath);
        var mockContext = new Mock<IFlowExecutionContext>();

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("Out", item), Times.Once);
        mockContext.Verify(c => c.EmitAsync("Structured", item), Times.Once);

        string json = item.Metadata["AI:VlmJson"]?.ToString() ?? string.Empty;
        json.Should().Contain("categoria");
        json.Should().Contain("etiquetas_descriptivas");
        json.Should().Contain("confianza_aproximada");
    }

    [Fact]
    public async Task ExecuteAsync_WithInProcessProvider_DocumentOcrAndSummary_ShouldProduceSummary()
    {
        // Arrange
        string imgPath = Path.Combine(_tempDir, "contract_doc.png");
        using (var img = new Image<Rgba32>(200, 200))
        {
            FillImage(img, Color.White);
            await img.SaveAsPngAsync(imgPath);
        }

        var node = new MultimodalVisionLlmNode();
        node.Parameters["Provider"] = VlmAdapterFactory.ProviderInProcess;
        node.Parameters["TaskPreset"] = "DocumentOcrAndSummary";

        var item = new FileItemContext(imgPath);
        item.Metadata["Ocr:Text"] = "Este contrato regula la confidencialidad de la información corporativa y técnica transferida entre ambas partes.";

        var mockContext = new Mock<IFlowExecutionContext>();

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("Out", item), Times.Once);
        item.Metadata.Should().ContainKey("AI:VlmResponse");
        string response = item.Metadata["AI:VlmResponse"]?.ToString() ?? string.Empty;
        response.Should().NotBeNullOrWhiteSpace();
        response.Should().Contain("Resumen");
    }

    [Fact]
    public async Task MultimodalVisionLlmNode_ModelLifecycle_ShouldReflectLoadedStateAndIdentifier()
    {
        // Arrange
        var node = new MultimodalVisionLlmNode();

        // Default external provider
        node.IsModelLoaded.Should().BeTrue();
        node.ModelIdentifier.Should().Contain("qwen2.5-vl-7b-instruct");

        // Switch to In-Process
        node.Parameters["Provider"] = VlmAdapterFactory.ProviderInProcess;
        node.IsModelLoaded.Should().BeTrue();
        node.ModelIdentifier.Should().Be("FileFlow In-Process VLM");

        bool eventTriggered = false;
        node.ModelStatusChanged += () => eventTriggered = true;

        await node.PreloadModelAsync();
        eventTriggered.Should().BeTrue();

        eventTriggered = false;
        node.UnloadModel();
        eventTriggered.Should().BeTrue();
    }
}

