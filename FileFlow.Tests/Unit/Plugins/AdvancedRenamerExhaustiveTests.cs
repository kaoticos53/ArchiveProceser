using System.IO;
using FluentAssertions;
using FileFlow.Plugin.FileSystem;
using FileFlow.Sdk;
using FileFlow.Sdk.Renaming;
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
        node.Parameters["RenameMode"] = "DirectInPlace";
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
        node.Parameters["RenameMode"] = "DirectInPlace";
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
        node.Parameters["RenameMode"] = "DirectInPlace";
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

    [Fact]
    public async Task AdvancedRenamer_MethodStepsPipeline_ShouldExecuteCorrectly()
    {
        // Arrange
        string sourceFile = Path.Combine(_tempDirectory, "raw_sample_2026.txt");
        await File.WriteAllTextAsync(sourceFile, "contenido");

        var node = new AdvancedRenamerNode();
        node.Parameters["RenameMode"] = "DirectInPlace";
        node.Parameters["PipelineName"] = "Pipeline Limpio";
        node.Parameters["CollisionStrategy"] = "AutoIncrement";
        node.Parameters["MethodSteps"] = FileFlow.Sdk.Renaming.RenamerPresetService.SerializeSteps(
        [
            new FileFlow.Sdk.Renaming.RenameMethodStep
            {
                MethodType = FileFlow.Sdk.Renaming.RenameMethodType.SearchReplace,
                ApplyTo = FileFlow.Sdk.Renaming.ApplyToTarget.NameOnly,
                SearchText = "raw_",
                ReplaceText = "FINAL_",
                IsEnabled = true
            },
            new FileFlow.Sdk.Renaming.RenameMethodStep
            {
                MethodType = FileFlow.Sdk.Renaming.RenameMethodType.CaseConversion,
                ApplyTo = FileFlow.Sdk.Renaming.ApplyToTarget.ExtensionOnly,
                CaseType = FileFlow.Sdk.Renaming.CaseTransformType.Uppercase,
                IsEnabled = true
            }
        ]);

        var item = new FileItemContext(sourceFile);
        var mockContext = new Mock<IFlowExecutionContext>();

        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        item.FileName.Should().Be("FINAL_sample_2026.TXT");
        File.Exists(item.CurrentPath).Should().BeTrue();
    }

    [Fact]
    public void AdvancedRenamer_NewInstance_ShouldHaveDefaultPipelineName_AndNoPatternParameter()
    {
        // Arrange & Act
        var node = new AdvancedRenamerNode();

        // Assert
        node.Parameters.ContainsKey("PipelineName").Should().BeTrue();
        node.Parameters["PipelineName"].Should().Be("Pipeline Predeterminado");
        node.Parameters.ContainsKey("CollisionStrategy").Should().BeTrue();
        node.Parameters.ContainsKey("Pattern").Should().BeFalse();
        node.Parameters.ContainsKey("CaseTransformation").Should().BeFalse();
    }

    [Fact]
    public async Task AdvancedRenamer_LegacyPattern_ShouldBeMigratedAndRemovedFromParameters()
    {
        // Arrange
        string sourceFile = Path.Combine(_tempDirectory, "legacy_file.txt");
        await File.WriteAllTextAsync(sourceFile, "data");

        var node = new AdvancedRenamerNode();
        node.Parameters["RenameMode"] = "DirectInPlace";
        node.Parameters["Pattern"] = "migrated_{FileName}";
        node.Parameters["CaseTransformation"] = "Uppercase";

        var item = new FileItemContext(sourceFile);
        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        item.FileName.Should().Be("MIGRATED_LEGACY_FILE.TXT");
        node.Parameters.ContainsKey("Pattern").Should().BeFalse();
        node.Parameters.ContainsKey("CaseTransformation").Should().BeFalse();
        node.Parameters.ContainsKey("PipelineName").Should().BeTrue();
    }

    [Fact]
    public async Task AdvancedRenamer_InjectedVariables_ShouldBeRecognizedAndTransformed()
    {
        // Arrange
        string sourceFile = Path.Combine(_tempDirectory, "document_draft.pdf");
        await File.WriteAllTextAsync(sourceFile, "dummy");

        var item = new FileItemContext(sourceFile);
        item.Metadata["CustomClient"] = "ACME";
        item.Metadata["ProjectPhase"] = "FINAL";

        var node = new AdvancedRenamerNode();
        node.Parameters["RenameMode"] = "DirectInPlace";
        var steps = new List<RenameMethodStep>
        {
            new()
            {
                MethodType = RenameMethodType.NewName,
                Pattern = "{CustomClient}_{ProjectPhase}_{FileNameNoExt}"
            }
        };
        node.Parameters["MethodSteps"] = RenamerPresetService.SerializeSteps(steps);

        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        item.FileName.Should().Be("ACME_FINAL_document_draft.pdf");
        File.Exists(item.CurrentPath).Should().BeTrue();
    }

    [Fact]
    public async Task AdvancedRenamer_NormalizeNumbers_ShouldPadSequencesAndEpisodes()
    {
        // Arrange
        string file1 = Path.Combine(_tempDirectory, "serie guapa papo 1x2.mov");
        await File.WriteAllTextAsync(file1, "dummy video data");

        var item = new FileItemContext(file1);
        var node = new AdvancedRenamerNode();
        node.Parameters["RenameMode"] = "DirectInPlace";
        var steps = new List<RenameMethodStep>
        {
            new()
            {
                MethodType = RenameMethodType.NormalizeNumbers,
                NumberTarget = NumberPaddingTarget.EpisodeFormat,
                NumberPaddingDigits = 2,
                IsEnabled = true
            }
        };
        node.Parameters["MethodSteps"] = RenamerPresetService.SerializeSteps(steps);

        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        item.FileName.Should().Be("serie guapa papo 1x02.mov");
        File.Exists(item.GetExistingPhysicalPath()).Should().BeTrue();
    }

    [Fact]
    public async Task AdvancedRenamer_VirtualMode_PreservesOriginalFile_AndDestinationSinkCopiesWithNewName()
    {
        // Arrange
        string sourceFile = Path.Combine(_tempDirectory, "DSC_0001.JPG");
        await File.WriteAllTextAsync(sourceFile, "contenido imagen original");
        string destFolder = Path.Combine(_tempDirectory, "OutputSink");

        var renamerNode = new AdvancedRenamerNode();
        renamerNode.Parameters["RenameMode"] = "Virtual";
        renamerNode.Parameters["MethodSteps"] = FileFlow.Sdk.Renaming.RenamerPresetService.SerializeSteps(
        [
            new FileFlow.Sdk.Renaming.RenameMethodStep
            {
                MethodType = FileFlow.Sdk.Renaming.RenameMethodType.NewName,
                ApplyTo = FileFlow.Sdk.Renaming.ApplyToTarget.FullName,
                Pattern = "Vacaciones_2026_{FileNameNoExt}.jpg",
                IsEnabled = true
            }
        ]);

        var sinkNode = new DestinationSinkNode();
        sinkNode.Parameters["DestinationRoot"] = destFolder;
        sinkNode.Parameters["ConflictStrategy"] = "Overwrite";

        var item = new FileItemContext(sourceFile);
        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Returns(Task.CompletedTask);

        // Act 1: Ejecutar Renamer en modo Virtual
        await renamerNode.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert 1: El archivo original sigue existiendo intacto con su nombre original en disco
        File.Exists(sourceFile).Should().BeTrue();
        item.FileName.Should().Be("Vacaciones_2026_DSC_0001.jpg");

        // Act 2: Ejecutar DestinationSink
        await sinkNode.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert 2: El archivo original sigue existiendo intacto y el nuevo archivo fue copiado al destino con el nuevo nombre
        File.Exists(sourceFile).Should().BeTrue();
        string expectedDestFile = Path.Combine(destFolder, "Vacaciones_2026_DSC_0001.jpg");
        File.Exists(expectedDestFile).Should().BeTrue();
        (await File.ReadAllTextAsync(expectedDestFile)).Should().Be("contenido imagen original");
    }

    [Fact]
    public async Task AdvancedRenamer_DirectInPlaceMode_RenamesFileDirectlyOnDisk()
    {
        // Arrange
        string sourceFile = Path.Combine(_tempDirectory, "old_name.txt");
        await File.WriteAllTextAsync(sourceFile, "texto");

        var renamerNode = new AdvancedRenamerNode();
        renamerNode.Parameters["RenameMode"] = "DirectInPlace";
        renamerNode.Parameters["MethodSteps"] = FileFlow.Sdk.Renaming.RenamerPresetService.SerializeSteps(
        [
            new FileFlow.Sdk.Renaming.RenameMethodStep
            {
                MethodType = FileFlow.Sdk.Renaming.RenameMethodType.NewName,
                ApplyTo = FileFlow.Sdk.Renaming.ApplyToTarget.FullName,
                Pattern = "new_name.txt",
                IsEnabled = true
            }
        ]);

        var item = new FileItemContext(sourceFile);
        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Returns(Task.CompletedTask);

        // Act
        await renamerNode.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert: El archivo de origen fue renombrado físicamente in situ
        File.Exists(sourceFile).Should().BeFalse();
        string newPhysicalPath = Path.Combine(_tempDirectory, "new_name.txt");
        File.Exists(newPhysicalPath).Should().BeTrue();
    }
}
