using System.IO;
using System.IO.Compression;
using FileFlow.Plugin.Archives.Services;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

public class DotNetZipArchiveExtractorTests : IDisposable
{
    private readonly string _testRoot;

    public DotNetZipArchiveExtractorTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "DotNetZipArchiveExtractorTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, true);
            }
        }
        catch { }
    }

    [Fact]
    public void CanHandle_ShouldReturnTrue_ForZipAndCbzExtensions()
    {
        string zipPath = Path.Combine(_testRoot, "test.zip");
        string cbzPath = Path.Combine(_testRoot, "comic.cbz");
        string epubPath = Path.Combine(_testRoot, "book.epub");
        string txtPath = Path.Combine(_testRoot, "file.txt");

        File.WriteAllText(zipPath, "dummy");
        File.WriteAllText(cbzPath, "dummy");
        File.WriteAllText(epubPath, "dummy");
        File.WriteAllText(txtPath, "dummy");

        DotNetZipArchiveExtractor.CanHandle(zipPath).Should().BeTrue();
        DotNetZipArchiveExtractor.CanHandle(cbzPath).Should().BeTrue();
        DotNetZipArchiveExtractor.CanHandle(epubPath).Should().BeTrue();
        DotNetZipArchiveExtractor.CanHandle(txtPath).Should().BeFalse();
        DotNetZipArchiveExtractor.CanHandle("").Should().BeFalse();
    }

    [Fact]
    public async Task ExtractAsync_ShouldExtractAllEntriesAndPreserveSubdirectories()
    {
        // Arrange
        string zipPath = Path.Combine(_testRoot, "sample.cbz");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry1 = archive.CreateEntry("01_cover.jpg");
            using (var writer = new StreamWriter(entry1.Open()))
            {
                await writer.WriteAsync("Cover image bytes");
            }

            var entry2 = archive.CreateEntry("Chapter 1/02_page.jpg");
            using (var writer = new StreamWriter(entry2.Open()))
            {
                await writer.WriteAsync("Chapter 1 page 2 bytes");
            }
        }

        string targetDir = Path.Combine(_testRoot, "Extracted");

        // Act
        var result = await DotNetZipArchiveExtractor.ExtractAsync(zipPath, targetDir, cancellationToken: CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.EngineUsed.Should().Be("DotNetZip");
        result.TotalEntriesCount.Should().Be(2);
        result.ExtractedFiles.Should().HaveCount(2);

        string file1 = Path.Combine(targetDir, "01_cover.jpg");
        string file2 = Path.Combine(targetDir, "Chapter 1", "02_page.jpg");

        File.Exists(file1).Should().BeTrue();
        File.Exists(file2).Should().BeTrue();
        (await File.ReadAllTextAsync(file1)).Should().Be("Cover image bytes");
        (await File.ReadAllTextAsync(file2)).Should().Be("Chapter 1 page 2 bytes");
    }
}
