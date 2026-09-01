using System.IO;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Sdk.Themes;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

public class ThemeCustomizerViewModelTests : IDisposable
{
    private readonly string _testStoragePath;
    private readonly CustomThemeService _service;

    public ThemeCustomizerViewModelTests()
    {
        _testStoragePath = Path.Combine(Path.GetTempPath(), $"theme_vm_test_{Guid.NewGuid():N}.json");
        _service = new CustomThemeService(_testStoragePath);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_testStoragePath))
            {
                File.Delete(_testStoragePath);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public void Constructor_ShouldInitializeAvailableThemes_AndSelectDefault()
    {
        // Act
        var vm = new ThemeCustomizerViewModel(_service);

        // Assert
        vm.AvailableThemes.Should().NotBeEmpty();
        vm.SelectedTheme.Should().NotBeNull();
        vm.EditingTheme.Should().NotBeNull();
        vm.LivePreviewResources.Should().NotBeNull();
        vm.AvailableFontFamilies.Should().NotBeEmpty();
        vm.AvailableCodeFonts.Should().NotBeEmpty();
    }

    [Fact]
    public void NewCustomTheme_ShouldAddAndSelectNewTheme()
    {
        // Arrange
        var vm = new ThemeCustomizerViewModel(_service);
        int initialCount = vm.AvailableThemes.Count;

        // Act
        vm.NewCustomTheme();

        // Assert
        vm.AvailableThemes.Count.Should().Be(initialCount + 1);
        vm.SelectedTheme.Should().NotBeNull();
        vm.SelectedTheme!.IsBuiltIn.Should().BeFalse();
        vm.IsCustomTheme.Should().BeTrue();
    }

    [Fact]
    public void DuplicateTheme_ShouldAddDuplicatedTheme()
    {
        // Arrange
        var vm = new ThemeCustomizerViewModel(_service);
        int initialCount = vm.AvailableThemes.Count;

        // Act
        vm.DuplicateTheme();

        // Assert
        vm.AvailableThemes.Count.Should().Be(initialCount + 1);
        vm.SelectedTheme!.Name.Should().Contain("Copia");
        vm.IsCustomTheme.Should().BeTrue();
    }

    [Fact]
    public void SaveCustomTheme_ShouldPersistEditingThemeChanges()
    {
        // Arrange
        var vm = new ThemeCustomizerViewModel(_service);
        vm.NewCustomTheme();
        vm.EditingTheme.Name = "Tema Renombrado";
        vm.EditingTheme.AccentPrimary = "#ABCDEF";

        // Act
        vm.SaveCustomTheme();

        // Assert
        var saved = _service.GetThemeById(vm.EditingTheme.Id);
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("Tema Renombrado");
        saved.AccentPrimary.Should().Be("#ABCDEF");
    }

    [Fact]
    public void UpdateLivePreview_ShouldRegenerateLivePreviewResources()
    {
        // Arrange
        var vm = new ThemeCustomizerViewModel(_service);
        vm.EditingTheme.AppBackground = "#123456";

        // Act
        vm.UpdateLivePreview();

        // Assert
        vm.LivePreviewResources.Should().NotBeNull();
        vm.LivePreviewResources.Contains("AppBackgroundBrush").Should().BeTrue();
    }

    [Fact]
    public void AvailableThemes_ShouldIncludeBothBuiltInAndCustomThemes_WhenNewThemeSaved()
    {
        // Arrange
        var vm = new ThemeCustomizerViewModel(_service);
        vm.NewCustomTheme();
        vm.EditingTheme.Name = "Tema Personalizado de Prueba";
        vm.SaveCustomTheme();

        // Act
        var all = _service.GetAllThemes();

        // Assert
        all.Should().Contain(t => t.Id == "dark_fluent");
        all.Should().Contain(t => t.Name == "Tema Personalizado de Prueba" && !t.IsBuiltIn);
    }
}
