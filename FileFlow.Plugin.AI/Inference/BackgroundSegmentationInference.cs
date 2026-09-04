using FileFlow.Plugin.AI.Inference.Adapters;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FileFlow.Plugin.AI.Inference;

/// <summary>
/// Motor de inferencia canónico para segmentación de sujetos y eliminación de fondo.
/// Delega la ejecución en el adaptador resuelto por <see cref="BackgroundRemoverAdapterFactory"/>.
/// </summary>
public static class BackgroundSegmentationInference
{
    public static Image<Rgba32> RemoveBackground(
        string modelPath,
        Image<Rgba32> image,
        Rgba32? backgroundColor = null,
        bool maskOnly = false)
    {
        var session = OnnxSessionManager.GetOrCreateSession(modelPath);
        var adapter = BackgroundRemoverAdapterFactory.GetAdapter(session);
        return adapter.RemoveBackground(session, modelPath, image, backgroundColor, maskOnly);
    }
}
