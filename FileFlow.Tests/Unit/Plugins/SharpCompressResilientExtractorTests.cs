using System.IO;
using System.IO.Compression;
using FileFlow.Plugin.Archives.Services;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

public class SharpCompressResilientExtractorTests : IDisposable
{
    private readonly string _testRoot;

    public SharpCompressResilientExtractorTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "SharpCompressResilientExtractorTests_" + Guid.NewGuid().ToString("N"));
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
    public async Task ExtractAsync_ShouldExtractArchiveSuccessfully()
    {
        // Arrange
        string zipPath = Path.Combine(_testRoot, "resilient_test.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry1 = archive.CreateEntry("data.csv");
            using (var writer = new StreamWriter(entry1.Open()))
            {
                await writer.WriteAsync("col1,col2\nval1,val2");
            }

            var entry2 = archive.CreateEntry("sub/nested.txt");
            using (var writer = new StreamWriter(entry2.Open()))
            {
                await writer.WriteAsync("nested content");
            }
        }

        string targetDir = Path.Combine(_testRoot, "SharpOut");

        // Act
        var result = await SharpCompressResilientExtractor.ExtractAsync(zipPath, targetDir, cancellationToken: CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.ExtractedFiles.Should().HaveCount(2);

        string file1 = Path.Combine(targetDir, "data.csv");
        string file2 = Path.Combine(targetDir, "sub", "nested.txt");

        File.Exists(file1).Should().BeTrue();
        File.Exists(file2).Should().BeTrue();
        (await File.ReadAllTextAsync(file1)).Should().Contain("col1,col2");
        (await File.ReadAllTextAsync(file2)).Should().Be("nested content");
    }
}
