namespace FileFlow.Sdk.Services;

/// <summary>
/// Implementación neutra (Null Object) de <see cref="IClipboardService"/> en memoria para pruebas unitarias y CLI.
/// </summary>
public sealed class NullClipboardService : IClipboardService
{
    private static readonly Lazy<NullClipboardService> _instance = new(() => new NullClipboardService());
    public static NullClipboardService Instance => _instance.Value;

    private string? _currentText;
    private readonly System.Threading.Lock _lock = new();

    public Task SetTextAsync(string text)
    {
        lock (_lock)
        {
            _currentText = text;
        }
        return Task.CompletedTask;
    }

    public Task<string?> GetTextAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_currentText);
        }
    }

    public Task<bool> ContainsTextAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(!string.IsNullOrEmpty(_currentText));
        }
    }
}
