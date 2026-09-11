using System.IO;
using System.IO.Compression;
using FileFlow.Plugin.Archives.Services;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

public class UniversalArchiveExtractorTests : IDisposable
{
    private readonly string _testRoot;

    public UniversalArchiveExtractorTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "UniversalArchiveExtractorTests_" + Guid.NewGuid().ToString("N"));
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
    public async Task UniversalExtractAsync_Auto_ShouldExtractCbzAndAllNestedFolders()
    {
        // Arrange: Create a comic cbz with multiple subfolders
        string cbzPath = Path.Combine(_testRoot, "SpiderMan_Issue01.cbz");
        using (var archive = ZipFile.Open(cbzPath, ZipArchiveMode.Create))
        {
            var cover = archive.CreateEntry("00_cover.webp");
            using (var w = new StreamWriter(cover.Open())) w.Write("COVER");

            var page1 = archive.CreateEntry("Act 1/01_page.jpg");
            using (var w = new StreamWriter(page1.Open())) w.Write("PAGE1");

            var page2 = archive.CreateEntry("Act 2/02_page.jpg");
            using (var w = new StreamWriter(page2.Open())) w.Write("PAGE2");

            var meta = archive.CreateEntry("metadata/ComicInfo.xml");
            using (var w = new StreamWriter(meta.Open())) w.Write("<ComicInfo/>");
        }

        string dest = Path.Combine(_testRoot, "Out_Auto");

        // Act
        var result = await SafeArchiveExtractor.UniversalExtractAsync(
            cbzPath,
            dest,
            passwordCandidates: [null],
            engine: ArchiveExtractionEngine.Auto,
            cancellationToken: CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.TotalEntriesCount.Should().Be(4);
        result.ExtractedFiles.Should().HaveCount(4);

        File.Exists(Path.Combine(dest, "00_cover.webp")).Should().BeTrue();
        File.Exists(Path.Combine(dest, "Act 1", "01_page.jpg")).Should().BeTrue();
        File.Exists(Path.Combine(dest, "Act 2", "02_page.jpg")).Should().BeTrue();
        File.Exists(Path.Combine(dest, "metadata", "ComicInfo.xml")).Should().BeTrue();
    }

    [Fact]
    public async Task UniversalExtractAsync_DotNetZip_ShouldExtractSuccessfully()
    {
        string zipPath = Path.Combine(_testRoot, "native_test.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("hello.txt");
            using var w = new StreamWriter(entry.Open());
            w.Write("world");
        }

        string dest = Path.Combine(_testRoot, "Out_DotNetZip");

        var result = await SafeArchiveExtractor.UniversalExtractAsync(
            zipPath,
            dest,
            passwordCandidates: [null],
            engine: ArchiveExtractionEngine.DotNetZip,
            cancellationToken: CancellationToken.None);

        result.Success.Should().BeTrue();
        result.EngineUsed.Should().Be("DotNetZip");
        File.Exists(Path.Combine(dest, "hello.txt")).Should().BeTrue();
    }

    [Fact]
    public async Task UniversalExtractAsync_SharpCompress_ShouldExtractSuccessfully()
    {
        string zipPath = Path.Combine(_testRoot, "sharp_test.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("nested/doc.txt");
            using var w = new StreamWriter(entry.Open());
            w.Write("managed sharpcompress");
        }

        string dest = Path.Combine(_testRoot, "Out_Sharp");

        var result = await SafeArchiveExtractor.UniversalExtractAsync(
            zipPath,
            dest,
            passwordCandidates: [null],
            engine: ArchiveExtractionEngine.SharpCompress,
            cancellationToken: CancellationToken.None);

        result.Success.Should().BeTrue();
        File.Exists(Path.Combine(dest, "nested", "doc.txt")).Should().BeTrue();
    }
}
