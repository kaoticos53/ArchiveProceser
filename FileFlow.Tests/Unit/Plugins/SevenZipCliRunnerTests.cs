using System.IO;
using System.IO.Compression;
using FileFlow.Plugin.Archives.Services;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

public class SevenZipCliRunnerTests : IDisposable
{
    private readonly string _testRoot;

    public SevenZipCliRunnerTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "SevenZipCliRunnerTests_" + Guid.NewGuid().ToString("N"));
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
    public void FindSevenZipExecutable_ShouldLocateInstalled7z_OnWindowsEnvironment()
    {
        // Act
        string? exePath = SevenZipCliRunner.FindSevenZipExecutable();

        // Assert
        // On systems with 7-Zip installed (like the user environment C:\Program Files\7-Zip\7z.exe)
        if (File.Exists(@"C:\Program Files\7-Zip\7z.exe"))
        {
            exePath.Should().NotBeNullOrWhiteSpace();
            File.Exists(exePath).Should().BeTrue();
            SevenZipCliRunner.IsAvailable().Should().BeTrue();
        }
    }

    [Fact]
    public async Task ExtractAsync_ShouldExtractZipUsing7z_WhenAvailable()
    {
        if (!SevenZipCliRunner.IsAvailable())
        {
            return; // Skip if 7z is not on test runner machine
        }

        // Arrange
        string zipPath = Path.Combine(_testRoot, "test_7z.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("nested/doc.txt");
            using var writer = new StreamWriter(entry.Open());
            await writer.WriteAsync("Hello 7-Zip CLI");
        }

        string targetDir = Path.Combine(_testRoot, "Out7z");

        // Act
        var result = await SevenZipCliRunner.ExtractAsync(zipPath, targetDir, cancellationToken: CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.EngineUsed.Should().Be("SevenZipCLI");
        result.ExtractedFiles.Should().NotBeEmpty();

        string extractedFile = Path.Combine(targetDir, "nested", "doc.txt");
        File.Exists(extractedFile).Should().BeTrue();
        (await File.ReadAllTextAsync(extractedFile)).Should().Be("Hello 7-Zip CLI");
    }
}
