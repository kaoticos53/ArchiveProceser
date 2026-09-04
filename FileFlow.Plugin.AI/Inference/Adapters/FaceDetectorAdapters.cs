using Microsoft.ML.OnnxRuntime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FileFlow.Plugin.AI.Inference.Adapters;

/// <summary>
/// Adaptador de detección facial con UltraFace RFB-320 (entrada 320x240 NCHW, normalización (px-127)/128, NMS IoU 0.45).
/// </summary>
public class UltraFaceDetectorAdapter : IFaceDetectorAdapter
{
    private readonly struct FaceBox
    {
        public readonly float X1;
        public readonly float Y1;
        public readonly float X2;
        public readonly float Y2;
        public readonly float Score;

        public FaceBox(float x1, float y1, float x2, float y2, float score)
        {
            X1 = MathF.Min(x1, x2);
            Y1 = MathF.Min(y1, y2);
            X2 = MathF.Max(x1, x2);
            Y2 = MathF.Max(y1, y2);
            Score = score;
        }

        public float Area => MathF.Max(0, X2 - X1) * MathF.Max(0, Y2 - Y1);

        public float IoU(FaceBox other)
        {
            float interX1 = MathF.Max(X1, other.X1);
            float interY1 = MathF.Max(Y1, other.Y1);
            float interX2 = MathF.Min(X2, other.X2);
            float interY2 = MathF.Min(Y2, other.Y2);

            float interW = MathF.Max(0, interX2 - interX1);
            float interH = MathF.Max(0, interY2 - interY1);
            float interArea = interW * interH;

            if (interArea <= 0) return 0f;

            float unionArea = Area + other.Area - interArea;
            return unionArea > 0 ? interArea / unionArea : 0f;
        }
    }

    public bool CanHandle(InferenceSession session)
    {
        return session.OutputNames.Count >= 2;
    }

    public (int FaceCount, double MaxConfidence, List<DetectedFaceBox> Faces) DetectFaces(
        InferenceSession session,
        string modelPath,
        Image<Rgb24> image,
        double confidenceThreshold = 0.7)
    {
        int targetW = 320;
        int targetH = 240;

        using var resized = (image.Width == targetW && image.Height == targetH) ? null : image.Clone(ctx => ctx.Resize(targetW, targetH));
        var targetImage = resized ?? image;

        var tensor = TensorPreprocessors.CreateNchwTensorNormalized(targetImage, targetW, targetH, scale: 1.0f / 128.0f, shift: -127.0f / 128.0f);

        string inputName = session.InputNames[0];
        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, tensor) };

        float[] rawScores;
        float[] rawBoxes;

        using (var outputs = OnnxSessionManager.RunInference(modelPath, inputs))
        {
            var outputList = outputs.ToList();
            var scoresOutput = outputList.FirstOrDefault(o => o.Name.Contains("scores", StringComparison.OrdinalIgnoreCase)) ?? outputList[0];
            var boxesOutput = outputList.FirstOrDefault(o => o.Name.Contains("boxes", StringComparison.OrdinalIgnoreCase)) ?? outputList[1];

            rawScores = scoresOutput.AsTensor<float>().ToArray();
            rawBoxes = boxesOutput.AsTensor<float>().ToArray();
        }

        int numBoxes = rawScores.Length / 2;
        var candidates = new List<FaceBox>();

        for (int i = 0; i < numBoxes; i++)
        {
            float backgroundScore = rawScores[i * 2 + 0];
            float faceScore = rawScores[i * 2 + 1];

            float maxLogit = MathF.Max(backgroundScore, faceScore);
            float expBg = MathF.Exp(backgroundScore - maxLogit);
            float expFace = MathF.Exp(faceScore - maxLogit);
            float probFace = expFace / (expBg + expFace);

            if (probFace >= confidenceThreshold)
            {
                float x1 = rawBoxes[i * 4 + 0];
                float y1 = rawBoxes[i * 4 + 1];
                float x2 = rawBoxes[i * 4 + 2];
                float y2 = rawBoxes[i * 4 + 3];

                candidates.Add(new FaceBox(
                    Math.Clamp(x1, 0f, 1f),
                    Math.Clamp(y1, 0f, 1f),
                    Math.Clamp(x2, 0f, 1f),
                    Math.Clamp(y2, 0f, 1f),
                    probFace
                ));
            }
        }

        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        var kept = new List<FaceBox>();
        const float iouThreshold = 0.45f;

        foreach (var candidate in candidates)
        {
            bool shouldKeep = true;
            foreach (var existing in kept)
            {
                if (candidate.IoU(existing) > iouThreshold)
                {
                    shouldKeep = false;
                    break;
                }
            }

            if (shouldKeep)
            {
                kept.Add(candidate);
                if (kept.Count >= 50) break;
            }
        }

        double maxConf = kept.Count > 0 ? kept.Max(f => f.Score) : 0.0;
        var faceBoxes = kept.Select(f => new DetectedFaceBox(f.X1, f.Y1, f.X2, f.Y2, f.Score)).ToList();

        return (kept.Count, maxConf, faceBoxes);
    }
}

/// <summary>
/// Factoría de adaptadores de detección de rostros.
/// </summary>
public static class FaceDetectorAdapterFactory
{
    private static readonly IFaceDetectorAdapter[] Adapters =
    [
        new UltraFaceDetectorAdapter()
    ];

    public static IFaceDetectorAdapter GetAdapter(InferenceSession session)
    {
        foreach (var adapter in Adapters)
        {
            if (adapter.CanHandle(session)) return adapter;
        }

        return Adapters[0];
    }
}
