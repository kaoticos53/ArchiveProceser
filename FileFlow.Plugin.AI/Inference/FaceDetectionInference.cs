using Microsoft.ML.OnnxRuntime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FileFlow.Plugin.AI.Inference;

/// <summary>
/// Contenedor de caja delimitadora de rostro detectado con coordenadas normalizadas.
/// </summary>
public record struct DetectedFaceBox(float X1, float Y1, float X2, float Y2, float Score);

/// <summary>
/// Motor de inferencia para detección de rostros con UltraFace RFB 320 y Supresión de No Máximos (NMS).
/// </summary>
public static class FaceDetectionInference
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

    public static (int FaceCount, double MaxConfidence, List<DetectedFaceBox> Faces) DetectFaces(
        string modelPath,
        Image<Rgb24> image,
        double confidenceThreshold = 0.7)
    {
        var session = OnnxSessionManager.GetOrCreateSession(modelPath);

        using var resized = (image.Width == 320 && image.Height == 240) ? null : image.Clone(ctx => ctx.Resize(320, 240));
        var targetImage = resized ?? image;
        var tensor = TensorPreprocessors.CreateNchwTensorNormalized(targetImage, 320, 240, scale: 1.0f / 128.0f, shift: -127.0f / 128.0f);

        string inputName = session.InputNames[0];
        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, tensor) };

        float[]? scoresArr;
        float[]? boxesArr;
        int numAnchors;

        using (var outputs = OnnxSessionManager.RunInference(modelPath, inputs))
        {
            var outputList = outputs.ToList();
            if (outputList.Count == 0) return (0, 0.0, []);

            var scoresVal = outputList.FirstOrDefault(o => o.Name.Contains("score", StringComparison.OrdinalIgnoreCase) || o.Name.Contains("conf", StringComparison.OrdinalIgnoreCase))
                            ?? (outputList.Count > 1 && outputList[0].AsTensor<float>().Dimensions[^1] == 2 ? outputList[0] : outputList.FirstOrDefault(o => o.AsTensor<float>().Dimensions[^1] == 2));

            var boxesVal = outputList.FirstOrDefault(o => o.Name.Contains("box", StringComparison.OrdinalIgnoreCase) || o.Name.Contains("loc", StringComparison.OrdinalIgnoreCase))
                           ?? (outputList.Count > 1 && outputList[1].AsTensor<float>().Dimensions[^1] == 4 ? outputList[1] : outputList.FirstOrDefault(o => o.AsTensor<float>().Dimensions[^1] == 4));

            if (scoresVal == null || boxesVal == null)
            {
                scoresVal = outputList[0];
                boxesVal = outputList.Count > 1 ? outputList[1] : outputList[0];
            }

            var scoresTensor = scoresVal.AsTensor<float>();
            var boxesTensor = boxesVal.AsTensor<float>();

            scoresArr = scoresTensor.ToArray();
            boxesArr = boxesTensor.ToArray();
            numAnchors = scoresTensor.Dimensions.Length >= 2 
                ? scoresTensor.Dimensions[1] 
                : (int)(scoresTensor.Length / 2);
        }

        if (scoresArr == null || boxesArr == null || numAnchors == 0) return (0, 0.0, []);

        var candidateBoxes = new List<FaceBox>();

        for (int i = 0; i < numAnchors; i++)
        {
            float bgScore = scoresArr[i * 2];
            float faceScore = scoresArr[i * 2 + 1];

            float maxVal = MathF.Max(bgScore, faceScore);
            float expBg = MathF.Exp(bgScore - maxVal);
            float expFace = MathF.Exp(faceScore - maxVal);
            float faceProb = expFace / (expBg + expFace);

            if (faceProb >= confidenceThreshold)
            {
                float x1 = 0, y1 = 0, x2 = 0, y2 = 0;
                if (boxesArr.Length >= (i + 1) * 4)
                {
                    x1 = boxesArr[i * 4];
                    y1 = boxesArr[i * 4 + 1];
                    x2 = boxesArr[i * 4 + 2];
                    y2 = boxesArr[i * 4 + 3];
                }

                candidateBoxes.Add(new FaceBox(x1, y1, x2, y2, faceProb));
            }
        }

        if (candidateBoxes.Count == 0)
        {
            return (0, 0.0, []);
        }

        candidateBoxes.Sort((a, b) => b.Score.CompareTo(a.Score));

        var selectedFaces = new List<FaceBox>();
        const float iouThreshold = 0.45f;

        while (candidateBoxes.Count > 0)
        {
            var best = candidateBoxes[0];
            selectedFaces.Add(best);
            candidateBoxes.RemoveAt(0);

            candidateBoxes.RemoveAll(box => best.IoU(box) > iouThreshold);
        }

        int faceCount = selectedFaces.Count;
        double maxConf = selectedFaces.Count > 0 ? selectedFaces.Max(f => f.Score) : 0.0;
        var faceResults = selectedFaces.Select(f => new DetectedFaceBox(f.X1, f.Y1, f.X2, f.Y2, f.Score)).ToList();

        return (faceCount, maxConf, faceResults);
    }
}
