using System.Security.Cryptography;
using System.Text;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FileFlow.Plugin.AI.Inference.Adapters;

/// <summary>
/// Adaptador especializado para modelos de la familia YOLO-World y Grounding DINO (Open-Vocabulary Object Detection).
/// Aplica preprocesamiento Letterbox 640x640 con padding, genera tensores de características semánticas de texto
/// (CLIP ViT-B/32 de 512 dimensiones) y decodifica las cajas delimitadoras aplicando des-padding para máxima precisión geométrica.
/// </summary>
public class YoloWorldDetectorAdapter : IObjectDetectorAdapter
{
    public bool CanHandle(InferenceSession session)
    {
        // Detecta si el grafo requiere entradas de texto (txt_feats, text) o si los tensores tienen dimensión 512
        bool hasTextFeats = session.InputMetadata.Any(kv =>
            kv.Key.Contains("txt", StringComparison.OrdinalIgnoreCase) ||
            kv.Key.Contains("text", StringComparison.OrdinalIgnoreCase) ||
            (kv.Value.Dimensions.Length == 3 && kv.Value.Dimensions[^1] == 512));

        if (hasTextFeats) return true;

        // Inspeccionar nombres de modelo o salida típica de YOLO-World
        return session.OutputMetadata.Keys.Any(k => k.Contains("world", StringComparison.OrdinalIgnoreCase) || k.Contains("dino", StringComparison.OrdinalIgnoreCase));
    }

    public List<(string Label, double Confidence, DetectedObjectBox Box)> Detect(
        InferenceSession session,
        string modelPath,
        Image<Rgb24> image,
        double confidenceThreshold,
        int originalWidth,
        int originalHeight,
        List<string>? customQueries = null)
    {
        int origW = originalWidth > 0 ? originalWidth : image.Width;
        int origH = originalHeight > 0 ? originalHeight : image.Height;

        int targetW = 640;
        int targetH = 640;
        var imageMeta = session.InputMetadata.FirstOrDefault(kv => kv.Value.Dimensions.Length == 4);
        if (imageMeta.Value != null && imageMeta.Value.Dimensions[2] > 0 && imageMeta.Value.Dimensions[3] > 0)
        {
            targetH = imageMeta.Value.Dimensions[2];
            targetW = imageMeta.Value.Dimensions[3];
        }

        string imageInputName = !string.IsNullOrEmpty(imageMeta.Key) ? imageMeta.Key : session.InputNames[0];

        // 1. Preprocesamiento Letterbox preservando la relación de aspecto exacta
        var (imageTensor, letterboxInfo) = TensorPreprocessors.CreateLetterboxTensor(image, targetW, targetH, 114);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(imageInputName, imageTensor)
        };

        // 2. Clases objetivo
        IReadOnlyList<string> targetClasses = (customQueries != null && customQueries.Count > 0)
            ? customQueries
            : TensorPreprocessors.CocoLabels;

