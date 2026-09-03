using System.IO;
using FileFlow.Plugin.AI;
using FileFlow.Sdk.Storage;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.AI;

public class AiModelManagerConfigTests
{
    [Fact]
    public void AiModelManager_GetDefaultUrls_ShouldReturnWorkingUrlsForAllCatalogModels()
    {
        // Assert: cada modelo del catálogo debe tener al menos una URL por defecto
        foreach (var (id, info) in AiModelManager.Catalog)
        {
            var defaultUrls = AiModelManager.GetDefaultUrls(id);
            defaultUrls.Should().NotBeEmpty($"El modelo '{id}' debe tener al menos una URL por defecto");
            defaultUrls[0].Should().StartWith("http", $"La URL por defecto de '{id}' debe ser HTTP/HTTPS");
        }

        // Grounding DINO debe apuntar al repositorio funcional de Hugging Face
        var dinoUrls = AiModelManager.GetDefaultUrls("grounding-dino");
        dinoUrls.Should().Contain(u => u.Contains("Instemic/yolo-world-onnx"));
    }

    [Fact]
    public void AiModelManager_SetCustomUrls_AndReset_ShouldPersistAndRevertProperly()
    {
        string modelId = "tiny-yolov3";
        AiModelManager.ResetCustomUrls(modelId);

        var originalUrls = AiModelManager.GetConfiguredUrls(modelId);
        var customList = new[]
        {
            "https://mirror1.example.com/models/tiny-yolov3.onnx",
            "https://mirror2.example.com/models/tiny-yolov3.onnx"
        };

        try
        {
            // Act 1: Configurar URLs personalizadas
            AiModelManager.SetCustomUrls(modelId, customList);

            // Assert 1
            AiModelManager.HasCustomUrls(modelId).Should().BeTrue();
            var configured = AiModelManager.GetConfiguredUrls(modelId);
            configured.Should().BeEquivalentTo(customList);

            // Simular recarga desde fichero en disco
            AiModelManager.LoadConfig();
            AiModelManager.HasCustomUrls(modelId).Should().BeTrue();
            AiModelManager.GetConfiguredUrls(modelId).Should().BeEquivalentTo(customList);

            // Act 2: Restablecer a valores por defecto
            AiModelManager.ResetCustomUrls(modelId);

            // Assert 2
            AiModelManager.HasCustomUrls(modelId).Should().BeFalse();
            AiModelManager.GetConfiguredUrls(modelId).Should().BeEquivalentTo(AiModelManager.GetDefaultUrls(modelId));
        }
        finally
        {
            AiModelManager.ResetCustomUrls(modelId);
        }
    }

    [Fact]
    public async Task AiModelManager_DownloadWithFallback_ShouldTryNextMirrorWhenFirstFails()
    {
        string modelId = "ultraface";
        var defaultUrls = AiModelManager.GetDefaultUrls(modelId);
        defaultUrls.Should().NotBeEmpty();

        // Configurar URL 1 inválida (404 seguro) y URL 2 la oficial funcional
        var fallbackList = new[]
        {
            "https://huggingface.co/invalid-user-for-testing-404/non-existent-repo/resolve/main/model.onnx",
            defaultUrls[0]
        };

        try
        {
            AiModelManager.DeleteModel(modelId);
            AiModelManager.SetCustomUrls(modelId, fallbackList);

            var logs = new List<string>();
            string? resultPath = await AiModelManager.DownloadModelWithProgressAsync(
                modelId,
                progress: null,
                statusLogger: msg => logs.Add(msg),
                cancellationToken: CancellationToken.None);

            // Assert: debe haberse recuperado usando el espejo 2
            resultPath.Should().NotBeNull();
            File.Exists(resultPath!).Should().BeTrue();
            logs.Should().Contain(msg => msg.Contains("Conmutando al siguiente espejo") || msg.Contains("espejo 2"));
            logs.Should().Contain(msg => msg.Contains("descargado correctamente"));
        }
        finally
        {
            AiModelManager.ResetCustomUrls(modelId);
        }
    }
}
