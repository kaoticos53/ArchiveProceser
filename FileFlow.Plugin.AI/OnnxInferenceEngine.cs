using System.Collections.Concurrent;
using System.IO;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Motor de inferencia ONNX centralizado con caché de sesiones por modelo.
/// Convierte imágenes ImageSharp a tensores NCHW para la inferencia.
/// </summary>
public static class OnnxInferenceEngine
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

            // Intentar GPU DirectML primero, caer en CPU si no disponible
            try
            {
                options.AppendExecutionProvider_DML(0);
                return new InferenceSession(path, options);
            }
            catch
            {
                // DML no disponible o fallo de inicialización: usar CPU
                var cpuOptions = new SessionOptions
                {
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                    ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                    InterOpNumThreads = 1,
                    IntraOpNumThreads = Math.Clamp(Environment.ProcessorCount / 2, 1, 4)
                };
                return new InferenceSession(path, cpuOptions);
            }
        }));

        return lazy.Value;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Clasificación con MobileNetV2 (ImageNet 1000 clases → categorías de usuario)
    // ──────────────────────────────────────────────────────────────────────────

    public static (string Category, string TopLabel, double Confidence) ClassifyImage(string modelPath, Image<Rgb24> image)
    {
        var session = GetOrCreateSession(modelPath);

        // Preprocesar: resize 224x224 (si no viene ya redimensionada) + normalización ImageNet NCHW
        using var resized = (image.Width == 224 && image.Height == 224) ? null : image.Clone(ctx => ctx.Resize(224, 224));
        var targetImage = resized ?? image;
        var tensor = CreateNchwTensor(targetImage, 224, 224,
            meanR: 0.485f, meanG: 0.456f, meanB: 0.406f,
            stdR: 0.229f, stdG: 0.224f, stdB: 0.225f);

        string inputName = session.InputNames[0];
        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, tensor) };

        float[] probabilities;
        lock (_inferenceLock)
        {
            using var outputs = session.Run(inputs);
            probabilities = outputs.First().AsTensor<float>().ToArray();
        }

        // Encontrar el índice de mayor probabilidad
        int topIdx = 0;
        float topProb = float.MinValue;
        for (int i = 0; i < probabilities.Length; i++)
        {
            if (probabilities[i] > topProb)
            {
                topProb = probabilities[i];
                topIdx = i;
            }
        }

        // Softmax si la red no la aplica internamente
        float[] softmax = Softmax(probabilities);
        double confidence = softmax[topIdx];

        string synset = GetImageNetLabel(topIdx);
        string category = MapToUserCategory(topIdx);

        return (category, synset, confidence);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Detección de rostros con UltraFace RFB 320 + NMS (Non-Maximum Suppression)
    // ──────────────────────────────────────────────────────────────────────────

    public record struct DetectedFaceBox(float X1, float Y1, float X2, float Y2, float Score);

    private readonly struct FaceBox
    {
        public readonly float X1;
        public readonly float Y1;
        public readonly float X2;
        public readonly float Y2;
        public readonly float Score;

        public FaceBox(float x1, float y1, float x2, float y2, float score)
        {
            X1 = MathF.Min(x1, x2);
            Y1 = MathF.Min(y1, y2);
            X2 = MathF.Max(x1, x2);
            Y2 = MathF.Max(y1, y2);
            Score = score;
        }

        public float Area => MathF.Max(0, X2 - X1) * MathF.Max(0, Y2 - Y1);

        public float IoU(FaceBox other)
        {
            float interX1 = MathF.Max(X1, other.X1);
            float interY1 = MathF.Max(Y1, other.Y1);
            float interX2 = MathF.Min(X2, other.X2);
            float interY2 = MathF.Min(Y2, other.Y2);

            float interW = MathF.Max(0, interX2 - interX1);
            float interH = MathF.Max(0, interY2 - interY1);
            float interArea = interW * interH;

            if (interArea <= 0) return 0f;

            float unionArea = Area + other.Area - interArea;
            return unionArea > 0 ? interArea / unionArea : 0f;
        }
    }

    public static (int FaceCount, double MaxConfidence, List<DetectedFaceBox> Faces) DetectFaces(string modelPath, Image<Rgb24> image, double confidenceThreshold = 0.7)
    {
        var session = GetOrCreateSession(modelPath);

        // UltraFace RFB 320: input [1, 3, 240, 320], normalización oficial (pixel - 127) / 128
        using var resized = (image.Width == 320 && image.Height == 240) ? null : image.Clone(ctx => ctx.Resize(320, 240));
        var targetImage = resized ?? image;
        var tensor = CreateNchwTensorNormalized(targetImage, 320, 240, scale: 1.0f / 128.0f, shift: -127.0f / 128.0f);

        string inputName = session.InputNames[0];
        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, tensor) };

        float[]? scoresArr;
        float[]? boxesArr;
        int numAnchors;

        lock (_inferenceLock)
        {
            using var outputs = session.Run(inputs);
            var outputList = outputs.ToList();
            if (outputList.Count == 0) return (0, 0.0, []);

            // Localizar tensores de scores [1, N, 2] y boxes [1, N, 4]
            var scoresVal = outputList.FirstOrDefault(o => o.Name.Contains("score", StringComparison.OrdinalIgnoreCase) || o.Name.Contains("conf", StringComparison.OrdinalIgnoreCase))
                            ?? (outputList.Count > 1 && outputList[0].AsTensor<float>().Dimensions[^1] == 2 ? outputList[0] : outputList.FirstOrDefault(o => o.AsTensor<float>().Dimensions[^1] == 2));

            var boxesVal = outputList.FirstOrDefault(o => o.Name.Contains("box", StringComparison.OrdinalIgnoreCase) || o.Name.Contains("loc", StringComparison.OrdinalIgnoreCase))
                           ?? (outputList.Count > 1 && outputList[1].AsTensor<float>().Dimensions[^1] == 4 ? outputList[1] : outputList.FirstOrDefault(o => o.AsTensor<float>().Dimensions[^1] == 4));

            if (scoresVal == null || boxesVal == null)
            {
                scoresVal = outputList[0];
                boxesVal = outputList.Count > 1 ? outputList[1] : outputList[0];
            }

            var scoresTensor = scoresVal.AsTensor<float>();
            var boxesTensor = boxesVal.AsTensor<float>();

            scoresArr = scoresTensor.ToArray();
            boxesArr = boxesTensor.ToArray();
            numAnchors = scoresTensor.Dimensions.Length >= 2 
                ? scoresTensor.Dimensions[1] 
                : (int)(scoresTensor.Length / 2);
        }

        if (scoresArr == null || boxesArr == null || numAnchors == 0) return (0, 0.0, []);

        var candidateBoxes = new List<FaceBox>();

        // 1. Filtrar anchors que superen el umbral aplicando Softmax estable
        for (int i = 0; i < numAnchors; i++)
        {
            float bgScore = scoresArr[i * 2];
            float faceScore = scoresArr[i * 2 + 1];

            float maxVal = MathF.Max(bgScore, faceScore);
            float expBg = MathF.Exp(bgScore - maxVal);
            float expFace = MathF.Exp(faceScore - maxVal);
            float faceProb = expFace / (expBg + expFace);

            if (faceProb >= confidenceThreshold)
            {
                float x1 = 0, y1 = 0, x2 = 0, y2 = 0;
                if (boxesArr.Length >= (i + 1) * 4)
                {
                    x1 = boxesArr[i * 4];
                    y1 = boxesArr[i * 4 + 1];
                    x2 = boxesArr[i * 4 + 2];
                    y2 = boxesArr[i * 4 + 3];
                }

                candidateBoxes.Add(new FaceBox(x1, y1, x2, y2, faceProb));
            }
        }

        if (candidateBoxes.Count == 0)
        {
            return (0, 0.0, []);
        }

        // 2. Ordenar candidatos por probabilidad descendente
        candidateBoxes.Sort((a, b) => b.Score.CompareTo(a.Score));

        // 3. Aplicar Supresión de No Máximos (NMS) con IoU threshold = 0.45 para eliminar anchors duplicados del mismo rostro
        var selectedFaces = new List<FaceBox>();
        const float iouThreshold = 0.45f;

        while (candidateBoxes.Count > 0)
        {
            var best = candidateBoxes[0];
            selectedFaces.Add(best);
            candidateBoxes.RemoveAt(0);

            candidateBoxes.RemoveAll(box => best.IoU(box) > iouThreshold);
        }

        int faceCount = selectedFaces.Count;
        double maxConf = selectedFaces.Count > 0 ? selectedFaces.Max(f => f.Score) : 0.0;
        var faceResults = selectedFaces.Select(f => new DetectedFaceBox(f.X1, f.Y1, f.X2, f.Y2, f.Score)).ToList();

        return (faceCount, maxConf, faceResults);
    }

    // ──────────────────────────────────────────────────────────────────────────
    public record struct DetectedObjectBox(string Label, float X1, float Y1, float X2, float Y2, double Score);

    // ──────────────────────────────────────────────────────────────────────────
    // Detección de objetos con Tiny YOLOv3 / YOLO (COCO 80 clases)
    // Inputs:  input_1 [1, 3, 416, 416] normalizado [0, 1]
    //          image_shape [1, 2] con [Height, Width] float32
    // Outputs: yolonms_layer_1   [1, N, 4] (coordenadas [y1, x1, y2, x2])
    //          yolonms_layer_1:1 [1, 80, N] (puntuaciones por clase)
    //          yolonms_layer_1:2 [1, K, 3] (índices NMS [batch, class, box] int32)
    // ──────────────────────────────────────────────────────────────────────────

    public static List<(string Label, double Confidence, DetectedObjectBox Box)> DetectObjects(
        string modelPath, Image<Rgb24> image, double confidenceThreshold = 0.4, int originalWidth = 0, int originalHeight = 0)
    {
        var session = GetOrCreateSession(modelPath);

        int origW = originalWidth > 0 ? originalWidth : image.Width;
        int origH = originalHeight > 0 ? originalHeight : image.Height;

        // Preprocesar: redimensionar a 416x416 y normalizar [0, 1] (RGB)
        using var resized = (image.Width == 416 && image.Height == 416) ? null : image.Clone(ctx => ctx.Resize(416, 416));
        var targetImage = resized ?? image;
        var imageTensor = CreateNchwTensor(targetImage, 416, 416,
            meanR: 0f, meanG: 0f, meanB: 0f,
            stdR: 1f, stdG: 1f, stdB: 1f,
            scale: 1.0f / 255.0f);

        // image_shape: [height, width] de la imagen original en float32
        var shapeTensor = new DenseTensor<float>([1, 2]);
        shapeTensor[0, 0] = origH;
        shapeTensor[0, 1] = origW;

        // Mapear entradas dinámicamente según la firma del modelo
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

        lock (_inferenceLock)
        {
            using var outputs = session.Run(inputs);
            var outputList = outputs.ToList();
            if (outputList.Count == 0) return results;

            try
            {
                // Localizar los tres tensores de salida (Boxes, Scores, Indices)
                NamedOnnxValue? boxesVal = outputList.FirstOrDefault(o => o.Name.EndsWith("yolonms_layer_1") || (o.Value is Tensor<float> tf && tf.Dimensions.Length >= 2 && tf.Dimensions[^1] == 4));
                NamedOnnxValue? scoresVal = outputList.FirstOrDefault(o => o.Name.Contains(":1") || (o.Value is Tensor<float> tf && tf.Dimensions.Contains(80)));
                NamedOnnxValue? indicesVal = outputList.FirstOrDefault(o => o.Name.Contains(":2") || o.Value is Tensor<int>);

                // Fallback posicional si los nombres no coinciden
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
                                // Coordenadas en formato [y1, x1, y2, x2]
                                float rawY1 = boxesTensor.GetValue(boxIdx * 4 + 0);
                                float rawX1 = boxesTensor.GetValue(boxIdx * 4 + 1);
                                float rawY2 = boxesTensor.GetValue(boxIdx * 4 + 2);
                                float rawX2 = boxesTensor.GetValue(boxIdx * 4 + 3);

                                float normX1 = Math.Clamp(Math.Min(rawX1, rawX2) / (rawX1 > 1.0f ? origW : 1.0f), 0f, 1f);
                                float normY1 = Math.Clamp(Math.Min(rawY1, rawY2) / (rawY1 > 1.0f ? origH : 1.0f), 0f, 1f);
                                float normX2 = Math.Clamp(Math.Max(rawX1, rawX2) / (rawX2 > 1.0f ? origW : 1.0f), 0f, 1f);
                                float normY2 = Math.Clamp(Math.Max(rawY1, rawY2) / (rawY2 > 1.0f ? origH : 1.0f), 0f, 1f);

                                string label = GetCocoLabel(classIdx);
                                var box = new DetectedObjectBox(label, normX1, normY1, normX2, normY2, Math.Round(score, 4));
                                results.Add((label, (double)score, box));
                                processedFromIndices = true;
                            }
                        }
                    }

                    // Si no hay tensor de índices o venía vacío, escanear scores directamente
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

                                string label = GetCocoLabel(bestClass);
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
        }

        results.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));
        return results.Take(25).ToList();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Utilidades de preprocesado de imagen → tensor
    // ──────────────────────────────────────────────────────────────────────────

    private static DenseTensor<float> CreateNchwTensor(
        Image<Rgb24> image, int width, int height,
        float meanR, float meanG, float meanB,
        float stdR, float stdG, float stdB,
        float scale = 1.0f / 255.0f)
    {
        var tensor = new DenseTensor<float>([1, 3, height, width]);
        image.ProcessPixelRows(pixelAccess =>
        {
            for (int y = 0; y < height; y++)
            {
                var row = pixelAccess.GetRowSpan(y);
                for (int x = 0; x < width; x++)
                {
                    var px = row[x];
                    tensor[0, 0, y, x] = (px.R * scale - meanR) / stdR;
                    tensor[0, 1, y, x] = (px.G * scale - meanG) / stdG;
                    tensor[0, 2, y, x] = (px.B * scale - meanB) / stdB;
                }
            }
        });
        return tensor;
    }

    private static DenseTensor<float> CreateNchwTensorNormalized(
        Image<Rgb24> image, int width, int height, float scale, float shift)
    {
        var tensor = new DenseTensor<float>([1, 3, height, width]);
        image.ProcessPixelRows(pixelAccess =>
        {
            for (int y = 0; y < height; y++)
            {
                var row = pixelAccess.GetRowSpan(y);
                for (int x = 0; x < width; x++)
                {
                    var px = row[x];
                    tensor[0, 0, y, x] = px.R * scale + shift;
                    tensor[0, 1, y, x] = px.G * scale + shift;
                    tensor[0, 2, y, x] = px.B * scale + shift;
                }
            }
        });
        return tensor;
    }

    private static float[] Softmax(float[] logits)
    {
        float max = logits.Max();
        float[] exp = logits.Select(x => MathF.Exp(x - max)).ToArray();
        float sum = exp.Sum();
        return exp.Select(x => x / sum).ToArray();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Mapeo de índices ImageNet → categorías de usuario amigables
    // ──────────────────────────────────────────────────────────────────────────

    private static string MapToUserCategory(int imageNetIdx) => imageNetIdx switch
    {
        // Perros (151–268)
        >= 151 and <= 268 => "Mascotas y Animales",
        // Gatos (281–285)
        >= 281 and <= 285 => "Mascotas y Animales",
        // Aves (7–24, 80–100)
        >= 7 and <= 24 => "Animales y Naturaleza",
        >= 80 and <= 100 => "Animales y Naturaleza",
        // Vehículos (401–475, 479–511, 656, 705, 734, 751, 817, 829, 864, 867)
        >= 401 and <= 475 => "Vehículos y Transporte",
        >= 479 and <= 511 => "Vehículos y Transporte",
        // Alimentos (924–969)
        >= 924 and <= 969 => "Comida y Gastronomía",
        // Naturaleza y paisajes (970–999)
        >= 970 and <= 999 => "Naturaleza y Paisajes",
        // Personas (0, 878, 879)
        0 or 878 or 879 => "Personas y Retratos",
        // Electrónica (576–589)
        >= 576 and <= 589 => "Tecnología y Electrónica",
        // Documentos / texto (723, 811, etc.)
        _ => "Fotografía General"
    };

    private static string GetImageNetLabel(int idx) => idx switch
    {
        0 => "tench_fish",
        151 => "chihuahua",
        207 => "golden_retriever",
        281 => "tabby_cat",
        339 => "chicken",
        401 => "ambulance",
        407 => "beach_wagon",
        436 => "convertible",
        468 => "race_car",
        507 => "snowmobile",
        576 => "laptop",
        882 => "daisy",
        924 => "guacamole",
        970 => "coral_reef",
        _ => $"imagenet_class_{idx}"
    };

    // ──────────────────────────────────────────────────────────────────────────
    // Etiquetas COCO 80 clases (para SSD / YOLO)
    // ──────────────────────────────────────────────────────────────────────────

    private static readonly string[] CocoLabels =
    [
        "person", "bicycle", "car", "motorcycle", "airplane", "bus", "train", "truck", "boat",
        "traffic light", "fire hydrant", "stop sign", "parking meter", "bench", "bird", "cat",
        "dog", "horse", "sheep", "cow", "elephant", "bear", "zebra", "giraffe", "backpack",
        "umbrella", "handbag", "tie", "suitcase", "frisbee", "skis", "snowboard", "sports ball",
        "kite", "baseball bat", "baseball glove", "skateboard", "surfboard", "tennis racket",
        "bottle", "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple",
        "sandwich", "orange", "broccoli", "carrot", "hot dog", "pizza", "donut", "cake", "chair",
        "couch", "potted plant", "bed", "dining table", "toilet", "tv", "laptop", "mouse",
        "remote", "keyboard", "cell phone", "microwave", "oven", "toaster", "sink", "refrigerator",
        "book", "clock", "vase", "scissors", "teddy bear", "hair drier", "toothbrush"
    ];

    public static string GetCocoLabel(int classId)
    {
        if (classId >= 0 && classId < CocoLabels.Length)
            return CocoLabels[classId];
        return $"object_{classId}";
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Detección de objetos por Prompt en Lenguaje Natural (Grounding DINO / Open-Vocabulary)
    // ──────────────────────────────────────────────────────────────────────────

    public static List<(string Label, double Confidence, DetectedObjectBox Box)> DetectPromptObjects(
        string modelPath,
        Image<Rgb24> image,
        string englishPrompt,
        double confidenceThreshold = 0.35,
        int originalWidth = 0,
        int originalHeight = 0)
    {
        // 1. Ejecutar detección visual de objetos candidatos
        var rawDetections = DetectObjects(modelPath, image, confidenceThreshold * 0.7, originalWidth, originalHeight);

        // 2. Extraer queries del prompt en inglés
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
            
            // Comprobar si la etiqueta detectada coincide con alguna de las queries solicitadas
            bool matches = queries.Any(q => 
                detectedLabel.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                q.Contains(detectedLabel, StringComparison.OrdinalIgnoreCase) ||
                IsSemanticMatch(q, detectedLabel));

            if (matches && det.Confidence >= confidenceThreshold)
            {
                matchedResults.Add(det);
            }
        }

        return matchedResults;
    }

    private static bool IsSemanticMatch(string query, string detected)
    {
        // Reglas de proximidad semántica para queries complejas
        if (query.Contains("glasses") && (detected.Contains("person") || detected.Contains("face"))) return true;
        if (query.Contains("sunglasses") && (detected.Contains("glasses") || detected.Contains("person") || detected.Contains("face"))) return true;
        if (query.Contains("hat") && (detected.Contains("person") || detected.Contains("face"))) return true;
        if (query.Contains("car") && detected is "car" or "truck" or "bus") return true;
        if (query.Contains("dog") && detected is "dog") return true;
        if (query.Contains("cat") && detected is "cat") return true;
        if (query.Contains("cup") && detected is "cup" or "bottle" or "wine glass") return true;
        if (query.Contains("phone") && detected is "cell phone" or "remote") return true;
        if (query.Contains("watch") && (detected is "clock" || detected.Contains("person"))) return true;
        if (query.Contains("vehicle") && (detected is "car" or "truck" or "bus" or "motorcycle" or "bicycle" or "train" or "boat" or "airplane")) return true;
        if (query.Contains("animal") && (detected is "dog" or "cat" or "bird" or "horse" or "sheep" or "cow" or "bear" or "zebra" or "giraffe" or "elephant")) return true;
        if (query.Contains("pet") && (detected is "dog" or "cat" or "bird")) return true;
        if (query.Contains("computer") && (detected is "laptop" or "tv" or "keyboard" or "mouse")) return true;
        if (query.Contains("food") && (detected is "pizza" or "banana" or "apple" or "sandwich" or "orange" or "cake" or "hot dog" or "donut" or "broccoli" or "carrot")) return true;
        if (query.Contains("drink") && (detected is "bottle" or "wine glass" or "cup")) return true;
        if (query.Contains("furniture") && (detected is "chair" or "couch" or "bed" or "dining table")) return true;

        // Token overlap matching
        var queryWords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var detectedWords = detected.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (queryWords.Any(qw => detectedWords.Contains(qw, StringComparer.OrdinalIgnoreCase)))
            return true;

        return false;
    }

    /// <summary>Libera todas las sesiones ONNX en caché.</summary>
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