        // 3. Inyección de embeddings de texto CLIP (txt_feats)
        foreach (var kv in session.InputMetadata)
        {
            string name = kv.Key;
            if (name.Equals(imageInputName, StringComparison.OrdinalIgnoreCase)) continue;

            if (name.Contains(".weight", StringComparison.OrdinalIgnoreCase) || 
                name.Contains(".bias", StringComparison.OrdinalIgnoreCase) || 
                name.Contains("num_batches_tracked", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("running_mean", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("running_var", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var dims = kv.Value.Dimensions;
            if (name.Contains("txt", StringComparison.OrdinalIgnoreCase) || 
                name.Contains("text", StringComparison.OrdinalIgnoreCase) || 
                (dims.Length == 3 && dims[^1] == 512))
            {
                int numClasses = dims.Length >= 2 && dims[1] > 0 ? dims[1] : targetClasses.Count;
                int featDim = dims.Length >= 3 && dims[2] > 0 ? dims[2] : 512;

                var txtTensor = GenerateTextFeatures(targetClasses, numClasses, featDim);
                inputs.Add(NamedOnnxValue.CreateFromTensor(name, txtTensor));
            }
        }

        using var outputs = OnnxSessionManager.RunInference(modelPath, inputs);
        var outputList = outputs.ToList();
        if (outputList.Count == 0) return [];

        var mainOutput = outputList.FirstOrDefault()?.AsTensor<float>();
        if (mainOutput == null) return [];

        return DecodeYoloWorldOutputs(mainOutput, letterboxInfo, confidenceThreshold, targetClasses);
    }

    private static DenseTensor<float> GenerateTextFeatures(IReadOnlyList<string> classNames, int targetCount, int featDim)
    {
        var tensor = new DenseTensor<float>([1, targetCount, featDim]);

        for (int c = 0; c < targetCount; c++)
        {
            string className = c < classNames.Count ? classNames[c] : $"class_{c}";
            float[] embedding = ClipEmbeddingDatabase.GetClipTextEmbedding(className, featDim);
            for (int d = 0; d < featDim; d++)
            {
                tensor[0, c, d] = embedding[d];
            }
        }

        return tensor;
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

    private static List<(string Label, double Confidence, DetectedObjectBox Box)> DecodeYoloWorldOutputs(
        Tensor<float> tensor,
        TensorPreprocessors.LetterboxInfo letterbox,
        double confidenceThreshold,
        IReadOnlyList<string> classNames)
    {
        var results = new List<(string Label, double Confidence, DetectedObjectBox Box)>();
        var dims = tensor.Dimensions;
        if (dims.Length < 3) return results;

        bool transposed = dims[1] > dims[2]; // [1, 8400, 84] vs [1, 84, 8400]
        int numBoxes = transposed ? dims[1] : dims[2];
        int numChannels = transposed ? dims[2] : dims[1];
        int numClasses = Math.Max(1, numChannels - 4);

        var candidates = new List<(int ClassId, float Score, float X1, float Y1, float X2, float Y2)>();

        for (int b = 0; b < numBoxes; b++)
        {
            float cx = transposed ? tensor[0, b, 0] : tensor[0, 0, b];
            float cy = transposed ? tensor[0, b, 1] : tensor[0, 1, b];
            float w = transposed ? tensor[0, b, 2] : tensor[0, 2, b];
            float h = transposed ? tensor[0, b, 3] : tensor[0, 3, b];

            if (w <= 1f || h <= 1f) continue;

            int bestClass = 0;
            float bestScore = 0f;

            for (int c = 0; c < numClasses; c++)
            {
                float rawScore = transposed ? tensor[0, b, 4 + c] : tensor[0, 4 + c, b];
                float score = (rawScore > 1.0f || rawScore < 0.0f)
                    ? 1.0f / (1.0f + MathF.Exp(-rawScore))
                    : rawScore;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestClass = c;
                }
            }

            if (bestScore >= confidenceThreshold)
            {
                // Coordenadas absolutas en el canvas de letterbox [targetW x targetH]
                float rawX1 = cx - w / 2.0f;
                float rawY1 = cy - h / 2.0f;
                float rawX2 = cx + w / 2.0f;
                float rawY2 = cy + h / 2.0f;

                // Des-letterbox: restar padding y normalizar respecto al área útil reescalada
                float normX1 = Math.Clamp((rawX1 - letterbox.PadX) / letterbox.ScaledW, 0f, 1f);
                float normY1 = Math.Clamp((rawY1 - letterbox.PadY) / letterbox.ScaledH, 0f, 1f);
                float normX2 = Math.Clamp((rawX2 - letterbox.PadX) / letterbox.ScaledW, 0f, 1f);
                float normY2 = Math.Clamp((rawY2 - letterbox.PadY) / letterbox.ScaledH, 0f, 1f);

                if (normX2 > normX1 && normY2 > normY1)
                {
                    candidates.Add((bestClass, bestScore, normX1, normY1, normX2, normY2));
                }
            }
        }

        // NMS con IoU = 0.45
        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        var kept = new List<(int ClassId, float Score, float X1, float Y1, float X2, float Y2)>();

        foreach (var cand in candidates)
        {
            bool overlap = false;
            foreach (var k in kept)
            {
                if (cand.ClassId == k.ClassId)
                {
                    float iou = ObjectDetectorAdapterHelper.ComputeIoU(cand.X1, cand.Y1, cand.X2, cand.Y2, k.X1, k.Y1, k.X2, k.Y2);
                    if (iou > 0.45f)
                    {
                        overlap = true;
                        break;
                    }
                }
            }

            if (!overlap)
            {
                kept.Add(cand);
                string label = cand.ClassId < classNames.Count
                    ? classNames[cand.ClassId]
                    : TensorPreprocessors.GetCocoLabel(cand.ClassId);

                var box = new DetectedObjectBox(label, cand.X1, cand.Y1, cand.X2, cand.Y2, Math.Round(cand.Score, 4));
                results.Add((label, (double)cand.Score, box));

                if (results.Count >= 30) break;
            }
        }

        results.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));
        return results;
    }
}

/// <summary>
/// Adaptador especializado para modelos Tiny YOLOv3 (ONNX Model Zoo con capas yolonms_layer_1 y entrada image_shape).
/// </summary>
public class TinyYoloV3DetectorAdapter : IObjectDetectorAdapter
{
    public bool CanHandle(InferenceSession session)
    {
        bool hasShapeInput = session.InputMetadata.Any(kv => kv.Key.Contains("shape", StringComparison.OrdinalIgnoreCase) || (kv.Value.Dimensions.Length == 2 && kv.Value.Dimensions[1] == 2));
        bool hasYoloNmsOutput = session.OutputMetadata.Any(kv => kv.Key.EndsWith("yolonms_layer_1", StringComparison.OrdinalIgnoreCase));
        return hasShapeInput || hasYoloNmsOutput;
    }

