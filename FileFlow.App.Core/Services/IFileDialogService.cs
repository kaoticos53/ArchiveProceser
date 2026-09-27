namespace FileFlow.App.Services;

/// <summary>
/// Contrato para el servicio de diálogos de archivos de sistema operativo.
/// </summary>
public interface IFileDialogService
{
    string? ShowOpenFileDialog(string title, string filter, string defaultExt = "");
    string? ShowSaveFileDialog(string title, string filter, string defaultExt = "", string defaultFileName = "");
    string? ShowFolderBrowserDialog(string title);

    /// <summary>
    /// La variante asíncrona (hito 240): la vía de los hosts modernos — el picker se abre en el
    /// hilo de UI SIN bloquear el hilo llamador, así que se puede llamar desde un click de UI
    /// (el «Probar» del inspector) sin interbloqueo y sin el aborto declarado del síncrono.
    /// La implementación por defecto delega en la síncrona: los hosts que aún no la traen y los
    /// dobles de prueba siguen funcionando sin cambios.
    /// </summary>
    async Task<string?> ShowOpenFileDialogAsync(string title, string filter, string defaultExt = "")
    {
        return await Task.Run(() => ShowOpenFileDialog(title, filter, defaultExt)).ConfigureAwait(false);
    }

    async Task<string?> ShowSaveFileDialogAsync(string title, string filter, string defaultExt = "", string defaultFileName = "")
    {
        return await Task.Run(() => ShowSaveFileDialog(title, filter, defaultExt, defaultFileName)).ConfigureAwait(false);
    }

    async Task<string?> ShowFolderBrowserDialogAsync(string title)
    {
        return await Task.Run(() => ShowFolderBrowserDialog(title)).ConfigureAwait(false);
    }
}
