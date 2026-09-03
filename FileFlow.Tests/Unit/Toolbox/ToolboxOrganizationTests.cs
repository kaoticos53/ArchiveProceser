using System.Globalization;
using System.Reflection;
using FileFlow.App.Models;
using FileFlow.App.ViewModels;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.AI;
using FileFlow.Plugin.Archives;
using FileFlow.Plugin.Data;
using FileFlow.Plugin.Documents;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Hashing;
using FileFlow.Plugin.Images;
using FileFlow.Plugin.Integrations;
using FileFlow.Plugin.Logic;
using FileFlow.Plugin.Network;
using FileFlow.Plugin.Scripting;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Toolbox;

[Collection("Localization")]
public class ToolboxOrganizationTests
{
    private static readonly HashSet<string> ExpectedCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Files",
        "ImageVision",
        "AudioVoice",
        "Documents",
        "Data",
        "LanguageAI",
        "Security",
        "Logic",
        "Archives",
        "Network",
        "Integrations"
    };

    private static readonly Assembly[] PluginAssemblies =
    [
        typeof(FolderSourceNode).Assembly,
        typeof(SmartUnpackNode).Assembly,
        typeof(ImageOptimizerNode).Assembly,
        typeof(PdfMergeNode).Assembly,
        typeof(ExcelReaderNode).Assembly,
        typeof(HashCalculatorNode).Assembly,
        typeof(SwitchCaseNode).Assembly,
        typeof(CliExecutionNode).Assembly,
        typeof(CustomScriptNode).Assembly,
        typeof(RemoteDownloadNode).Assembly,
        typeof(SmartImageClassifierNode).Assembly
    ];

    private static PluginLoader CreatePopulatedLoader()
    {
        var loader = new PluginLoader();
        foreach (var assembly in PluginAssemblies)
        {
            loader.RegisterNodeTypesFromAssembly(assembly);
        }
        return loader;
    }

    [Fact]
    public void AllNodes_MustHaveValidDefinitionAttribute_CategoryAndPipelineRole()
    {
        // Arrange
        var loader = CreatePopulatedLoader();
        var nodeTypes = loader.DiscoveredNodeTypes.Values.Distinct().ToList();

        // Assert
        nodeTypes.Should().HaveCountGreaterOrEqualTo(60, "There should be at least 60 official nodes loaded across all plugins");

        foreach (var type in nodeTypes)
        {
            var defAttr = type.GetCustomAttribute<NodeDefinitionAttribute>();
            defAttr.Should().NotBeNull($"Node '{type.Name}' must have a [NodeDefinition] attribute.");
            defAttr!.Category.Should().NotBeNullOrWhiteSpace($"Node '{type.Name}' must declare a category.");
            Enum.IsDefined(typeof(PipelineRole), defAttr.Role).Should().BeTrue($"Node '{type.Name}' must define a valid PipelineRole.");
            defAttr.Tags.Should().NotBeNull($"Node '{type.Name}' tags array must not be null.");
            defAttr.Tags.Should().NotBeEmpty($"Node '{type.Name}' should declare search tags.");
        }
    }

    [Fact]
    public void AllNodes_MustBelongToUnifiedTaxonomyCategories()
    {
        // Arrange
        var loader = CreatePopulatedLoader();
        var nodeTypes = loader.DiscoveredNodeTypes.Values.Distinct().ToList();

        // Assert
        foreach (var type in nodeTypes)
        {
            var defAttr = type.GetCustomAttribute<NodeDefinitionAttribute>();
            ExpectedCategories.Should().Contain(defAttr!.Category,
                $"Node '{type.Name}' has category '{defAttr.Category}', which is not in the unified 11-category taxonomy.");
        }
    }

    [Theory]
    [InlineData("recortar", "BackgroundRemoverNode")]
    [InlineData("fondo", "BackgroundRemoverNode")]
    [InlineData("dni", "PiiAnonymizerNode")]
    [InlineData("iban", "PiiAnonymizerNode")]
    [InlineData("gdpr", "PiiAnonymizerNode")]
    [InlineData("mp3", "MediaTranscoderNode")]
    [InlineData("excel", "ExcelReaderNode")]
    [InlineData("duplicados", "DeduplicationFilterNode")]
    [InlineData("deduplicar", "DeduplicationFilterNode")]
    [InlineData("upscale", "SuperResolutionUpscalerNode")]
    [InlineData("semantica", "ZeroShotSemanticSearchNode")]
    [InlineData("silero", "VoiceActivityDetectorNode")]
    [InlineData("piper", "TextToSpeechNode")]
    public void MultilingualSearch_ByTags_ShouldFindMatchingNodes(string tagQuery, string expectedNodeNameSubstring)
    {
        // Arrange
        var loader = CreatePopulatedLoader();
        using var toolbox = new ToolboxViewModel(loader);

        // Act
        toolbox.SearchText = tagQuery;

        // Assert
        var matchedItems = toolbox.CategoryGroups.SelectMany(g => g.Items).ToList();
        matchedItems.Should().NotBeEmpty($"Searching for '{tagQuery}' should find matching nodes via Tags.");
        matchedItems.Should().Contain(i => i.TypeName.Contains(expectedNodeNameSubstring, StringComparison.OrdinalIgnoreCase),
            $"Searching for '{tagQuery}' must find '{expectedNodeNameSubstring}'.");
    }

    [Fact]
    public void PerspectiveToggle_ShouldGroupByPipelineRole_InProperOrder()
    {
        // Arrange
        var loader = CreatePopulatedLoader();
        using var toolbox = new ToolboxViewModel(loader);

        // Act - Switch to Pipeline Stage perspective
        toolbox.TogglePerspectiveCommand.Execute(null);

        // Assert
        toolbox.CurrentPerspective.Should().Be(ToolboxPerspective.ByPipelineRole);
        toolbox.IsPipelineRolePerspective.Should().BeTrue();

        var groups = toolbox.CategoryGroups.ToList();
        groups.Should().NotBeEmpty();

        // Check that groups contain roles
        var allItems = groups.SelectMany(g => g.Items).ToList();
        allItems.Should().HaveCountGreaterOrEqualTo(60);

        // Ensure distinct PipelineRoles are represented
        var representedRoles = allItems.Select(i => i.Role).Distinct().ToList();
        representedRoles.Should().Contain(PipelineRole.Source);
        representedRoles.Should().Contain(PipelineRole.Filter);
        representedRoles.Should().Contain(PipelineRole.Transform);
        representedRoles.Should().Contain(PipelineRole.Analyze);
        representedRoles.Should().Contain(PipelineRole.Sink);
        representedRoles.Should().Contain(PipelineRole.Control);

        // Toggle back to Category perspective
        toolbox.TogglePerspectiveCommand.Execute(null);
        toolbox.CurrentPerspective.Should().Be(ToolboxPerspective.ByCategory);
        toolbox.IsPipelineRolePerspective.Should().BeFalse();
    }

    [Fact]
    public void PipelineRole_Localization_ShouldReturnValidStringsInBothLanguages()
    {
        var roles = Enum.GetValues<PipelineRole>();
        var originalCulture = LocalizationManager.Instance.CurrentCulture;

        try
        {
            // Spanish
            LocalizationManager.Instance.CurrentCulture = new CultureInfo("es-ES");
            foreach (var role in roles)
            {
                string locRole = LocalizationManager.Instance.GetString($"Role_{role}", string.Empty);
                locRole.Should().NotBeNullOrWhiteSpace($"Role_{role} must have a Spanish translation.");
            }

            foreach (var cat in ExpectedCategories)
            {
                string locCat = LocalizationManager.Instance.GetString($"Category_{cat}", string.Empty);
                locCat.Should().NotBeNullOrWhiteSpace($"Category_{cat} must have a Spanish translation.");
            }

            // English
            LocalizationManager.Instance.CurrentCulture = new CultureInfo("en-US");
            foreach (var role in roles)
            {
                string locRole = LocalizationManager.Instance.GetString($"Role_{role}", string.Empty);
                locRole.Should().NotBeNullOrWhiteSpace($"Role_{role} must have an English translation.");
            }

            foreach (var cat in ExpectedCategories)
            {
                string locCat = LocalizationManager.Instance.GetString($"Category_{cat}", string.Empty);
                locCat.Should().NotBeNullOrWhiteSpace($"Category_{cat} must have an English translation.");
            }
        }
        finally
        {
            LocalizationManager.Instance.CurrentCulture = originalCulture;
        }
    }
}
