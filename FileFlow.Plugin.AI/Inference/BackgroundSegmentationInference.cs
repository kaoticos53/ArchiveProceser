using Microsoft.ML.OnnxRuntime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FileFlow.Plugin.AI.Inference;

/// <summary>
/// Motor de inferencia para segmentación de sujetos y eliminación de fondo (RMBG-1.4, MODNet).
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
                var mRow = maskAccessor.GetRowSpan(y);
                int offset = y * origW;
                for (int x = 0; x < origW; x++)
                {
                    maskBytes[offset + x] = mRow[x].PackedValue;
                }
            }
        });

        if (maskOnly)
        {
            var maskImage = new Image<Rgba32>(origW, origH);
            maskImage.ProcessPixelRows(outAccessor =>
            {
                for (int y = 0; y < origH; y++)
                {
                    var oRow = outAccessor.GetRowSpan(y);
                    int offset = y * origW;
                    for (int x = 0; x < origW; x++)
                    {
                        byte l = maskBytes[offset + x];
                        oRow[x] = new Rgba32(l, l, l, 255);
                    }
                }
            });
            return maskImage;
        }

        var result = image.Clone();
        result.ProcessPixelRows(resAccessor =>
        {
            for (int y = 0; y < origH; y++)
            {
                var rRow = resAccessor.GetRowSpan(y);
                int offset = y * origW;
                for (int x = 0; x < origW; x++)
                {
                    byte alpha = maskBytes[offset + x];
                    if (backgroundColor.HasValue)
                    {
                        float a = alpha / 255.0f;
                        byte r = (byte)(rRow[x].R * a + backgroundColor.Value.R * (1 - a));
                        byte g = (byte)(rRow[x].G * a + backgroundColor.Value.G * (1 - a));
                        byte b = (byte)(rRow[x].B * a + backgroundColor.Value.B * (1 - a));
                        rRow[x] = new Rgba32(r, g, b, 255);
                    }
                    else
                    {
                        rRow[x] = new Rgba32(rRow[x].R, rRow[x].G, rRow[x].B, alpha);
                    }
                }
            }
        });

        return result;
    }
}
