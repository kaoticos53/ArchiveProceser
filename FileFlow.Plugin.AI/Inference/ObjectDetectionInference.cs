using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FileFlow.Plugin.AI.Inference;

/// <summary>
/// Contenedor de caja delimitadora de objeto detectado con etiqueta, confianza y coordenadas normalizadas.
/// </summary>
public record struct DetectedObjectBox(string Label, float X1, float Y1, float X2, float Y2, double Score);

/// <summary>
/// Motor de inferencia para detección de objetos en tiempo real y detección guiada por prompts (Open-Vocabulary).
/// </summary>
public static class ObjectDetectionInference
{
    public static List<(string Label, double Confidence, DetectedObjectBox Box)> DetectObjects(
        string modelPath,
        Image<Rgb24> image,
        double confidenceThreshold = 0.4,
        int originalWidth = 0,
        int originalHeight = 0)
    {
        var session = OnnxSessionManager.GetOrCreateSession(modelPath);

        int origW = originalWidth > 0 ? originalWidth : image.Width;
        int origH = originalHeight > 0 ? originalHeight : image.Height;

        using var resized = (image.Width == 416 && image.Height == 416) ? null : image.Clone(ctx => ctx.Resize(416, 416));
        var targetImage = resized ?? image;
        var imageTensor = TensorPreprocessors.CreateNchwTensor(targetImage, 416, 416,
            meanR: 0f, meanG: 0f, meanB: 0f,
            stdR: 1f, stdG: 1f, stdB: 1f,
            scale: 1.0f / 255.0f);

        var shapeTensor = new DenseTensor<float>([1, 2]);
        shapeTensor[0, 0] = origH;
        shapeTensor[0, 1] = origW;

        string imageInputName = session.InputMetadata
            .FirstOrDefault(kv => kv.Value.Dimensions.Length == 4 || kv.Key.Contains("input", StringComparison.OrdinalIgnoreCase) || kv.Key.Contains("image", StringComparison.OrdinalIgnoreCase)).Key 
            ?? session.InputNames[0];

        string? shapeInputName = session.InputMetadata
            .FirstOrDefault(kv => kv.Value.Dimensions.Length == 2 || kv.Key.Contains("shape", StringComparison.OrdinalIgnoreCase)).Key;

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(imageInputName, imageTensor)
        };

        if (!string.IsNullOrEmpty(shapeInputName))
        {
            inputs.Add(NamedOnnxValue.CreateFromTensor(shapeInputName, shapeTensor));
        }

        var results = new List<(string Label, double Confidence, DetectedObjectBox Box)>();

        using var outputs = OnnxSessionManager.RunInference(modelPath, inputs);
        var outputList = outputs.ToList();
        if (outputList.Count == 0) return results;

