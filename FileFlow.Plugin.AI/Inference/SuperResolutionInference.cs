using FileFlow.Plugin.AI.Inference.Adapters;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FileFlow.Plugin.AI.Inference;

/// <summary>
/// Motor de inferencia canónico para superresolución y escalado neuronal de imágenes.
/// Delega la ejecución en el adaptador resuelto por <see cref="SuperResolutionAdapterFactory"/>.
/// </summary>
public static class SuperResolutionInference
{
    public static Image<Rgb24> UpscaleImage(
        string modelPath,
        Image<Rgb24> image,
        int requestedScale = 4)
    {
        var session = OnnxSessionManager.GetOrCreateSession(modelPath);
        var adapter = SuperResolutionAdapterFactory.GetAdapter(session);
        return adapter.Upscale(session, modelPath, image, requestedScale);
    }
}
