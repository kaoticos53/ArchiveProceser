using System.Diagnostics;
using FileFlow.Sdk;
using FileFlow.Sdk.TemplateEngine;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Performance;

public class PerformanceStressTests
{
    [Fact]
    public void Resolve_ShouldEvaluate10000ItemsUnder1000Milliseconds()
    {
        // Arrange
        const int itemQuantity = 10_000;
        var items = new List<FileItemContext>(itemQuantity);

        for (int i = 0; i < itemQuantity; i++)
        {
            var item = new FileItemContext($@"C:\Photos\Batch_{i}\image_{i}.jpg", isDirectory: false);
            item.Metadata["SourceRootPath"] = @"C:\Photos";
            item.Metadata["DateTaken"] = "2026-08-20 12:00:00";
            item.Metadata["Counter"] = i;
            items.Add(item);
        }

        string template = @"C:\Output\{Year(DateTaken)}/Folder_{PadLeft(Counter, 4, ""0"")}/{RelativePath}/{FileNameNoExt}.{Extension}";

        // Act
        var sw = Stopwatch.StartNew();
        foreach (var item in items)
        {
            _ = VariableTemplateResolver.Resolve(template, item);
        }
        sw.Stop();

        // Assert
        sw.ElapsedMilliseconds.Should().BeLessThan(1000, "10,000 template resolutions should complete in less than 1 second");
    }
}
