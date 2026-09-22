using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Plugin.AI.Inference;
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
public sealed class ZeroShotSemanticSearchNode : AiFlowNodeBase
{
    public override string Name => LocalizationManager.Instance.GetString("ZeroShotSemanticSearchNode_Name", "Búsqueda y Clasificación Semántica (Zero-Shot)");
    public override string Category => "LanguageAI";
    public override string Description => LocalizationManager.Instance.GetString("ZeroShotSemanticSearchNode_Desc", "Clasifica y enruta documentos o imágenes mediante similitud semántica en lenguaje natural.");
    public override AiTaskType TaskType => AiTaskType.SemanticEmbeddings;

    /// <summary>
    /// Los embeddings viven en su propio almacén, que ahora publica cambios al mismo evento que el de
    /// visión: declararlo aquí es lo que permite al nodo reportar <c>IsModelLoaded</c>, precargar y
    /// descargar el modelo de CLIP o BGE como cualquier otro nodo de IA.
    /// </summary>
    protected override OnnxSessionStore SessionStore => SemanticEmbeddingEngine.SessionStore;

    public ZeroShotSemanticSearchNode()
    {
        Inputs =
        [
            new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
        ];

        Outputs =
        [
            new NodePort("Matched", typeof(FileItemContext), PortDirection.Output, "Matched"),
            new NodePort("Unmatched", typeof(FileItemContext), PortDirection.Output, "Unmatched"),
            new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
            new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
        ];

        Parameters["Model"] = "Auto";
        Parameters["SearchQuery"] = "";
        Parameters["CandidateLabels"] = "Factura, Contrato, Nómina, Presupuesto, Documento";
        Parameters["SimilarityThreshold"] = 0.55;
        Parameters["TopK"] = 3;
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
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

    public override async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        var storage = context.GetStorage();
        bool fileExists = await storage.FileExistsAsync(item.CurrentPath, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !fileExists)
        {
            Log(context, $"[SemanticSearch] Archivo no encontrado: '{item.CurrentPath}'", LogLevel.Error, item);
            await EmitAsync(context, item, "Error").ConfigureAwait(false);
            return;
        }

        try
        {
            string searchQuery = GetParameter("SearchQuery", string.Empty);
            string candidateLabelsRaw = GetParameter("CandidateLabels", string.Empty);
            double threshold = GetParameter("SimilarityThreshold", 0.55);

            var candidateLabels = candidateLabelsRaw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            string? modelPath = await ResolveModelPathAsync(context, item, cancellationToken).ConfigureAwait(false);

            // Leer el contenido textual del archivo para generar embeddings semánticos significativos.
            // Si es una imagen o binario, usar el nombre del archivo como texto representativo.
            // Esto evita que el embedding se calcule sobre la ruta del archivo, que no tiene valor semántico.
            string contentForEmbedding = await ResolveContentForEmbeddingAsync(storage, item, cancellationToken).ConfigureAwait(false);

            Log(context, $"[SemanticSearch] 🔍 Analizando semántica de '{item.FileName}' contra {candidateLabels.Count} categorías...", LogLevel.Information, item);

            var result = await Task.Run(
                () => SemanticEmbeddingEngine.ClassifyZeroShot(modelPath, contentForEmbedding, candidateLabels, searchQuery, threshold),
                cancellationToken).ConfigureAwait(false);

            item.Metadata["AI:TopCategory"] = result.TopCategory;
            item.Metadata["AI:TopSimilarityScore"] = result.TopScore;
            item.Metadata["AI:IsQueryMatch"] = result.IsQueryMatch;
            item.Metadata["AI:CategoryScoresJson"] = JsonSerializer.Serialize(result.CategoryScores);
            item.Metadata["AI:EmbeddingModel"] = string.IsNullOrWhiteSpace(modelPath) ? "semantic-embedder" : Path.GetFileNameWithoutExtension(modelPath);

            Log(context, $"[SemanticSearch] Clasificación: '{result.TopCategory}' ({result.TopScore:P1} confianza). Coincidencia con consulta: {result.IsQueryMatch}.",
                LogLevel.Information, item);

            if (result.IsQueryMatch)
            {
                await EmitAsync(context, item, "Matched").ConfigureAwait(false);
            }
            else
            {
                await EmitAsync(context, item, "Unmatched").ConfigureAwait(false);
            }

            await EmitAsync(context, item).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log(context, $"[SemanticSearch] ❌ Error analizando {item.FileName}: {ex.Message}", LogLevel.Error, item);
            await EmitAsync(context, item, "Error").ConfigureAwait(false);
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
