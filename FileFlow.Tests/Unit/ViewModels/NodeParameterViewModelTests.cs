using System.Resources;
using FileFlow.App.ViewModels;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.ViewModels;

[Collection("Localization")]
public class NodeParameterViewModelTests
{
    [Fact]
    public void DisplayName_ShouldReturnFormattedFallback_WhenNoResourceManagerRegistered()
    {
        // Arrange
        using var param = new NodeParameterViewModel("TargetFormat", "WebP");

        // Act & Assert
        param.DisplayName.Should().Be("Target Format");
    }

    [Fact]
    public void DisplayName_ShouldUpdateReactively_WhenCultureChanges()
    {
        // Arrange
        var resourceManager = new ResourceManager("FileFlow.App.Resources.Strings", typeof(FileFlow.App.App).Assembly);
        LocalizationManager.Instance.RegisterResourceManager(resourceManager);

        LocalizationManager.Instance.SetCulture("es-ES");
        using var param = new NodeParameterViewModel("Width", 1920);

        // Act - En español
        string nameEs = param.DisplayName;

        // Cambiar a inglés
        LocalizationManager.Instance.SetCulture("en-US");
        string nameEn = param.DisplayName;

        // Assert
        nameEs.Should().Be("Ancho");
        nameEn.Should().Be("Width");

        // Reset
        LocalizationManager.Instance.SetCulture("es-ES");
    }

    [Fact]
    public void EvaluatedValue_ShouldDetectExpression_WhenBracesOrTagsPresent()
    {
        // Arrange & Act
        using var paramPlain = new NodeParameterViewModel("Destination", @"C:\Output\StaticFolder");
        using var paramExpr = new NodeParameterViewModel("Destination", @"{RelativeDir}\Output");
        using var paramTag = new NodeParameterViewModel("Destination", @"<FileName>_backup");

        // Assert
        paramPlain.HasExpression.Should().BeFalse();
        paramPlain.EvaluatedValue.Should().Be(@"C:\Output\StaticFolder");

        paramExpr.HasExpression.Should().BeTrue();
        paramTag.HasExpression.Should().BeTrue();
    }

    [Fact]
    public void EvaluatedValue_ShouldResolveWithFileContext_WhenContextUpdated()
    {
        // Arrange
        using var param = new NodeParameterViewModel("Destination", @"{SourceDir}\Output_{Year}\{FileName}");
        var item = new FileItemContext(@"C:\Photos\Album\image.png");

        // Act
        param.UpdateEvaluationContext(item);

        // Assert
        param.HasExpression.Should().BeTrue();
        param.EvaluatedValue.Should().Be($@"C:\Photos\Album\Output_{DateTime.Now.Year}\image.png");
    }

    [Fact]
    public void EvaluatedValue_ShouldRecalculate_WhenValueChanged()
    {
        // Arrange
        using var param = new NodeParameterViewModel("Prefix", "PlainPrefix");
        var item = new FileItemContext(@"C:\Data\file.txt");
        item.Metadata["CustomTag"] = "XYZ";
        param.UpdateEvaluationContext(item);

        param.HasExpression.Should().BeFalse();
        param.EvaluatedValue.Should().Be("PlainPrefix");

        // Act - Cambiar valor a plantilla con metadato
        param.Value = "Prefix_{CustomTag}_{FileName}";

        // Assert
        param.HasExpression.Should().BeTrue();
        param.EvaluatedValue.Should().Be("Prefix_XYZ_file.txt");
    }
}
