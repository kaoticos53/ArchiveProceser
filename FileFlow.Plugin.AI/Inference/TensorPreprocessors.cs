using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FileFlow.Plugin.AI.Inference;

/// <summary>
/// Utilidades matemáticas y de preprocesamiento de tensores para visión computacional y modelos ONNX.
/// </summary>
public static class TensorPreprocessors
{
    public static readonly string[] CocoLabels =
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

    public record struct LetterboxInfo(int TargetW, int TargetH, int ScaledW, int ScaledH, float PadX, float PadY, float Scale);

    public static (DenseTensor<float> Tensor, LetterboxInfo Info) CreateLetterboxTensor(
        Image<Rgb24> image,
        int targetWidth = 640,
        int targetHeight = 640,
        byte padColor = 114)
    {
        float scale = Math.Min((float)targetWidth / image.Width, (float)targetHeight / image.Height);
        int scaledW = Math.Max(1, (int)Math.Round(image.Width * scale));
        int scaledH = Math.Max(1, (int)Math.Round(image.Height * scale));
        float padX = (targetWidth - scaledW) / 2.0f;
        float padY = (targetHeight - scaledH) / 2.0f;

        var info = new LetterboxInfo(targetWidth, targetHeight, scaledW, scaledH, padX, padY, scale);

        using var resized = image.Clone(ctx => ctx.Resize(scaledW, scaledH));
        var tensor = new DenseTensor<float>([1, 3, targetHeight, targetWidth]);

        float padNorm = padColor / 255.0f;
        for (int c = 0; c < 3; c++)
        {
            for (int y = 0; y < targetHeight; y++)
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    tensor[0, c, y, x] = padNorm;
                }
            }
        }

        int startX = (int)Math.Round(padX);
        int startY = (int)Math.Round(padY);

        resized.ProcessPixelRows(pixelAccess =>
        {
            for (int y = 0; y < scaledH; y++)
            {
                var row = pixelAccess.GetRowSpan(y);
                int destY = startY + y;
                if (destY >= targetHeight) break;

                for (int x = 0; x < scaledW; x++)
                {
                    int destX = startX + x;
                    if (destX >= targetWidth) break;

                    var px = row[x];
                    tensor[0, 0, destY, destX] = px.R / 255.0f;
                    tensor[0, 1, destY, destX] = px.G / 255.0f;
                    tensor[0, 2, destY, destX] = px.B / 255.0f;
                }
            }
        });

        return (tensor, info);
    }

    public static DenseTensor<float> CreateNchwTensor(
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

    public static DenseTensor<float> CreateNchwTensorNormalized(
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

    public static float[] Softmax(float[] logits)
    {
        float max = logits.Max();
        float[] exp = logits.Select(x => MathF.Exp(x - max)).ToArray();
        float sum = exp.Sum();
        return exp.Select(x => x / sum).ToArray();
    }

    public static string GetCocoLabel(int classId)
    {
        if (classId >= 0 && classId < CocoLabels.Length)
            return CocoLabels[classId];
        return $"object_{classId}";
    }

    public static string MapToUserCategory(int imageNetIdx) => imageNetIdx switch
    {
        >= 151 and <= 268 => "Mascotas y Animales",
        >= 281 and <= 285 => "Mascotas y Animales",
        >= 7 and <= 24 => "Animales y Naturaleza",
        >= 80 and <= 100 => "Animales y Naturaleza",
        >= 401 and <= 475 => "Vehículos y Transporte",
        >= 479 and <= 511 => "Vehículos y Transporte",
        >= 924 and <= 969 => "Comida y Gastronomía",
        >= 970 and <= 999 => "Naturaleza y Paisajes",
        0 or 878 or 879 => "Personas y Retratos",
        >= 576 and <= 589 => "Tecnología y Electrónica",
        _ => "Fotografía General"
    };

    public static string GetImageNetLabel(int idx) => idx switch
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

    public static bool IsSemanticMatch(string query, string detected)
    {
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

        var queryWords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var detectedWords = detected.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return queryWords.Any(qw => detectedWords.Contains(qw, StringComparer.OrdinalIgnoreCase));
    }
}
