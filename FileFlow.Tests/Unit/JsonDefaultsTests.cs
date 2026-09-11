using System.IO;
using System.Text.Json;
using FileFlow.Core.Engine;
using FileFlow.Sdk;
using FileFlow.Sdk.Serialization;
using FileFlow.Sdk.Telemetry;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit;

public class JsonDefaultsTests
{
    [Fact]
    public void UnescapeUnicode_WithEscapedBackticksAndQuotes_ShouldUnescapeAccurately()
    {
        string input = @"\u0060\u0060\u0060json\n{\n  \u0022categoria\u0022: \u0022Fotografia_Paisaje\u0022\n}\n\u0060\u0060\u0060";
        string result = JsonDefaults.UnescapeUnicode(input);

        result.Should().Contain("```json");
        result.Should().Contain("\"categoria\": \"Fotografia_Paisaje\"");
        result.Should().NotContain(@"\u0060");
        result.Should().NotContain(@"\u0022");
    }

    [Fact]
    public void UnescapeUnicode_WithSpanishAccentedCharacters_ShouldDecodeProperly()
    {
        string input = @"Validaci\u00F3n de im\u00E1genes y dise\u00F1o";
        string result = JsonDefaults.UnescapeUnicode(input);

        result.Should().Be("Validación de imágenes y diseño");
    }

    [Fact]
    public void UnescapeUnicode_WithWindowsPaths_ShouldPreservePathIntact()
    {
        string path = @"C:\Users\kaoti\Documents\GitHub\ArchiveProceser\file.txt";
        string result = JsonDefaults.UnescapeUnicode(path);

        result.Should().Be(path);
    }

    [Fact]
    public void SerializeRelaxed_ShouldNotEscapeBackticksOrQuotesOrAccents()
    {
        var model = new
        {
            code = "```json\n{\n  \"categoria\": \"Fotografia_Paisaje\"\n}\n```",
            description = "Descripción con acentos y símbolo +",
            math = "a < b && c > d"
        };

        string json = JsonDefaults.SerializeRelaxed(model, indented: true);

        json.Should().NotContain(@"\u0060");
        json.Should().NotContain(@"\u0022");
        json.Should().NotContain(@"\u002B");
        json.Should().NotContain(@"\u003C");
        json.Should().NotContain(@"\u003E");
        json.Should().Contain("```json");
        json.Should().Contain("Descripción con acentos y símbolo +");
        json.Should().Contain("<");
        json.Should().Contain(">");
    }

    [Fact]
    public void FormatDetailsForDisplay_WithEscapedJsonPayload_ShouldFormatAndUnescape()
    {
        string rawJson = "{\"AI:VlmResponse\":\"\\u0060\\u0060\\u0060json\\n{\\n  \\u0022categoria\\u0022: \\u0022Fotografia_Paisaje\\u0022\\n}\\n\\u0060\\u0060\\u0060\"}";

        string formatted = JsonDefaults.FormatDetailsForDisplay(rawJson);

        formatted.Should().Contain("```json");
        formatted.Should().NotContain(@"\u0060");
        formatted.Should().NotContain(@"\u0022");
        formatted.Should().Contain("\"AI:VlmResponse\"");
    }

    [Fact]
    public void StructuredLogRecord_DisplayDetails_ShouldProvideFormattedUnescapedText()
    {
        string rawDetails = "{\"AI:VlmResponse\":\"\\u0060\\u0060\\u0060json\\n{\\n  \\u0022categoria\\u0022: \\u0022Fotografia_Paisaje\\u0022\\n}\\n\\u0060\\u0060\\u0060\"}";
        var record = StructuredLogRecord.Create(
            executionId: "exec-1",
            level: LogLevel.Information,
            message: "Inferencia completada",
            detailsJson: rawDetails
        );

        record.DisplayDetails.Should().Contain("```json");
        record.DisplayDetails.Should().NotContain(@"\u0060");
        record.DisplayDetails.Should().NotContain(@"\u0022");
    }

    [Fact]
    public void WorkflowExecutionContext_Log_WithMetadata_ShouldSerializeWithoutEscapingBackticksOrQuotes()
    {
        var executor = new WorkflowExecutor();
        var item = new FileItemContext(@"C:\Photos\Landscape.jpg");
        item.Metadata["AI:VlmResponse"] = "```json\n{\n  \"categoria\": \"Fotografia_Paisaje\"\n}\n```";
        item.Metadata["AI:VlmCategory"] = "Fotografía";

        StructuredLogRecord? emittedRecord = null;
        executor.StructuredLogEmitted += record => emittedRecord = record;

        var context = new WorkflowExecutionContext("VlmNode", executor, CancellationToken.None, item);
        context.Log("Inferencia VLM completada", LogLevel.Information, item);

        emittedRecord.Should().NotBeNull();
        emittedRecord!.DetailsJson.Should().NotBeNull();
        emittedRecord.DetailsJson.Should().NotContain(@"\u0060");
        emittedRecord.DetailsJson.Should().NotContain(@"\u0022");
        emittedRecord.DetailsJson.Should().Contain("```json");
        emittedRecord.DetailsJson.Should().Contain("Fotografía");
        emittedRecord.DisplayDetails.Should().Contain("```json");
    }
}
