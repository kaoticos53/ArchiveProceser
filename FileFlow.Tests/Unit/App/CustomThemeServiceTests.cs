using System.IO;
using System.Windows;
using System.Windows.Media;
using FileFlow.App.Services;
using FileFlow.Sdk.Themes;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

public class CustomThemeServiceTests : IDisposable
{
    private readonly string _testStoragePath;
    private readonly CustomThemeService _service;

    public CustomThemeServiceTests()
    {
        _testStoragePath = Path.Combine(Path.GetTempPath(), $"custom_themes_test_{Guid.NewGuid():N}.json");
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
    public void GetAllThemes_ShouldIncludeBuiltInThemes()
    {
        // Act
        var themes = _service.GetAllThemes();

        // Assert
        themes.Should().NotBeEmpty();
        themes.Should().Contain(t => t.Id == "dark_fluent" && t.IsBuiltIn);
        themes.Should().Contain(t => t.Id == "light_studio" && t.IsBuiltIn);
        themes.Should().Contain(t => t.Id == "cyber_neon" && t.IsBuiltIn);
        themes.Should().Contain(t => t.Id == "midnight_oled" && t.IsBuiltIn);
        themes.Should().Contain(t => t.Id == "nord_slate" && t.IsBuiltIn);
    }

    [Fact]
    public void SaveCustomTheme_And_GetThemeById_ShouldPersistTheme()
    {
        // Arrange
        var custom = new ThemeDefinition
        {
            Id = "my_custom_theme",
            Name = "Mi Tema Futurista",
            AppBackground = "#112233",
            AccentPrimary = "#FF0055",
            IsDark = true
        };

        // Act
        _service.SaveCustomTheme(custom);
        var retrieved = _service.GetThemeById("my_custom_theme");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Mi Tema Futurista");
        retrieved.AppBackground.Should().Be("#112233");
        retrieved.AccentPrimary.Should().Be("#FF0055");
        retrieved.IsBuiltIn.Should().BeFalse();
    }

    [Fact]
    public void DuplicateTheme_ShouldCreateIndependentCopy()
    {
        // Arrange
        var original = _service.GetThemeById("cyber_neon");
        original.Should().NotBeNull();

        // Act
        var copy = _service.DuplicateTheme(original!, "Cyber Personalizado");

        // Assert
        copy.Id.Should().NotBe(original!.Id);
        copy.Name.Should().Be("Cyber Personalizado");
        copy.IsBuiltIn.Should().BeFalse();
        copy.AccentPrimary.Should().Be(original.AccentPrimary);
    }

    [Fact]
    public void DeleteCustomTheme_ShouldRemoveCustomTheme()
    {
        // Arrange
        var custom = new ThemeDefinition
        {
            Id = "to_delete",
            Name = "Tema a Eliminar"
        };
        _service.SaveCustomTheme(custom);
        _service.GetThemeById("to_delete").Should().NotBeNull();

        // Act
        bool result = _service.DeleteCustomTheme("to_delete");

        // Assert
        result.Should().BeTrue();
        _service.GetThemeById("to_delete").Should().BeNull();
    }

    [Fact]
    public void ExportThemeToJson_And_ImportThemeFromJson_ShouldRoundtripAccurately()
    {
        // Arrange
        var theme = new ThemeDefinition
        {
            Name = "Tema Exportable",
            AccentPrimary = "#00FFAA",
            FontFamily = "Inter, Segoe UI",
            CornerRadius = 12.0
        };

        // Act
        string json = _service.ExportThemeToJson(theme);
        var imported = _service.ImportThemeFromJson(json);

        // Assert
        imported.Name.Should().Be("Tema Exportable");
        imported.AccentPrimary.Should().Be("#00FFAA");
        imported.FontFamily.Should().Be("Inter, Segoe UI");
        imported.CornerRadius.Should().Be(12.0);
    }

    [Fact]
    public void BuildResourceDictionary_ShouldGenerateValidWpfBrushesAndFonts()
    {
        // Arrange
        var theme = new ThemeDefinition
        {
            AppBackground = "#101010",
            AccentPrimary = "#AABBCC",
            WireColorStart = "#FF0000",
            WireColorMid = "#00FF00",
            WireColorEnd = "#0000FF",
            FontFamily = "Segoe UI",
            CodeFontFamily = "Cascadia Code",
            BaseFontSize = 13.5,
            CornerRadius = 8.0,
            NodeShadowBlur = 30.0
        };

        // Act
        var dict = CustomThemeService.BuildResourceDictionary(theme);

        // Assert
        dict.Should().NotBeNull();
        dict.Contains("AppBackgroundBrush").Should().BeTrue();
        dict["AppBackgroundBrush"].Should().BeOfType<SolidColorBrush>();

        dict.Contains("AccentPrimaryBrush").Should().BeTrue();
        dict.Contains("ConnectionWireBrush").Should().BeTrue();
        dict["ConnectionWireBrush"].Should().BeOfType<LinearGradientBrush>();

        dict.Contains("NodeShadowEffect").Should().BeTrue();
        dict.Contains("AppFontFamily").Should().BeTrue();
        dict["AppFontSize"].Should().Be(13.5);
        dict["AppCornerRadius"].Should().Be(new CornerRadius(8.0));
    }
}
