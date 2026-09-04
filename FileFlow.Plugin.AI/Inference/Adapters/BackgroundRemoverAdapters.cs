using Microsoft.ML.OnnxRuntime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FileFlow.Plugin.AI.Inference.Adapters;

/// <summary>
/// Adaptador especializado para modelos RMBG-1.4 y MODNet de segmentación de sujetos.
/// </summary>
public class RmbgSegmentationAdapter : IBackgroundRemoverAdapter
{
    public bool CanHandle(InferenceSession session)
    {
        return session.InputNames.Count >= 1 && session.OutputNames.Count >= 1;
    }

    public Image<Rgba32> RemoveBackground(
        InferenceSession session,
        string modelPath,
        Image<Rgba32> image,
        Rgba32? backgroundColor = null,
        bool maskOnly = false)
    {
        int targetW = 1024;
        int targetH = 1024;
        try
        {
            var dims = session.InputMetadata.Values.FirstOrDefault()?.Dimensions;
            if (dims != null && dims.Length >= 4)
            {
                if (dims[2] > 0 && dims[3] > 0)
                {
                    targetH = dims[2];
                    targetW = dims[3];
                }
            }
        }
        catch { }

        int origW = image.Width;
        int origH = image.Height;

        using var rgbImage = new Image<Rgb24>(targetW, targetH);
        using var resized = image.Clone(ctx => ctx.Resize(targetW, targetH));
        resized.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < targetH; y++)
            {
                var srcRow = accessor.GetRowSpan(y);
                for (int x = 0; x < targetW; x++)
                {
                    rgbImage[x, y] = new Rgb24(srcRow[x].R, srcRow[x].G, srcRow[x].B);
                }
            }
        });

        var tensor = TensorPreprocessors.CreateNchwTensor(rgbImage, targetW, targetH,
            meanR: 0.5f, meanG: 0.5f, meanB: 0.5f,
            stdR: 1.0f, stdG: 1.0f, stdB: 1.0f,
            scale: 1.0f / 255.0f);

        string inputName = session.InputNames[0];
        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, tensor) };

        float[] maskData;
        int outH = targetH;
        int outW = targetW;

        using (var outputs = OnnxSessionManager.RunInference(modelPath, inputs))
        {
            var outTensor = outputs.First().AsTensor<float>();
            var dims = outTensor.Dimensions;
            if (dims.Length >= 2)
            {
                outH = dims[^2];
                outW = dims[^1];
            }
            maskData = outTensor.ToArray();
        }

        using var rawMask = new Image<L8>(outW, outH);
        rawMask.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < outH; y++)
            {
                var row = accessor.GetRowSpan(y);
                int offset = y * outW;
                for (int x = 0; x < outW; x++)
                {
                    float val = maskData[offset + x];
                    val = Math.Clamp(val, 0f, 1f);
                    row[x] = new L8((byte)(val * 255));
                }
            }
        });

        using var finalMask = rawMask.Clone(ctx => ctx.Resize(origW, origH));

        byte[] maskBytes = new byte[origW * origH];
        finalMask.ProcessPixelRows(maskAccessor =>
        {
            for (int y = 0; y < origH; y++)
            {
                var row = maskAccessor.GetRowSpan(y);
                int offset = y * origW;
                for (int x = 0; x < origW; x++)
                {
                    maskBytes[offset + x] = row[x].PackedValue;
                }
            }
        });

        if (maskOnly)
        {
            var resultMask = new Image<Rgba32>(origW, origH);
            resultMask.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < origH; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    int offset = y * origW;
                    for (int x = 0; x < origW; x++)
                    {
                        byte alpha = maskBytes[offset + x];
                        row[x] = new Rgba32(alpha, alpha, alpha, 255);
                    }
                }
            });
            return resultMask;
        }

        var result = image.Clone();
        result.ProcessPixelRows(dstAccessor =>
        {
            for (int y = 0; y < origH; y++)
            {
                var dstRow = dstAccessor.GetRowSpan(y);
                int offset = y * origW;

                for (int x = 0; x < origW; x++)
                {
                    byte alpha = maskBytes[offset + x];
                    var srcPx = dstRow[x];

                    if (backgroundColor.HasValue)
                    {
                        var bg = backgroundColor.Value;
                        float a = alpha / 255.0f;
                        byte r = (byte)(srcPx.R * a + bg.R * (1.0f - a));
                        byte g = (byte)(srcPx.G * a + bg.G * (1.0f - a));
                        byte b = (byte)(srcPx.B * a + bg.B * (1.0f - a));
                        dstRow[x] = new Rgba32(r, g, b, 255);
                    }
                    else
                    {
                        dstRow[x] = new Rgba32(srcPx.R, srcPx.G, srcPx.B, alpha);
                    }
                }
            }
        });

        return result;
    }
}

/// <summary>
/// Factoría de adaptadores de segmentación y eliminación de fondo.
/// </summary>
public static class BackgroundRemoverAdapterFactory
{
    private static readonly IBackgroundRemoverAdapter[] Adapters =
    [
        new RmbgSegmentationAdapter()
    ];

    public static IBackgroundRemoverAdapter GetAdapter(InferenceSession session)
    {
        foreach (var adapter in Adapters)
        {
            if (adapter.CanHandle(session)) return adapter;
        }

        return Adapters[0];
    }
}
