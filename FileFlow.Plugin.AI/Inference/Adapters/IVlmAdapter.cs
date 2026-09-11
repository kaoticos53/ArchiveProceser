using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Sdk;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Parámetros y contexto de ejecución para un adaptador de modelo Visión-Lenguaje (VLM).
/// </summary>
public record VlmExecutionRequest(
    string ImagePath,
    string Base64ImageDataUrl,
    string SystemPrompt,
    string UserPrompt,
    VlmTaskPreset TaskPreset,
    string TargetLanguage,
    bool ForceJsonOutput,
    int MaxTokens,
    double Temperature,
    int MaxImageDimension,
    string EndpointUrl,
    string ModelName,
    string ApiKey,
    TimeSpan Timeout,
    HttpClient? CustomHttpClient,
    IFlowExecutionContext Context,
    FileItemContext Item,
    int ConcurrencyLimit = 1,
    string? JsonSchema = null
);

/// <summary>
/// Contrato de abstracción para adaptadores de inferencia VLM (Vision-Language Model).
/// Permite desacoplar y alternar entre servidores externos (LM Studio, Ollama, OpenAI)
/// y el motor interno in-process de FileFlow Studio.
/// </summary>
public interface IVlmAdapter
{
    /// <summary>
    /// Identificador amigable del proveedor o estrategia de inferencia.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Ejecuta la inferencia multimodal de visión y lenguaje.
    /// </summary>
    Task<VlmInferenceResult> ExecuteAsync(VlmExecutionRequest request, CancellationToken cancellationToken);
}
