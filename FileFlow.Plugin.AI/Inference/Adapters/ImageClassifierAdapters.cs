using Microsoft.ML.OnnxRuntime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FileFlow.Plugin.AI.Inference.Adapters;

/// <summary>
/// Adaptador de clasificación para redes convolucionales estándar tipo MobileNetV2 y ResNet
/// (entrada 224x224 NCHW, normalización ImageNet, salida de 1000 probabilidades o vector logits).
/// </summary>
public class MobileNetClassifierAdapter : IImageClassifierAdapter
{
    public bool CanHandle(InferenceSession session)
    {
        var inMeta = session.InputMetadata.Values.FirstOrDefault();
        if (inMeta != null && inMeta.Dimensions.Length == 4)
        {
            if (inMeta.Dimensions[2] == 224 && inMeta.Dimensions[3] == 224) return true;
        }

        return session.InputNames.Count == 1 && session.OutputNames.Count == 1;
    }

    public (string Category, string TopLabel, double Confidence) Classify(
        InferenceSession session,
        string modelPath,
        Image<Rgb24> image)
    {
        using var resized = (image.Width == 224 && image.Height == 224) ? null : image.Clone(ctx => ctx.Resize(224, 224));
        var targetImage = resized ?? image;
        var tensor = TensorPreprocessors.CreateNchwTensor(targetImage, 224, 224,
            meanR: 0.485f, meanG: 0.456f, meanB: 0.406f,
            stdR: 0.229f, stdG: 0.224f, stdB: 0.225f);

        string inputName = session.InputNames[0];
        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, tensor) };

        float[] probabilities;
        using (var outputs = OnnxSessionManager.RunInference(modelPath, inputs))
        {
            probabilities = outputs.First().AsTensor<float>().ToArray();
        }

        int topIdx = 0;
        float topProb = float.MinValue;
        for (int i = 0; i < probabilities.Length; i++)
        {
            if (probabilities[i] > topProb)
            {
                topProb = probabilities[i];
                topIdx = i;
            }
        }

        float[] softmax = TensorPreprocessors.Softmax(probabilities);
        double confidence = softmax[topIdx];

        string synset = TensorPreprocessors.GetImageNetLabel(topIdx);
        string category = TensorPreprocessors.MapToUserCategory(topIdx);

        return (category, synset, confidence);
    }

    public double DetectNsfwScore(
        InferenceSession session,
        string modelPath,
        Image<Rgb24> image)
    {
        using var resized = (image.Width == 224 && image.Height == 224) ? null : image.Clone(ctx => ctx.Resize(224, 224));
        var targetImage = resized ?? image;

        var tensor = TensorPreprocessors.CreateNchwTensor(targetImage, 224, 224,
            meanR: 0.485f, meanG: 0.456f, meanB: 0.406f,
            stdR: 0.229f, stdG: 0.224f, stdB: 0.225f);

        string inputName = session.InputNames[0];
        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, tensor) };

        float[] rawScores;
        using (var outputs = OnnxSessionManager.RunInference(modelPath, inputs))
        {
            rawScores = outputs.First().AsTensor<float>().ToArray();
        }

        if (rawScores.Length == 0) return 0.0;

        if (rawScores.Length >= 2)
        {
            var probs = TensorPreprocessors.Softmax(rawScores);
            return Math.Clamp((double)probs[1], 0.0, 1.0);
        }

        float score = rawScores[0];
        if (score > 1.0f || score < 0f)
        {
            score = 1.0f / (1.0f + MathF.Exp(-score));
        }
        return Math.Clamp((double)score, 0.0, 1.0);
    }
}

/// <summary>
/// Factoría de adaptadores de clasificación de imágenes.
/// </summary>
public static class ImageClassifierAdapterFactory
{
    private static readonly IImageClassifierAdapter[] Adapters =
    [
        new MobileNetClassifierAdapter()
    ];

    public static IImageClassifierAdapter GetAdapter(InferenceSession session)
    {
        foreach (var adapter in Adapters)
        {
            if (adapter.CanHandle(session))
            {
                return adapter;
            }
        }

        return Adapters[0];
    }
}
