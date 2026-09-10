using System.IO;
using FileFlow.Plugin.Archives.Services;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

public class ArchiveVolumeResolverTests
{
    [Theory]
    [InlineData("archive.zip", true)]
    [InlineData("archive.7z", true)]
    [InlineData("archive.part01.rar", true)]
    [InlineData("archive.part1.rar", true)]
    [InlineData("archive.part02.rar", false)]
    [InlineData("archive.part5.rar", false)]
    [InlineData("document.pdf", false)]
    public void IsPrimaryArchiveFile_ShouldIdentifyCorrectly(string fileName, bool expectedResult)
    {
        bool result = ArchiveVolumeResolver.IsPrimaryArchiveFile(fileName);
        result.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData("archive.r01", true)]
    [InlineData("archive.part02.rar", true)]
    [InlineData("archive.z01", true)]
    [InlineData("archive.part01.rar", false)]
    [InlineData("archive.zip", false)]
    public void IsSecondaryVolumeFile_ShouldIdentifyCorrectly(string fileName, bool expectedResult)
    {
        bool result = ArchiveVolumeResolver.IsSecondaryVolumeFile(fileName);
        result.Should().Be(expectedResult);
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Assertions", "xUnit2004:Do not use Assert.Equal() to check for boolean conditions", Justification = "Testing string result")]
    public void GetCommonRootFolder_WithSingleWrapper_ShouldReturnRoot()
    {
        var entries = new List<string>
        {
            "my_folder/file1.txt",
            "my_folder/file2.png",
            "my_folder/sub/file3.json"
        };

        string? root = ArchiveVolumeResolver.GetCommonRootFolder(entries);
        root.Should().Be("my_folder");
    }

    [Fact]
    public void GetCommonRootFolder_WithMultipleRoots_ShouldReturnNull()
    {
        var entries = new List<string>
        {
            "folderA/file1.txt",
            "folderB/file2.png"
        };

        string? root = ArchiveVolumeResolver.GetCommonRootFolder(entries);
        root.Should().BeNull();
    }

    [Fact]
    public void FindRelatedVolumeFiles_WhenDirectoryDoesNotExist_ShouldReturnOnlyInput()
    {
        string archivePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "movie.part01.rar");

        var volumes = ArchiveVolumeResolver.FindRelatedVolumeFiles(archivePath);

        volumes.Should().ContainSingle();
        volumes[0].Should().Be(archivePath);
    }

    [Fact]
    public void FindRelatedVolumeFiles_ForPartRar_ShouldIncludeSiblingParts()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "ArchiveVolumeResolverTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string part01 = Path.Combine(tempDir, "album.part01.rar");
            string part02 = Path.Combine(tempDir, "album.part02.rar");
            string part03 = Path.Combine(tempDir, "album.part03.rar");
            string other = Path.Combine(tempDir, "other.part01.rar");

            File.WriteAllText(part01, "");
            File.WriteAllText(part02, "");
            File.WriteAllText(part03, "");
            File.WriteAllText(other, "");

            var volumes = ArchiveVolumeResolver.FindRelatedVolumeFiles(part01);

            volumes.Should().Contain(part01);
            volumes.Should().Contain(part02);
            volumes.Should().Contain(part03);
            volumes.Should().NotContain(other);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void FindRelatedVolumeFiles_ForZipSplit_ShouldIncludeSiblingZVolumes()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "ArchiveVolumeResolverTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string zip = Path.Combine(tempDir, "backup.zip");
            string z01 = Path.Combine(tempDir, "backup.z01");
            string z02 = Path.Combine(tempDir, "backup.z02");
            string other = Path.Combine(tempDir, "another.z01");

            File.WriteAllText(zip, "");
            File.WriteAllText(z01, "");
            File.WriteAllText(z02, "");
            File.WriteAllText(other, "");

            var volumes = ArchiveVolumeResolver.FindRelatedVolumeFiles(zip);

            volumes.Should().Contain(zip);
            volumes.Should().Contain(z01);
            volumes.Should().Contain(z02);
            volumes.Should().NotContain(other);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
