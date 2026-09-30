using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Services;

namespace FileFlow.Plugin.FileSystem.UI.Services;

/// <summary>Una entrada del desplegable de tipos del selector: el rótulo que lee el usuario y sus patrones.</summary>
/// <param name="Name">El rótulo («Archivos JSON (*.json)», «Todos los archivos (*.*)»).</param>
/// <param name="Patterns">Los patrones que acepta esa entrada.</param>
internal sealed record FileTypeFilter(string Name, string[] Patterns);

/// <summary>
/// Un fichero elegido por el usuario, con el canal ya abierto y la ruta de la que salió (la ruta se lee para
/// el mensaje de estado: «exportado a …»).
/// </summary>
internal sealed record PickedFile(Stream Stream, string Path);

/// <summary>
/// El SELECTOR DE ARCHIVOS del plugin: la costura por la que un view model PORTABLE —el del
/// Diseñador de Datasets, que pinta el host— pide un fichero sin conocer el toolkit de UI.
/// </summary>
internal static class PortableFilePicker
{
    /// <summary>
    /// Sin selector nativo disponible, se declara la superficie por los diálogos de quien abrió
    /// y se devuelve <c>null</c>, que el diseñador entiende como «no hay nada que importar ni exportar».
    /// </summary>
    public static Task<PickedFile?> PickOpenAsync(string title, IReadOnlyList<FileTypeFilter> filters, IDialogService? dialogs)
    {
        Declare(dialogs);
        return Task.FromResult<PickedFile?>(null);
    }

    /// <inheritdoc cref="PickOpenAsync" />
    public static Task<PickedFile?> PickSaveAsync(
        string title,
        IReadOnlyList<FileTypeFilter> filters,
        string suggestedFileName,
        string defaultExtension,
        IDialogService? dialogs)
    {
        Declare(dialogs);
        return Task.FromResult<PickedFile?>(null);
    }

    private static void Declare(IDialogService? dialogs) =>
        UnavailableSurface.Declare(
            dialogs,
            LocalizationManager.Instance.GetString("Plugin_FilePicker_Name", "Selector de archivos"),
            LocalizationManager.Instance.GetString("Plugin_SurfaceUnavailable_Title", "Función no disponible"),
            LocalizationManager.Instance.GetFormattedString(
                "Plugin_SurfaceUnavailable_Message",
                "«{0}» no está disponible en este host.",
                LocalizationManager.Instance.GetString("Plugin_FilePicker_Name", "Selector de archivos")));
}

