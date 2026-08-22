using FileFlow.Sdk;
using FileFlow.Sdk.TemplateEngine;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Sdk;

public class TokenEngineTests
{
    [Fact]
    public void Resolve_ExifToken_ReturnsMetadataValue()
    {
        var item = new FileItemContext(@"C:\Photos\DSC_001.JPG");
        item.Metadata["Exif:CameraModel"] = "Nikon Z8";

        string result = VariableTemplateResolver.Resolve("{Exif:CameraModel}_{FileName}", item);

        result.Should().Be("Nikon Z8_DSC_001.JPG");
    }

    [Fact]
    public void Resolve_HashWithLengthModifier_TruncatesHash()
    {
        var item = new FileItemContext(@"C:\Files\document.pdf");
        item.Metadata["Hash:SHA256"] = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

        string result = VariableTemplateResolver.Resolve("doc_{Hash:SHA256:8}.pdf", item);

        result.Should().Be("doc_e3b0c442.pdf");
    }

    [Fact]
    public void Resolve_FileSizeFormatting_ReturnsFormattedUnits()
    {
        var item = new FileItemContext(@"C:\Videos\movie.mp4")
        {
            FileSizeBytes = 10485760 // 10 MB
        };

        string resultMb = VariableTemplateResolver.Resolve("{FileSize:MB} MB", item);
        resultMb.Should().Be("10.00 MB");
    }

    [Fact]
    public void Resolve_DateTokens_ReturnsValidDateFormat()
    {
        var item = new FileItemContext(@"C:\Files\data.txt");
        string result = VariableTemplateResolver.Resolve("{Now:yyyy}", item);

        result.Should().Be(DateTime.Now.Year.ToString());
    }
}
