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
}
