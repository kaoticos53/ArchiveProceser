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

public class SecurityAndSemanticNodesTests : IDisposable
{
    private readonly string _tempDir;

    public SecurityAndSemanticNodesTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FileFlow_SecurityTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    [Fact]
    public void PiiAnonymizerNode_ShouldHaveValidPortsAndParameters()
    {
        // Arrange & Act
        var node = new PiiAnonymizerNode();

        // Assert
        node.Inputs.Should().ContainSingle(p => p.Name == "In");
        node.Outputs.Select(p => p.Name).Should().Contain(["Clean", "SensitiveFound", "Out", "Error"]);

        node.Parameters.Should().ContainKey("Model");
        node.Parameters["Model"].Should().Be("Auto");
        node.Parameters.Should().ContainKey("AnonymizationMode");
        node.Parameters["AnonymizationMode"].Should().Be("TagReplacement");
        node.Parameters.Should().ContainKey("FilterDniNie");
        node.Parameters.Should().ContainKey("FilterIban");
        node.Parameters.Should().ContainKey("FilterCreditCards");
        node.Parameters.Should().ContainKey("FilterEmails");
        node.Parameters.Should().ContainKey("FilterPhones");
        node.Parameters.Should().ContainKey("FilterIpAddresses");
        node.Parameters.Should().ContainKey("FilterPersonNames");
        node.Parameters.Should().ContainKey("OutputDirectory");

        var modeDesc = node.ParameterDescriptors.FirstOrDefault(d => d.Key == "AnonymizationMode");
        modeDesc.Should().NotBeNull();
        modeDesc!.Options.Should().Contain(["TagReplacement", "Mask", "Hash", "Remove"]);
    }

    [Fact]
    public void ZeroShotSemanticSearchNode_ShouldHaveValidPortsAndParameters()
    {
        // Arrange & Act
        var node = new ZeroShotSemanticSearchNode();

        // Assert
        node.Inputs.Should().ContainSingle(p => p.Name == "In");
        node.Outputs.Select(p => p.Name).Should().Contain(["Matched", "Unmatched", "Out", "Error"]);

        node.Parameters.Should().ContainKey("Model");
        node.Parameters["Model"].Should().Be("Auto");
        node.Parameters.Should().ContainKey("SearchQuery");
        node.Parameters.Should().ContainKey("CandidateLabels");
        node.Parameters.Should().ContainKey("SimilarityThreshold");
        node.Parameters["SimilarityThreshold"].Should().Be(0.55);
        node.Parameters.Should().ContainKey("TopK");
        node.Parameters["TopK"].Should().Be(3);

        var modelDesc = node.ParameterDescriptors.FirstOrDefault(d => d.Key == "Model");
        modelDesc.Should().NotBeNull();
        modelDesc!.Options.Should().Contain(["Auto", "clip-vit-b32", "bge-small-multilingual", "Custom"]);
    }

    [Fact]
    public void Catalog_ShouldContainSecurityAndSemanticModels()
    {
        // Act & Assert
        var piiModels = AiModelManager.GetModelsForTask(AiTaskType.PiiAnonymization);
        piiModels.Should().NotBeEmpty();
        piiModels.Select(m => m.Id).Should().Contain("pii-ner-multilingual");

        var semanticModels = AiModelManager.GetModelsForTask(AiTaskType.SemanticEmbeddings);
        semanticModels.Should().NotBeEmpty();
        semanticModels.Select(m => m.Id).Should().Contain(["clip-vit-b32", "bge-small-multilingual"]);
    }

