using FileFlow.Sdk.Localization;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Sdk;

[Collection("Localization")]
public class LocalizationManagerTests : IDisposable
{
    public void Dispose()
    {
        LocalizationManager.Instance.SetCulture("es-ES");
    }

    [Fact]
    public void Instance_ShouldReturnSingleton_WhenAccessedMultipleTimes()
    {
        // Act
        var instance1 = LocalizationManager.Instance;
        var instance2 = LocalizationManager.Instance;

        // Assert
        instance1.Should().NotBeNull();
        instance1.Should().BeSameAs(instance2);
    }

    [Fact]
    public void SetCulture_ShouldFireLanguageChangedEvent_WhenCultureChanges()
    {
        // Arrange
        var manager = LocalizationManager.Instance;
        bool eventFired = false;
        manager.LanguageChanged += (sender, args) => eventFired = true;

        // Act
        manager.SetCulture("en-US");

        // Assert
        eventFired.Should().BeTrue();
    }

    [Fact]
    public void SetCulture_ShouldNormalizeUiCulture_ToResourceLanguageParent()
    {
        // Arrange
        var manager = LocalizationManager.Instance;

        // Act
        manager.SetCulture("es-ES");

        // Assert
        manager.CurrentCulture.Name.Should().Be("es-ES");
        System.Globalization.CultureInfo.CurrentUICulture.Name.Should().Be("es");
    }

    [Fact]
    public void SetCulture_ShouldRaisePropertyChangedForIndexer_WhenCultureChanges()
    {
        // Arrange
        var manager = LocalizationManager.Instance;
        manager.SetCulture("fr-FR"); // Asegurar valor previo distinto

        var changedProperties = new List<string?>();
        manager.PropertyChanged += (sender, args) => changedProperties.Add(args.PropertyName);

        try
        {
            // Act
            manager.SetCulture("es-ES");

            // Assert
            changedProperties.Should().Contain(p => p == "Item[]");
            changedProperties.Should().Contain(p => p == string.Empty);
        }
        finally
        {
            manager.SetCulture("es-ES");
        }
    }

    [Fact]
    public void PluginResources_ShouldBeAutoDiscoveredAndResolved_WhenPluginAssemblyLoaded()
    {
        // Arrange
        var loader = new FileFlow.Core.Plugins.PluginLoader();
        var fileSystemAssembly = typeof(FileFlow.Plugin.FileSystem.AdvancedRenamerNode).Assembly;

        // Act
        loader.RegisterNodeTypesFromAssembly(fileSystemAssembly);

        // Assert - Test Spanish resolution
        LocalizationManager.Instance.SetCulture("es-ES");
        string titleEs = LocalizationManager.Instance["RegexHelper_WindowTitle"];
        titleEs.Should().Contain("Asistente y Probador de Expresiones Regulares");

        // Assert - Test English resolution
        LocalizationManager.Instance.SetCulture("en-US");
        string titleEn = LocalizationManager.Instance["RegexHelper_WindowTitle"];
        titleEn.Should().Contain("Regular Expressions Assistant");

        // Reset
        LocalizationManager.Instance.SetCulture("es-ES");
    }

    [Fact]
    public void ScriptingPluginResources_ShouldResolveLocalizedStrings_WhenPluginAssemblyLoaded()
    {
        // Arrange
        var loader = new FileFlow.Core.Plugins.PluginLoader();
        var scriptingAssembly = typeof(FileFlow.Plugin.Scripting.CustomScriptNode).Assembly;

        // Act
        loader.RegisterNodeTypesFromAssembly(scriptingAssembly);

        // Assert - Test Spanish resolution
        LocalizationManager.Instance.SetCulture("es-ES");
        string titleEs = LocalizationManager.Instance["ScriptStudio_Title"];
        string nodeNameEs = LocalizationManager.Instance["CustomScriptNode_Name"];
        titleEs.Should().Be("Estudio de Scripts");
        nodeNameEs.Should().Be("Script Personalizado (C# / JavaScript)");

        // Assert - Test English resolution
        LocalizationManager.Instance.SetCulture("en-US");
        string titleEn = LocalizationManager.Instance["ScriptStudio_Title"];
        string nodeNameEn = LocalizationManager.Instance["CustomScriptNode_Name"];
        titleEn.Should().Be("Script Studio");
        nodeNameEn.Should().Be("Custom Script (C# / JavaScript)");

        // Reset
        LocalizationManager.Instance.SetCulture("es-ES");
    }

