using System.IO;
using FluentAssertions;
using FileFlow.Plugin.FileSystem;
using FileFlow.Sdk;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

public class AdvancedRenamerExhaustiveTests : IDisposable
{
    private readonly string _tempDirectory;

    public AdvancedRenamerExhaustiveTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "FileFlow_RenamerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try { Directory.Delete(_tempDirectory, true); } catch { }
        }
    }

    [Fact]
    public async Task AdvancedRenamer_IllegalCharacters_ShouldBeSanitizedAutomatically()
    {
        // Arrange
        string sourceFile = Path.Combine(_tempDirectory, "documento.txt");
        await File.WriteAllTextAsync(sourceFile, "contenido de prueba");

        var node = new AdvancedRenamerNode();
        node.Parameters["Pattern"] = "Factura:2026/08*test.txt"; // Contiene ':', '/', '*' ilegales en nombres de archivo
        node.Parameters["CaseTransformation"] = "None";

        var item = new FileItemContext(sourceFile);
        var mockContext = new Mock<IFlowExecutionContext>();
        string? emittedPin = null;
        FileItemContext? emittedItem = null;

        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((pin, it) =>
            {
                emittedPin = pin;
                emittedItem = it;
            })
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        emittedPin.Should().Be("Out");
        emittedItem.Should().NotBeNull();
        emittedItem!.FileName.Should().NotContainAny(":", "/", "*");
        emittedItem.FileName.Should().Be("Factura_2026_08_test.txt");
        File.Exists(emittedItem.CurrentPath).Should().BeTrue();
    }

    [Fact]
    public async Task AdvancedRenamer_CaseTransformation_ShouldApplyCorrectly()
    {
        // Arrange
        string sourceFile = Path.Combine(_tempDirectory, "report_FINAL.PDF");
        await File.WriteAllTextAsync(sourceFile, "data");

        var node = new AdvancedRenamerNode();
        node.Parameters["Pattern"] = "{FileName}";
        node.Parameters["CaseTransformation"] = "LOWERCASE";

        var item = new FileItemContext(sourceFile);
        var mockContext = new Mock<IFlowExecutionContext>();

        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        item.FileName.Should().Be("report_final.pdf");
    }

    [Fact]
    public async Task AdvancedRenamer_AutoIncrementStrategy_ShouldResolveCollision()
    {
        // Arrange
        string existingFile = Path.Combine(_tempDirectory, "invoice.txt");
        await File.WriteAllTextAsync(existingFile, "original");

        string sourceFile = Path.Combine(_tempDirectory, "temp_source.txt");
        await File.WriteAllTextAsync(sourceFile, "nuevo contenido");

        var node = new AdvancedRenamerNode();
        node.Parameters["Pattern"] = "invoice.txt";
        node.Parameters["CollisionStrategy"] = "AutoIncrement";

        var item = new FileItemContext(sourceFile);
        var mockContext = new Mock<IFlowExecutionContext>();

        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        item.FileName.Should().Be("invoice_1.txt");
        File.Exists(existingFile).Should().BeTrue();
        File.Exists(item.CurrentPath).Should().BeTrue();
    }
}
