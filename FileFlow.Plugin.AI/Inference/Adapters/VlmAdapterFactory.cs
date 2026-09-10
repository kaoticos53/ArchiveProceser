using System;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Factoría de adaptadores de inferencia VLM (Vision-Language Model).
/// Resuelve de forma desacoplada la estrategia de ejecución entre servidores externos
/// (LM Studio, Ollama, OpenAI) y el motor interno in-process.
/// </summary>
public static class VlmAdapterFactory
{
    public const string ProviderInProcess = "Internal Engine (In-Process)";
    public const string ProviderLmStudio = "LM Studio (localhost:1234)";
    public const string ProviderOllama = "Ollama (localhost:11434)";
    public const string ProviderCustom = "Custom Endpoint";

    public static readonly string[] AvailableProviders =
    [
        ProviderLmStudio,
        ProviderOllama,
        ProviderInProcess,
        ProviderCustom
    ];

    /// <summary>
    /// Resuelve e instancia el adaptador correspondiente para el proveedor especificado.
    /// </summary>
    public static IVlmAdapter Create(string? provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return new OpenAiCompatibleVlmAdapter();
        }

        if (provider.Contains("In-Process", StringComparison.OrdinalIgnoreCase) ||
            provider.Contains("Internal", StringComparison.OrdinalIgnoreCase) ||
            provider.Contains("Local Engine", StringComparison.OrdinalIgnoreCase))
        {
            return new InProcessVlmAdapter();
        }

        return new OpenAiCompatibleVlmAdapter();
    }
}