    public List<(string Label, double Confidence, DetectedObjectBox Box)> Detect(
        InferenceSession session,
        string modelPath,
        Image<Rgb24> image,
        double confidenceThreshold,
        int originalWidth,
        int originalHeight,
        List<string>? customQueries = null)
    {
        int origW = originalWidth > 0 ? originalWidth : image.Width;
        int origH = originalHeight > 0 ? originalHeight : image.Height;

        int targetW = 416;
        int targetH = 416;

        using var resized = (image.Width == targetW && image.Height == targetH) ? null : image.Clone(ctx => ctx.Resize(targetW, targetH));
        var targetImage = resized ?? image;
        var imageTensor = TensorPreprocessors.CreateNchwTensor(targetImage, targetW, targetH,
            meanR: 0f, meanG: 0f, meanB: 0f,
            stdR: 1f, stdG: 1f, stdB: 1f,
            scale: 1.0f / 255.0f);

        string imageInputName = session.InputNames[0];
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(imageInputName, imageTensor)
        };

        foreach (var kv in session.InputMetadata)
        {
            string name = kv.Key;
            if (name.Equals(imageInputName, StringComparison.OrdinalIgnoreCase)) continue;

            var dims = kv.Value.Dimensions;
            if (name.Contains("shape", StringComparison.OrdinalIgnoreCase) || (dims.Length == 2 && dims[1] == 2))
            {
                var shapeTensor = new DenseTensor<float>([1, 2]);
                shapeTensor[0, 0] = origH;
                shapeTensor[0, 1] = origW;
                inputs.Add(NamedOnnxValue.CreateFromTensor(name, shapeTensor));
            }
        }

        var results = new List<(string Label, double Confidence, DetectedObjectBox Box)>();
        using var outputs = OnnxSessionManager.RunInference(modelPath, inputs);
        var outputList = outputs.ToList();
        if (outputList.Count == 0) return results;

        NamedOnnxValue? boxesVal = outputList.FirstOrDefault(o => o.Name.EndsWith("yolonms_layer_1") || (o.Value is Tensor<float> tf && tf.Dimensions.Length >= 2 && tf.Dimensions[^1] == 4));
        NamedOnnxValue? scoresVal = outputList.FirstOrDefault(o => o.Name.Contains(":1") || (o.Value is Tensor<float> tf && tf.Dimensions.Contains(80)));
        NamedOnnxValue? indicesVal = outputList.FirstOrDefault(o => o.Name.Contains(":2") || o.Value is Tensor<int>);

        if (boxesVal != null && scoresVal != null)
        {
            DecodeTinyYoloOutputs(boxesVal, scoresVal, indicesVal, origW, origH, confidenceThreshold, results);
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
}

/// <summary>
/// Adaptador especializado para modelos YOLOv8 / YOLOv11 estándar (1 tensor de entrada de imagen, 1 tensor de salida de predicciones).
/// </summary>
public class YoloV8StandardDetectorAdapter : IObjectDetectorAdapter
{
    public bool CanHandle(InferenceSession session)
    {
        if (session.InputNames.Count != 1) return false;
        if (session.OutputNames.Count != 1) return false;

        var outMeta = session.OutputMetadata.Values.FirstOrDefault();
        if (outMeta == null || outMeta.Dimensions.Length != 3) return false;

        // Típicamente [1, 84, 8400] o [1, 8400, 84]
        return outMeta.Dimensions.Contains(84) || outMeta.Dimensions.Contains(8400);
    }

