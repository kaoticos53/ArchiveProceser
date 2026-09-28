using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
#if !FILEFLOW_NO_DESKTOP_TOOLKIT
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
#endif
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
/// El SELECTOR DE ARCHIVOS del plugin (hito 268): la costura por la que un view model PORTABLE —el del
/// Diseñador de Datasets, que pintan los DOS hosts— pide un fichero sin conocer el toolkit.
///
/// <para><b>Para qué existe.</b> Ese view model lo compila también el host Uno, que no trae el toolkit del
/// escritorio: mientras el view model llamaba a la API de almacenamiento de Avalonia, el ensamblado del
/// plugin arrastraba Avalonia entera —y el importar/exportar del host Uno era un no-op silencioso—. Aquí vive
/// la única parte que sabe de toolkits, con una implementación por sabor: la misma división que el resto del
/// producto hace por ficheros, y no con condicionales repartidos por la lógica del diseñador.</para>
/// </summary>
internal static class DesktopFilePicker
{
#if FILEFLOW_NO_DESKTOP_TOOLKIT

    /// <summary>
    /// Sin el toolkit del escritorio no hay selector: se DECLARA la frontera por los diálogos de quien abrió
    /// —el aviso llega al usuario y queda en la traza— y se devuelve <c>null</c>, que el diseñador ya entiende
    /// como «no hay nada que importar ni exportar».
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
        DesktopOnlySurface.Declare(
            dialogs,
            LocalizationManager.Instance.GetString("Plugin_FilePicker_Name", "Selector de archivos"),
            LocalizationManager.Instance.GetString("Plugin_DesktopOnly_Title", "Ventana del host de escritorio"),
            LocalizationManager.Instance.GetFormattedString(
                "Plugin_DesktopOnly_Message",
                "«{0}» se abre en el host de escritorio: este host no tiene el toolkit que lo monta. Ábrelo desde la aplicación de escritorio.",
                LocalizationManager.Instance.GetString("Plugin_FilePicker_Name", "Selector de archivos")));

#else

    /// <summary>
    /// Abre el selector del sistema para LEER un fichero. Devuelve <c>null</c> tanto si el usuario cancela
    /// como si no hay ventana donde abrirlo: en los dos casos no hay nada que hacer con el fichero.
    /// </summary>
    public static async Task<PickedFile?> PickOpenAsync(string title, IReadOnlyList<FileTypeFilter> filters, IDialogService? dialogs)
    {
        _ = dialogs; // en el escritorio el selector existe: la frontera no se alcanza

        if (TopLevelOf() is not { } topLevel)
        {
            return null;
        }

        IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [.. filters.Select(Type)],
        });

        if (files.Count == 0)
        {
            return null;
        }

        IStorageFile file = files[0];
        return new PickedFile(await file.OpenReadAsync(), file.Path.LocalPath);
    }

    /// <summary>Abre el selector del sistema para ESCRIBIR un fichero, con el nombre sugerido de quien llama.</summary>
    public static async Task<PickedFile?> PickSaveAsync(
        string title,
        IReadOnlyList<FileTypeFilter> filters,
        string suggestedFileName,
        string defaultExtension,
        IDialogService? dialogs)
    {
        _ = dialogs;

        if (TopLevelOf() is not { } topLevel)
        {
            return null;
        }

        IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            DefaultExtension = defaultExtension,
            FileTypeChoices = [.. filters.Select(Type)],
        });

        return file == null ? null : new PickedFile(await file.OpenWriteAsync(), file.Path.LocalPath);
    }

    /// <summary>La ventana del escritorio sobre la que abrir el selector: la principal, si está montada.</summary>
    private static TopLevel? TopLevelOf() =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow != null
                ? TopLevel.GetTopLevel(desktop.MainWindow)
                : null;

    private static FilePickerFileType Type(FileTypeFilter filter) =>
        new(filter.Name) { Patterns = filter.Patterns };

#endif
}
