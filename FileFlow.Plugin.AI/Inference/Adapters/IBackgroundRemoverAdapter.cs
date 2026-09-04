using Microsoft.ML.OnnxRuntime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FileFlow.Plugin.AI.Inference.Adapters;

/// <summary>
/// Contrato canónico de adaptador de inferencia para segmentación y eliminación de fondo.
/// </summary>
public interface IBackgroundRemoverAdapter
{
    bool CanHandle(InferenceSession session);

    Image<Rgba32> RemoveBackground(
        InferenceSession session,
        string modelPath,
        Image<Rgba32> image,
        Rgba32? backgroundColor = null,
        bool maskOnly = false);
}
