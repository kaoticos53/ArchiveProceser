using Microsoft.ML.OnnxRuntime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FileFlow.Plugin.AI.Inference.Adapters;

/// <summary>
/// Contrato canónico de adaptador de inferencia para detección de rostros.
/// </summary>
public interface IFaceDetectorAdapter
{
    bool CanHandle(InferenceSession session);

    (int FaceCount, double MaxConfidence, List<DetectedFaceBox> Faces) DetectFaces(
        InferenceSession session,
        string modelPath,
        Image<Rgb24> image,
        double confidenceThreshold = 0.7);
}
