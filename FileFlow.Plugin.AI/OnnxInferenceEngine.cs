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

    private static InferenceSession GetOrCreateSession(string modelPath)
    {
        var lazy = _sessionCache.GetOrAdd(modelPath, path => new Lazy<InferenceSession>(() =>
        {
            var options = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode = ExecutionMode.ORT_PARALLEL
            };

            // Intentar GPU DirectML primero, caer en CPU si no disponible
            try
            {
                options.AppendExecutionProvider_DML(0);
            }
            catch
            {
                // DML no disponible, usar CPU (ya está por defecto)
            }

            return new InferenceSession(path, options);
        }));

        return lazy.Value;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Clasificación con MobileNetV2 (ImageNet 1000 clases → categorías de usuario)
    // ──────────────────────────────────────────────────────────────────────────

    public static (string Category, string TopLabel, double Confidence) ClassifyImage(string modelPath, Image<Rgb24> image)
    {
        var session = GetOrCreateSession(modelPath);

        // Preprocesar: resize 224x224 + normalización ImageNet NCHW
        using var resized = image.Clone(ctx => ctx.Resize(224, 224));
        var tensor = CreateNchwTensor(resized, 224, 224,
            meanR: 0.485f, meanG: 0.456f, meanB: 0.406f,
            stdR: 0.229f, stdG: 0.224f, stdB: 0.225f);

        string inputName = session.InputNames[0];
        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, tensor) };

        using var outputs = session.Run(inputs);
        var probabilities = outputs.First().AsTensor<float>().ToArray();

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
    // Detección de rostros con UltraFace RFB 320
    // ──────────────────────────────────────────────────────────────────────────

    public static (int FaceCount, double MaxConfidence) DetectFaces(string modelPath, Image<Rgb24> image, double confidenceThreshold = 0.7)
    {
        var session = GetOrCreateSession(modelPath);

        // UltraFace RFB 320: input [1, 3, 240, 320], normalize [-1, 1]
        using var resized = image.Clone(ctx => ctx.Resize(320, 240));
        var tensor = CreateNchwTensorNormalized(resized, 320, 240, scale: 1.0f / 127.5f, shift: -1.0f);

        string inputName = session.InputNames[0];
        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, tensor) };

        using var outputs = session.Run(inputs);

        // Output: scores [1, N, 2], boxes [1, N, 4]
        // El primer output son las puntuaciones
        var scoresOutput = outputs.First(o => o.Name.Contains("score", StringComparison.OrdinalIgnoreCase)
                                              || o == outputs.First()).AsTensor<float>().ToArray();

        int faceCount = 0;
        double maxConf = 0.0;

        // scoresOutput tiene pares [background_score, face_score] para cada anchor
        for (int i = 1; i < scoresOutput.Length; i += 2)
        {
            float faceScore = scoresOutput[i];
            if (faceScore >= confidenceThreshold)
            {
                faceCount++;
                if (faceScore > maxConf) maxConf = faceScore;
            }
        }

        return (faceCount, maxConf);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Detección de objetos con Tiny YOLOv3
    // Outputs: yolonms_layer_1/ExpandDims_1:0 → boxes [1, N, 1, 4]
    //          yolonms_layer_1/concat_1:0     → scores [1, N, 80]
    // ──────────────────────────────────────────────────────────────────────────

    public static List<(string Label, double Confidence)> DetectObjects(string modelPath, Image<Rgb24> image, double confidenceThreshold = 0.4)
    {
        var session = GetOrCreateSession(modelPath);

        // TinyYOLOv3: dos inputs — image_shape [1,2] y input_1 [1,3,416,416] normalizado [0,1]
        using var resized = image.Clone(ctx => ctx.Resize(416, 416));
        var imageTensor = CreateNchwTensor(resized, 416, 416,
            meanR: 0f, meanG: 0f, meanB: 0f,
            stdR: 1f, stdG: 1f, stdB: 1f,
            scale: 1.0f / 255.0f);

        // image_shape: [height, width] de la imagen ORIGINAL (antes de resize)
        var shapeTensor = new DenseTensor<float>([1, 2]);
        shapeTensor[0, 0] = image.Height;
        shapeTensor[0, 1] = image.Width;

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(session.InputNames[0], imageTensor),
            NamedOnnxValue.CreateFromTensor(session.InputNames[1], shapeTensor)
        };

        using var outputs = session.Run(inputs);
        var results = new List<(string Label, double Confidence)>();

        var outputList = outputs.ToList();
        if (outputList.Count < 2) return results;

        try
        {
            // Output 0: boxes   [num_boxes, 1, 4]
            // Output 1: scores  [num_boxes, num_classes]
            var boxesTensor = outputList[0].AsTensor<float>();
            var scoresTensor = outputList[1].AsTensor<float>();

            int numBoxes = boxesTensor.Dimensions[0];
            int numClasses = scoresTensor.Dimensions.Length > 1 ? scoresTensor.Dimensions[1] : 80;

            for (int b = 0; b < numBoxes; b++)
            {
                // Encontrar la clase con mayor puntuación
                int bestClass = 0;
                float bestScore = 0f;
                for (int c = 0; c < numClasses; c++)
                {
                    float score = scoresTensor.GetValue(b * numClasses + c);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestClass = c;
                    }
                }

                if (bestScore >= confidenceThreshold)
                {
                    results.Add((GetCocoLabel(bestClass), bestScore));
                }
            }
        }
        catch
        {
            // Si el formato de salida es diferente, devolvemos vacío (sin crash)
            return results;
        }

        results.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));
        return results.Take(20).ToList();
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
        "background", "person", "bicycle", "car", "motorcycle", "airplane", "bus", "train",
        "truck", "boat", "traffic_light", "fire_hydrant", "stop_sign", "parking_meter",
        "bench", "bird", "cat", "dog", "horse", "sheep", "cow", "elephant", "bear",
        "zebra", "giraffe", "backpack", "umbrella", "handbag", "tie", "suitcase",
        "frisbee", "skis", "snowboard", "sports_ball", "kite", "baseball_bat",
        "baseball_glove", "skateboard", "surfboard", "tennis_racket", "bottle",
        "wine_glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple",
        "sandwich", "orange", "broccoli", "carrot", "hot_dog", "pizza", "donut",
        "cake", "chair", "couch", "potted_plant", "bed", "dining_table", "toilet",
        "tv", "laptop", "mouse", "remote", "keyboard", "cell_phone", "microwave",
        "oven", "toaster", "sink", "refrigerator", "book", "clock", "vase",
        "scissors", "teddy_bear", "hair_drier", "toothbrush"
    ];

    public static string GetCocoLabel(int classId)
    {
        if (classId >= 0 && classId < CocoLabels.Length)
            return CocoLabels[classId];
        return $"object_{classId}";
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