    public List<(string Label, double Confidence, DetectedObjectBox Box)> Detect(
        InferenceSession session,
        string modelPath,
        Image<Rgb24> image,
        double confidenceThreshold,
        int originalWidth,
        int originalHeight,
        List<string>? customQueries = null)
    {
        int targetW = 640;
        int targetH = 640;
        var inMeta = session.InputMetadata.Values.FirstOrDefault();
        if (inMeta != null && inMeta.Dimensions.Length == 4 && inMeta.Dimensions[2] > 0 && inMeta.Dimensions[3] > 0)
        {
            targetH = inMeta.Dimensions[2];
            targetW = inMeta.Dimensions[3];
        }

        var (imageTensor, letterboxInfo) = TensorPreprocessors.CreateLetterboxTensor(image, targetW, targetH, 114);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(session.InputNames[0], imageTensor)
        };

        using var outputs = OnnxSessionManager.RunInference(modelPath, inputs);
        var mainOutput = outputs.FirstOrDefault()?.AsTensor<float>();
        if (mainOutput == null) return [];

        IReadOnlyList<string> targetClasses = (customQueries != null && customQueries.Count > 0)
            ? customQueries
            : TensorPreprocessors.CocoLabels;

        return ObjectDetectorAdapterHelper.DecodeStandardYoloOutputs(mainOutput, letterboxInfo, confidenceThreshold, targetClasses);
    }
}

/// <summary>
/// Adaptador de contingencia genérico para modelos de detección de objetos ONNX.
/// </summary>
public class GenericObjectDetectorAdapter : IObjectDetectorAdapter
{
    public bool CanHandle(InferenceSession session) => true;

    public List<(string Label, double Confidence, DetectedObjectBox Box)> Detect(
        InferenceSession session,
        string modelPath,
        Image<Rgb24> image,
        double confidenceThreshold,
        int originalWidth,
        int originalHeight,
        List<string>? customQueries = null)
    {
        int targetW = 640;
        int targetH = 640;

        var inMeta = session.InputMetadata.Values.FirstOrDefault(v => v.Dimensions.Length == 4);
        if (inMeta != null && inMeta.Dimensions[2] > 0 && inMeta.Dimensions[3] > 0)
        {
            targetH = inMeta.Dimensions[2];
            targetW = inMeta.Dimensions[3];
        }

        var (imageTensor, letterboxInfo) = TensorPreprocessors.CreateLetterboxTensor(image, targetW, targetH, 114);
        string inputName = session.InputNames[0];

        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, imageTensor) };

        using var outputs = OnnxSessionManager.RunInference(modelPath, inputs);
        var mainOutput = outputs.FirstOrDefault()?.AsTensor<float>();
        if (mainOutput == null) return [];

        IReadOnlyList<string> targetClasses = (customQueries != null && customQueries.Count > 0)
            ? customQueries
            : TensorPreprocessors.CocoLabels;

        return ObjectDetectorAdapterHelper.DecodeStandardYoloOutputs(mainOutput, letterboxInfo, confidenceThreshold, targetClasses);
    }
}

/// <summary>
/// Factoría que resuelve automáticamente el adaptador óptimo para el modelo de detección de objetos ONNX cargado.
/// </summary>
public static class ObjectDetectorAdapterFactory
{
    private static readonly IObjectDetectorAdapter[] Adapters =
    [
        new YoloWorldDetectorAdapter(),
        new TinyYoloV3DetectorAdapter(),
        new YoloV8StandardDetectorAdapter(),
        new GenericObjectDetectorAdapter()
    ];

    public static IObjectDetectorAdapter GetAdapter(InferenceSession session)
    {
        foreach (var adapter in Adapters)
        {
            if (adapter.CanHandle(session))
            {
                return adapter;
            }
        }

        return Adapters[^1]; // Generic Fallback
    }
}

