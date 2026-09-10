using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Resultado del análisis y clasificación de tipo de imagen.
/// </summary>
public record ImageTypeAnalysisResult(
    string TopCategory,
    double TopScore,
    Dictionary<string, double> CategoryScores,
    bool HasFaces,
    int FaceCount,
    bool HasCameraExif,
    double AspectRatio,
    double BrightnessPercentage,
    double TextEdgeDensity);

/// <summary>
/// Motor determinista de visión por computador y análisis estructural para clasificar tipos de imágenes:
/// Documento, Recibo/Factura, Retrato, Foto Grupal, Fotografía, Captura de Pantalla, Ilustración y Documento de Identidad.
/// </summary>
public static class ImageTypeAnalyzerEngine
{
    public const string CategoryDocument = "Document";
    public const string CategoryReceipt = "Receipt";
    public const string CategoryPortrait = "Portrait";
    public const string CategoryGroupPhoto = "GroupPhoto";
    public const string CategoryPhoto = "Photo";
    public const string CategoryScreenshot = "Screenshot";
    public const string CategoryIllustration = "Illustration";
    public const string CategoryIdCard = "IDCard";
    public const string CategoryOther = "Other";

    public static readonly string[] AllCategories =
    [
        CategoryDocument,
        CategoryReceipt,
        CategoryPortrait,
        CategoryGroupPhoto,
        CategoryPhoto,
        CategoryScreenshot,
        CategoryIllustration,
        CategoryIdCard,
        CategoryOther
    ];

