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
}
