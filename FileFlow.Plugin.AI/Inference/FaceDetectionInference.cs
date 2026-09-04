using FileFlow.Plugin.AI.Inference.Adapters;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FileFlow.Plugin.AI.Inference;

/// <summary>
/// Contenedor de caja delimitadora de rostro detectado con coordenadas normalizadas.
/// </summary>
public record struct DetectedFaceBox(float X1, float Y1, float X2, float Y2, float Score);

/// <summary>
/// Motor de inferencia canónico para detección de rostros.
/// Delega la ejecución en el adaptador resuelto por <see cref="FaceDetectorAdapterFactory"/>.
/// </summary>
public static class FaceDetectionInference
{
    public static (int FaceCount, double MaxConfidence, List<DetectedFaceBox> Faces) DetectFaces(
        string modelPath,
        Image<Rgb24> image,
        double confidenceThreshold = 0.7)
    {
        var session = OnnxSessionManager.GetOrCreateSession(modelPath);
        var adapter = FaceDetectorAdapterFactory.GetAdapter(session);
        return adapter.DetectFaces(session, modelPath, image, confidenceThreshold);
    }
}
