using System.IO;
using FileFlow.App.Services;
using FileFlow.Core.Engine;
using FileFlow.Sdk;
using FileFlow.Sdk.Renaming;
using FileFlow.Sdk.Renaming.Handlers;
using FileFlow.Sdk.Themes;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Refactoring;

public class ModularRefactoringComponentsTests
{
    [Fact]
    public void BuiltInThemesCatalog_ShouldReturnEightFactoryThemes()
    {
        // Act
        var themes = BuiltInThemesCatalog.GetThemes();

        // Assert
        themes.Should().NotBeNull();
        themes.Should().HaveCount(8);
        themes.Select(t => t.Id).Should().Contain(["dark_fluent", "light_studio", "cyber_neon", "pastel_spring", "midnight_oled", "nord_slate", "dracula_purple", "emerald_forest"]);
    }

    [Fact]
    public void ThemeResourceApplier_ShouldGenerateWpfResources()
    {
        // Arrange
        var theme = new ThemeDefinition
        {
            Id = "test_theme",
            Name = "Test Theme",
            AppBackground = "#123456",
            AccentPrimary = "#654321",
            FontFamily = "Arial"
        };

        // Act
        var resources = ThemeResourceApplier.BuildResourceDictionary(theme);

        // Assert
        resources.Should().NotBeNull();
        resources.Contains("AppBackgroundBrush").Should().BeTrue();
        resources.Contains("AccentPrimaryBrush").Should().BeTrue();
        resources.Contains("AppFontFamily").Should().BeTrue();
    }

    [Fact]
    public void RenameIndexCalculator_CalculateInsertIndex_ShouldClampProperly()
    {
        // Act & Assert
        RenameIndexCalculator.CalculateInsertIndex(CharacterPosition.FromStart, 3, 10).Should().Be(3);
        RenameIndexCalculator.CalculateInsertIndex(CharacterPosition.FromEnd, 2, 10).Should().Be(8);
        RenameIndexCalculator.CalculateInsertIndex(CharacterPosition.FromStart, 99, 10).Should().Be(10);
        RenameIndexCalculator.CalculateInsertIndex(CharacterPosition.FromStart, -5, 10).Should().Be(0);
    }

    [Fact]
    public void RenameIndexCalculator_CalculateRemoveStartIndex_ShouldClampProperly()
    {
        // Act & Assert
        RenameIndexCalculator.CalculateRemoveStartIndex(CharacterPosition.FromStart, 2, 10, 3).Should().Be(2);
        RenameIndexCalculator.CalculateRemoveStartIndex(CharacterPosition.FromEnd, 2, 10, 3).Should().Be(5); // 10 - 2 - 3 = 5
    }

    [Fact]
    public void WorkflowTelemetryTracker_ShouldTrackItemsAndGenerateAccurateSnapshots()
    {
        // Arrange
        var tracker = new WorkflowTelemetryTracker();
        tracker.Reset();
        tracker.SetTotalExpectedItems(100);

        // Act
        tracker.IncrementSourceItemsEmitted();
        tracker.IncrementSourceItemsEmitted();
        tracker.AddTotalItems(2);
        tracker.IncrementProcessedItems();
        tracker.IncrementCompletedFiles();
        tracker.AddProcessedBytes(1024 * 1024);

        var snapshot = tracker.GetSnapshot(isRunning: true);

        // Assert
        snapshot.ProcessedItems.Should().Be(1);
        snapshot.TotalItems.Should().Be(100);
        snapshot.ProcessedBytes.Should().Be(1024 * 1024);
        snapshot.Percentage.Should().Be(1.0);
        snapshot.StatusMessage.Should().Contain("Procesando");
    }

    [Fact]
    public void AppResourceLocator_FindFileInAppOrRepo_ShouldFindExistingRepoFile()
    {
        // Act
        string? manualPath = AppResourceLocator.FindFileInAppOrRepo("Docs", "manual_de_usuario.md", "docs/manual_de_usuario.md");

        // Assert
        manualPath.Should().NotBeNull();
        File.Exists(manualPath).Should().BeTrue();
    }
}
