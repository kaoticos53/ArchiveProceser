using FileFlow.Plugin.AI.Inference.Adapters;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FileFlow.Plugin.AI.Inference;

/// <summary>
/// Motor de inferencia canónico para clasificación de imágenes y detección de contenido sensible.
/// Delega la ejecución en el adaptador resuelto por <see cref="ImageClassifierAdapterFactory"/>.
/// </summary>
public static class ImageClassificationInference
{
    public static (string Category, string TopLabel, double Confidence) ClassifyImage(string modelPath, Image<Rgb24> image)
    {
        var session = OnnxSessionManager.GetOrCreateSession(modelPath);
        var adapter = ImageClassifierAdapterFactory.GetAdapter(session);
        return adapter.Classify(session, modelPath, image);
    }

    public static double DetectNsfwScore(string modelPath, Image<Rgb24> image)
    {
        var session = OnnxSessionManager.GetOrCreateSession(modelPath);
        var adapter = ImageClassifierAdapterFactory.GetAdapter(session);
        return adapter.DetectNsfwScore(session, modelPath, image);
    }
}