            try
            {
                NamedOnnxValue? boxesVal = outputList.FirstOrDefault(o => o.Name.EndsWith("yolonms_layer_1") || (o.Value is Tensor<float> tf && tf.Dimensions.Length >= 2 && tf.Dimensions[^1] == 4));
                NamedOnnxValue? scoresVal = outputList.FirstOrDefault(o => o.Name.Contains(":1") || (o.Value is Tensor<float> tf && tf.Dimensions.Contains(80)));
                NamedOnnxValue? indicesVal = outputList.FirstOrDefault(o => o.Name.Contains(":2") || o.Value is Tensor<int>);

                boxesVal ??= outputList.ElementAtOrDefault(0);
                scoresVal ??= outputList.ElementAtOrDefault(1);
                indicesVal ??= outputList.ElementAtOrDefault(2);

                if (boxesVal != null && scoresVal != null)
                {
                    var boxesTensor = boxesVal.AsTensor<float>();
                    var scoresTensor = scoresVal.AsTensor<float>();

                    int numClasses = scoresTensor.Dimensions.Length >= 2 ? scoresTensor.Dimensions[1] : 80;
                    int numBoxes = scoresTensor.Dimensions.Length >= 3 
                        ? scoresTensor.Dimensions[2] 
                        : (boxesTensor.Dimensions.Length >= 2 ? boxesTensor.Dimensions[1] : 2535);

                    bool processedFromIndices = false;

                    if (indicesVal != null && indicesVal.Value is Tensor<int> indicesTensor)
                    {
                        int totalDetections = (int)(indicesTensor.Length / 3);
                        for (int k = 0; k < totalDetections; k++)
                        {
                            int classIdx = indicesTensor.GetValue(k * 3 + 1);
                            int boxIdx = indicesTensor.GetValue(k * 3 + 2);

                            if (classIdx < 0 || classIdx >= 80 || boxIdx < 0 || boxIdx >= numBoxes) continue;

                            float score = scoresTensor.GetValue(classIdx * numBoxes + boxIdx);
                            if (score >= confidenceThreshold)
                            {
                                float rawY1 = boxesTensor.GetValue(boxIdx * 4 + 0);
                                float rawX1 = boxesTensor.GetValue(boxIdx * 4 + 1);
                                float rawY2 = boxesTensor.GetValue(boxIdx * 4 + 2);
                                float rawX2 = boxesTensor.GetValue(boxIdx * 4 + 3);

                                float normX1 = Math.Clamp(Math.Min(rawX1, rawX2) / (rawX1 > 1.0f ? origW : 1.0f), 0f, 1f);
                                float normY1 = Math.Clamp(Math.Min(rawY1, rawY2) / (rawY1 > 1.0f ? origH : 1.0f), 0f, 1f);
                                float normX2 = Math.Clamp(Math.Max(rawX1, rawX2) / (rawX2 > 1.0f ? origW : 1.0f), 0f, 1f);
                                float normY2 = Math.Clamp(Math.Max(rawY1, rawY2) / (rawY2 > 1.0f ? origH : 1.0f), 0f, 1f);

                                string label = TensorPreprocessors.GetCocoLabel(classIdx);
                                var box = new DetectedObjectBox(label, normX1, normY1, normX2, normY2, Math.Round(score, 4));
                                results.Add((label, (double)score, box));
                                processedFromIndices = true;
                            }
                        }
                    }

                    if (!processedFromIndices && results.Count == 0)
                    {
                        for (int b = 0; b < numBoxes; b++)
                        {
                            int bestClass = 0;
                            float bestScore = 0f;

                            for (int c = 0; c < Math.Min(numClasses, 80); c++)
                            {
                                float score = scoresTensor.GetValue(c * numBoxes + b);
                                if (score > bestScore)
                                {
                                    bestScore = score;
                                    bestClass = c;
                                }
                            }

                            if (bestScore >= confidenceThreshold)
                            {
                                float rawY1 = boxesTensor.GetValue(b * 4 + 0);
                                float rawX1 = boxesTensor.GetValue(b * 4 + 1);
                                float rawY2 = boxesTensor.GetValue(b * 4 + 2);
                                float rawX2 = boxesTensor.GetValue(b * 4 + 3);

                                float normX1 = Math.Clamp(Math.Min(rawX1, rawX2) / (rawX1 > 1.0f ? origW : 1.0f), 0f, 1f);
                                float normY1 = Math.Clamp(Math.Min(rawY1, rawY2) / (rawY1 > 1.0f ? origH : 1.0f), 0f, 1f);
                                float normX2 = Math.Clamp(Math.Max(rawX1, rawX2) / (rawX2 > 1.0f ? origW : 1.0f), 0f, 1f);
                                float normY2 = Math.Clamp(Math.Max(rawY1, rawY2) / (rawY2 > 1.0f ? origH : 1.0f), 0f, 1f);

                                string label = TensorPreprocessors.GetCocoLabel(bestClass);
                                var box = new DetectedObjectBox(label, normX1, normY1, normX2, normY2, Math.Round(bestScore, 4));
                                results.Add((label, (double)bestScore, box));
                            }
                        }
                    }
                }
            }
            catch
            {
                return results;
            }

        results.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));
        return results.Take(25).ToList();
    }

    public static List<(string Label, double Confidence, DetectedObjectBox Box)> DetectPromptObjects(
        string modelPath,
        Image<Rgb24> image,
        string englishPrompt,
        double confidenceThreshold = 0.35,
        int originalWidth = 0,
        int originalHeight = 0)
    {
        var rawDetections = DetectObjects(modelPath, image, confidenceThreshold * 0.7, originalWidth, originalHeight);

        var queries = englishPrompt.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(q => q.Trim().ToLowerInvariant())
            .Where(q => !string.IsNullOrEmpty(q))
            .ToList();

        if (queries.Count == 0)
        {
            return rawDetections;
        }

        var matchedResults = new List<(string Label, double Confidence, DetectedObjectBox Box)>();

        foreach (var det in rawDetections)
        {
            string detectedLabel = det.Label.ToLowerInvariant();
            
            bool matches = queries.Any(q => 
                detectedLabel.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                q.Contains(detectedLabel, StringComparison.OrdinalIgnoreCase) ||
                TensorPreprocessors.IsSemanticMatch(q, detectedLabel));

            if (matches && det.Confidence >= confidenceThreshold)
            {
                matchedResults.Add(det);
            }
        }

        return matchedResults;
    }
}
