using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

public class RegexHelperViewModelTests
{
    [Fact]
    public void EvaluateRegex_WithValidPattern_ShouldExtractMatchesAndGroups()
    {
        // Arrange
        var vm = new RegexHelperViewModel(
            initialPattern: @"(\d+)[xX](\d+)",
            initialReplacement: "${1}x${2}",
            initialTestInput: "serie_guapa_1x02.mov");

        // Assert
        vm.IsValidRegex.Should().BeTrue();
        vm.MatchCount.Should().Be(1);
        vm.Matches.Should().HaveCount(1);

        var firstMatch = vm.Matches[0];
        firstMatch.Value.Should().Be("1x02");
        firstMatch.Groups.Should().HaveCount(2);
        firstMatch.Groups[0].Value.Should().Be("1");
        firstMatch.Groups[1].Value.Should().Be("02");
        vm.ReplacementResult.Should().Be("serie_guapa_1x02.mov");
    }

    [Fact]
    public void EvaluateRegex_WithInvalidPattern_ShouldSetErrorWithoutCrashing()
    {
        // Arrange
        var vm = new RegexHelperViewModel(
            initialPattern: @"([a-z+",
            initialTestInput: "test");

        // Assert
        vm.IsValidRegex.Should().BeFalse();
        vm.RegexErrorMessage.Should().Contain("Error sintáctico");
        vm.MatchCount.Should().Be(0);
    }

    [Fact]
    public void EvaluateRegex_Replacement_ShouldApplyCaptureGroupsAccurately()
    {
        // Arrange
        var vm = new RegexHelperViewModel(
            initialPattern: @"(\d{4})_(\d{2})_(\d{2})",
            initialReplacement: "$3-$2-$1",
            initialTestInput: "report_2026_09_01.pdf");

        // Assert
        vm.ReplacementResult.Should().Be("report_01-09-2026.pdf");
    }

    [Fact]
    public void ApplyLibraryPattern_ShouldUpdateTesterFields()
    {
        // Arrange
        var vm = new RegexHelperViewModel();
        var preset = RegexLibraryService.Instance.GetBuiltInPatterns().First(p => p.Name.Contains("NxN"));

        // Act
        vm.ApplyLibraryPattern(preset);

        // Assert
        vm.Pattern.Should().Be(preset.Pattern);
        vm.Replacement.Should().Be(preset.Replacement);
        vm.IsValidRegex.Should().BeTrue();
    }

    [Fact]
    public void EvaluateRegex_Replacement_WithTemplateFunctionsAndVariables_ShouldEvaluateInLiveTester()
    {
        // Arrange
        var vm = new RegexHelperViewModel(
            initialPattern: @"(\w+)\s+(\d+)",
            initialReplacement: "{Upper($1)}_Ep_{PadLeft($2, 3, 0)}_{Year}",
            initialTestInput: "episode 5.mkv");

        // Assert
        string currentYear = DateTime.Now.Year.ToString();
        vm.ReplacementResult.Should().Be($"EPISODE_Ep_005_{currentYear}.mkv");
    }
}
