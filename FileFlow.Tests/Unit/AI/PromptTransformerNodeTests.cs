using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Plugin.AI;
using FileFlow.Sdk;
using FluentAssertions;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.AI;

public class PromptTransformerNodeTests
{
    [Fact]
    public async Task ExecuteAsync_WithMetadataVariables_ShouldEvaluateAndTranslatePrompt()
    {
        // Arrange
        var item = new FileItemContext("imagen.jpg")
        {
            Metadata =
            {
                ["AI:Category"] = "Vehículos",
                ["UserTag"] = "deportivo"
            }
        };
        var mockContext = new Mock<IFlowExecutionContext>();

        var node = new PromptTransformerNode();
        node.Parameters["PromptTemplate"] = "{AI:Category}, gafas de sol, {UserTag}, coche rojo";
        node.Parameters["TargetLanguage"] = "English";
        node.Parameters["ExpandSynonyms"] = false;

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("Transformed", item), Times.Once);
        item.Metadata.Should().ContainKey("AI:EvaluatedPrompt");
        item.Metadata.Should().ContainKey("AI:TranslatedPrompt");

        string evaluated = item.Metadata["AI:EvaluatedPrompt"]?.ToString() ?? string.Empty;
        evaluated.Should().Contain("Vehículos");
        evaluated.Should().Contain("deportivo");

        string translated = item.Metadata["AI:TranslatedPrompt"]?.ToString() ?? string.Empty;
        translated.Should().Contain("sunglasses");
        translated.Should().Contain("red car");
    }

    [Fact]
    public async Task ExecuteAsync_WithExpandSynonyms_ShouldAddVisualSynonyms()
    {
        // Arrange
        var item = new FileItemContext("test.jpg");
        var mockContext = new Mock<IFlowExecutionContext>();

        var node = new PromptTransformerNode();
        node.Parameters["PromptTemplate"] = "coche, perro";
        node.Parameters["TargetLanguage"] = "English";
        node.Parameters["ExpandSynonyms"] = true;

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("Transformed", item), Times.Once);
        string translated = item.Metadata["AI:TranslatedPrompt"]?.ToString() ?? string.Empty;

        translated.Should().Contain("car");
        translated.Should().Contain("vehicle");
        translated.Should().Contain("dog");
        translated.Should().Contain("canine");
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateIsEmpty_ShouldEmitError()
    {
        // Arrange
        var item = new FileItemContext("test.jpg");
        var mockContext = new Mock<IFlowExecutionContext>();

        var node = new PromptTransformerNode();
        node.Parameters["PromptTemplate"] = "";

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("Error", item), Times.Once);
    }
}
