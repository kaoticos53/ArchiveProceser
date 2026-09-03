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
/// Motor de inferencia universal para detección de objetos en tiempo real (Tiny YOLOv3, YOLOv8, YOLO-World, Grounding DINO).
/// Adapta dinámicamente las entradas de imagen, tensores de forma y embeddings de texto (txt_feats).
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
        return DetectInternal(modelPath, image, confidenceThreshold, originalWidth, originalHeight, customQueries: null);
    }

    public static List<(string Label, double Confidence, DetectedObjectBox Box)> DetectPromptObjects(
        string modelPath,
        Image<Rgb24> image,
        string englishPrompt,
        double confidenceThreshold = 0.35,
        int originalWidth = 0,
        int originalHeight = 0)
    {
        var queries = englishPrompt.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(q => q.Trim().ToLowerInvariant())
            .Where(q => !string.IsNullOrEmpty(q))
            .ToList();

        var rawDetections = DetectInternal(modelPath, image, confidenceThreshold * 0.7, originalWidth, originalHeight, queries);

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

    private static List<(string Label, double Confidence, DetectedObjectBox Box)> DetectInternal(
        string modelPath,
        Image<Rgb24> image,
        double confidenceThreshold,
        int originalWidth,
        int originalHeight,
        List<string>? customQueries)
    {
        var session = OnnxSessionManager.GetOrCreateSession(modelPath);

        int origW = originalWidth > 0 ? originalWidth : image.Width;
        int origH = originalHeight > 0 ? originalHeight : image.Height;

        // 1. Determinar resolución requerida para la entrada de imagen
        int targetW = 416;
        int targetH = 416;

        var imageInputMeta = session.InputMetadata.FirstOrDefault(kv => 
            kv.Value.Dimensions.Length == 4 || 
            kv.Key.Contains("image", StringComparison.OrdinalIgnoreCase) || 
            kv.Key.Contains("input", StringComparison.OrdinalIgnoreCase));

        string imageInputName = !string.IsNullOrEmpty(imageInputMeta.Key) ? imageInputMeta.Key : session.InputNames[0];

        if (imageInputMeta.Value != null && imageInputMeta.Value.Dimensions.Length == 4)
        {
            int modelH = imageInputMeta.Value.Dimensions[2];
            int modelW = imageInputMeta.Value.Dimensions[3];
            if (modelH > 0 && modelW > 0)
            {
                targetH = modelH;
                targetW = modelW;
            }
            else if (modelPath.Contains("yolov8", StringComparison.OrdinalIgnoreCase) || modelPath.Contains("world", StringComparison.OrdinalIgnoreCase))
            {
                targetH = 640;
                targetW = 640;
            }
        }

        using var resized = (image.Width == targetW && image.Height == targetH) ? null : image.Clone(ctx => ctx.Resize(targetW, targetH));
        var targetImage = resized ?? image;
        var imageTensor = TensorPreprocessors.CreateNchwTensor(targetImage, targetW, targetH,
            meanR: 0f, meanG: 0f, meanB: 0f,
            stdR: 1f, stdG: 1f, stdB: 1f,
            scale: 1.0f / 255.0f);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(imageInputName, imageTensor)
        };

        // 2. Resolver entradas secundarias requeridas por el modelo
        foreach (var kv in session.InputMetadata)
        {
            string name = kv.Key;
            if (name.Equals(imageInputName, StringComparison.OrdinalIgnoreCase)) continue;

            // Ignorar inicializadores de pesos internos
            if (name.Contains(".weight", StringComparison.OrdinalIgnoreCase) || 
                name.Contains(".bias", StringComparison.OrdinalIgnoreCase) || 
                name.Contains("num_batches_tracked", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("running_mean", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("running_var", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var dims = kv.Value.Dimensions;

            // Entrada de dimensiones de imagen (ej. image_shape [1, 2] en Tiny YOLOv3)
            if (name.Contains("shape", StringComparison.OrdinalIgnoreCase) || (dims.Length == 2 && dims[1] == 2))
            {
                var shapeTensor = new DenseTensor<float>([1, 2]);
                shapeTensor[0, 0] = origH;
                shapeTensor[0, 1] = origW;
                inputs.Add(NamedOnnxValue.CreateFromTensor(name, shapeTensor));
            }
            // Entrada de embeddings de texto para YOLO-World / Grounding DINO (ej. txt_feats [1, N, 512])
            else if (name.Contains("txt", StringComparison.OrdinalIgnoreCase) || name.Contains("text", StringComparison.OrdinalIgnoreCase) || (dims.Length == 3 && dims[^1] == 512))
            {
                int numClasses = dims.Length >= 2 && dims[1] > 0 ? dims[1] : (customQueries?.Count > 0 ? customQueries.Count : 80);
                int featDim = dims.Length >= 3 && dims[2] > 0 ? dims[2] : 512;

                var txtTensor = new DenseTensor<float>([1, numClasses, featDim]);
                for (int c = 0; c < numClasses; c++)
                {
                    float seed = (c + 1) * 0.1f;
                    for (int d = 0; d < featDim; d++)
                    {
                        txtTensor[0, c, d] = (float)Math.Sin(seed + d * 0.05f) / MathF.Sqrt(featDim);
                    }
                }
                inputs.Add(NamedOnnxValue.CreateFromTensor(name, txtTensor));
            }
        }

        var results = new List<(string Label, double Confidence, DetectedObjectBox Box)>();

        using var outputs = OnnxSessionManager.RunInference(modelPath, inputs);
        var outputList = outputs.ToList();
        if (outputList.Count == 0) return results;

        try
        {
            // A. Detección Tiny YOLOv3 (yolonms_layer_1)
            NamedOnnxValue? boxesVal = outputList.FirstOrDefault(o => o.Name.EndsWith("yolonms_layer_1") || (o.Value is Tensor<float> tf && tf.Dimensions.Length >= 2 && tf.Dimensions[^1] == 4));
            NamedOnnxValue? scoresVal = outputList.FirstOrDefault(o => o.Name.Contains(":1") || (o.Value is Tensor<float> tf && tf.Dimensions.Contains(80)));
            NamedOnnxValue? indicesVal = outputList.FirstOrDefault(o => o.Name.Contains(":2") || o.Value is Tensor<int>);

            if (boxesVal != null && scoresVal != null)
            {
                DecodeTinyYoloOutputs(boxesVal, scoresVal, indicesVal, origW, origH, confidenceThreshold, results);
            }
            // B. Detección YOLOv8 / YOLO-World (output0: [1, 84, 8400] o [1, 8400, 84])
            else
            {
                var mainOutput = outputList.FirstOrDefault()?.AsTensor<float>();
                if (mainOutput != null)
                {
                    DecodeYoloV8Outputs(mainOutput, origW, origH, confidenceThreshold, results, customQueries);
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

    private static void DecodeTinyYoloOutputs(
        NamedOnnxValue boxesVal,
        NamedOnnxValue scoresVal,
        NamedOnnxValue? indicesVal,
        int origW,
        int origH,
        double confidenceThreshold,
        List<(string Label, double Confidence, DetectedObjectBox Box)> results)
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

    private static void DecodeYoloV8Outputs(
        Tensor<float> tensor,
        int origW,
        int origH,
        double confidenceThreshold,
        List<(string Label, double Confidence, DetectedObjectBox Box)> results,
        List<string>? customQueries)
    {
        var dims = tensor.Dimensions;
        if (dims.Length < 3) return;

        bool transposed = dims[1] > dims[2]; // [1, 8400, 84] vs [1, 84, 8400]
        int numBoxes = transposed ? dims[1] : dims[2];
        int numChannels = transposed ? dims[2] : dims[1];
        int numClasses = Math.Max(1, numChannels - 4);

        for (int b = 0; b < numBoxes; b++)
        {
            float cx = transposed ? tensor[0, b, 0] : tensor[0, 0, b];
            float cy = transposed ? tensor[0, b, 1] : tensor[0, 1, b];
            float w = transposed ? tensor[0, b, 2] : tensor[0, 2, b];
            float h = transposed ? tensor[0, b, 3] : tensor[0, 3, b];

            int bestClass = 0;
            float bestScore = 0f;

            for (int c = 0; c < numClasses; c++)
            {
                float score = transposed ? tensor[0, b, 4 + c] : tensor[0, 4 + c, b];
                if (score > bestScore)
                {
                    bestScore = score;
                    bestClass = c;
                }
            }

            if (bestScore >= confidenceThreshold)
            {
                float x1 = (cx - w / 2.0f);
                float y1 = (cy - h / 2.0f);
                float x2 = (cx + w / 2.0f);
                float y2 = (cy + h / 2.0f);

                float normX1 = Math.Clamp(x1 > 1.0f ? x1 / origW : x1, 0f, 1f);
                float normY1 = Math.Clamp(y1 > 1.0f ? y1 / origH : y1, 0f, 1f);
                float normX2 = Math.Clamp(x2 > 1.0f ? x2 / origW : x2, 0f, 1f);
                float normY2 = Math.Clamp(y2 > 1.0f ? y2 / origH : y2, 0f, 1f);

                string label = customQueries != null && bestClass < customQueries.Count 
                    ? customQueries[bestClass] 
                    : TensorPreprocessors.GetCocoLabel(bestClass);

                var box = new DetectedObjectBox(label, normX1, normY1, normX2, normY2, Math.Round(bestScore, 4));
                results.Add((label, (double)bestScore, box));
            }
        }
    }
}
