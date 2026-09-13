using System;
using System.IO;
using Avalonia.Media.Imaging;
using SixLabors.ImageSharp;

namespace FileFlow.App.Preview.Helpers;

/// <summary>
/// Utilidad universal para la decodificación y carga de imágenes en Avalonia UI.
/// Soporta formatos nativos y formatos avanzados (WebP, TGA, TIFF, etc.) mediante ImageSharp.
/// </summary>
public static class AvaloniaImageLoader
{
    public static Bitmap? LoadBitmap(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;

        try
        {
            return new Bitmap(filePath);
        }
        catch
        {
            // Fallback a decodificador universal con ImageSharp si falla el decodificador nativo
            try
            {
                using var image = SixLabors.ImageSharp.Image.Load(filePath);
                using var ms = new MemoryStream();
                image.SaveAsPng(ms);
                ms.Position = 0;
                return new Bitmap(ms);
            }
            catch
            {
                return null;
            }
        }
    }
}
