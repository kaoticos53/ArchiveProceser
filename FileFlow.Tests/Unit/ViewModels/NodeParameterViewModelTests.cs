using System.Resources;
using FileFlow.App.ViewModels;
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
}