    [Fact]
    public void DataPluginResources_ShouldResolveLocalizedStrings_WhenPluginAssemblyLoaded()
    {
        var loader = new FileFlow.Core.Plugins.PluginLoader();
        var assembly = typeof(FileFlow.Plugin.Data.ExcelReaderNode).Assembly;

        loader.RegisterNodeTypesFromAssembly(assembly);

        LocalizationManager.Instance.SetCulture("es-ES");
        LocalizationManager.Instance["ExcelReaderNode_Name"].Should().Be("Lector de Hojas Excel");

        LocalizationManager.Instance.SetCulture("en-US");
        LocalizationManager.Instance["ExcelReaderNode_Name"].Should().Be("Excel Sheet Reader");

        LocalizationManager.Instance.SetCulture("es-ES");
    }

    [Fact]
    public void DocumentsPluginResources_ShouldResolveLocalizedStrings_WhenPluginAssemblyLoaded()
    {
        var loader = new FileFlow.Core.Plugins.PluginLoader();
        var assembly = typeof(FileFlow.Plugin.Documents.PdfTextExtractorNode).Assembly;

        loader.RegisterNodeTypesFromAssembly(assembly);

        LocalizationManager.Instance.SetCulture("es-ES");
        LocalizationManager.Instance["PdfTextExtractorNode_Name"].Should().Be("Extraer Texto de PDF");

        LocalizationManager.Instance.SetCulture("en-US");
        LocalizationManager.Instance["PdfTextExtractorNode_Name"].Should().Be("PDF Text Extractor");

        LocalizationManager.Instance.SetCulture("es-ES");
    }

    [Fact]
    public void ImagesPluginResources_ShouldResolveLocalizedStrings_WhenPluginAssemblyLoaded()
    {
        var loader = new FileFlow.Core.Plugins.PluginLoader();
        var assembly = typeof(FileFlow.Plugin.Images.ExifMetadataNode).Assembly;

        loader.RegisterNodeTypesFromAssembly(assembly);

        LocalizationManager.Instance.SetCulture("es-ES");
        LocalizationManager.Instance["ImageOptimizerNode_Name"].Should().Be("Optimizador de Imágenes");

        LocalizationManager.Instance.SetCulture("en-US");
        LocalizationManager.Instance["ImageOptimizerNode_Name"].Should().Be("Image Optimizer");

        LocalizationManager.Instance.SetCulture("es-ES");
    }

    [Fact]
    public void HashingPluginResources_ShouldResolveLocalizedStrings_WhenPluginAssemblyLoaded()
    {
        var loader = new FileFlow.Core.Plugins.PluginLoader();
        var assembly = typeof(FileFlow.Plugin.Hashing.DeduplicationFilterNode).Assembly;

        loader.RegisterNodeTypesFromAssembly(assembly);

        LocalizationManager.Instance.SetCulture("es-ES");
        LocalizationManager.Instance["DeduplicationFilterNode_Name"].Should().Be("Filtro de Deduplicación por Hash");

        LocalizationManager.Instance.SetCulture("en-US");
        LocalizationManager.Instance["DeduplicationFilterNode_Name"].Should().Be("Hash-Based Deduplication Filter");

        LocalizationManager.Instance.SetCulture("es-ES");
    }

    [Fact]
    public void ConcurrentAccess_ShouldBeThreadSafe()
    {
        // Arrange
        var manager = LocalizationManager.Instance;

        // Act & Assert
        Parallel.For(0, 100, i =>
        {
            _ = manager.GetString("NonExistentKey_" + i, "Fallback_" + i);
            _ = manager.CurrentLanguage;
        });
    }
}
