using System.Linq;
using Point = FileFlow.Sdk.Point;
using FileFlow.App.ViewModels;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Images;
using FileFlow.Sdk;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

public class NodeParameterManagerTests
{
    [Fact]
    public void InitializeParameters_ShouldUseParameterDescriptors_WhenNodeDefinesThem()
    {
        // Arrange: ImageOptimizerNode define ParameterDescriptors (Width 1º, Height 2º, TargetFormat 3º, Quality 4º...)
        var node = new ImageOptimizerNode();
        using var nodeVm = new NodeViewModel(node, new Point(0, 0));

        // Act
        var paramsList = nodeVm.Parameters.ToList();

        // Assert
        paramsList.Should().NotBeEmpty();
        paramsList[0].Key.Should().Be("Width");
        paramsList[0].Value.Should().Be("");

        paramsList[1].Key.Should().Be("Height");
        paramsList[1].Value.Should().Be("100%");

        paramsList[2].Key.Should().Be("TargetFormat");
        paramsList[2].IsDropdown.Should().BeTrue();
        paramsList[2].Options.Should().Contain("WebP");

        paramsList[3].Key.Should().Be("Quality");
        paramsList[3].IsSlider.Should().BeTrue();
        paramsList[3].SliderMin.Should().Be(1);
        paramsList[3].SliderMax.Should().Be(100);
    }

    [Fact]
    public void InitializeParameters_ShouldCorrectlyIdentifyFolderAndDropdownTypes_FromDescriptors()
    {
        // Arrange
        var node = new FolderSourceNode();
        using var nodeVm = new NodeViewModel(node, new Point(0, 0));

        // Act
        var sourcePathParam = nodeVm.Parameters.FirstOrDefault(p => p.Key == "SourcePath");
        var emitModeParam = nodeVm.Parameters.FirstOrDefault(p => p.Key == "EmitMode");
        var recursiveParam = nodeVm.Parameters.FirstOrDefault(p => p.Key == "Recursive");

        // Assert
        sourcePathParam.Should().NotBeNull();
        sourcePathParam!.IsFolderPath.Should().BeTrue();
        sourcePathParam.HasBrowseButton.Should().BeTrue();

        emitModeParam.Should().NotBeNull();
        emitModeParam!.IsDropdown.Should().BeTrue();
        emitModeParam.Options.Should().Equal(["FilesOnly", "DirectoriesOnly", "FilesAndDirectories"]);

        recursiveParam.Should().NotBeNull();
        recursiveParam!.IsBooleanAndNoOptions.Should().BeTrue();
    }

    [Fact]
    public void InitializeParameters_ShouldNotExposeLegacyPatternOrMethodSteps_ForAdvancedRenamerNode()
    {
        // Arrange
        var node = new AdvancedRenamerNode();
        // Simular presencia de parámetros legados e internos
        node.Parameters["Pattern"] = "{ParentDir}_{FileName}";
        node.Parameters["NameTemplate"] = "{FileName}";
        node.Parameters["CaseTransformation"] = "Uppercase";
        node.Parameters["MethodSteps"] = "[{}]";

        using var nodeVm = new NodeViewModel(node, new Point(0, 0));

        // Act
        var paramKeys = nodeVm.Parameters.Select(p => p.Key).ToList();

        // Assert: Solo deben exponerse los descriptores oficiales (PipelineName, RenameMode, CollisionStrategy)
        paramKeys.Should().Contain("PipelineName");
        paramKeys.Should().Contain("RenameMode");
        paramKeys.Should().Contain("CollisionStrategy");
        paramKeys.Should().NotContain("Pattern");
        paramKeys.Should().NotContain("NameTemplate");
        paramKeys.Should().NotContain("CaseTransformation");
        paramKeys.Should().NotContain("MethodSteps");
    }

    [Fact]
    public void NodeViewModel_ShouldPopulateCustomActions_FromNodeDefinition()
    {
        // Arrange
        var renamerNode = new AdvancedRenamerNode();
        using var renamerVm = new NodeViewModel(renamerNode, new Point(0, 0));

        var varInjectorNode = new VariableInjectorNode();
        using var varInjectorVm = new NodeViewModel(varInjectorNode, new Point(0, 0));

        // Act & Assert
        renamerVm.CustomActions.Should().HaveCount(1);
        renamerVm.CustomActions[0].ActionId.Should().Be("OpenRenamerPipeline");
        renamerVm.CustomActions[0].Title.Should().Contain("Pipeline");

        varInjectorVm.CustomActions.Should().HaveCount(1);
        varInjectorVm.CustomActions[0].ActionId.Should().Be("AddVariable");
    }

    [Fact]
    public void SyncParametersFromNodeInstance_ShouldUpdateParameterValuesAndOptions()
    {
        // Arrange
        var renamerNode = new AdvancedRenamerNode();
        using var renamerVm = new NodeViewModel(renamerNode, new Point(0, 0));

        var pipelineParam = renamerVm.Parameters.First(p => p.Key == "PipelineName");
        pipelineParam.Value.Should().Be("Pipeline Predeterminado");

        // Act - Mutate parameters in node
        renamerNode.Parameters["PipelineName"] = "0️⃣1️⃣ Rellenar Números (1, 2... 10 -> 01, 02... 10)";
        renamerVm.SyncParametersFromNodeInstance();

        // Assert
        pipelineParam.Value.Should().Be("0️⃣1️⃣ Rellenar Números (1, 2... 10 -> 01, 02... 10)");
    }

    /// <summary>
    /// OBJETO: La edición del parámetro por el VM ESCRIBE al NodeInstance (el write-through).
    /// QUÉ:    Asignar p.Value (el mismo setter que la ficha usa al editar) tiene que terminar en
    ///         NodeInstance.Parameters — es lo que el motor ejecuta y lo que el flujo guarda. Es el
    ///         testigo de la mutación inspector-sin-write-back (rebanada 4 del host Uno): si la cadena
    ///         p.Value -> OnValueChanged -> OnParameterValueChanged -> NodeParameterManager se corta,
    ///         el VM mantiene su valor observable pero el nodo jamás se entera.
    /// CÓMO:  Crea el ImageOptimizerNode, toma el parámetro Quality del VM, edita por el setter y
    ///         exige el valor nuevo leído del diccionario del NodeInstance (el mismo camino que el
    ///         selfcheck del host Uno verifica en runtime con 'Width' = '__probe__').
    /// </summary>
    [Fact]
    public void EditingParameterThroughTheViewModel_ShouldWriteThroughToTheNodeInstance()
    {
        // Arrange
        var node = new ImageOptimizerNode();
        using var nodeVm = new NodeViewModel(node, new Point(0, 0));

        var quality = nodeVm.Parameters.First(p => p.Key == "Quality");
        object? before = node.Parameters.TryGetValue("Quality", out var v0) ? v0 : null;
        before.Should().NotBeNull("el nodo inicializa Quality en su constructor");

        // Act - la edición del usuario (el setter del VM, el mismo que el panel Uno invoca)
        quality.Value = 42;

        // Assert - el valor nuevo vive en el nodo (no sólo en el VM)
        node.Parameters.TryGetValue("Quality", out var v1).Should().BeTrue();
        v1?.ToString().Should().Be("42",
            "la edición del parámetro tiene que escribir al NodeInstance: es lo que ejecuta el motor " +
            "y lo que guarda el flujo — un write-back cortado dejaría el nodo con valores viejos");
    }
}
