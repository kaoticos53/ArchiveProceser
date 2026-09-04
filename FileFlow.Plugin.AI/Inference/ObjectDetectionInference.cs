using FileFlow.Plugin.AI.Inference.Adapters;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FileFlow.Plugin.AI.Inference;

/// <summary>
/// Contenedor de caja delimitadora de objeto detectado con etiqueta, confianza y coordenadas normalizadas [0..1].
/// </summary>
public record struct DetectedObjectBox(string Label, float X1, float Y1, float X2, float Y2, double Score);

/// <summary>
/// Fachada de inferencia canónica para detección de objetos en tiempo real (Tiny YOLOv3, YOLOv8, YOLO-World, Grounding DINO).
/// Delega la ejecución en el adaptador especializado resuelto por <see cref="ObjectDetectorAdapterFactory"/>.
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
        var adapter = ObjectDetectorAdapterFactory.GetAdapter(session);

        return adapter.Detect(
            session,
            modelPath,
            image,
            confidenceThreshold,
            originalWidth > 0 ? originalWidth : image.Width,
            originalHeight > 0 ? originalHeight : image.Height,
            customQueries: null);
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

        var session = OnnxSessionManager.GetOrCreateSession(modelPath);
        var adapter = ObjectDetectorAdapterFactory.GetAdapter(session);

        var rawDetections = adapter.Detect(
            session,
            modelPath,
            image,
            confidenceThreshold * 0.75,
            originalWidth > 0 ? originalWidth : image.Width,
            originalHeight > 0 ? originalHeight : image.Height,
            queries);

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
