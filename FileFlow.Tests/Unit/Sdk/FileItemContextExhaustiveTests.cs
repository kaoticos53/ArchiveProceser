using System.IO;
using FluentAssertions;
using FileFlow.Sdk;
using Xunit;

namespace FileFlow.Tests.Unit.Sdk;

public class FileItemContextExhaustiveTests
{
    [Fact]
    public void FileItemContext_Initialization_ShouldCalculateMemoizedPropertiesCorrectly()
    {
        // Arrange
        string samplePath = @"C:\Proyectos\Docs\Informe_Anual_2026.pdf";

        // Act
        var item = new FileItemContext(samplePath);

        // Assert
        item.Id.Should().NotBeEmpty();
        item.OriginalPath.Should().Be(samplePath);
        item.CurrentPath.Should().Be(samplePath);
        item.FileName.Should().Be("Informe_Anual_2026.pdf");
        Path.GetExtension(item.FileName).Should().Be(".pdf");
        item.IdString.Should().Be(item.Id.ToString());
        item.ShortIdString.Should().Be(item.Id.ToString()[..8]);
        item.IsDirectory.Should().BeFalse();
    }

    [Fact]
    public void FileItemContext_WhenCurrentPathMutates_FileNameShouldUpdateReactively()
    {
        // Arrange
        var item = new FileItemContext(@"C:\Temp\Original.txt");
        item.FileName.Should().Be("Original.txt");

        // Act
        item.CurrentPath = @"C:\Temp\Renamed_Optimized.webp";

        // Assert
        item.FileName.Should().Be("Renamed_Optimized.webp");
        Path.GetExtension(item.FileName).Should().Be(".webp");
    }

    [Fact]
    public void FileItemContext_DeepClone_ShouldCreateIndependentCopiesOfMetadataAndLogs()
    {
        // Arrange
        var original = new FileItemContext(@"C:\Data\file.dat");
        original.Metadata["Key1"] = "Value1";
        original.Metadata["Counter"] = 42;
        original.AddLog("Initial Log Message");

        // Act
        var clone = original.DeepClone();

        // Mutar el clon
        clone.Metadata["Key1"] = "ModifiedInClone";
        clone.Metadata["NewKey"] = "AddedToClone";
        clone.AddLog("Clone Specific Log");

        // Assert
        clone.Id.Should().Be(original.Id);
        clone.CurrentPath.Should().Be(original.CurrentPath);
        original.Metadata["Key1"].Should().Be("Value1");
        original.Metadata.Should().NotContainKey("NewKey");
        original.ExecutionLog.Should().HaveCount(1);
        clone.ExecutionLog.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(@"C:\Path\Makefile", "Makefile", "")]
    [InlineData(@"C:\Path\.gitignore", ".gitignore", ".gitignore")]
    [InlineData(@"C:\Ruta con Espacios y Acentos\archivo_ñandú.tar.gz", "archivo_ñandú.tar.gz", ".gz")]
    [InlineData("", "", "")]
    public void FileItemContext_EdgeCasePaths_ShouldHandleExtensionsGracefully(string path, string expectedName, string expectedExt)
    {
        // Act
        var item = new FileItemContext(path);

        // Assert
        item.FileName.Should().Be(expectedName);
        Path.GetExtension(item.FileName).Should().Be(expectedExt);
    }
}
