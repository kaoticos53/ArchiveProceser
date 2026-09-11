using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using FileFlow.Plugin.AI;
using FileFlow.Plugin.AI.Management;
using FileFlow.Plugin.AI.ViewModels;
using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace FileFlow.Tests.Unit.AI;

public sealed class MultimodalVlmConfigViewModelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly VlmConfigurationStorageService _storageService;

    public MultimodalVlmConfigViewModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FileFlow_VlmVmTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _storageService = new VlmConfigurationStorageService(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    [Fact]
    public void Constructor_ShouldInitializeProvidersAndTemplates()
    {
        // Act
        var vm = new MultimodalVlmConfigViewModel(storageService: _storageService);

        // Assert
        vm.Providers.Should().NotBeEmpty();
        vm.Templates.Should().NotBeEmpty();
        vm.SelectedProvider.Should().NotBeNull();
        vm.SelectedTemplate.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithTargetNode_ShouldSelectNodeValues()
    {
        // Arrange
        var node = new MultimodalVisionLlmNode();
        node.Parameters["Provider"] = VlmAdapterFactory.ProviderInProcess;
        node.Parameters["TaskPreset"] = "DocumentOcrAndSummary";
        node.Parameters["AdditionalPrompt"] = "Enfocarse en la firma inferior";

        // Act
        var vm = new MultimodalVlmConfigViewModel(targetNode: node, storageService: _storageService);

        // Assert
        vm.SelectedProvider.Should().NotBeNull();
        vm.SelectedProvider!.ProviderId.Should().Be(VlmAdapterFactory.ProviderInProcess);
        vm.SelectedTemplate.Should().NotBeNull();
        vm.SelectedTemplate!.Id.Should().Be("DocumentOcrAndSummary");
        vm.PreviewAdditionalPrompt.Should().Be("Enfocarse en la firma inferior");
        vm.PreviewFinalPrompt.Should().Contain("Enfocarse en la firma inferior");
    }

    [Fact]
    public void NewTemplate_ShouldAddCustomTemplateAndSelectIt()
    {
        // Arrange
        var vm = new MultimodalVlmConfigViewModel(storageService: _storageService);
        int initialCount = vm.Templates.Count;

        // Act
        vm.NewTemplate();

        // Assert
        vm.Templates.Should().HaveCount(initialCount + 1);
        vm.SelectedTemplate.Should().NotBeNull();
        vm.SelectedTemplate!.IsBuiltIn.Should().BeFalse();
        vm.SelectedTemplate.Id.Should().StartWith("Custom_");
    }

    [Fact]
    public void DuplicateTemplate_ShouldCloneSelectedTemplate()
    {
        // Arrange
        var vm = new MultimodalVlmConfigViewModel(storageService: _storageService);
        vm.SelectedTemplate = vm.Templates.First(t => t.Id == "ExtractInvoiceReceiptJson");
        int initialCount = vm.Templates.Count;

        // Act
        vm.DuplicateTemplate();

        // Assert
        vm.Templates.Should().HaveCount(initialCount + 1);
        vm.SelectedTemplate.Should().NotBeNull();
        vm.SelectedTemplate!.IsBuiltIn.Should().BeFalse();
        vm.SelectedTemplate.Name.Should().Contain("Facturas");
        vm.SelectedTemplate.ForceJsonOutput.Should().BeTrue();
    }

    [Fact]
    public void DeleteTemplate_ShouldNotDeleteBuiltIn_AndShouldDeleteCustom()
    {
        // Arrange
        var vm = new MultimodalVlmConfigViewModel(storageService: _storageService);
        var builtIn = vm.Templates.First(t => t.IsBuiltIn);
        vm.SelectedTemplate = builtIn;
        int countBefore = vm.Templates.Count;

        // Act 1: Intentar borrar plantilla del sistema
        vm.DeleteTemplate();

        // Assert 1: Debe protegerla
        vm.Templates.Should().HaveCount(countBefore);
        vm.StatusMessage.Should().Match(s => s.Contains("sistema") || s.Contains("system") || s.Contains("Built-in"));

        // Act 2: Crear una personalizada y borrarla
        vm.NewTemplate();
        int countWithCustom = vm.Templates.Count;
        vm.DeleteTemplate();

        // Assert 2: Debe haber sido eliminada
        vm.Templates.Should().HaveCount(countWithCustom - 1);
    }

    [Fact]
    public async Task TestConnectionAsync_ShouldSucceedForInProcessProvider()
    {
        // Arrange
        var vm = new MultimodalVlmConfigViewModel(storageService: _storageService);
        vm.SelectedProvider = vm.Providers.First(p => p.ProviderId.Contains("In-Process"));

        // Act
        await vm.TestConnectionAsync();

        // Assert
        vm.ConnectionTestSuccess.Should().BeTrue();
        vm.ConnectionTestStatus.Should().Match(s => s.Contains("Motor interno listo") || s.Contains("Internal engine ready"));
    }

    [Fact]
    public void SaveAll_ShouldUpdateTargetNodeParameters()
    {
        // Arrange
        var node = new MultimodalVisionLlmNode();
        var vm = new MultimodalVlmConfigViewModel(targetNode: node, storageService: _storageService);

        var ollama = vm.Providers.First(p => p.ProviderId.Contains("Ollama"));
        ollama.EndpointUrl = "http://custom-ollama:11434/v1";
        ollama.ModelName = "qwen2.5-vl:32b";
        vm.SelectedProvider = ollama;

        var ocrTpl = vm.Templates.First(t => t.Id == "DocumentOcrAndSummary");
        vm.SelectedTemplate = ocrTpl;
        vm.PreviewAdditionalPrompt = "Traducir resumen a inglés";

        // Act
        vm.SaveAll();

        // Assert
        node.Parameters["Provider"].Should().Be(ollama.DisplayName);
        node.Parameters["EndpointUrl"].Should().Be("http://custom-ollama:11434/v1");
        node.Parameters["ModelName"].Should().Be("qwen2.5-vl:32b");
        node.Parameters["TaskPreset"].Should().Be(ocrTpl.Name);
        node.Parameters["AdditionalPrompt"].Should().Be("Traducir resumen a inglés");
    }

    [Fact]
    public void NewProvider_ShouldAddCustomProviderAndSelectIt()
    {
        // Arrange
        var vm = new MultimodalVlmConfigViewModel(storageService: _storageService);
        int initialCount = vm.Providers.Count;

        // Act
        vm.NewProvider();

        // Assert
        vm.Providers.Should().HaveCount(initialCount + 1);
        vm.SelectedProvider.Should().NotBeNull();
        vm.SelectedProvider!.IsBuiltIn.Should().BeFalse();
        vm.SelectedProvider.ProviderId.Should().StartWith("Custom_");
    }

    [Fact]
    public void DuplicateProvider_ShouldCloneSelectedProvider()
    {
        // Arrange
        var vm = new MultimodalVlmConfigViewModel(storageService: _storageService);
        vm.SelectedProvider = vm.Providers.First(p => p.ProviderId.Contains("LM Studio"));
        int initialCount = vm.Providers.Count;

        // Act
        vm.DuplicateProvider();

        // Assert
        vm.Providers.Should().HaveCount(initialCount + 1);
        vm.SelectedProvider.Should().NotBeNull();
        vm.SelectedProvider!.IsBuiltIn.Should().BeFalse();
        vm.SelectedProvider.DisplayName.Should().Contain("LM Studio");
    }

    [Fact]
    public void DeleteProvider_ShouldNotDeleteBuiltIn_AndShouldDeleteCustom()
    {
        // Arrange
        var vm = new MultimodalVlmConfigViewModel(storageService: _storageService);
        var builtIn = vm.Providers.First(p => p.IsBuiltIn);
        vm.SelectedProvider = builtIn;
        int countBefore = vm.Providers.Count;

        // Act 1: Intentar borrar proveedor del sistema
        vm.DeleteProvider();

        // Assert 1: No debe eliminarse
        vm.Providers.Should().HaveCount(countBefore);
        (vm.StatusMessage.Contains("sistema", StringComparison.OrdinalIgnoreCase) ||
         vm.StatusMessage.Contains("system", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();

        // Act 2: Crear uno personalizado y borrarlo
        vm.NewProvider();
        int countWithCustom = vm.Providers.Count;
        vm.DeleteProvider();

        // Assert 2: Debe haber sido eliminado
        vm.Providers.Should().HaveCount(countWithCustom - 1);
    }

    [Fact]
    public async Task RefreshModelsAsync_ShouldPopulateAvailableModels_ForInProcessProvider()
    {
        // Arrange
        var vm = new MultimodalVlmConfigViewModel(storageService: _storageService);
        vm.SelectedProvider = vm.Providers.First(p => p.ProviderId.Contains("In-Process"));

        // Act
        await vm.RefreshModelsAsync();

        // Assert
        vm.AvailableModels.Should().Contain("FileFlow-Structural-VLM");
        vm.AvailableModels.Should().Contain("FileFlow-InProcess-Fast");
    }

    [Fact]
    public async Task RefreshModelsAsync_WithMockHttpOpenAi_ShouldParseModels()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/models") == true)
            {
                var json = "{\"data\": [{\"id\": \"qwen2-vl-7b-instruct\"}, {\"id\": \"llava-v1.6-34b\"}]}";
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        });

        using var httpClient = new System.Net.Http.HttpClient(mockHandler);
        var vm = new MultimodalVlmConfigViewModel(storageService: _storageService, customHttpClient: httpClient);
        var provider = vm.Providers.First(p => p.ProviderId.Contains("LM Studio"));
        vm.SelectedProvider = provider;

        // Act
        await vm.RefreshModelsAsync();

        // Assert
        vm.AvailableModels.Should().Contain("qwen2-vl-7b-instruct");
        vm.AvailableModels.Should().Contain("llava-v1.6-34b");
    }

    [Fact]
    public async Task RefreshModelsAsync_WithMockHttpOllama_ShouldParseModels()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler(request =>
        {
            // /v1/models fails or 404, fallback to /api/tags
            if (request.RequestUri?.AbsolutePath.EndsWith("/api/tags") == true)
            {
                var json = "{\"models\": [{\"name\": \"minicpm-v:8b\"}, {\"name\": \"llama3.2-vision:11b\"}]}";
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        });

        using var httpClient = new System.Net.Http.HttpClient(mockHandler);
        var vm = new MultimodalVlmConfigViewModel(storageService: _storageService, customHttpClient: httpClient);
        var provider = vm.Providers.First(p => p.ProviderId.Contains("Ollama"));
        vm.SelectedProvider = provider;

        // Act
        await vm.RefreshModelsAsync();

        // Assert
        vm.AvailableModels.Should().Contain("minicpm-v:8b");
        vm.AvailableModels.Should().Contain("llama3.2-vision:11b");
    }

    [Fact]
    public async Task RunSampleInferenceAsync_AndApplyDiscoveredVariables_ShouldPopulateNodeParameters()
    {
        // Arrange: crear imagen de prueba
        string sampleImg = Path.Combine(_tempDir, "sample_doc.png");
        using (var img = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(50, 50))
        {
            await img.SaveAsPngAsync(sampleImg);
        }

        var node = new MultimodalVisionLlmNode();
        node.Parameters["Provider"] = VlmAdapterFactory.ProviderInProcess;
        node.Parameters["TaskPreset"] = "ExtractInvoiceReceiptJson";

        var vm = new MultimodalVlmConfigViewModel(targetNode: node, storageService: _storageService);
        var inProcessProvider = vm.Providers.First(p => p.ProviderId.Contains("In-Process"));
        vm.SelectedProvider = inProcessProvider;
        vm.SelectedTemplate = vm.Templates.First(t => t.Id == "ExtractInvoiceReceiptJson");
        vm.SampleFilePath = sampleImg;

        // Act 1: Ejecutar inferencia de muestra
        await vm.RunSampleInferenceAsync();

        // Assert 1: Variables descubiertas
        vm.SampleDiscoveredVariables.Should().NotBeEmpty();
        vm.CanApplyVariables.Should().BeTrue();
        vm.SampleExecutionStatus.Should().Match(s => s.Contains("completada") || s.Contains("completed"));

        // Act 2: Aplicar variables al nodo
        vm.ApplyDiscoveredVariables();

        // Assert 2: Parámetro DiscoveredVariables guardado en el nodo
        node.Parameters.Should().ContainKey("DiscoveredVariables");
        string discoveredJson = node.Parameters["DiscoveredVariables"]?.ToString() ?? string.Empty;
        discoveredJson.Should().NotBeNullOrWhiteSpace();
        discoveredJson.Should().Contain("AI:VlmResponse");
    }

    private sealed class MockHttpMessageHandler(Func<System.Net.Http.HttpRequestMessage, HttpResponseMessage> handler)
        : System.Net.Http.HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            System.Net.Http.HttpRequestMessage request,
            System.Threading.CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }
}
