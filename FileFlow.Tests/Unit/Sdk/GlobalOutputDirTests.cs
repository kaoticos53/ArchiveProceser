using FileFlow.Sdk;
using FileFlow.Sdk.TemplateEngine;
using Xunit;

namespace FileFlow.Tests.Unit.Sdk;

public class GlobalOutputDirTests
{
    [Fact]
    public void ResolveOutputPath_WithRelativePath_AnchorsUnderGlobalOutputDir()
    {
        var item = new FileItemContext(@"C:\Source\SampleFile.pdf");
        item.Metadata["GlobalOutputDir"] = @"D:\FileFlowOutput";

        string resolved = ParameterHelper.ResolveOutputPath("Converted/{FileNameNoExt}.zip", item);

        Assert.Equal(@"D:\FileFlowOutput\Converted\SampleFile.zip", resolved);
    }

    [Fact]
    public void ResolveOutputPath_WithAbsolutePath_KeepsAbsolutePathUnchanged()
    {
        var item = new FileItemContext(@"C:\Source\SampleFile.pdf");
        item.Metadata["GlobalOutputDir"] = @"D:\FileFlowOutput";

        string resolved = ParameterHelper.ResolveOutputPath(@"E:\CustomLocation\Output.zip", item);

        Assert.Equal(@"E:\CustomLocation\Output.zip", resolved);
    }

    [Fact]
    public void VariableTemplateResolver_ResolvesGlobalOutputDirToken()
    {
        var item = new FileItemContext(@"C:\Source\SampleFile.pdf");
        item.Metadata["GlobalOutputDir"] = @"D:\FileFlowOutput";

        string resolved = VariableTemplateResolver.Resolve("{GlobalOutputDir}/Exports/{FileName}", item);

        Assert.Equal(@"D:\FileFlowOutput/Exports/SampleFile.pdf", resolved);
    }

    [Fact]
    public void ResolveOutputPath_WithoutGlobalOutputDir_AnchorsUnderSourceDirectory()
    {
        // Arrange - File directly in d:\pepe\
        var item = new FileItemContext(@"d:\pepe\archivo.txt");

        // Act - Pattern with {RelativeDir}\Output
        string resolved = ParameterHelper.ResolveOutputPath(@"{RelativeDir}\Output", item);

        // Assert - RelativeDir is empty, so Output is anchored directly to d:\pepe\Output
        Assert.Equal(@"d:\pepe\Output", resolved);
    }

    [Fact]
    public void ResolveOutputPath_WithSubdirectoryAndSourceRootPath_AnchorsCorrectly()
    {
        // Arrange - File in subfolder d:\pepe\sub1\sub2\archivo.txt with SourceRootPath = d:\pepe
        var item = new FileItemContext(@"d:\pepe\sub1\sub2\archivo.txt");
        item.Metadata["SourceRootPath"] = @"d:\pepe";

        // Act
        string resolved = ParameterHelper.ResolveOutputPath(@"{RelativeDir}\Output", item);

        // Assert - RelativeDir is sub1\sub2, anchored under d:\pepe -> d:\pepe\sub1\sub2\Output
        Assert.Equal(@"d:\pepe\sub1\sub2\Output", resolved);
    }
}
