using System;
using System.IO;
using System.Windows.Media.Imaging;
using SixLabors.ImageSharp;

namespace FileFlow.App.Preview.Helpers;

/// <summary>
/// Utilidad universal para la decodificación y carga de imágenes en WPF.
/// Soporta formatos nativos de Windows (JPEG, PNG, BMP) y formatos avanzados
/// no soportados de forma nativa por WIC en Windows (WebP, TGA, TIFF, etc.) mediante ImageSharp.
/// </summary>
public static class WpfImageLoader
{
    public static BitmapSource? LoadBitmapSource(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;

        string ext = Path.GetExtension(filePath).ToLowerInvariant();

        // Para formatos que WIC soporta bien de forma nativa (JPEG, PNG, BMP, GIF), intentar carga nativa rápida
        if (ext is not ".webp" and not ".tga" and not ".pbm" and not ".svg")
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(filePath);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                // Fallback a decodificador universal con ImageSharp si WIC falla
            }
        }

        // Carga universal multi-formato (WebP, TGA, TIFF, etc.) mediante ImageSharp
        try
        {
            using var image = Image.Load(filePath);
            using var ms = new MemoryStream();
            image.SaveAsPng(ms);
            ms.Position = 0;

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }
}
