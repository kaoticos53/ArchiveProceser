using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace FileFlow.App.Preview.Core;

/// <summary>
/// Contrato base para proveedores desacoplados de vista previa de archivos.
/// </summary>
public interface IFilePreviewProvider
{
    string ProviderName { get; }
    int Priority { get; }

    bool CanHandle(FilePreviewContext context);

    Task<Control> CreateVisualElementAsync(FilePreviewContext context, CancellationToken cancellationToken);
}