    [Theory]
    [InlineData(AiTaskType.PiiAnonymization)]
    [InlineData(AiTaskType.SemanticEmbeddings)]
    public void HardwareCapabilityDetector_ShouldSelectOptimalModelForSecurityAndSemanticTasks(AiTaskType task)
    {
        // Act
        var model = HardwareCapabilityDetector.GetOptimalModelForTask(task);

        // Assert
        model.Should().NotBeNull();
        model.TaskType.Should().Be(task);
        model.FileName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void PiiDetectionEngine_AnonymizeText_ShouldDetectAndMaskPersonalData()
    {
        // Arrange
        string sampleText = "El usuario Juan Pérez con DNI 12345678Z, email juan@empresa.com y teléfono 612345678 firmó el contrato.";
        var options = new PiiOptions(Mode: "TagReplacement");

        // Act
        var result = PiiDetectionEngine.AnonymizeText(sampleText, options);

        // Assert
        result.PiiDetected.Should().BeTrue();
        result.TotalCount.Should().BeGreaterThanOrEqualTo(2);
        result.Categories.Should().Contain(["DNI/NIE", "Email"]);
        result.SanitizedText.Should().Contain("[DNI/NIE]");
        result.SanitizedText.Should().Contain("[EMAIL]");
        result.SanitizedText.Should().NotContain("12345678Z");
        result.SanitizedText.Should().NotContain("juan@empresa.com");
    }

    [Fact]
    public void PiiDetectionEngine_AnonymizeText_WithCleanText_ShouldReturnNoPii()
    {
        // Arrange
        string cleanText = "El informe mensual de rendimiento indica una mejora del 15% en los procesos batch.";
        var options = new PiiOptions();

        // Act
        var result = PiiDetectionEngine.AnonymizeText(cleanText, options);

        // Assert
        result.PiiDetected.Should().BeFalse();
        result.TotalCount.Should().Be(0);
        result.SanitizedText.Should().Be(cleanText);
    }

    [Fact]
    public void SemanticEmbeddingEngine_ClassifyZeroShot_ShouldRankMatchingCategoryHighest()
    {
        // Arrange
        string invoiceText = "Factura de suministro eléctrico, total a pagar: 145.20 EUR con IVA desglosado.";
        var categories = new[] { "Factura", "Contrato de Trabajo", "Receta Médica", "Fotografía" };

        // Act
        var result = SemanticEmbeddingEngine.ClassifyZeroShot(null, invoiceText, categories, "factura de electricidad");

        // Assert
        result.TopCategory.Should().Be("Factura");
        result.TopScore.Should().BeGreaterThan(0.0);
        result.CategoryScores.Should().ContainKey("Factura");
    }

    [Fact]
    public async Task PiiAnonymizerNode_ExecuteAsync_WithSensitiveData_ShouldEmitSensitiveFound()
    {
        // Arrange
        string filePath = Path.Combine(_tempDir, "contract.txt");
        await File.WriteAllTextAsync(filePath, "Contacto: contacto@empresa.com, DNI: 12345678Z");
        var node = new PiiAnonymizerNode();
        var item = new FileItemContext(filePath);
        var mockContext = new Mock<IFlowExecutionContext>();

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("SensitiveFound", It.IsAny<FileItemContext>()), Times.Once);
        mockContext.Verify(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()), Times.Once);
    }

    [Fact]
    public async Task PiiAnonymizerNode_ExecuteAsync_WithCleanData_ShouldEmitClean()
    {
        // Arrange
        string filePath = Path.Combine(_tempDir, "clean.txt");
        await File.WriteAllTextAsync(filePath, "Registro de sistema sin datos personales.");
        var node = new PiiAnonymizerNode();
        var item = new FileItemContext(filePath);
        var mockContext = new Mock<IFlowExecutionContext>();

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("Clean", It.IsAny<FileItemContext>()), Times.Once);
        mockContext.Verify(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()), Times.Once);
    }

    [Fact]
    public async Task ZeroShotSemanticSearchNode_ExecuteAsync_WithMatchingQuery_ShouldEmitMatched()
    {
        // Arrange
        string filePath = Path.Combine(_tempDir, "invoice.txt");
        await File.WriteAllTextAsync(filePath, "Factura de venta y recibo de honorarios número 2026-44.");
        var node = new ZeroShotSemanticSearchNode();
        node.Parameters["SearchQuery"] = "Factura de venta";
        node.Parameters["CandidateLabels"] = "Factura, Contrato, Nómina";
        node.Parameters["SimilarityThreshold"] = 0.3;

        var item = new FileItemContext(filePath);
        var mockContext = new Mock<IFlowExecutionContext>();

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("Matched", item), Times.Once);
        mockContext.Verify(c => c.EmitAsync("Out", item), Times.Once);
    }
}
