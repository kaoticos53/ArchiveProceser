using Microsoft.ML.OnnxRuntime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FileFlow.Plugin.AI.Inference.Adapters;

/// <summary>
/// Contrato canónico de adaptador de inferencia para superresolución y escalado neuronal.
/// </summary>
public interface ISuperResolutionAdapter
{
    bool CanHandle(InferenceSession session);

    Image<Rgb24> Upscale(
        InferenceSession session,
        string modelPath,
        Image<Rgb24> image,
        int requestedScale = 4);
}
