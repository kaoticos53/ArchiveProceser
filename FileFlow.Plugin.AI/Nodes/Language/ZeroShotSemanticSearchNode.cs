using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Storage;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Nodo de búsqueda semántica y clasificación zero-shot para documentos e imágenes (CLIP / BGE Small).
/// Calcula similitud de coseno con prompts en lenguaje natural libre y enruta según umbral de confianza.
/// </summary>
[NodeDefinition("ZeroShotSemanticSearchNode_Name", "LanguageAI", "ZeroShotSemanticSearchNode_Desc", PipelineRole.Filter,
    "semantica", "embeddings", "clip", "bge", "similitud", "zero shot", "buscar", "clasificar")]
public sealed class ZeroShotSemanticSearchNode : IFlowNode
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
        ["SearchQuery"] = "",
        ["CandidateLabels"] = "Factura, Contrato, Nómina, Presupuesto, Documento",
        ["SimilarityThreshold"] = 0.55,
        ["TopK"] = 3
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("Model", ParameterEditorType.Dropdown, DefaultValue: "Auto",
            Options: ["Auto", "clip-vit-b32", "bge-small-multilingual"],
            HelpText: "Modelo neural de embeddings semánticos ('Auto' selecciona según hardware).", DisplayOrder: 1),
        new("SearchQuery", ParameterEditorType.Text, DefaultValue: "",
            HelpText: "Consulta o concepto clave en lenguaje natural para filtrar o buscar.", DisplayOrder: 2),
        new("CandidateLabels", ParameterEditorType.MultiLineText, DefaultValue: "Factura, Contrato, Nómina, Presupuesto, Documento",
            HelpText: "Lista de categorías candidatas separadas por comas para clasificar el elemento.", DisplayOrder: 3),
        new("SimilarityThreshold", ParameterEditorType.Slider, DefaultValue: 0.55, Min: 0.1, Max: 0.95, Step: 0.05,
            HelpText: "Umbral mínimo de similitud de coseno para bifurcar hacia el puerto 'Matched'.", DisplayOrder: 4),
        new("TopK", ParameterEditorType.Number, DefaultValue: 3, Min: 1, Max: 10,
            HelpText: "Número de categorías principales a registrar en los metadatos.", DisplayOrder: 5)
    ];

    public async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        var storage = context.GetStorage();
        bool fileExists = await storage.FileExistsAsync(item.CurrentPath, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !fileExists)
        {
            context.Log($"[SemanticSearch] Archivo no encontrado: '{item.CurrentPath}'", LogLevel.Error, item);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
            return;
        }

        try
        {
            string modelChoice = Parameters.TryGetValue("Model", out var mVal) ? mVal?.ToString() ?? "Auto" : "Auto";
            string searchQuery = Parameters.TryGetValue("SearchQuery", out var sqVal) ? sqVal?.ToString() ?? string.Empty : string.Empty;
            string candidateLabelsRaw = Parameters.TryGetValue("CandidateLabels", out var clVal) ? clVal?.ToString() ?? string.Empty : string.Empty;
            double threshold = Parameters.TryGetValue("SimilarityThreshold", out var stVal) ? ParameterHelper.GetDouble(stVal, 0.55) : 0.55;

            var candidateLabels = candidateLabelsRaw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            string? modelPath = await AiModelManager.ResolveModelPathAsync(
                modelChoice,
                AiTaskType.SemanticEmbeddings,
                context,
                item,
                cancellationToken).ConfigureAwait(false);

            // Leer el contenido textual del archivo para generar embeddings semánticos significativos.
            // Si es una imagen o binario, usar el nombre del archivo como texto representativo.
            // Esto evita que el embedding se calcule sobre la ruta del archivo, que no tiene valor semántico.
            string contentForEmbedding = await ResolveContentForEmbeddingAsync(storage, item, cancellationToken).ConfigureAwait(false);

            context.Log($"[SemanticSearch] 🔍 Analizando semántica de '{item.FileName}' contra {candidateLabels.Count} categorías...", LogLevel.Information, item);

            var result = await Task.Run(
                () => SemanticEmbeddingEngine.ClassifyZeroShot(modelPath, contentForEmbedding, candidateLabels, searchQuery, threshold),
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            context.Log($"[SemanticSearch] ❌ Error analizando {item.FileName}: {ex.Message}", LogLevel.Error, item);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Resuelve el contenido textual del archivo para pasar al motor de embeddings.
    /// Para archivos de texto lee hasta 2000 caracteres; para imágenes/binarios devuelve el nombre base.
    /// </summary>
    private static async Task<string> ResolveContentForEmbeddingAsync(
        IStorageService storage,
        FileItemContext item,
        CancellationToken cancellationToken)
    {
        string ext = Path.GetExtension(item.CurrentPath).ToLowerInvariant();

        // Para imágenes: pasar la ruta real (el engine ONNX de CLIP puede usar la imagen directamente)
        // o el nombre base como texto semántico en modo fallback léxico.
        if (ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp" or ".gif" or ".tiff")
        {
            // Si el archivo existe físicamente, el engine puede leer la imagen directamente.
            // Usamos la ruta real; si es VFS, caemos al nombre de archivo como texto.
            return File.Exists(item.CurrentPath)
                ? item.CurrentPath
                : Path.GetFileNameWithoutExtension(item.FileName);
        }

        // Para archivos de texto: leer contenido via IStorageService (compatible con VFS)
        try
        {
            await using var stream = await storage.OpenReadAsync(item.CurrentPath, cancellationToken).ConfigureAwait(false);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: false);
            char[] buffer = new char[2000];
            int charsRead = await reader.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            string content = new string(buffer, 0, charsRead).Trim();
            return string.IsNullOrWhiteSpace(content) ? item.FileName : content;
        }
        catch
        {
            // Fallback al nombre de archivo si no se puede leer el contenido
            return Path.GetFileNameWithoutExtension(item.FileName);
        }
    }
}
