using System.IO;
using System.IO.Compression;
using FileFlow.Plugin.Archives;
using FileFlow.Plugin.Images;
using FileFlow.Sdk;
using FluentAssertions;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

public class ArchiveFanOutFanInPipelineTests
{
    [Fact]
    public async Task ComicOptimizationPipeline_ShouldExtract_OptimizeImagesToWebP_AndRepackToCbz()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "FileFlow_PipelineTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        string srcDir = Path.Combine(tempDir, "Source");
        string outDir = Path.Combine(tempDir, "Destination");
        string sessionsDir = Path.Combine(tempDir, "Sessions");
        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(outDir);

        string originalCbz = Path.Combine(srcDir, "Batman_Adventures_01.cbz");

        // Create sample CBZ with 2 real images and ComicInfo.xml
        using (var zipStream = new FileStream(originalCbz, FileMode.Create))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
        {
            // Page 1: 200x200 image
            var e1 = archive.CreateEntry("page_001.png");
            using (var eStream = e1.Open())
            {
                using var img = new Image<Rgba32>(200, 200);
                for (int y = 0; y < 200; y++)
                    for (int x = 0; x < 200; x++)
                        img[x, y] = new Rgba32((byte)(x % 255), (byte)(y % 255), 128);
                await img.SaveAsPngAsync(eStream);
            }

            // Page 2: 200x200 image
            var e2 = archive.CreateEntry("page_002.png");
            using (var eStream = e2.Open())
            {
                using var img = new Image<Rgba32>(200, 200);
                for (int y = 0; y < 200; y++)
                    for (int x = 0; x < 200; x++)
                        img[x, y] = new Rgba32((byte)(y % 255), (byte)(x % 255), 200);
                await img.SaveAsPngAsync(eStream);
            }

            // ComicInfo.xml
            var eMeta = archive.CreateEntry("ComicInfo.xml");
            using (var writer = new StreamWriter(eMeta.Open()))
            {
                await writer.WriteAsync("<ComicInfo><Series>Batman</Series><Number>1</Number></ComicInfo>");
            }
        }

        try
        {
            // 1. Fan-Out Node
            var fanOutNode = new ArchiveFanOutNode();
            fanOutNode.Parameters["WorkingFolder"] = sessionsDir;

            // 2. Image Optimizer Node
            var imageOptNode = new ImageOptimizerNode();
            imageOptNode.Parameters["TargetFormat"] = "WebP";
            imageOptNode.Parameters["Quality"] = 80;
            imageOptNode.Parameters["KeepOriginalIfLarger"] = true;
            imageOptNode.Parameters["ReplaceOriginalInPlace"] = true;

            // 3. Fan-In Node
            var fanInNode = new ArchiveFanInNode();
            fanInNode.Parameters["DestinationFolder"] = outDir;
            fanInNode.Parameters["ArchiveName"] = "{Archive:OriginalArchiveFileName}";
            fanInNode.Parameters["ArchiveFormat"] = "Auto";
            fanInNode.Parameters["CleanWorkingFolder"] = true;

            var fanOutItems = new List<FileItemContext>();
            var mockFanOutContext = new Mock<IFlowExecutionContext>();
            mockFanOutContext.Setup(c => c.EmitAsync("ItemOut", It.IsAny<FileItemContext>()))
                             .Callback<string, FileItemContext>((port, item) => fanOutItems.Add(item))
                             .Returns(Task.CompletedTask);

            // Step 1: Execute Fan-Out
            var initialItem = new FileItemContext(originalCbz, isDirectory: false);
            await fanOutNode.ExecuteAsync("In", initialItem, mockFanOutContext.Object, CancellationToken.None);

            fanOutItems.Should().HaveCount(3);

            // Step 2 & 3: Route items through ImageOptimizer (if image) and into FanIn
            var fanInFinalEmitted = new List<FileItemContext>();
            var mockFanInContext = new Mock<IFlowExecutionContext>();
            mockFanInContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
                            .Callback<string, FileItemContext>((port, item) => fanInFinalEmitted.Add(item))
                            .Returns(Task.CompletedTask);

            foreach (var childItem in fanOutItems)
            {
                string ext = Path.GetExtension(childItem.CurrentPath).ToLowerInvariant();
                if (ext is ".png" or ".jpg" or ".jpeg")
                {
                    var optEmitted = new List<FileItemContext>();
                    var mockOptContext = new Mock<IFlowExecutionContext>();
                    mockOptContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
                                  .Callback<string, FileItemContext>((port, item) => optEmitted.Add(item))
                                  .Returns(Task.CompletedTask);

                    await imageOptNode.ExecuteAsync("In", childItem, mockOptContext.Object, CancellationToken.None);

                    optEmitted.Should().HaveCount(1);
                    await fanInNode.ExecuteAsync("In", optEmitted[0], mockFanInContext.Object, CancellationToken.None);
                }
                else
                {
                    // Non-image files (e.g. ComicInfo.xml) bypass optimizer and go directly to FanIn
                    await fanInNode.ExecuteAsync("In", childItem, mockFanInContext.Object, CancellationToken.None);
                }
            }

            // Assert: Repacked CBZ emitted to destination folder
            fanInFinalEmitted.Should().HaveCount(1);
            var finalCbzItem = fanInFinalEmitted[0];
            string finalCbzPath = finalCbzItem.CurrentPath;

            File.Exists(finalCbzPath).Should().BeTrue();
            finalCbzPath.Should().Be(Path.Combine(outDir, "Batman_Adventures_01.cbz"));

            // Inspect contents of repacked CBZ
            using (var zipStream = File.OpenRead(finalCbzPath))
            using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Read))
            {
                zip.Entries.Should().HaveCount(3);
                var entryNames = zip.Entries.Select(e => e.Name).ToList();

                entryNames.Should().Contain("ComicInfo.xml");
                entryNames.Any(n => n.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();

                // Verify ComicInfo.xml content preserved
                var xmlEntry = zip.GetEntry("ComicInfo.xml");
                xmlEntry.Should().NotBeNull();
                using var reader = new StreamReader(xmlEntry!.Open());
                string xmlText = await reader.ReadToEndAsync();
                xmlText.Should().Contain("<Series>Batman</Series>");
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}
