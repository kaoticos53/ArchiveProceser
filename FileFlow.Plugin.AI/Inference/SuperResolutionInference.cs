using Microsoft.ML.OnnxRuntime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FileFlow.Plugin.AI.Inference;

/// <summary>
/// Motor de inferencia para superresolución y escalado neuronal de imágenes (Real-ESRGAN, Swin2SR, FSRCNN).
/// </summary>
public static class SuperResolutionInference
{
    public static Image<Rgb24> UpscaleImage(
        string modelPath,
        Image<Rgb24> image,
        int requestedScale = 4)
    {
        var session = OnnxSessionManager.GetOrCreateSession(modelPath);

        int origW = image.Width;
        int origH = image.Height;

        var tensor = TensorPreprocessors.CreateNchwTensor(image, origW, origH,
            meanR: 0f, meanG: 0f, meanB: 0f,
            stdR: 1f, stdG: 1f, stdB: 1f,
            scale: 1.0f / 255.0f);

        string inputName = session.InputNames[0];
        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, tensor) };

        float[] outputData;
        int outH;
        int outW;

        using (var outputs = OnnxSessionManager.RunInference(modelPath, inputs))
        {
            var outTensor = outputs.First().AsTensor<float>();
            var dims = outTensor.Dimensions;
            outH = dims.Length >= 3 ? dims[^2] : origH * 4;
            outW = dims.Length >= 4 ? dims[^1] : origW * 4;
            outputData = outTensor.ToArray();
        }

        var upscaled = new Image<Rgb24>(outW, outH);
        int planeSize = outH * outW;

        upscaled.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < outH; y++)
            {
                var row = accessor.GetRowSpan(y);
                int offset = y * outW;
                for (int x = 0; x < outW; x++)
                {
                    int idx = offset + x;
                    float r = Math.Clamp(outputData[idx], 0f, 1f) * 255f;
                    float g = Math.Clamp(outputData[planeSize + idx], 0f, 1f) * 255f;
                    float b = Math.Clamp(outputData[planeSize * 2 + idx], 0f, 1f) * 255f;
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
