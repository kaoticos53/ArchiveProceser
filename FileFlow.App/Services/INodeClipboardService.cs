using Avalonia;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Services;

/// <summary>
/// Contrato del servicio de portapapeles para operaciones de copia, corte, pegado y duplicación de nodos con preservación de parámetros.
/// </summary>
public interface INodeClipboardService
{
    /// <summary>
    /// Copia al portapapeles los nodos seleccionados y las conexiones internas entre ellos.
    /// </summary>
    void Copy(IEnumerable<NodeViewModel> nodes, IEnumerable<ConnectionViewModel> connections);

    /// <summary>
    /// Determina si hay datos válidos de nodos en el portapapeles listos para ser pegados.
    /// </summary>
    bool CanPaste();

    /// <summary>
    /// Pega los nodos del portapapeles en el editor, creando nuevas instancias con nuevos IDs, restaurando
    /// parámetros y reconectando aristas internas.
    ///
    /// Devuelve además lo que <b>no</b> pudo reconectar: uno de estos nodos puede faltar en esta máquina
    /// —su plugin no está instalado— y entonces los cables que lo nombraban se pierden. Quien pega recibe la
    /// cuenta sin pedirla, porque descartarlos en silencio es justo lo que convierte un pegado en un grafo
    /// incompleto que parece completo.
    /// </summary>
    ClipboardPasteResult Paste(EditorViewModel editor, Point? targetPosition = null);

    /// <summary>
    /// Duplica inmediatamente los nodos seleccionados aplicando un desplazamiento y restaurando todos sus
    /// parámetros y conexiones, con el mismo informe que <see cref="Paste"/>.
    /// </summary>
    ClipboardPasteResult Duplicate(IEnumerable<NodeViewModel> nodes, IEnumerable<ConnectionViewModel> connections, EditorViewModel editor);
}
