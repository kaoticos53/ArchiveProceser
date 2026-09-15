using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using FileFlow.Sdk.Services;

namespace FileFlow.App.Services;

/// <summary>
/// Adaptador de acceso al portapapeles del sistema operativo usando Avalonia.Input.Platform.IClipboard.
/// </summary>
public sealed class AvaloniaClipboardService : IClipboardService
{
    private static readonly Lazy<AvaloniaClipboardService> _instance = new(() => new AvaloniaClipboardService());
    public static AvaloniaClipboardService Instance => _instance.Value;

    private static IClipboard? GetClipboard()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow?.Clipboard;
        }
        return null;
    }

    public async Task SetTextAsync(string text)
    {
        var clipboard = GetClipboard();
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    public async Task<string?> GetTextAsync()
    {
        var clipboard = GetClipboard();
        return clipboard != null ? await clipboard.TryGetTextAsync() : null;
    }

    public async Task<bool> ContainsTextAsync()
    {
        var text = await GetTextAsync();
        return !string.IsNullOrEmpty(text);
    }
}
