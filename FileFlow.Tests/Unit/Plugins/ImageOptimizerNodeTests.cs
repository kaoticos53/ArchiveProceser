using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Plugin.Images;
using FileFlow.Sdk;
using FluentAssertions;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

public class ImageOptimizerNodeTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldEmitError_WhenInputFileDoesNotExist()
    {
        // Arrange
        string nonExistentFile = @"C:\FakeImage_" + Guid.NewGuid() + ".jpg";
        var node = new ImageOptimizerNode();
        var item = new FileItemContext(nonExistentFile, isDirectory: false);

        var emittedErrors = new List<FileItemContext>();
        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.Setup(c => c.EmitAsync("Error", It.IsAny<FileItemContext>()))
                   .Callback<string, FileItemContext>((port, emItem) => emittedErrors.Add(emItem))
                   .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        emittedErrors.Should().HaveCount(1);
    }

    [Fact]
    public void CalculateTargetDimensions_WidthOnly_PreservesAspectRatio()
    {
        // 4000x2000 (aspect 2:1) -> width 1000, height vacío
        var result = ImageOptimizerNode.CalculateTargetDimensions(
            origWidth: 4000,
            origHeight: 2000,
            widthSpec: "1000",
            heightSpec: "",
            onlyDownscale: true);

        result.TargetWidth.Should().Be(1000);
        result.TargetHeight.Should().Be(500);
        result.ResizeNeeded.Should().BeTrue();
    }

    [Fact]
    public void CalculateTargetDimensions_HeightOnly_PreservesAspectRatio()
    {
        // 3000x1500 (aspect 2:1) -> width 0, height 600
        var result = ImageOptimizerNode.CalculateTargetDimensions(
            origWidth: 3000,
            origHeight: 1500,
            widthSpec: 0,
            heightSpec: "600px",
            onlyDownscale: true);

        result.TargetWidth.Should().Be(1200);
        result.TargetHeight.Should().Be(600);
        result.ResizeNeeded.Should().BeTrue();
    }

    [Fact]
    public void CalculateTargetDimensions_BothDimensions_MaintainAspectRatio_BoxesCorrectly()
    {
        // 3840x2160 (16:9) inside bounding box 1920x1080 -> 1920x1080
        var result = ImageOptimizerNode.CalculateTargetDimensions(
            origWidth: 3840,
            origHeight: 2160,
            widthSpec: "1920",
            heightSpec: "1080",
            onlyDownscale: true);

        result.TargetWidth.Should().Be(1920);
        result.TargetHeight.Should().Be(1080);
        result.ResizeNeeded.Should().BeTrue();
    }

    [Fact]
    public void CalculateTargetDimensions_Percentage_ScalesProportionally()
    {
        // 2000x1000 at 50% scale
        var result = ImageOptimizerNode.CalculateTargetDimensions(
            origWidth: 2000,
            origHeight: 1000,
            widthSpec: "50%",
            heightSpec: "50%",
            onlyDownscale: true);

        result.TargetWidth.Should().Be(1000);
        result.TargetHeight.Should().Be(500);
        result.ResizeNeeded.Should().BeTrue();
    }

    [Fact]
    public void CalculateTargetDimensions_SinglePercentage_AutoHeight()
    {
        // 2000x1000 with only width 50%
        var result = ImageOptimizerNode.CalculateTargetDimensions(
            origWidth: 2000,
            origHeight: 1000,
            widthSpec: "50%",
            heightSpec: "auto",
            onlyDownscale: true);

        result.TargetWidth.Should().Be(1000);
        result.TargetHeight.Should().Be(500);
        result.ResizeNeeded.Should().BeTrue();
    }

    [Fact]
    public void CalculateTargetDimensions_Percentage_Asymmetric()
    {
        // 1000x1000 with Width 50% and Height 25%
        var result = ImageOptimizerNode.CalculateTargetDimensions(
            origWidth: 1000,
            origHeight: 1000,
            widthSpec: "50%",
            heightSpec: "25%",
            onlyDownscale: true);

        result.TargetWidth.Should().Be(500);
        result.TargetHeight.Should().Be(250);
        result.ResizeNeeded.Should().BeTrue();
    }

    [Fact]
    public void CalculateTargetDimensions_OnlyDownscale_PreventsUpscalingSmallImages()
    {
        // Image is 800x600, target width is 1920 (onlyDownscale = true) -> should NOT enlarge
        var result = ImageOptimizerNode.CalculateTargetDimensions(
            origWidth: 800,
            origHeight: 600,
            widthSpec: "1920",
            heightSpec: "",
            onlyDownscale: true);

        result.TargetWidth.Should().Be(800);
        result.TargetHeight.Should().Be(600);
        result.ResizeNeeded.Should().BeFalse();
    }

    [Fact]
    public void CalculateTargetDimensions_AllowUpscale_EnlargesSmallImagesWhenConfigured()
    {
        // Image is 800x400, target width is 1600 (onlyDownscale = false) -> should upscale
        var result = ImageOptimizerNode.CalculateTargetDimensions(
            origWidth: 800,
            origHeight: 400,
            widthSpec: 1600,
            heightSpec: 0,
            onlyDownscale: false);

        result.TargetWidth.Should().Be(1600);
        result.TargetHeight.Should().Be(800);
        result.ResizeNeeded.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_RealImage_ProcessesCorrectlyWithOnlyWidth()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "FileFlow_ImgTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string srcImgPath = Path.Combine(tempDir, "test_input.png");
        string outDir = Path.Combine(tempDir, "Out");

        try
        {
            // Create a real 800x400 PNG test image
            using (var img = new Image<Rgba32>(800, 400))
            {
                await img.SaveAsPngAsync(srcImgPath);
            }

            var node = new ImageOptimizerNode();
            node.Parameters["Width"] = "400";
            node.Parameters["Height"] = "";
            node.Parameters["OnlyDownscale"] = true;
            node.Parameters["TargetFormat"] = "WebP";
            node.Parameters["OutputDirectory"] = outDir;

            var item = new FileItemContext(srcImgPath, isDirectory: false)
            {
                FileSizeBytes = new FileInfo(srcImgPath).Length
            };

            var emittedItems = new List<FileItemContext>();
            var mockContext = new Mock<IFlowExecutionContext>();
            mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
                       .Callback<string, FileItemContext>((port, emItem) => emittedItems.Add(emItem))
                       .Returns(Task.CompletedTask);

            // Act
            await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

            // Assert
            emittedItems.Should().HaveCount(1);
            string generatedFile = emittedItems[0].CurrentPath;
            File.Exists(generatedFile).Should().BeTrue();
            generatedFile.Should().EndWith(".webp");

            // Verify dimensions of generated WebP image
            using var resultImg = await Image.LoadAsync(generatedFile);
            resultImg.Width.Should().Be(400);
            resultImg.Height.Should().Be(200); // 800x400 downscaled by half keeping aspect ratio
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
    public async Task ExecuteAsync_RealImage_ProcessesWithPercentageScale()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "FileFlow_ImgTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string srcImgPath = Path.Combine(tempDir, "test_input_pct.png");
        string outDir = Path.Combine(tempDir, "OutPct");

        try
        {
            // Create a real 600x300 PNG test image
            using (var img = new Image<Rgba32>(600, 300))
            {
                await img.SaveAsPngAsync(srcImgPath);
            }

            var node = new ImageOptimizerNode();
            node.Parameters["Width"] = "50%";
            node.Parameters["Height"] = "50%";
            node.Parameters["TargetFormat"] = "PNG";
            node.Parameters["OutputDirectory"] = outDir;

            var item = new FileItemContext(srcImgPath, isDirectory: false)
            {
                FileSizeBytes = new FileInfo(srcImgPath).Length
            };

            var emittedItems = new List<FileItemContext>();
            var mockContext = new Mock<IFlowExecutionContext>();
            mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
                       .Callback<string, FileItemContext>((port, emItem) => emittedItems.Add(emItem))
                       .Returns(Task.CompletedTask);

            // Act
            await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

            // Assert
            emittedItems.Should().HaveCount(1);
            string generatedFile = emittedItems[0].CurrentPath;
            File.Exists(generatedFile).Should().BeTrue();

            using var resultImg = await Image.LoadAsync(generatedFile);
            resultImg.Width.Should().Be(300);
            resultImg.Height.Should().Be(150);
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
    public void CalculateTargetDimensions_DefaultParameters_PreservesFullResolutionAndAspectRatio()
    {
        // Arrange: default node parameters -> Width: "", Height: "100%"
        var node = new ImageOptimizerNode();
        node.Parameters["Width"].Should().Be("");
        node.Parameters["Height"].Should().Be("100%");

        // Act: 3840x2160 (16:9 4K image)
        var result = ImageOptimizerNode.CalculateTargetDimensions(
            origWidth: 3840,
            origHeight: 2160,
            widthSpec: node.Parameters["Width"],
            heightSpec: node.Parameters["Height"],
            onlyDownscale: true);

        // Assert: 100% scale keeps 3840x2160 without deformation
        result.TargetWidth.Should().Be(3840);
        result.TargetHeight.Should().Be(2160);
    }
}
