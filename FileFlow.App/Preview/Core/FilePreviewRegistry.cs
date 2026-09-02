using FileFlow.App.Preview.Providers;

namespace FileFlow.App.Preview.Core;

/// <summary>
/// Registro centralizado de proveedores de previsualización de archivos.
/// </summary>
public class FilePreviewRegistry
{
    private static readonly Lazy<FilePreviewRegistry> _instance = new(() => new FilePreviewRegistry());
    public static FilePreviewRegistry Instance => _instance.Value;

    private readonly List<IFilePreviewProvider> _providers = [];
    private readonly Lock _lock = new();

    public FilePreviewRegistry()
    {
        // Registrar proveedores por defecto
        RegisterProvider(new ImagePreviewProvider());
        RegisterProvider(new TextCodePreviewProvider());
        RegisterProvider(new SpreadsheetPreviewProvider());
        RegisterProvider(new AudioPreviewProvider());
        RegisterProvider(new ArchiveTreePreviewProvider());
        RegisterProvider(new FallbackPreviewProvider());
    }

    public void RegisterProvider(IFilePreviewProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        lock (_lock)
        {
            _providers.RemoveAll(p => p.GetType() == provider.GetType());
            _providers.Add(provider);
            _providers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
    }

    public IFilePreviewProvider? GetProvider(FilePreviewContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        lock (_lock)
        {
            return _providers.FirstOrDefault(p => p.CanHandle(context));
        }
    }

    public IReadOnlyList<IFilePreviewProvider> AllProviders
    {
        get
        {
            lock (_lock)
            {
                return _providers.ToList();
            }
        }
    }
}
