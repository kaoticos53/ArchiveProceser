using System;
using System.IO;
using PdfSharp.Fonts;

namespace FileFlow.Plugin.Documents;

/// <summary>
/// Resolvedor de fuentes multiplataforma para PDFsharp en entornos Windows, Linux y macOS.
/// </summary>
public sealed class FileFlowFontResolver : IFontResolver
{
    private static readonly Lock _lock = new();
    private static bool _initialized;

    public static void EnsureInitialized()
    {
        if (_initialized) return;
        lock (_lock)
        {
            if (_initialized) return;
            try
            {
                if (GlobalFontSettings.FontResolver == null)
                {
                    GlobalFontSettings.FontResolver = new FileFlowFontResolver();
                }
            }
            catch { }
            _initialized = true;
        }
    }

    public byte[]? GetFont(string faceName)
    {
        string[] searchPaths = [
            "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/truetype/freefont/FreeSans.ttf",
            "/usr/share/fonts/liberation-sans/LiberationSans-Regular.ttf",
            "/usr/share/fonts/dejavu-sans-fonts/DejaVuSans.ttf",
            "C:\\Windows\\Fonts\\arial.ttf",
            "/System/Library/Fonts/Helvetica.ttc",
            "/System/Library/Fonts/SFNSText.ttf"
        ];

        foreach (var path in searchPaths)
        {
            if (File.Exists(path))
            {
                try { return File.ReadAllBytes(path); } catch { }
            }
        }

        return null;
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        return new FontResolverInfo("FileFlowFallbackFont");
    }
}
