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

public class ModelLifecycleAndMemoryTests
{
    [Fact]
    public void AiNodes_ShouldImplementIModelLifecycleNode()
    {
        // Arrange
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
    public void UserPreferences_AutoUnloadAiModelsOnCompletion_ShouldDefaultToFalseAndBeMutable()
    {
        // Arrange & Act
        var prefs = UserPreferencesService.Instance.Preferences;

        // Assert
        prefs.AutoUnloadAiModelsOnCompletion.Should().BeFalse();

        // Update preference
        UserPreferencesService.Instance.UpdatePreferences(p => p.AutoUnloadAiModelsOnCompletion = true);
        UserPreferencesService.Instance.Preferences.AutoUnloadAiModelsOnCompletion.Should().BeTrue();

        // Revert back
        UserPreferencesService.Instance.UpdatePreferences(p => p.AutoUnloadAiModelsOnCompletion = false);
        UserPreferencesService.Instance.Preferences.AutoUnloadAiModelsOnCompletion.Should().BeFalse();
    }
}
