using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FileFlow.Plugin.AI.Inference.Adapters;

/// <summary>
/// Adaptador de superresolución neuronal (Real-ESRGAN / Swin2SR / FSRCNN).
/// </summary>
public class RealEsrganAdapter : ISuperResolutionAdapter
{
    public bool CanHandle(InferenceSession session)
    {
        return session.InputNames.Count == 1 && session.OutputNames.Count == 1;
    }

    public Image<Rgb24> Upscale(
        InferenceSession session,
        string modelPath,
        Image<Rgb24> image,
        int requestedScale = 4)
    {
        int origW = image.Width;
        int origH = image.Height;

        var tensor = TensorPreprocessors.CreateNchwTensor(image, origW, origH,
            meanR: 0f, meanG: 0f, meanB: 0f,
            stdR: 1f, stdG: 1f, stdB: 1f,
            scale: 1.0f / 255.0f);

        string inputName = session.InputNames[0];
        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, tensor) };

        using var outputs = OnnxSessionManager.RunInference(modelPath, inputs);
        var outTensor = outputs.First().AsTensor<float>();
        var dims = outTensor.Dimensions;
        int outH = dims.Length >= 3 ? dims[^2] : origH * 4;
        int outW = dims.Length >= 4 ? dims[^1] : origW * 4;

        Memory<float> outputMem = outTensor is DenseTensor<float> dense
            ? dense.Buffer
            : outTensor.ToArray();

        var upscaled = new Image<Rgb24>(outW, outH);
        int planeSize = outH * outW;

        upscaled.ProcessPixelRows(accessor =>
        {
            var span = outputMem.Span;
            for (int y = 0; y < outH; y++)
            {
                var row = accessor.GetRowSpan(y);
                int offset = y * outW;
                for (int x = 0; x < outW; x++)
                {
                    int idx = offset + x;
                    float r = Math.Clamp(span[idx], 0f, 1f) * 255f;
                    float g = Math.Clamp(span[planeSize + idx], 0f, 1f) * 255f;
                    float b = Math.Clamp(span[planeSize * 2 + idx], 0f, 1f) * 255f;
                    row[x] = new Rgb24((byte)r, (byte)g, (byte)b);
                }
            }
        });

        if (requestedScale == 2 && outW > origW * 2)
        {
            upscaled.Mutate(ctx => ctx.Resize(origW * 2, origH * 2));
        }

        return upscaled;
    }
}

/// <summary>
/// Factoría de adaptadores de superresolución.
/// </summary>
public static class SuperResolutionAdapterFactory
{
    private static readonly ISuperResolutionAdapter[] Adapters =
    [
        new RealEsrganAdapter()
    ];

    public static ISuperResolutionAdapter GetAdapter(InferenceSession session)
    {
        foreach (var adapter in Adapters)
        {
            if (adapter.CanHandle(session)) return adapter;
        }

        return Adapters[0];
    }
}
