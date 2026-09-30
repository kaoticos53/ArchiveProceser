using FileFlow.Sdk.Services;

namespace FileFlow.App.Services;

/// <summary>
/// Ancla mutable de los servicios de ventana del host. Los ViewModels construidos sin contenedor DI
/// (los que hoy se hacen con <c>new</c> dentro de otros VMs) toman estas instancias por defecto;
/// el arranque del host las fija con sus implementaciones y las pruebas pueden poner dobles. Es la única
/// pieza de estado global nueva, y sustituye a los singletons de diálogos que ya existían con el mismo patrón.
/// </summary>
public static class ServiceHolders
{
    public static IWindowService WindowService { get; set; } = NullWindowService.Instance;
    public static IFileDialogService FileDialog { get; set; } = NullFileDialogService.Instance;
    public static IPopupMenuService PopupMenu { get; set; } = NullPopupMenuService.Instance;
}

/// <summary>Atajo de lectura de la ventana del host; la instalación la hace el arranque.</summary>
public static class WindowServiceHolder
{
    public static IWindowService Instance => ServiceHolders.WindowService;
}

/// <summary>Atajo de lectura del menú contextual del host; la instalación la hace el arranque.</summary>
public static class PopupMenuServiceHolder
{
    public static IPopupMenuService Instance => ServiceHolders.PopupMenu;
}

public static class FileDialogServiceHolder
{
    public static IFileDialogService Instance => ServiceHolders.FileDialog;
}

/// <summary>Servicio de ficheros nulo (sin ventana no hay fichero): mismas respuestas que el host da hoy sin ventana.</summary>
public sealed class NullFileDialogService : IFileDialogService
{
    public static NullFileDialogService Instance { get; } = new();
    public string? ShowOpenFileDialog(string title, string filter, string defaultExt = "") => null;
    public string? ShowSaveFileDialog(string title, string filter, string defaultExt = "", string defaultFileName = "") => null;
    public string? ShowFolderBrowserDialog(string title) => null;

    // La variante asíncrona hereda el DIM del contrato (delegación en la síncrona): el nulo no
    // necesita override — las Async devuelven null sin bloquear, como sus hermanas.
}

/// <summary>Servicio de menús nulo: sin ancla no hay menú, igual que el comportamiento actual.</summary>
public sealed class NullPopupMenuService : IPopupMenuService
{
    public static NullPopupMenuService Instance { get; } = new();
    public void ShowMenu(PopupMenuDescriptor descriptor, object? anchor = null) { }
}
