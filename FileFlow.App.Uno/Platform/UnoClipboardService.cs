using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using FileFlow.Sdk.Services;

namespace FileFlow.App.Uno.Platform;

/// <summary>
/// Adaptador de portapapeles implementado sobre Windows.ApplicationModel.DataTransfer (WinRT),
/// que el host Uno en Windows expone igual que WinAppSDK. Contrato: <see cref="IClipboardService"/> de Sdk.
/// </summary>
public sealed class UnoClipboardService : IClipboardService
{
    public Task SetTextAsync(string text)
    {
        var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        dp.SetText(text);
        Clipboard.SetContent(dp);
        return Task.CompletedTask;
    }

    public async Task<string?> GetTextAsync()
    {
        var content = Clipboard.GetContent();
        if (!content.Contains(StandardDataFormats.Text))
        {
            return null;
        }
        return await content.GetTextAsync();
    }

    public Task<bool> ContainsTextAsync()
        => Task.FromResult(Clipboard.GetContent().Contains(StandardDataFormats.Text));
}
