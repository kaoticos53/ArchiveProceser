using System;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.App.Services;
using FileFlow.Plugin.AI;
using FileFlow.Plugin.AI.Inference;
using FileFlow.Sdk;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.AI;

[Collection(OnnxInferenceCollection.Name)]
public class ModelLifecycleAndMemoryTests
{
    [Fact]
    public void AiNodes_ShouldImplementIModelLifecycleNode()
    {
        // Arrange
        AiPluginInitializer.ClearAllSessions();

        IFlowNode[] aiNodes =
        [
            new BackgroundRemoverNode(),
            new SuperResolutionUpscalerNode(),
            new ContentModerationFilterNode(),
            new ObjectDetectorNode(),
            new PromptObjectDetectorNode(),
            new FaceDetectorNode(),
            new SmartImageClassifierNode(),
            new VoiceActivityDetectorNode(),
            new TextToSpeechNode(),
            new LocalLlmProcessorNode(),
            new LocalAiTranslatorNode(),
            new PromptTransformerNode(),
            new PiiAnonymizerNode()
        ];

        // Act & Assert
        foreach (var node in aiNodes)
        {
            node.Should().BeAssignableTo<IModelLifecycleNode>();
            var lifecycle = (IModelLifecycleNode)node;
            lifecycle.ModelIdentifier.Should().NotBeNullOrWhiteSpace();
            lifecycle.UnloadModel();
            lifecycle.IsModelLoaded.Should().BeFalse();
        }
    }

    [Fact]
    public void ModelLifecycleNode_UnloadModel_ShouldTriggerModelStatusChangedEvent()
    {
        // Arrange
        var node = new BackgroundRemoverNode();
        bool eventFired = false;
        node.ModelStatusChanged += () => eventFired = true;

        // Act
        node.UnloadModel();

        // Assert
        eventFired.Should().BeTrue();
        node.IsModelLoaded.Should().BeFalse();
    }

    [Fact]
    public void OnnxSessionManager_ClearSessionCache_ShouldTriggerSessionStateChanged()
    {
        // Arrange
        bool eventFired = false;
        void Handler() => eventFired = true;
        OnnxSessionManager.SessionStateChanged += Handler;

        try
        {
            // Act
            OnnxSessionManager.ClearSessionCache();

            // Assert
            eventFired.Should().BeTrue();
            OnnxSessionManager.GetLoadedSessionCount().Should().Be(0);
        }
        finally
        {
            OnnxSessionManager.SessionStateChanged -= Handler;
        }
    }

    [Fact]
    public void AudioInferenceEngine_ClearSessionCache_ShouldTriggerSessionStateChanged()
    {
        // Arrange
        bool eventFired = false;
        void Handler() => eventFired = true;
        AudioInferenceEngine.SessionStateChanged += Handler;

        try
        {
            // Act
            AudioInferenceEngine.ClearSessionCache();

            // Assert
            eventFired.Should().BeTrue();
        }
        finally
        {
            AudioInferenceEngine.SessionStateChanged -= Handler;
        }
    }

    [Fact]
    public void AiPluginInitializer_ClearAllSessions_ShouldNotThrow()
    {
        // Act
        var act = () => AiPluginInitializer.ClearAllSessions();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void UserPreferences_AutoUnloadAiModelsOnCompletion_ShouldDefaultToFalse()
    {
        // El valor por defecto es una propiedad del modelo de datos, no del perfil del usuario: comprobarlo
        // contra el singleton lo hacía depender del `user_preferences.json` real de la máquina y del estado
        // en que lo hubiera dejado una ejecución anterior.
        new UserPreferencesData().AutoUnloadAiModelsOnCompletion.Should().BeFalse();
    }

    [Fact]
    public void UserPreferences_AutoUnloadAiModelsOnCompletion_ShouldBeMutableAndRestoreItsValue()
    {
        var service = UserPreferencesService.Instance;
        bool original = service.Preferences.AutoUnloadAiModelsOnCompletion;

        try
        {
            service.UpdatePreferences(p => p.AutoUnloadAiModelsOnCompletion = true);
            service.Preferences.AutoUnloadAiModelsOnCompletion.Should().BeTrue();

            service.UpdatePreferences(p => p.AutoUnloadAiModelsOnCompletion = false);
            service.Preferences.AutoUnloadAiModelsOnCompletion.Should().BeFalse();
        }
        finally
        {
            // El servicio persiste en el perfil real del usuario: la prueba deja el valor como estaba.
            service.UpdatePreferences(p => p.AutoUnloadAiModelsOnCompletion = original);
        }
    }
}
