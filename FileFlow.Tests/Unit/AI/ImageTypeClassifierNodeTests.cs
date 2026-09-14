using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Plugin.AI;
using FileFlow.Sdk;
using FluentAssertions;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace FileFlow.Tests.Unit.AI;

public class ImageTypeClassifierNodeTests : IDisposable
{
    private readonly string _tempDir;

    public ImageTypeClassifierNodeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FileFlow_ImageTypeTests_" + Guid.NewGuid().ToString("N"));
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
    public void ImageTypeClassifierNode_ShouldHaveExpectedPortsAndParameters()
    {
        // Arrange & Act
        var node = new ImageTypeClassifierNode();

        // Assert ports
        node.Inputs.Should().ContainSingle(p => p.Name == "In");
        node.Outputs.Select(p => p.Name).Should().Contain([
            "Document",
            "Receipt",
            "Portrait",
            "GroupPhoto",
            "Photo",
            "Screenshot",
            "Illustration",
            "IDCard",
            "Other",
            "Out",
            "Error"
        ]);

        // Assert parameters
        node.Parameters.Should().ContainKey("ConfidenceThreshold");
        node.Parameters.Should().ContainKey("EnableFaceDetection");
        node.Parameters["EnableFaceDetection"].Should().Be(true);
        node.Parameters.Should().ContainKey("CheckExifMetadata");
        node.Parameters["CheckExifMetadata"].Should().Be(true);
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingFile_ShouldEmitError()
    {
        // Arrange
        var node = new ImageTypeClassifierNode();
        var item = new FileItemContext(Path.Combine(_tempDir, "non_existent.jpg"));
        var mockContext = new Mock<IFlowExecutionContext>();

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("Error", item), Times.Once);
        mockContext.Verify(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithCorruptFile_ShouldEmitError()
    {
        // Arrange
        string corruptPath = Path.Combine(_tempDir, "corrupted.png");
        await File.WriteAllTextAsync(corruptPath, "Not a real PNG header");

        var node = new ImageTypeClassifierNode();
        var item = new FileItemContext(corruptPath);
        var mockContext = new Mock<IFlowExecutionContext>();

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("Error", item), Times.Once);
        mockContext.Verify(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithSyntheticDocument_ShouldClassifyAsDocument()
    {
        // Arrange: Generate an A4-like image (400x560) with white background and dark text-like horizontal stripes
        string docPath = Path.Combine(_tempDir, "sample_doc.png");
        using (var img = new Image<Rgba32>(400, 560))
        {
            FillImage(img, Color.White);

            // Draw horizontal dark stripes resembling lines of text
            for (int y = 50; y < 500; y += 20)
            {
                for (int x = 40; x < 360; x++)
                {
                    img[x, y] = Color.Black;
                    img[x, y + 1] = Color.Black;
                }
            }
            await img.SaveAsPngAsync(docPath);
        }

        var node = new ImageTypeClassifierNode();
        node.Parameters["EnableFaceDetection"] = false; // Pure structural test
        var item = new FileItemContext(docPath);
        var mockContext = new Mock<IFlowExecutionContext>();

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("Document", item), Times.Once);
        mockContext.Verify(c => c.EmitAsync("Out", item), Times.Once);
        item.Metadata["AI:ImageType"].Should().Be("Document");
        Convert.ToDouble(item.Metadata["AI:ImageTypeConfidence"]).Should().BeGreaterThanOrEqualTo(0.50);
    }

    [Fact]
    public async Task ExecuteAsync_WithSyntheticReceipt_ShouldClassifyAsReceipt()
    {
        // Arrange: Generate a tall ticket (aspect ratio 200x500 = 2.5 > 1.8) with white background and text lines
        string receiptPath = Path.Combine(_tempDir, "sample_receipt.png");
        using (var img = new Image<Rgba32>(200, 500))
        {
            FillImage(img, Color.WhiteSmoke);

            for (int y = 30; y < 470; y += 15)
            {
                for (int x = 20; x < 180; x++)
                {
                    img[x, y] = Color.Black;
                }
            }
            await img.SaveAsPngAsync(receiptPath);
        }

        var node = new ImageTypeClassifierNode();
        node.Parameters["EnableFaceDetection"] = false;
        var item = new FileItemContext(receiptPath);
        var mockContext = new Mock<IFlowExecutionContext>();

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("Receipt", item), Times.Once);
        mockContext.Verify(c => c.EmitAsync("Out", item), Times.Once);
        item.Metadata["AI:ImageType"].Should().Be("Receipt");
    }

    [Fact]
    public async Task ExecuteAsync_WithSyntheticScreenshot_ShouldClassifyAsScreenshot()
    {
        // Arrange: Generate 16:9 ratio image (640x360) with solid flat color blocks (UI header, sidebar, canvas)
        string screenshotPath = Path.Combine(_tempDir, "sample_screenshot.png");
        using (var img = new Image<Rgba32>(640, 360))
        {
            // Dark theme background
            FillImage(img, Color.FromRgb(30, 30, 30));

            // Top navbar block
            for (int y = 0; y < 50; y++)
            {
                for (int x = 0; x < 640; x++)
                {
                    img[x, y] = Color.FromRgb(45, 45, 48);
                }
            }

            // Left sidebar block
            for (int y = 50; y < 360; y++)
            {
                for (int x = 0; x < 150; x++)
                {
                    img[x, y] = Color.FromRgb(37, 37, 38);
                }
            }

            await img.SaveAsPngAsync(screenshotPath);
        }

        var node = new ImageTypeClassifierNode();
        node.Parameters["EnableFaceDetection"] = false;
        node.Parameters["ConfidenceThreshold"] = 0.45;
        var item = new FileItemContext(screenshotPath);
        var mockContext = new Mock<IFlowExecutionContext>();

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("Screenshot", item), Times.Once);
        mockContext.Verify(c => c.EmitAsync("Out", item), Times.Once);
        item.Metadata["AI:ImageType"].Should().Be("Screenshot");
    }

    [Fact]
    public async Task ExecuteAsync_WhenConfidenceBelowThreshold_ShouldEmitOther()
    {
        // Arrange: Set confidence threshold unrealistically high (0.99)
        string docPath = Path.Combine(_tempDir, "strict_threshold.png");
        using (var img = new Image<Rgba32>(300, 300))
        {
            FillImage(img, Color.LightGray);
            await img.SaveAsPngAsync(docPath);
        }

        var node = new ImageTypeClassifierNode();
        node.Parameters["ConfidenceThreshold"] = 0.99;
        node.Parameters["EnableFaceDetection"] = false;
        var item = new FileItemContext(docPath);
        var mockContext = new Mock<IFlowExecutionContext>();

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("Other", item), Times.Once);
        mockContext.Verify(c => c.EmitAsync("Out", item), Times.Once);
        item.Metadata["AI:ImageType"].Should().Be("Other");
    }

    [Fact]
    public void AnalyzerEngine_DirectAnalysis_ShouldReturnDetailedResult()
    {
        // Arrange
        using var rgbImage = new Image<Rgb24>(200, 200);

        // Act
        var result = ImageTypeAnalyzerEngine.AnalyzeImage(rgbImage, null, enableFaceDetection: false, checkExif: false);

        // Assert
        result.Should().NotBeNull();
        result.TopScore.Should().BeInRange(0.0, 1.0);
        result.TopCategory.Should().NotBeNullOrWhiteSpace();
        result.CategoryScores.Should().NotBeEmpty();
        result.AspectRatio.Should().BeApproximately(1.0, 0.01);
    }
}
