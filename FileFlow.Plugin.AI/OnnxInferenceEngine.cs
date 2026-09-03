using FileFlow.Plugin.AI.Inference;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FileFlow.Plugin.AI;

// Reexportación de tipos para compatibilidad 100% hacia atrás
public record struct DetectedFaceBox(float X1, float Y1, float X2, float Y2, float Score);
public record struct DetectedObjectBox(string Label, float X1, float Y1, float X2, float Y2, double Score);

/// <summary>
/// Fachada (Facade) unificada del motor de inferencia ONNX para visión computacional,
/// clasificación, detección, superresolución y segmentación.
/// </summary>
public static class OnnxInferenceEngine
{
    /// <summary>Clasifica una imagen usando MobileNet/ResNet.</summary>
    public static (string Category, string TopLabel, double Confidence) ClassifyImage(string modelPath, Image<Rgb24> image) =>
        ImageClassificationInference.ClassifyImage(modelPath, image);

    /// <summary>Evalúa el nivel de contenido sensible (NSFW).</summary>
    public static double DetectNsfwScore(string modelPath, Image<Rgb24> image) =>
        ImageClassificationInference.DetectNsfwScore(modelPath, image);

    /// <summary>Detecta rostros humanos usando UltraFace RFB 320.</summary>
    public static (int FaceCount, double MaxConfidence, List<FileFlow.Plugin.AI.Inference.DetectedFaceBox> Faces) DetectFaces(
        string modelPath, Image<Rgb24> image, double confidenceThreshold = 0.7) =>
        FaceDetectionInference.DetectFaces(modelPath, image, confidenceThreshold);

    /// <summary>Detecta objetos en tiempo real con YOLO.</summary>
    public static List<(string Label, double Confidence, FileFlow.Plugin.AI.Inference.DetectedObjectBox Box)> DetectObjects(
        string modelPath, Image<Rgb24> image, double confidenceThreshold = 0.4, int originalWidth = 0, int originalHeight = 0) =>
        ObjectDetectionInference.DetectObjects(modelPath, image, confidenceThreshold, originalWidth, originalHeight);

    /// <summary>Detecta objetos mediante prompts en lenguaje natural (Open-Vocabulary).</summary>
    public static List<(string Label, double Confidence, FileFlow.Plugin.AI.Inference.DetectedObjectBox Box)> DetectPromptObjects(
        string modelPath, Image<Rgb24> image, string englishPrompt, double confidenceThreshold = 0.35, int originalWidth = 0, int originalHeight = 0) =>
        ObjectDetectionInference.DetectPromptObjects(modelPath, image, englishPrompt, confidenceThreshold, originalWidth, originalHeight);

    /// <summary>Elimina el fondo o segmenta el sujeto principal de la imagen.</summary>
    public static Image<Rgba32> RemoveBackground(
        string modelPath, Image<Rgba32> image, Rgba32? backgroundColor = null, bool maskOnly = false) =>
        BackgroundSegmentationInference.RemoveBackground(modelPath, image, backgroundColor, maskOnly);

    /// <summary>Escala y restaura una imagen usando superresolución neuronal (Real-ESRGAN / Swin2SR).</summary>
    public static Image<Rgb24> UpscaleImage(string modelPath, Image<Rgb24> image, int requestedScale = 4) =>
        SuperResolutionInference.UpscaleImage(modelPath, image, requestedScale);

    /// <summary>Obtiene la etiqueta COCO asociada a un identificador de clase.</summary>
    public static string GetCocoLabel(int classId) =>
        TensorPreprocessors.GetCocoLabel(classId);

    /// <summary>Libera todas las sesiones ONNX en memoria.</summary>
    public static void ClearSessionCache() =>
        OnnxSessionManager.ClearSessionCache();
}
