using Microsoft.ML.OnnxRuntime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FileFlow.Plugin.AI.Inference.Adapters;

/// <summary>
/// Contrato canónico de adaptador de inferencia para clasificación de imágenes.
/// </summary>
public interface IImageClassifierAdapter
{
    bool CanHandle(InferenceSession session);

    (string Category, string TopLabel, double Confidence) Classify(
        InferenceSession session,
        string modelPath,
        Image<Rgb24> image);

    double DetectNsfwScore(
        InferenceSession session,
        string modelPath,
        Image<Rgb24> image);
}