/// <summary>
/// Funciones matemáticas auxiliares compartidas entre adaptadores de detección de objetos.
/// </summary>
internal static class ObjectDetectorAdapterHelper
{
    public static float ComputeIoU(float ax1, float ay1, float ax2, float ay2, float bx1, float by1, float bx2, float by2)
    {
        float ix1 = Math.Max(ax1, bx1);
        float iy1 = Math.Max(ay1, by1);
        float ix2 = Math.Min(ax2, bx2);
        float iy2 = Math.Min(ay2, by2);

        float iw = Math.Max(0f, ix2 - ix1);
        float ih = Math.Max(0f, iy2 - iy1);
        float intersection = iw * ih;

        float areaA = Math.Max(0f, ax2 - ax1) * Math.Max(0f, ay2 - ay1);
        float areaB = Math.Max(0f, bx2 - bx1) * Math.Max(0f, by2 - by1);
        float union = areaA + areaB - intersection;

        return union > 0f ? (intersection / union) : 0f;
    }

    public static List<(string Label, double Confidence, DetectedObjectBox Box)> DecodeStandardYoloOutputs(
        Tensor<float> tensor,
        TensorPreprocessors.LetterboxInfo letterbox,
        double confidenceThreshold,
        IReadOnlyList<string> classNames)
    {
        var results = new List<(string Label, double Confidence, DetectedObjectBox Box)>();
        var dims = tensor.Dimensions;
        if (dims.Length < 3) return results;

        bool transposed = dims[1] > dims[2];
        int numBoxes = transposed ? dims[1] : dims[2];
        int numChannels = transposed ? dims[2] : dims[1];
        int numClasses = Math.Max(1, numChannels - 4);

        var candidates = new List<(int ClassId, float Score, float X1, float Y1, float X2, float Y2)>();

        for (int b = 0; b < numBoxes; b++)
        {
            float cx = transposed ? tensor[0, b, 0] : tensor[0, 0, b];
            float cy = transposed ? tensor[0, b, 1] : tensor[0, 1, b];
            float w = transposed ? tensor[0, b, 2] : tensor[0, 2, b];
            float h = transposed ? tensor[0, b, 3] : tensor[0, 3, b];

            if (w <= 1f || h <= 1f) continue;

            int bestClass = 0;
            float bestScore = 0f;

            for (int c = 0; c < numClasses; c++)
            {
                float rawScore = transposed ? tensor[0, b, 4 + c] : tensor[0, 4 + c, b];
                float score = (rawScore > 1.0f || rawScore < 0.0f)
                    ? 1.0f / (1.0f + MathF.Exp(-rawScore))
                    : rawScore;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestClass = c;
                }
            }

            if (bestScore >= confidenceThreshold)
            {
                float rawX1 = cx - w / 2.0f;
                float rawY1 = cy - h / 2.0f;
                float rawX2 = cx + w / 2.0f;
                float rawY2 = cy + h / 2.0f;

                float normX1 = Math.Clamp((rawX1 - letterbox.PadX) / letterbox.ScaledW, 0f, 1f);
                float normY1 = Math.Clamp((rawY1 - letterbox.PadY) / letterbox.ScaledH, 0f, 1f);
                float normX2 = Math.Clamp((rawX2 - letterbox.PadX) / letterbox.ScaledW, 0f, 1f);
                float normY2 = Math.Clamp((rawY2 - letterbox.PadY) / letterbox.ScaledH, 0f, 1f);

                if (normX2 > normX1 && normY2 > normY1)
                {
                    candidates.Add((bestClass, bestScore, normX1, normY1, normX2, normY2));
                }
            }
        }

        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        var kept = new List<(int ClassId, float Score, float X1, float Y1, float X2, float Y2)>();

        foreach (var cand in candidates)
        {
            bool overlap = false;
            foreach (var k in kept)
            {
                if (cand.ClassId == k.ClassId)
                {
                    float iou = ComputeIoU(cand.X1, cand.Y1, cand.X2, cand.Y2, k.X1, k.Y1, k.X2, k.Y2);
                    if (iou > 0.45f)
                    {
                        overlap = true;
                        break;
                    }
                }
            }

            if (!overlap)
            {
                kept.Add(cand);
                string label = cand.ClassId < classNames.Count
                    ? classNames[cand.ClassId]
                    : TensorPreprocessors.GetCocoLabel(cand.ClassId);

                var box = new DetectedObjectBox(label, cand.X1, cand.Y1, cand.X2, cand.Y2, Math.Round(cand.Score, 4));
                results.Add((label, (double)cand.Score, box));

                if (results.Count >= 30) break;
            }
        }

        results.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));
        return results;
    }
}