    /// <summary>
    /// Analiza una imagen en memoria y calcula las probabilidades para cada categoría de imagen.
    /// </summary>
    public static ImageTypeAnalysisResult AnalyzeImage(
        Image<Rgb24> originalImage,
        string? faceModelPath = null,
        bool enableFaceDetection = true,
        bool checkExif = true,
        double confidenceThreshold = 0.55)
    {
        ArgumentNullException.ThrowIfNull(originalImage);

        int origWidth = originalImage.Width;
        int origHeight = originalImage.Height;
        double aspectRatio = (double)Math.Max(origWidth, origHeight) / Math.Max(1, Math.Min(origWidth, origHeight));
        bool isTall = origHeight > origWidth;

        // 1. Detección de metadatos EXIF fotográficos
        bool hasCameraExif = false;
        if (checkExif && originalImage.Metadata.ExifProfile != null)
        {
            var exif = originalImage.Metadata.ExifProfile;
            hasCameraExif = exif.TryGetValue(ExifTag.Make, out _) ||
                            exif.TryGetValue(ExifTag.Model, out _) ||
                            exif.TryGetValue(ExifTag.ISOSpeedRatings, out _) ||
                            exif.TryGetValue(ExifTag.FNumber, out _) ||
                            exif.TryGetValue(ExifTag.ExposureTime, out _);
        }

        // 2. Submuestreo rápido para análisis cromático y estructural (máx 384x384 para < 10ms)
        using var thumb = originalImage.Clone(ctx =>
        {
            ctx.Resize(new ResizeOptions
            {
                Size = new Size(384, 384),
                Mode = ResizeMode.Max
            });
        });

        int tw = thumb.Width;
        int th = thumb.Height;
        long totalPixels = (long)tw * th;

        long brightPixels = 0; // Luminancia > 200 (fondo blanco/claro)
        long veryBrightPixels = 0; // Luminancia > 230
        long darkPixels = 0;   // Luminancia < 80 (tinta/texto/bordes)
        long pureWhitePixels = 0; // R>245, G>245, B>245
        double sumLum = 0.0;
        double sumLumSq = 0.0;

        // Cuantización de color en 64 buckets (4x4x4) para medir riqueza cromática
        var colorBins = new HashSet<int>();
        long flatColorRuns = 0; // Segmentos de píxeles consecutivos idénticos (típico de UI)

        // Detección de bordes horizontales (gradientes verticales) para medir densidad de texto
        long horizontalTextEdges = 0;

        thumb.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < th; y++)
            {
                var row = accessor.GetRowSpan(y);
                Rgb24 prevPixel = row[0];
                int runLength = 1;

                for (int x = 0; x < tw; x++)
                {
                    var p = row[x];
                    double lum = 0.299 * p.R + 0.587 * p.G + 0.114 * p.B;
                    sumLum += lum;
                    sumLumSq += lum * lum;

                    if (lum > 200) brightPixels++;
                    if (lum > 230) veryBrightPixels++;
                    if (lum < 80) darkPixels++;
                    if (p.R > 245 && p.G > 245 && p.B > 245) pureWhitePixels++;

                    int bin = ((p.R >> 6) << 4) | ((p.G >> 6) << 2) | (p.B >> 6);
                    colorBins.Add(bin);

                    if (x > 0)
                    {
                        if (Math.Abs(p.R - prevPixel.R) < 3 &&
                            Math.Abs(p.G - prevPixel.G) < 3 &&
                            Math.Abs(p.B - prevPixel.B) < 3)
                        {
                            runLength++;
                        }
                        else
                        {
                            if (runLength >= 12) flatColorRuns += runLength;
                            runLength = 1;
                        }
                    }
                    prevPixel = p;

                    // Detección de contraste de línea de texto (comparar con fila inferior)
                    if (y < th - 2)
                    {
                        var nextRow = accessor.GetRowSpan(y + 2);
                        var np = nextRow[x];
                        double nextLum = 0.299 * np.R + 0.587 * np.G + 0.114 * np.B;
                        if (Math.Abs(lum - nextLum) > 60)
                        {
                            horizontalTextEdges++;
                        }
                    }
                }

                if (runLength >= 12) flatColorRuns += runLength;
            }
        });

        double brightRatio = (double)brightPixels / Math.Max(1, totalPixels);
        double veryBrightRatio = (double)veryBrightPixels / Math.Max(1, totalPixels);
        double darkRatio = (double)darkPixels / Math.Max(1, totalPixels);
        double pureWhiteRatio = (double)pureWhitePixels / Math.Max(1, totalPixels);
        double flatAreaRatio = (double)flatColorRuns / Math.Max(1, totalPixels);
        double textEdgeDensity = (double)horizontalTextEdges / Math.Max(1, totalPixels);
        int paletteDiversity = colorBins.Count; // 1 a 64

        double meanLum = sumLum / Math.Max(1, totalPixels);
        double varianceLum = Math.Max(0.0, (sumLumSq / Math.Max(1, totalPixels)) - (meanLum * meanLum));
        double stdDevLum = Math.Sqrt(varianceLum);

        // 3. Verificación Facial (UltraFace ONNX) si está habilitada
        bool hasFaces = false;
        int faceCount = 0;
        double maxFaceRatio = 0.0;

        if (enableFaceDetection && !string.IsNullOrWhiteSpace(faceModelPath) && File.Exists(faceModelPath))
        {
            try
            {
                using var faceInput = originalImage.Clone(ctx => ctx.Resize(320, 240));
                var (cnt, maxConf, faceBoxes) = OnnxInferenceEngine.DetectFaces(faceModelPath, faceInput, 0.65);
                faceCount = cnt;
                hasFaces = cnt > 0;

                if (faceBoxes.Count > 0)
                {
                    // Calcular el área relativa de la cara más grande (coordenadas normalizadas [0, 1])
                    foreach (var box in faceBoxes)
                    {
                        double area = Math.Abs(box.X2 - box.X1) * Math.Abs(box.Y2 - box.Y1);
                        if (area > maxFaceRatio) maxFaceRatio = area;
                    }
                }
            }
            catch
            {
                // Fallback seguro si falla UltraFace
            }
        }

        // 4. Puntuación ponderada para cada categoría
        var scores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        // --- A. DOCUMENTO (Document) ---
        // Fondo predominantemente blanco/claro (>60%), contraste bimodal (texto oscuro sobre fondo claro), ratio ~A4 (1.2 - 1.6)
        double docScore = 0.0;
        if (brightRatio >= 0.55 && darkRatio >= 0.02 && darkRatio <= 0.35)
        {
            docScore += 0.40;
            if (veryBrightRatio >= 0.50) docScore += 0.20;
            if (pureWhiteRatio >= 0.25) docScore += 0.15;
            if (textEdgeDensity >= 0.08) docScore += 0.15;
            if (aspectRatio >= 1.25 && aspectRatio <= 1.65) docScore += 0.10; // Cerca de 1.414 (A4)
            if (!hasCameraExif) docScore += 0.05;
            if (paletteDiversity <= 30) docScore += 0.05;
        }
        scores[CategoryDocument] = Math.Clamp(docScore, 0.0, 1.0);

        // --- B. RECIBO / TICKET / FACTURA TÉRMICA (Receipt) ---
        // Proporción muy vertical/alargada (ratio >= 1.8), fondo claro, texto denso
        double receiptScore = 0.0;
        if (brightRatio >= 0.50 && isTall && aspectRatio >= 1.70)
        {
            receiptScore += 0.45;
            if (aspectRatio >= 2.1) receiptScore += 0.25; // Típico ticket de supermercado/caja
            if (textEdgeDensity >= 0.06) receiptScore += 0.15;
            if (paletteDiversity <= 24) receiptScore += 0.15;
            if (pureWhiteRatio >= 0.20 || veryBrightRatio >= 0.40) receiptScore += 0.10;
        }
        scores[CategoryReceipt] = Math.Clamp(receiptScore, 0.0, 1.0);

        // --- C. TARJETA DE IDENTIDAD / DNI / CRÉDITO (IDCard) ---
        // Proporción estándar ID-1 (~1.58:1, entre 1.45 y 1.70), elementos compactos, posible micro-cara
        double idCardScore = 0.0;
        if (aspectRatio >= 1.48 && aspectRatio <= 1.68)
        {
            idCardScore += 0.25;
            if (hasFaces && faceCount == 1 && maxFaceRatio <= 0.12)
            {
                idCardScore += 0.50; // Foto carnet pequeña dentro de tarjeta
            }
            if (brightRatio >= 0.30 && textEdgeDensity >= 0.06) idCardScore += 0.20;
            if (paletteDiversity is >= 10 and <= 45) idCardScore += 0.15;
        }
        scores[CategoryIdCard] = Math.Clamp(idCardScore, 0.0, 1.0);

        // --- D. RETRATO INDIVIDUAL (Portrait) ---
        // 1 rostro dominante grande (>8% del área)
        double portraitScore = 0.0;
        if (faceCount == 1)
        {
            portraitScore += 0.50;
            if (maxFaceRatio >= 0.08) portraitScore += 0.35; // Cara en primer plano
            if (hasCameraExif) portraitScore += 0.15;
            if (paletteDiversity >= 20) portraitScore += 0.10;
        }
        scores[CategoryPortrait] = Math.Clamp(portraitScore, 0.0, 1.0);

        // --- E. FOTO GRUPAL (GroupPhoto) ---
        // 2 o más rostros
        double groupScore = 0.0;
        if (faceCount >= 2)
        {
            groupScore += 0.70;
            if (faceCount >= 3) groupScore += 0.15;
            if (hasCameraExif) groupScore += 0.15;
        }
        scores[CategoryGroupPhoto] = Math.Clamp(groupScore, 0.0, 1.0);

        // --- F. CAPTURA DE PANTALLA (Screenshot) ---
        // Ratios de pantalla exactos (16:9, 16:10, 19.5:9, 20:9), áreas planas de UI, sin EXIF
        double screenshotScore = 0.0;
        bool isScreenRatio = IsDisplayAspectRatio(origWidth, origHeight);
        if (isScreenRatio && !hasCameraExif)
        {
            screenshotScore += 0.40;
            if (flatAreaRatio >= 0.15) screenshotScore += 0.25;
            if (brightRatio < 0.75 || paletteDiversity >= 15) screenshotScore += 0.15;
            if (faceCount == 0) screenshotScore += 0.10;
            if (origWidth is 1920 or 1080 or 2560 or 3840 or 1280 or 1440 or 2160 or 720) screenshotScore += 0.15;
        }
        scores[CategoryScreenshot] = Math.Clamp(screenshotScore, 0.0, 1.0);

        // --- G. ILUSTRACIÓN / DIBUJO / MANGA / COMIC (Illustration) ---
        // Paleta reducida/discretizada, sin EXIF, no documentos
        double illustrationScore = 0.0;
        if (!hasCameraExif && faceCount == 0 && brightRatio < 0.65)
        {
            if (paletteDiversity <= 28) illustrationScore += 0.35;
            if (flatAreaRatio >= 0.10) illustrationScore += 0.25;
            if (stdDevLum > 40) illustrationScore += 0.20;
            if (origWidth % 100 != 0 && origHeight % 100 != 0) illustrationScore += 0.10;
        }
        scores[CategoryIllustration] = Math.Clamp(illustrationScore, 0.0, 1.0);

        // --- H. FOTOGRAFÍA / PAISAJE / OBJETO (Photo) ---
        // Metadatos de cámara, gradientes continuos, alta diversidad cromática, sin predominio de texto
        double photoScore = 0.0;
        if (hasCameraExif) photoScore += 0.45;
        if (paletteDiversity >= 30) photoScore += 0.25;
        if (brightRatio < 0.55) photoScore += 0.15;
        if (faceCount == 0) photoScore += 0.10;
        if (flatAreaRatio < 0.10) photoScore += 0.15;
        scores[CategoryPhoto] = Math.Clamp(photoScore, 0.0, 1.0);

        // Determinación de la categoría ganadora
        var best = scores.OrderByDescending(kv => kv.Value).FirstOrDefault();
        string topCategory = (best.Value >= confidenceThreshold) ? best.Key : CategoryOther;
        double topScore = (best.Value >= confidenceThreshold) ? best.Value : 0.0;

        return new ImageTypeAnalysisResult(
            topCategory,
            topScore,
            scores,
            hasFaces,
            faceCount,
            hasCameraExif,
            Math.Round(aspectRatio, 2),
            Math.Round(brightRatio, 2),
            Math.Round(textEdgeDensity, 3));
    }

    /// <summary>
    /// Comprueba si las dimensiones coinciden con relaciones de aspecto estándar de monitores y smartphones.
    /// </summary>
    private static bool IsDisplayAspectRatio(int w, int h)
    {
        double r = (double)Math.Max(w, h) / Math.Max(1, Math.Min(w, h));
        // 16:9 = 1.777, 16:10 = 1.60, 19.5:9 = 2.166, 20:9 = 2.222, 21:9 = 2.333, 4:3 = 1.333
        return Math.Abs(r - (16.0 / 9.0)) < 0.02 ||
               Math.Abs(r - (16.0 / 10.0)) < 0.02 ||
               Math.Abs(r - (19.5 / 9.0)) < 0.03 ||
               Math.Abs(r - (20.0 / 9.0)) < 0.03 ||
               Math.Abs(r - (21.0 / 9.0)) < 0.03 ||
               Math.Abs(r - (4.0 / 3.0)) < 0.02;
    }
}
