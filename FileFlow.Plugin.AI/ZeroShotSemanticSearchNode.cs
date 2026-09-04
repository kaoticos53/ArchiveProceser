using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Nodo de búsqueda semántica y clasificación zero-shot para documentos e imágenes (CLIP / BGE Small).
/// Calcula similitud de coseno con prompts en lenguaje natural libre y enruta según umbral de confianza.
/// </summary>
[NodeDefinition("ZeroShotSemanticSearchNode_Name", "LanguageAI", "ZeroShotSemanticSearchNode_Desc", PipelineRole.Filter,
    "semantica", "embeddings", "clip", "bge", "similitud", "zero shot", "buscar", "clasificar")]
public class ZeroShotSemanticSearchNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("ZeroShotSemanticSearchNode_Name", "Búsqueda y Clasificación Semántica (Zero-Shot)");
    public string Category => "LanguageAI";
    public string Description => LocalizationManager.Instance.GetString("ZeroShotSemanticSearchNode_Desc", "Clasifica y enruta documentos o imágenes mediante similitud semántica en lenguaje natural.");

    public IReadOnlyList<NodePort> Inputs { get; } =
    [
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    ];

    public IReadOnlyList<NodePort> Outputs { get; } =
    [
        new NodePort("Matched", typeof(FileItemContext), PortDirection.Output, "Matched"),
        new NodePort("Unmatched", typeof(FileItemContext), PortDirection.Output, "Unmatched"),
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
        new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
    ];

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Model"] = "Auto",
        ["CustomModelPath"] = "",
        ["SearchQuery"] = "",
        ["CandidateLabels"] = "Factura, Contrato, Nómina, Presupuesto, Documento",
        ["SimilarityThreshold"] = 0.55,
        ["TopK"] = 3
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("Model", ParameterEditorType.Dropdown, DefaultValue: "Auto",
            Options: ["Auto", "clip-vit-b32", "bge-small-multilingual", "Custom"],
            HelpText: "Modelo neural de embeddings semánticos ('Auto' selecciona según hardware).", DisplayOrder: 1),
        new("CustomModelPath", ParameterEditorType.FilePath, DefaultValue: "",
            HelpText: "Ruta a un archivo .onnx de embeddings local si seleccionó 'Custom'.", DisplayOrder: 2),
        new("SearchQuery", ParameterEditorType.Text, DefaultValue: "",
            HelpText: "Consulta o concepto clave en lenguaje natural para filtrar o buscar.", DisplayOrder: 3),
        new("CandidateLabels", ParameterEditorType.MultiLineText, DefaultValue: "Factura, Contrato, Nómina, Presupuesto, Documento",
            HelpText: "Lista de categorías candidatas separadas por comas para clasificar el elemento.", DisplayOrder: 4),
        new("SimilarityThreshold", ParameterEditorType.Slider, DefaultValue: 0.55, Min: 0.1, Max: 0.95, Step: 0.05,
            HelpText: "Umbral mínimo de similitud de coseno para bifurcar hacia el puerto 'Matched'.", DisplayOrder: 5),
        new("TopK", ParameterEditorType.Number, DefaultValue: 3, Min: 1, Max: 10,
            HelpText: "Número de categorías principales a registrar en los metadatos.", DisplayOrder: 6)
    ];

    public async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !File.Exists(item.CurrentPath))
        {
            context.Log($"[SemanticSearch] Archivo no encontrado: '{item.CurrentPath}'", LogLevel.Error, item);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
            return;
        }

        try
        {
            string modelChoice = Parameters.TryGetValue("Model", out var mVal) ? mVal?.ToString() ?? "Auto" : "Auto";
            string? customPath = Parameters.TryGetValue("CustomModelPath", out var cpVal) ? cpVal?.ToString() : null;
            string searchQuery = Parameters.TryGetValue("SearchQuery", out var sqVal) ? sqVal?.ToString() ?? string.Empty : string.Empty;
            string candidateLabelsRaw = Parameters.TryGetValue("CandidateLabels", out var clVal) ? clVal?.ToString() ?? string.Empty : string.Empty;
            double threshold = Parameters.TryGetValue("SimilarityThreshold", out var stVal) ? ParameterHelper.GetDouble(stVal, 0.55) : 0.55;

            var candidateLabels = candidateLabelsRaw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            string? modelPath = await AiModelManager.ResolveModelPathAsync(
                modelChoice,
                customPath,
                AiTaskType.SemanticEmbeddings,
                context,
                item,
                cancellationToken).ConfigureAwait(false);

            context.Log($"[SemanticSearch] 🔍 Analizando semántica de '{item.FileName}' contra {candidateLabels.Count} categorías...", LogLevel.Information, item);

            var result = await Task.Run(
                () => SemanticEmbeddingEngine.ClassifyZeroShot(modelPath, item.CurrentPath, candidateLabels, searchQuery, threshold),
                cancellationToken).ConfigureAwait(false);

            item.Metadata["AI:TopCategory"] = result.TopCategory;
            item.Metadata["AI:TopSimilarityScore"] = result.TopScore;
            item.Metadata["AI:IsQueryMatch"] = result.IsQueryMatch;
            item.Metadata["AI:CategoryScoresJson"] = JsonSerializer.Serialize(result.CategoryScores);
            item.Metadata["AI:EmbeddingModel"] = string.IsNullOrWhiteSpace(modelPath) ? "semantic-embedder" : Path.GetFileNameWithoutExtension(modelPath);

            context.Log($"[SemanticSearch] Clasificación: '{result.TopCategory}' ({result.TopScore:P1} confianza). Coincidencia con consulta: {result.IsQueryMatch}.",
                LogLevel.Information, item);

            if (result.IsQueryMatch)
            {
                await context.EmitAsync("Matched", item).ConfigureAwait(false);
            }
            else
            {
                await context.EmitAsync("Unmatched", item).ConfigureAwait(false);
            }

            await context.EmitAsync("Out", item).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            context.Log($"[SemanticSearch] ❌ Error analizando {item.FileName}: {ex.Message}", LogLevel.Error, item);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
        }
    }
}
