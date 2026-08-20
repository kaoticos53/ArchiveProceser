using FileFlow.Sdk;
using FileFlow.Sdk.TemplateEngine;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Sdk;

public class VariableTemplateResolverTests
{
    [Fact]
    public void Resolve_ShouldInterpolateSystemVariables_WhenValidItemProvided()
    {
        // Arrange (AAA)
        var item = new FileItemContext(@"C:\Photos\vacation.jpg", isDirectory: false);

        // Act
        string resolvedName = VariableTemplateResolver.Resolve("{FileName}", item);
        string resolvedNoExt = VariableTemplateResolver.Resolve("{FileNameNoExt}", item);
        string resolvedExt = VariableTemplateResolver.Resolve("{Extension}", item);

        // Assert
        resolvedName.Should().Be("vacation.jpg");
        resolvedNoExt.Should().Be("vacation");
        resolvedExt.Should().Be("jpg");
    }

    [Fact]
    public void Resolve_ShouldCalculateRelativeDirectory_ExcludingFileName()
    {
        // Arrange
        var item = new FileItemContext(@"C:\pepe\mami\antiguo\archivo.jpg", isDirectory: false);
        item.Metadata["SourceRootPath"] = @"C:\pepe";

        // Act
        string relativeDir = VariableTemplateResolver.Resolve("{RelativePath}", item);

        // Assert
        relativeDir.Should().Be(@"mami\antiguo");
    }

    [Fact]
    public void Resolve_ShouldExecuteDateFunctions_WhenDateTakenMetadataExists()
    {
        // Arrange
        var item = new FileItemContext(@"C:\Photos\img.jpg", isDirectory: false);
        item.Metadata["DateTaken"] = "2026-08-20 14:30:00";

        // Act
        string year = VariableTemplateResolver.Resolve("{Year(DateTaken)}", item);
        string month = VariableTemplateResolver.Resolve("{Month(DateTaken)}", item);
        string day = VariableTemplateResolver.Resolve("{Day(DateTaken)}", item);
        string fmtDate = VariableTemplateResolver.Resolve("{FormatDate(DateTaken, \"yyyy-MM\")}", item);

        // Assert
        year.Should().Be("2026");
        month.Should().Be("08");
        day.Should().Be("20");
        fmtDate.Should().Be("2026-08");
    }

    [Theory]
    [InlineData("hello world", "HELLO WORLD", "hello world")]
    public void Resolve_ShouldExecuteTextTransformationFunctions(string input, string expectedUpper, string expectedLower)
    {
        // Arrange
        var item = new FileItemContext(@"C:\test.txt", isDirectory: false);
        item.Metadata["CustomText"] = input;

        // Act
        string upper = VariableTemplateResolver.Resolve("{Upper(CustomText)}", item);
        string lower = VariableTemplateResolver.Resolve("{Lower(CustomText)}", item);

        // Assert
        upper.Should().Be(expectedUpper);
        lower.Should().Be(expectedLower);
    }

    [Fact]
    public void Resolve_ShouldSanitizeIllegalWindowsPathCharacters()
    {
        // Arrange
        var item = new FileItemContext(@"C:\test.txt", isDirectory: false);
        item.Metadata["IllegalName"] = @"Foto/Vacaciones:2026*Canon?";

        // Act
        string sanitized = VariableTemplateResolver.Resolve("{Sanitize(IllegalName)}", item);

        // Assert
        sanitized.Should().Be("Foto-Vacaciones-2026-Canon-");
    }

    [Fact]
    public void Resolve_ShouldPadLeftNumbers_WhenPadLeftFunctionUsed()
    {
        // Arrange
        var item = new FileItemContext(@"C:\test.txt", isDirectory: false);
        item.Metadata["Counter"] = 5;

        // Act
        string padded = VariableTemplateResolver.Resolve("{PadLeft(Counter, 4, \"0\")}", item);

        // Assert
        padded.Should().Be("0005");
    }

    [Fact]
    public void Resolve_ShouldExecuteCoalesceCascade_ReturningFirstNonEmptyValue()
    {
        // Arrange
        var item = new FileItemContext(@"C:\test.txt", isDirectory: false);
        item.Metadata["EmptyValue"] = "";
        item.Metadata["FallbackValue"] = "FoundMe";

        // Act
        string result = VariableTemplateResolver.Resolve("{Coalesce(EmptyValue, NonExistentKey, FallbackValue, \"Default\")}", item);

        // Assert
        result.Should().Be("FoundMe");
    }

    [Fact]
    public void Resolve_ShouldHandleRegexMatchAndReplace()
    {
        // Arrange
        var item = new FileItemContext(@"C:\Reports\PROD-12345_document.pdf", isDirectory: false);

        // Act
        string matched = VariableTemplateResolver.Resolve("{RegexMatch(FileNameNoExt, \"PROD-[0-9]+\")}", item);

        // Assert
        matched.Should().Be("PROD-12345");
    }

    [Fact]
    public void Resolve_ShouldReturnUnchangedTemplate_WhenNoTokensPresent()
    {
        // Arrange
        var item = new FileItemContext(@"C:\test.txt", isDirectory: false);
        string plainText = @"C:\Output\FixedFolder\Data";

        // Act
        string resolved = VariableTemplateResolver.Resolve(plainText, item);

        // Assert
        resolved.Should().Be(plainText);
    }
}
