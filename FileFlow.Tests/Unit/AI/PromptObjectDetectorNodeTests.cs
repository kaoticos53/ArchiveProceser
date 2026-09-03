using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Plugin.AI;
using FileFlow.Sdk;
using FluentAssertions;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.AI;

public class PromptObjectDetectorNodeTests
{
    [Fact]
    public async Task PromptTranslator_TranslateToEnglishAsync_ShouldTranslateCompoundPhrases()
    {
        // Arrange
        string inputSpanish = "gafas de sol, perro marrón, coche rojo, taza de café";

        // Act
        string result = await PromptTranslator.TranslateToEnglishAsync(inputSpanish);

        // Assert
        result.Should().Contain("sunglasses");
        result.Should().Contain("brown dog");
        result.Should().Contain("red car");
        result.Should().Contain("coffee cup");
    }

    [Fact]
    public async Task PromptTranslator_TranslateToEnglishAsync_ShouldStripArticlesAndHandlePlurals()
    {
        // Arrange
        string inputSpanish = "el perro, una bicicleta, los coches, las gafas de sol";

        // Act
        string result = await PromptTranslator.TranslateToEnglishAsync(inputSpanish);

        // Assert
        result.Should().Contain("dog");
        result.Should().Contain("bicycle");
        result.Should().Contain("cars");
        result.Should().Contain("sunglasses");
    }

    [Fact]
    public async Task PromptTranslator_TranslateToEnglishAsync_ShouldHandleSentencesAndConjunctions()
    {
        // Arrange
        string input = "detecta un coche rojo y un perro marrón o un gato blanco";

        // Act
        string result = await PromptTranslator.TranslateToEnglishAsync(input);

        // Assert
        result.Should().Contain("red car");
        result.Should().Contain("brown dog");
        result.Should().Contain("white cat");
    }

    [Fact]
    public async Task PromptTranslator_TranslateToEnglishAsync_ShouldHandleAccentsAndCompoundTechObjects()
    {
        // Arrange
        string input = "teléfono móvil, árbol de navidad, ordenador portátil, cámara de fotos";

        // Act
        string result = await PromptTranslator.TranslateToEnglishAsync(input);

        // Assert
        result.Should().Contain("cell phone");
        result.Should().Contain("christmas tree");
        result.Should().Contain("laptop");
        result.Should().Contain("camera");
    }

    [Fact]
    public void PromptObjectDetectorNode_ShouldHaveValidPortsAndParameters()
    {
        // Arrange & Act
        var node = new PromptObjectDetectorNode();

        // Assert
        node.Category.Should().Be("ImageVision");
        node.Inputs.Should().ContainSingle(p => p.Name == "In");
        node.Outputs.Should().Contain(p => p.Name == "ObjectsFound");
        node.Outputs.Should().Contain(p => p.Name == "NoObjects");
        node.Outputs.Should().Contain(p => p.Name == "Error");

        node.Parameters.Should().ContainKey("Prompt");
        node.Parameters.Should().ContainKey("MinimumConfidence");
        node.Parameters.Should().ContainKey("AutoTranslateToEnglish");
        node.Parameters.Should().ContainKey("MaxDetections");
    }

    [Fact]
    public async Task PromptObjectDetectorNode_ExecuteAsync_NonExistentFile_ShouldEmitToError()
    {
        // Arrange
        var node = new PromptObjectDetectorNode();
        var item = new FileItemContext(@"C:\FakePath\non_existent_image.jpg");
        var mockContext = new Mock<IFlowExecutionContext>();

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("Error", item), Times.Once);
    }

    [Fact]
    public async Task PromptObjectDetectorNode_ExecuteAsync_NonImageFile_ShouldEmitToNoObjects()
    {
        // Arrange
        var node = new PromptObjectDetectorNode();
        string tempFile = Path.Combine(Path.GetTempPath(), $"temp_test_{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(tempFile, "This is a text document, not an image.");

        try
        {
            var item = new FileItemContext(tempFile);
            var mockContext = new Mock<IFlowExecutionContext>();

            // Act
            await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

            // Assert
            mockContext.Verify(c => c.EmitAsync("NoObjects", item), Times.Once);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
