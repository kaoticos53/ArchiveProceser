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

    [Fact]
    public async Task ComicOptimizationPipeline_WhenSomeOptimizedAndSomeKeptOriginal_ShouldNeverContainDuplicates()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "FileFlow_NoDuplicatesTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        string srcDir = Path.Combine(tempDir, "Source");
        string outDir = Path.Combine(tempDir, "Destination");
        string sessionsDir = Path.Combine(tempDir, "Sessions");
        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(outDir);

        string originalCbz = Path.Combine(srcDir, "Spiderman_01.cbz");

        using (var zipStream = new FileStream(originalCbz, FileMode.Create))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
        {
            // Page 1: 300x300 image (WebP will be smaller)
            var e1 = archive.CreateEntry("page_001.png");
            using (var eStream = e1.Open())
            {
                using var img = new Image<Rgba32>(300, 300);
                for (int y = 0; y < 300; y++)
                    for (int x = 0; x < 300; x++)
                        img[x, y] = new Rgba32((byte)(x % 255), (byte)(y % 255), 100);
                await img.SaveAsPngAsync(eStream);
            }

            // Page 2: tiny 4x4 image
            var e2 = archive.CreateEntry("page_002.png");
            using (var eStream = e2.Open())
            {
                using var img = new Image<Rgba32>(4, 4);
                await img.SaveAsPngAsync(eStream);
            }

            // Page 3 in nested subfolder
            var e3 = archive.CreateEntry("Chapter 1/page_003.png");
            using (var eStream = e3.Open())
            {
                using var img = new Image<Rgba32>(200, 200);
                for (int y = 0; y < 200; y++)
                    for (int x = 0; x < 200; x++)
                        img[x, y] = new Rgba32((byte)(y % 255), (byte)(x % 255), 150);
                await img.SaveAsPngAsync(eStream);
            }

            // Metadata
            var eMeta = archive.CreateEntry("metadata.json");
            using (var writer = new StreamWriter(eMeta.Open()))
            {
                await writer.WriteAsync("{\"series\": \"Spiderman\"}");
            }
        }

        try
        {
            var fanOutNode = new ArchiveFanOutNode();
            fanOutNode.Parameters["WorkingFolder"] = sessionsDir;

            var imageOptNode = new ImageOptimizerNode();
            imageOptNode.Parameters["TargetFormat"] = "WebP";
            imageOptNode.Parameters["Quality"] = 80;
            imageOptNode.Parameters["KeepOriginalIfLarger"] = true;

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

            var initialItem = new FileItemContext(originalCbz, isDirectory: false);
            await fanOutNode.ExecuteAsync("In", initialItem, mockFanOutContext.Object, CancellationToken.None);

            fanOutItems.Should().HaveCount(4);

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
                    await fanInNode.ExecuteAsync("In", childItem, mockFanInContext.Object, CancellationToken.None);
                }
            }

            // Assert
            fanInFinalEmitted.Should().HaveCount(1);
            string finalCbzPath = fanInFinalEmitted[0].CurrentPath;
            File.Exists(finalCbzPath).Should().BeTrue();

            using (var zipStream = File.OpenRead(finalCbzPath))
            using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Read))
            {
                // Must have EXACTLY 4 entries without any duplicates
                zip.Entries.Should().HaveCount(4);

                var entryNames = zip.Entries.Select(e => e.FullName.Replace('\\', '/')).ToList();
                entryNames.Should().Contain("metadata.json");
                entryNames.Should().Contain("page_001.webp");
                entryNames.Should().NotContain("page_001.png");
                entryNames.Should().NotContain("page_001_optimized.webp");

                // Page 3 in nested subfolder must be Chapter 1/page_003.webp
                entryNames.Should().Contain("Chapter 1/page_003.webp");
                entryNames.Should().NotContain("Chapter 1/page_003.png");
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

    [Fact]
    public async Task DirectLinearPipeline_WhenPipingAllExtractedItemsDirectlyThroughOptimizer_ShouldPreserveAllEntriesAndNonImages()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "FileFlow_DirectLinearTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        string srcDir = Path.Combine(tempDir, "Source");
        string outDir = Path.Combine(tempDir, "Destination");
        string sessionsDir = Path.Combine(tempDir, "Sessions");
        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(outDir);

        string originalCbz = Path.Combine(srcDir, "DirectComic_01.cbz");

        using (var zipStream = new FileStream(originalCbz, FileMode.Create))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
        {
            // Image 1: Large image (WebP is smaller)
            var e1 = archive.CreateEntry("01_cover.png");
            using (var eStream = e1.Open())
            {
                using var img = new Image<Rgba32>(250, 250);
                for (int y = 0; y < 250; y++)
                    for (int x = 0; x < 250; x++)
                        img[x, y] = new Rgba32((byte)(x % 255), (byte)(y % 255), 50);
                await img.SaveAsPngAsync(eStream);
            }

            // Image 2: Tiny 2x2 image (original will be kept because WebP header is larger)
            var e2 = archive.CreateEntry("02_blank.png");
            using (var eStream = e2.Open())
            {
                using var img = new Image<Rgba32>(2, 2);
                await img.SaveAsPngAsync(eStream);
            }

            // Non-image 1: ComicInfo.xml
            var eXml = archive.CreateEntry("ComicInfo.xml");
            using (var writer = new StreamWriter(eXml.Open()))
            {
                await writer.WriteAsync("<ComicInfo><Title>Issue 1</Title></ComicInfo>");
            }

            // Non-image 2: notes.txt
            var eTxt = archive.CreateEntry("meta/notes.txt");
            using (var writer = new StreamWriter(eTxt.Open()))
            {
                await writer.WriteAsync("Release notes here.");
            }
        }

        try
        {
            var fanOutNode = new ArchiveFanOutNode();
            fanOutNode.Parameters["WorkingFolder"] = sessionsDir;

            var imageOptNode = new ImageOptimizerNode();
            imageOptNode.Parameters["TargetFormat"] = "WebP";
            imageOptNode.Parameters["Quality"] = 80;
            imageOptNode.Parameters["KeepOriginalIfLarger"] = true;
            imageOptNode.Parameters["PassThroughNonImages"] = true;

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

            var initialItem = new FileItemContext(originalCbz, isDirectory: false);
            await fanOutNode.ExecuteAsync("In", initialItem, mockFanOutContext.Object, CancellationToken.None);

            fanOutItems.Should().HaveCount(4);

            var fanInFinalEmitted = new List<FileItemContext>();
            var mockFanInContext = new Mock<IFlowExecutionContext>();
            mockFanInContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
                            .Callback<string, FileItemContext>((port, item) => fanInFinalEmitted.Add(item))
                            .Returns(Task.CompletedTask);

            // Directly route EVERY item through ImageOptimizer -> FanIn without any conditional branching
            foreach (var childItem in fanOutItems)
            {
                var optEmitted = new List<FileItemContext>();
                var mockOptContext = new Mock<IFlowExecutionContext>();
                mockOptContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
                              .Callback<string, FileItemContext>((port, item) => optEmitted.Add(item))
                              .Returns(Task.CompletedTask);

                await imageOptNode.ExecuteAsync("In", childItem, mockOptContext.Object, CancellationToken.None);

                optEmitted.Should().HaveCount(1, $"every item including non-images should be passed to Out");
                await fanInNode.ExecuteAsync("In", optEmitted[0], mockFanInContext.Object, CancellationToken.None);
            }

            // Assert: Fan-In must have completed automatically and emitted repacked CBZ
            fanInFinalEmitted.Should().HaveCount(1);
            string finalCbzPath = fanInFinalEmitted[0].CurrentPath;
            File.Exists(finalCbzPath).Should().BeTrue();

            using (var zipStream = File.OpenRead(finalCbzPath))
            using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Read))
            {
                // All 4 files must exist!
                zip.Entries.Should().HaveCount(4);

                var entryNames = zip.Entries.Select(e => e.FullName.Replace('\\', '/')).ToList();
                entryNames.Should().Contain("ComicInfo.xml");
                entryNames.Should().Contain("meta/notes.txt");
                entryNames.Should().Contain("01_cover.webp");
                entryNames.Any(n => n.StartsWith("02_blank.")).Should().BeTrue();

                // Verify ComicInfo.xml content preserved
                var xmlEntry = zip.GetEntry("ComicInfo.xml");
                xmlEntry.Should().NotBeNull();
                using var reader = new StreamReader(xmlEntry!.Open());
                string xmlText = await reader.ReadToEndAsync();
                xmlText.Should().Contain("<Title>Issue 1</Title>");
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

    [Fact]
    public async Task ComicPipeline_WithNestedDirectories_ShouldPreserveDirectoryStructureInDestinationFolder()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "FileFlow_NestedRelDirTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        string srcRoot = Path.Combine(tempDir, "Comics");
        string marvelSubDir = Path.Combine(srcRoot, "Marvel");
        string outRoot = Path.Combine(tempDir, "Salida");
        string sessionsDir = Path.Combine(tempDir, "Sessions");
        Directory.CreateDirectory(marvelSubDir);
        Directory.CreateDirectory(outRoot);

        string originalCbz = Path.Combine(marvelSubDir, "SpiderMan_01.cbz");

        // Create a simple CBZ with 1 image
        using (var zipStream = new FileStream(originalCbz, FileMode.Create))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
        {
            var e1 = archive.CreateEntry("01.png");
            using var eStream = e1.Open();
            using var img = new Image<Rgba32>(100, 100);
            await img.SaveAsPngAsync(eStream);
        }

        try
        {
            // Fan-out Node
            var fanOutNode = new ArchiveFanOutNode();
            fanOutNode.Parameters["WorkingFolder"] = sessionsDir;

            // Image Optimizer Node
            var optimizerNode = new ImageOptimizerNode();
            optimizerNode.Parameters["TargetFormat"] = "WebP";
            optimizerNode.Parameters["Quality"] = 80;

            // Fan-In Node with DestinationFolder = "{GlobalOutputDir}\{RelativeDir}"
            var fanInNode = new ArchiveFanInNode();
            fanInNode.Parameters["DestinationFolder"] = @"{GlobalOutputDir}\{RelativeDir}";
            fanInNode.Parameters["ArchiveName"] = "{Archive:OriginalArchiveFileName}";

            var fanOutEmitted = new List<FileItemContext>();
            var mockFanOutContext = new Mock<IFlowExecutionContext>();
            mockFanOutContext.Setup(c => c.RegisterTemporaryDirectory(It.IsAny<string>()));
            mockFanOutContext.Setup(c => c.EmitAsync("ItemOut", It.IsAny<FileItemContext>()))
                .Callback<string, FileItemContext>((p, itm) => fanOutEmitted.Add(itm))
                .Returns(Task.CompletedTask);

            var fanInFinalEmitted = new List<FileItemContext>();
            var mockFanInContext = new Mock<IFlowExecutionContext>();
            mockFanInContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
                .Callback<string, FileItemContext>((p, itm) => fanInFinalEmitted.Add(itm))
                .Returns(Task.CompletedTask);

            // Create root source item as emitted by FolderSourceNode
            var sourceArchiveItem = new FileItemContext(originalCbz, isDirectory: false);
            sourceArchiveItem.Metadata["SourceRootPath"] = srcRoot;
            sourceArchiveItem.Metadata["GlobalOutputDir"] = outRoot;

            // 1. Execute Fan-Out
            await fanOutNode.ExecuteAsync("In", sourceArchiveItem, mockFanOutContext.Object, CancellationToken.None);
            fanOutEmitted.Should().HaveCount(1);
            fanOutEmitted[0].Metadata["Archive:RelativeDir"].Should().Be("Marvel");

            // 2. Execute Image Optimizer
            var optEmitted = new List<FileItemContext>();
            var mockOptContext = new Mock<IFlowExecutionContext>();
            mockOptContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
                .Callback<string, FileItemContext>((p, itm) => optEmitted.Add(itm))
                .Returns(Task.CompletedTask);

            await optimizerNode.ExecuteAsync("In", fanOutEmitted[0], mockOptContext.Object, CancellationToken.None);
            optEmitted.Should().HaveCount(1);

            // 3. Execute Fan-In
            await fanInNode.ExecuteAsync("In", optEmitted[0], mockFanInContext.Object, CancellationToken.None);

            // Assert
            fanInFinalEmitted.Should().HaveCount(1);
            string expectedDestinationDir = Path.Combine(outRoot, "Marvel");
            string expectedOutputFile = Path.Combine(expectedDestinationDir, "SpiderMan_01.cbz");

            fanInFinalEmitted[0].CurrentPath.Should().Be(expectedOutputFile);
            File.Exists(expectedOutputFile).Should().BeTrue();
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
