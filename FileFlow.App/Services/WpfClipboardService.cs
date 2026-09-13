using System.Windows;
using FileFlow.Sdk.Services;

namespace FileFlow.App.Services;

/// <summary>
/// Adaptador de <see cref="IClipboardService"/> para el portapapeles de WPF con reintentos para mitigar bloqueos de Windows.
/// </summary>
public sealed class WpfClipboardService : IClipboardService
{
    private static readonly Lazy<WpfClipboardService> _instance = new(() => new WpfClipboardService());
    public static WpfClipboardService Instance => _instance.Value;

    public async Task SetTextAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        for (int i = 0; i < 5; i++)
        {
            try
            {
                Clipboard.SetText(text);
                return;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                await Task.Delay(50);
            }
            catch (Exception) when (i < 4)
            {
                await Task.Delay(50);
            }
        }
    }

    public async Task<string?> GetTextAsync()
    {
        for (int i = 0; i < 5; i++)
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    return Clipboard.GetText();
                }
                return null;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                await Task.Delay(50);
            }
            catch (Exception) when (i < 4)
            {
                await Task.Delay(50);
            }
        }
        return null;
    }

    public async Task<bool> ContainsTextAsync()
    {
        for (int i = 0; i < 5; i++)
        {
            try
            {
                return Clipboard.ContainsText();
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                await Task.Delay(50);
            }
            catch (Exception) when (i < 4)
            {
                await Task.Delay(50);
            }
        }
        return false;
    }
}
