using FileFlow.App.Models;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Sdk.Localization;
using Xunit;

namespace FileFlow.Tests.Unit.App;

public class TextEditorDialogViewModelTests
{
    [Fact]
    public void Constructor_InitializesDefaultStateCorrectly()
    {
        var vm = new TextEditorDialogViewModel("Custom Title", "Initial Hello World");

        Assert.Equal("Custom Title", vm.Title);
        Assert.Equal("Initial Hello World", vm.Text);
        Assert.Contains("19", vm.CharCountText); // "Initial Hello World".Length == 19
        Assert.Contains("3", vm.WordCountText);
        Assert.Contains("1", vm.LineCountText);
        Assert.NotEmpty(vm.AllVariables);
        Assert.NotEmpty(vm.FilteredSideVariables);
    }

    [Fact]
    public void UpdateStats_CalculatesWordsLinesAndCharsAccurately()
    {
        var vm = new TextEditorDialogViewModel("Title", "");
        vm.Text = "Line One\nLine Two\nLine Three with more words";

        Assert.Contains("3", vm.LineCountText);
        Assert.Contains("9", vm.WordCountText);
        Assert.Contains("Line One\nLine Two\nLine Three with more words".Length.ToString(), vm.CharCountText);
    }

    [Fact]
    public void FilterSideVariables_FiltersByTokenOrDescription()
    {
        var vm = new TextEditorDialogViewModel("Title", "");
        
        vm.SideSearchText = "GlobalOutputDir";
        Assert.Single(vm.FilteredSideVariables);
        Assert.Equal("{GlobalOutputDir}", vm.FilteredSideVariables[0].Token);

        vm.SideSearchText = "NonExistentVariableQuery12345";
        Assert.Empty(vm.FilteredSideVariables);

        vm.SideSearchText = string.Empty;
        Assert.Equal(vm.AllVariables.Count, vm.FilteredSideVariables.Count);
    }

    [Fact]
    public void EvaluateIntelliSense_DetectsOpenBraceAndFiltersMatches()
    {
        var vm = new TextEditorDialogViewModel("Title", "Prefix {File");
        
        int braceIndex = vm.EvaluateIntelliSense(vm.Text.Length);
        
        Assert.Equal(7, braceIndex);
        Assert.True(vm.IsIntelliSenseOpen);
        Assert.NotEmpty(vm.IntelliSenseCandidates);
        Assert.All(vm.IntelliSenseCandidates, c => Assert.True(
            c.Token.Contains("File", StringComparison.OrdinalIgnoreCase) ||
            c.Description.Contains("File", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void EvaluateIntelliSense_ClosesWhenBraceIsClosedOrNoMatch()
    {
        var vm = new TextEditorDialogViewModel("Title", "Prefix {FileName} extra");
        
        int braceIndex = vm.EvaluateIntelliSense(vm.Text.Length);
        Assert.Equal(-1, braceIndex);
        Assert.False(vm.IsIntelliSenseOpen);
    }

    [Fact]
    public void ApplyIntelliSenseSelection_ReplacesOpenBraceAndQueryWithToken()
    {
        var vm = new TextEditorDialogViewModel("Title", "Hello {File world");
        // Caret is at index 11 (after 'File')
        vm.EvaluateIntelliSense(11);
        
        var (newText, newCaret) = vm.ApplyIntelliSenseSelection(11, new VariableItem("FileName", "{FileName}", "Desc", "Cat", "val"));

        Assert.Equal("Hello {FileName} world", newText);
        Assert.Equal("Hello {FileName}".Length, newCaret);
        Assert.False(vm.IsIntelliSenseOpen);
    }

    [Fact]
    public void InsertTokenAt_InsertsTokenAtSpecifiedCaret()
    {
        var vm = new TextEditorDialogViewModel("Title", "Hello world");
        
        var (newText, newCaret) = vm.InsertTokenAt(5, " beautiful");

        Assert.Equal("Hello beautiful world", newText);
        Assert.Equal(15, newCaret);
    }

    [Fact]
    public void ClearText_EmptiesTextAndResetsStats()
    {
        var vm = new TextEditorDialogViewModel("Title", "Some initial text");
        vm.ClearText();

        Assert.Empty(vm.Text);
        Assert.Contains("0", vm.CharCountText);
        Assert.Contains("0", vm.WordCountText);
    }

    [Fact]
    public void ToggleSidePanel_TogglesVisibility()
    {
        var vm = new TextEditorDialogViewModel("Title", "");
        Assert.False(vm.IsSidePanelVisible);

        vm.ToggleSidePanel();
        Assert.True(vm.IsSidePanelVisible);

        vm.CloseSidePanel();
        Assert.False(vm.IsSidePanelVisible);
    }
}
