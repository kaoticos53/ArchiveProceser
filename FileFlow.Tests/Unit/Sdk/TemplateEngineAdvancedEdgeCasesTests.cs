using FileFlow.Sdk;
using FileFlow.Sdk.TemplateEngine;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Sdk;

public class TemplateEngineAdvancedEdgeCasesTests
{
    [Fact]
    public void Resolve_WithUnclosedBrace_ShouldReturnOriginalStringWithoutCrashing()
    {
        // Arrange (AAA)
        var item = new FileItemContext(@"C:\Photos\vacation.jpg", isDirectory: false);

        // Act
        string resolved = VariableTemplateResolver.Resolve("Prefix_{UnclosedToken_NoEndingBrace", item);

        // Assert
        resolved.Should().Be("Prefix_{UnclosedToken_NoEndingBrace");
    }

    [Fact]
    public void Resolve_WithCoalesceAndFallback_ShouldReturnFirstNonEmptyValue()
    {
        // Arrange
        var item = new FileItemContext(@"C:\Photos\vacation.jpg", isDirectory: false);
        item.Metadata["EmptyMeta"] = "";
        item.Metadata["ValidMeta"] = "SelectedValue";

        // Act
        string resolved = VariableTemplateResolver.Resolve("{Coalesce(EmptyMeta, MissingMeta, ValidMeta, \"Fallback\")}", item);

        // Assert
        resolved.Should().Be("SelectedValue");
    }

    [Fact]
    public void Resolve_WithSubstringOutOfBounds_ShouldHandleSafelyWithoutException()
    {
        // Arrange
        var item = new FileItemContext(@"C:\test.txt", isDirectory: false);
        item.Metadata["ShortText"] = "ABC";

        // Act
        string resultOverLength = VariableTemplateResolver.Resolve("{Substring(ShortText, 0, 100)}", item);
        string resultOverIndex = VariableTemplateResolver.Resolve("{Substring(ShortText, 50, 5)}", item);

        // Assert
        resultOverLength.Should().Be("ABC");
        resultOverIndex.Should().Be(string.Empty);
    }

    [Fact]
    public async Task Resolve_UnderHighParallelLoad_ShouldBeThreadSafeAndDeterministic()
    {
        // Arrange
        var item = new FileItemContext(@"C:\Data\Document.pdf", isDirectory: false);
        item.Metadata["Category"] = "Finance";
        item.Metadata["Counter"] = 42;

        const int iterations = 1000;
        var tasks = new Task<string>[iterations];

        // Act
        for (int i = 0; i < iterations; i++)
        {
            tasks[i] = Task.Run(() => VariableTemplateResolver.Resolve("{FileNameNoExt}_{Category}_{PadLeft(Counter, 4, \"0\")}", item));
        }

        string[] results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(iterations);
        results.Should().AllBeEquivalentTo("Document_Finance_0042");
    }
}
