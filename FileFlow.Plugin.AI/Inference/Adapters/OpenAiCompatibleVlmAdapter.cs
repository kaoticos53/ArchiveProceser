using System;
using System.Threading;
using System.Threading.Tasks;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Adaptador de inferencia VLM para servidores externos (LM Studio, Ollama, OpenAI)
/// que exponen la API HTTP de Chat Completions con soporte multimodal.
/// </summary>
public sealed class OpenAiCompatibleVlmAdapter : IVlmAdapter
{
    public string ProviderName => "OpenAI-Compatible HTTP Server (LM Studio / Ollama)";

    public async Task<VlmInferenceResult> ExecuteAsync(VlmExecutionRequest request, CancellationToken cancellationToken)
    {
        return await MultimodalVlmClientEngine.ExecuteChatCompletionAsync(
            endpointUrl: request.EndpointUrl,
            modelName: request.ModelName,
            apiKey: request.ApiKey,
            systemPrompt: request.SystemPrompt,
            userPrompt: request.UserPrompt,
            base64ImageDataUrl: request.Base64ImageDataUrl,
            temperature: request.Temperature,
            maxTokens: request.MaxTokens,
            forceJsonOutput: request.ForceJsonOutput,
            timeout: request.Timeout,
            customHttpClient: request.CustomHttpClient,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
