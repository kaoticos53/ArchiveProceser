using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FileFlow.App.Services;
using FileFlow.Sdk.Services;

namespace FileFlow.App.Core;

/// <summary>
/// Ancla mutable a los bordes de interfaz que sólo el host puede dar, para el núcleo portable:
/// portapapeles del sistema, exportación de registros, vista previa de archivos, generación de
/// tokens de tema y selector de color. Los ViewModels y servicios portables llaman a esta puerta
/// en lugar de construir ventanas ni tocar APIs del framework; el host Avalonia la instala en el
/// arranque (<c>HostUi.Install</c>) con sus implementaciones, y sin host todo queda en un no-op
/// seguro (mismo espíritu que <c>ServiceHolders</c>, al que acompaña).
/// </summary>
public static class HostUi
{
    private static IUiDispatcher s_dispatcher = NullUiDispatcher.Instance;
    private static IClipboardService s_clipboard = NullClipboardService.Instance;
    private static IColorPickerService s_colorPicker = NullColorPickerService.Instance;

    /// <summary>Despachado al hilo de la interfaz. Sin host, ejecuta en línea.</summary>
    public static IUiDispatcher Dispatcher => s_dispatcher;

    /// <summary>Portapapeles del sistema. Sin host, se descarta en silencio.</summary>
    public static IClipboardService Clipboard => s_clipboard;

    /// <summary>Selector de color del host. Sin host, no hay selección (el llamador ya lo trata).</summary>
    public static IColorPickerService ColorPicker => s_colorPicker;

    /// <summary>
    /// Ventana anfitriona del host en su representación nativa (un <c>Window</c> de Avalonia, uno de
    /// WinUI…), para las APIs de acciones personalizadas de nodos que piden un propietario como
    /// <c>object?</c>. Null cuando aún no hay ventana o no hay host.
    /// </summary>
    public static object? MainWindowOwner => s_mainWindowOwner;

    private static object? s_mainWindowOwner;

    /// <summary>Escribe texto en el portapapeles del sistema, tolerante a fallos.</summary>
    public static void SetClipboardText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        try
        {
            _ = s_clipboard.SetTextAsync(text);
        }
        catch
        {
            // Sin portapapeles (host headless) no hay nada que hacer: es un servicio de cortesía.
        }
    }

    /// <summary>Abre la vista previa de archivo del host. Sin host, no hay ventana que abrir.</summary>
    public static void ShowFilePreview(FilePreviewRequest request)
    {
        FilePreviewRequested?.Invoke(request);
    }

    /// <summary>El host instala aquí su ventana de vista previa de archivos.</summary>
    public static event Action<FilePreviewRequest>? FilePreviewRequested;

    /// <summary>
    /// Guarda los registros del almacén en un fichero elegido por el usuario y devuelve la ruta,
    /// o null si el usuario canceló (o no hay host con diálogos).
    /// </summary>
    public static Task<string?> ExportLogsAsync()
        => s_exportLogs?.Invoke() ?? Task.FromResult<string?>(null);

    private static Func<Task<string?>>? s_exportLogs;

    /// <summary>El host instala aquí su exportación de registros (diálogo nativo incluido).</summary>
    public static void SetLogExporter(Func<Task<string?>> exporter) => s_exportLogs = exporter;

    /// <summary>
    /// Genera los recursos visuales del tema en un diccionario genérico clave → valor (portable).
    /// Delega en el <see cref="ThemeHostBridge"/>, que es el único canal de tokens: el host lo
    /// instala una vez y tanto el editor de temas como cualquier otra pieza del núcleo lo consultan.
    /// Sin host, la previsualización en vivo del editor de temas simplemente no recibe tokens.
    /// </summary>
    public static IReadOnlyDictionary<string, object?> BuildThemeResources(object themeDefinition)
        => ThemeHostBridge.BuildResources?.Invoke(themeDefinition) ?? new Dictionary<string, object?>();

    /// <summary>
    /// Instala los bordes de una sola pieza que el host da al núcleo. Llamarlo en el arranque,
    /// antes de que ViewModels o ventanas hagan uso de ellos.
    /// </summary>
    public static void Install(
        IUiDispatcher? dispatcher = null,
        IClipboardService? clipboard = null,
        IColorPickerService? colorPicker = null,
        object? mainWindowOwner = null)
    {
        s_dispatcher = dispatcher ?? NullUiDispatcher.Instance;
        s_clipboard = clipboard ?? NullClipboardService.Instance;
        s_colorPicker = colorPicker ?? NullColorPickerService.Instance;
        s_mainWindowOwner = mainWindowOwner;
    }
}

/// <summary>
/// Solicitud portable de vista previa: contexto de archivo (el modelo del previsualizador) y
/// hermanos opcionales para navegar. El host construye su ventana con esto.
/// </summary>
public sealed record FilePreviewRequest(
    object Context,
    IReadOnlyList<object>? Siblings = null,
    object? Owner = null);
