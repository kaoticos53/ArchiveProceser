using System;
using System.IO;
using System.Linq;
using FileFlow.Plugin.AI.Management;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.AI;

public sealed class VlmConfigurationStorageServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly VlmConfigurationStorageService _service;

    public VlmConfigurationStorageServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FileFlow_VlmConfigTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _service = new VlmConfigurationStorageService(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    [Fact]
    public void LoadProviders_ShouldReturnDefaultProviders_WhenNoFileExists()
    {
        // Act
        var providers = _service.LoadProviders();

        // Assert
        providers.Should().NotBeNullOrEmpty();
        providers.Should().Contain(p => p.ProviderId.Contains("LM Studio"));
        providers.Should().Contain(p => p.ProviderId.Contains("Ollama"));
        providers.Should().Contain(p => p.ProviderId.Contains("In-Process"));
    }

    [Fact]
    public void SaveProviders_ShouldPersistAndReloadCorrectly()
    {
        // Arrange
        var providers = _service.LoadProviders();
        var lm = providers.First(p => p.ProviderId.Contains("LM Studio"));
        lm.ModelName = "qwen2.5-vl-32b-custom";
        lm.MaxTokens = 4096;
        lm.ConcurrencyLimit = 4;

        // Act
        _service.SaveProviders(providers);
        var reloadedService = new VlmConfigurationStorageService(_tempDir);
        var reloaded = reloadedService.LoadProviders();

        // Assert
        var reloadedLm = reloaded.First(p => p.ProviderId.Contains("LM Studio"));
        reloadedLm.ModelName.Should().Be("qwen2.5-vl-32b-custom");
        reloadedLm.MaxTokens.Should().Be(4096);
        reloadedLm.ConcurrencyLimit.Should().Be(4);
    }

    [Fact]
    public void LoadTemplates_ShouldReturnBuiltInTemplates()
    {
        // Act
        var templates = _service.LoadTemplates();

        // Assert
        templates.Should().NotBeNullOrEmpty();
        templates.Should().Contain(t => t.Id == "ExtractInvoiceReceiptJson");
        templates.Should().Contain(t => t.Id == "DocumentOcrAndSummary");
        templates.Should().Contain(t => t.Id == "CustomPrompt");
        templates.All(t => !string.IsNullOrWhiteSpace(t.Name)).Should().BeTrue();
    }

    [Fact]
    public void SaveTemplates_ShouldPersistCustomTemplate()
    {
        // Arrange
        var templates = _service.LoadTemplates();
        var custom = new VlmTemplateDefinition
        {
            Id = "CustomReceiptAudit",
            Name = "Auditoría de Tickets y Comidas",
            Description = "Extrae total e identifica propina",
            SystemPrompt = "Eres un auditor de dietas y kilometraje.",
            UserPrompt = "Analiza el ticket de comida adjunto.",
            ForceJsonOutput = true,
            IsBuiltIn = false
        };
        templates.Add(custom);

        // Act
        _service.SaveTemplates(templates);
        var reloadedService = new VlmConfigurationStorageService(_tempDir);
        var reloaded = reloadedService.LoadTemplates();

        // Assert
        var found = reloaded.FirstOrDefault(t => t.Id == "CustomReceiptAudit");
        found.Should().NotBeNull();
        found!.Name.Should().Be("Auditoría de Tickets y Comidas");
        found.ForceJsonOutput.Should().BeTrue();
    }

    [Fact]
    public void ResetTemplateToFactory_ShouldRestoreDefaultPrompts_WhenTemplateIsModified()
    {
        // Arrange
        var templates = _service.LoadTemplates();
        var invoiceTpl = templates.First(t => t.Id == "ExtractInvoiceReceiptJson");
        invoiceTpl.SystemPrompt = "MODIFIED SYSTEM PROMPT";
        _service.SaveTemplates(templates);

        // Act
        var restored = _service.ResetTemplateToFactory("ExtractInvoiceReceiptJson");

        // Assert
        restored.Should().NotBeNull();
        restored!.SystemPrompt.Should().NotBe("MODIFIED SYSTEM PROMPT");
        restored.SystemPrompt.Should().Contain("asistente contable");
    }

    [Fact]
    public void ResetProviderToFactory_ShouldRestoreDefaultProvider_WhenModified()
    {
        // Arrange
        var providers = _service.LoadProviders();
        var lm = providers.First(p => p.ProviderId.Contains("LM Studio"));
        lm.EndpointUrl = "http://modified-host:9999/v1";
        _service.SaveProviders(providers);

        // Act
        var restored = _service.ResetProviderToFactory(lm.ProviderId);

        // Assert
        restored.Should().NotBeNull();
        restored!.EndpointUrl.Should().Be("http://localhost:1234/v1");
    }

    [Fact]
    public void SaveProviders_ShouldPersistCustomAddedProvider()
    {
        // Arrange
        var providers = _service.LoadProviders();
        var custom = new VlmProviderProfile
        {
            ProviderId = "Custom_TestServer",
            DisplayName = "Servidor Privado vLLM",
            EndpointUrl = "http://192.168.1.100:8000/v1",
            ModelName = "mistral-small-instruct",
            ApiKey = "secret",
            IsBuiltIn = false
        };
        providers.Add(custom);

        // Act
        _service.SaveProviders(providers);
        var reloadedService = new VlmConfigurationStorageService(_tempDir);
        var reloaded = reloadedService.LoadProviders();

        // Assert
        var found = reloaded.FirstOrDefault(p => p.ProviderId == "Custom_TestServer");
        found.Should().NotBeNull();
        found!.DisplayName.Should().Be("Servidor Privado vLLM");
        found.EndpointUrl.Should().Be("http://192.168.1.100:8000/v1");
        found.IsBuiltIn.Should().BeFalse();
    }
}
