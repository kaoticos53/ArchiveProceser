using Microsoft.ML.OnnxRuntime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FileFlow.Plugin.AI.Inference.Adapters;

/// <summary>
/// Contrato canónico de adaptador de inferencia para detección de objetos.
/// Cada adaptador aísla el preprocesamiento específico del modelo (letterbox, normalización, embeddings CLIP),
/// la llamada a la sesión ONNX y la decodificación de cajas/NMS a coordenadas canónicas normalizadas [0..1].
/// </summary>
public interface IObjectDetectorAdapter
{
    /// <summary>
    /// Determina si este adaptador es compatible con la estructura/metadata de la sesión ONNX dada.
    /// </summary>
    bool CanHandle(InferenceSession session);

    /// <summary>
    /// Ejecuta la inferencia de detección de objetos de forma canónica.
    /// </summary>
    List<(string Label, double Confidence, DetectedObjectBox Box)> Detect(
        InferenceSession session,
        string modelPath,
        Image<Rgb24> image,
        double confidenceThreshold,
        int originalWidth,
        int originalHeight,
        List<string>? customQueries = null);
}
