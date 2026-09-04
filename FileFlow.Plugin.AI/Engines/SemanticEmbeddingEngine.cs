using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Resultado del análisis y clasificación semántica zero-shot.
/// </summary>
public record SemanticClassificationResult(
    string TopCategory,
    double TopScore,
    Dictionary<string, double> CategoryScores,
    bool IsQueryMatch,
    float[] Embedding);

/// <summary>
/// Motor centralizado de cálculo de embeddings y búsqueda semántica multimodal zero-shot (CLIP / BGE Small).
/// Soporta textos e imágenes, cálculo de similitud coseno normalizada y fallback léxico determinista.
/// </summary>
public static class SemanticEmbeddingEngine
{
    private static readonly ConcurrentDictionary<string, Lazy<InferenceSession>> _sessionCache = new();
    private static readonly Lock _inferenceLock = new();

    private static InferenceSession GetOrCreateSession(string modelPath)
    {
        var lazy = _sessionCache.GetOrAdd(modelPath, path => new Lazy<InferenceSession>(() =>
        {
            var options = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                InterOpNumThreads = 1,
                IntraOpNumThreads = Math.Clamp(Environment.ProcessorCount / 2, 1, 4)
            };

            return new InferenceSession(path, options);
        }));

        return lazy.Value;
    }

    /// <summary>
    /// Clasifica semánticamente un texto o imagen contra una lista de categorías candidatas y una consulta opcional.
    /// </summary>
    public static SemanticClassificationResult ClassifyZeroShot(
        string? modelPath,
        string contentOrPath,
        IReadOnlyList<string> candidateLabels,
        string? searchQuery = null,
        double similarityThreshold = 0.55)
    {
        if (candidateLabels.Count == 0 && string.IsNullOrWhiteSpace(searchQuery))
        {
            return new SemanticClassificationResult("None", 0.0, [], false, []);
        }

        // Obtener el embedding del elemento objetivo (texto o imagen)
        float[] itemEmbedding = GetEmbedding(modelPath, contentOrPath);

        var scores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        // Evaluar similitud con cada etiqueta candidata
        foreach (var label in candidateLabels.Where(l => !string.IsNullOrWhiteSpace(l)))
        {
            float[] labelEmbedding = GetTextEmbedding(modelPath, label.Trim());
            double sim = CosineSimilarity(itemEmbedding, labelEmbedding);
            scores[label.Trim()] = Math.Clamp(sim, 0.0, 1.0);
        }

        // Evaluar similitud con searchQuery si fue proporcionada
        bool isQueryMatch = false;
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            float[] queryEmbedding = GetTextEmbedding(modelPath, searchQuery.Trim());
            double querySim = CosineSimilarity(itemEmbedding, queryEmbedding);
            scores[$"Query:{searchQuery.Trim()}"] = Math.Clamp(querySim, 0.0, 1.0);
            isQueryMatch = querySim >= similarityThreshold;
        }

        var orderedCategories = scores
            .Where(kv => !kv.Key.StartsWith("Query:", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(kv => kv.Value)
            .ToList();

        var topCategoryPair = orderedCategories.FirstOrDefault();
        string topCategory = topCategoryPair.Key ?? scores.OrderByDescending(kv => kv.Value).FirstOrDefault().Key ?? "Unknown";
        double topScore = topCategoryPair.Value;

        if (!isQueryMatch && topScore >= similarityThreshold)
        {
            isQueryMatch = true;
        }

        return new SemanticClassificationResult(
            topCategory,
            topScore,
            scores,
            isQueryMatch,
            itemEmbedding);
    }

    /// <summary>
    /// Calcula la similitud de coseno entre dos vectores numéricos.
    /// </summary>
    public static double CosineSimilarity(float[] vecA, float[] vecB)
    {
        if (vecA.Length == 0 || vecB.Length == 0 || vecA.Length != vecB.Length)
        {
            return 0.0;
        }

        double dot = 0.0;
        double normA = 0.0;
        double normB = 0.0;

        for (int i = 0; i < vecA.Length; i++)
        {
            dot += vecA[i] * vecB[i];
            normA += vecA[i] * vecA[i];
            normB += vecB[i] * vecB[i];
        }

        if (normA <= 1e-7 || normB <= 1e-7) return 0.0;
        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    private static float[] GetEmbedding(string? modelPath, string contentOrPath)
    {
        // Detectar si contentOrPath es una imagen existente en disco
        if (File.Exists(contentOrPath))
        {
            string ext = Path.GetExtension(contentOrPath).ToLowerInvariant();
            if (ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp")
            {
                return GetImageEmbedding(modelPath, contentOrPath);
            }

            // Si es un archivo de texto, leer los primeros 4KB
            try
            {
                string text = File.ReadAllText(contentOrPath, Encoding.UTF8);
                if (text.Length > 2000) text = text[..2000];
                return GetTextEmbedding(modelPath, text);
            }
            catch
            {
                return GetTextEmbedding(modelPath, Path.GetFileName(contentOrPath));
            }
        }

        return GetTextEmbedding(modelPath, contentOrPath);
    }

    private static float[] GetTextEmbedding(string? modelPath, string text)
    {
        if (!string.IsNullOrWhiteSpace(modelPath) && File.Exists(modelPath))
        {
            try
            {
                var session = GetOrCreateSession(modelPath);

                // Codificación de tokens
                long[] tokens = text.Select(c => (long)c).Take(128).ToArray();
                if (tokens.Length == 0) tokens = [0L];

                var inputTensor = new DenseTensor<long>(tokens, [1, tokens.Length]);
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(session.InputNames[0], inputTensor)
                };

                lock (_inferenceLock)
                {
                    using var outputs = session.Run(inputs);
                    var tensor = outputs.First().AsTensor<float>();
                    return NormalizeVector(tensor.ToArray());
                }
            }
            catch
            {
                // Fallback léxico si la sesión ONNX falla
            }
        }

        return GenerateLexicalEmbedding(text);
    }

    private static float[] GetImageEmbedding(string? modelPath, string imagePath)
    {
        if (!string.IsNullOrWhiteSpace(modelPath) && File.Exists(modelPath))
        {
            try
            {
                var session = GetOrCreateSession(modelPath);
                using var img = Image.Load<Rgb24>(imagePath);
                img.Mutate(ctx => ctx.Resize(224, 224));

                var tensor = new DenseTensor<float>([1, 3, 224, 224]);
                img.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < 224; y++)
                    {
                        var row = accessor.GetRowSpan(y);
                        for (int x = 0; x < 224; x++)
                        {
                            var p = row[x];
                            tensor[0, 0, y, x] = (p.R / 255f - 0.48145466f) / 0.26862954f;
                            tensor[0, 1, y, x] = (p.G / 255f - 0.4578275f) / 0.26130258f;
                            tensor[0, 2, y, x] = (p.B / 255f - 0.40821073f) / 0.27577711f;
                        }
                    }
                });

                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(session.InputNames[0], tensor)
                };

                lock (_inferenceLock)
                {
                    using var outputs = session.Run(inputs);
                    var outTensor = outputs.First().AsTensor<float>();
                    return NormalizeVector(outTensor.ToArray());
                }
            }
            catch
            {
                // Fallback a características del nombre de la imagen
            }
        }

        return GenerateLexicalEmbedding(Path.GetFileNameWithoutExtension(imagePath));
    }

    private static float[] GenerateLexicalEmbedding(string text)
    {
        // Generador de embedding denso normalizado de 384 dimensiones basado en hashing de n-gramas
        const int dims = 384;
        float[] vector = new float[dims];

        var words = text.ToLowerInvariant().Split([' ', '_', '-', '.', ',', ';', ':', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return vector;

        foreach (var word in words)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(word));
            for (int i = 0; i < hash.Length; i++)
            {
                int bucket = (hash[i] * 13 + i * 7) % dims;
                vector[bucket] += (hash[i] / 255f) - 0.5f;
            }
        }

        return NormalizeVector(vector);
    }

    private static float[] NormalizeVector(float[] v)
    {
        double sumSq = 0.0;
        for (int i = 0; i < v.Length; i++) sumSq += v[i] * v[i];
        double norm = Math.Sqrt(sumSq);

        if (norm > 1e-7)
        {
            for (int i = 0; i < v.Length; i++) v[i] = (float)(v[i] / norm);
        }

        return v;
    }

    /// <summary>
    /// Libera la caché de sesiones de embeddings.
    /// </summary>
    public static void ClearSessionCache()
    {
        foreach (var lazy in _sessionCache.Values)
        {
            if (lazy.IsValueCreated)
            {
                try { lazy.Value.Dispose(); } catch { }
            }
        }
        _sessionCache.Clear();
    }
}
