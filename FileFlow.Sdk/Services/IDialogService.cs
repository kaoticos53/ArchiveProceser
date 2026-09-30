namespace FileFlow.Sdk.Services;

/// <summary>
/// Resultado de interacción con cuadros de diálogo modales.
/// </summary>
public enum DialogResult
{
    None = 0,
    Ok = 1,
    Cancel = 2,
    Yes = 6,
    No = 7
}

/// <summary>
/// Contrato de puerto para la presentación desacoplada de diálogos informativos, alertas y confirmaciones.
/// Permite desacoplar plugins y ViewModels de subsistemas de ventanas de la interfaz gráfica.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Muestra un mensaje informativo.
    /// </summary>
    void ShowInformation(string message, string title = "FileFlow Studio");

    /// <summary>
    /// Muestra una advertencia.
    /// </summary>
    void ShowWarning(string message, string title = "FileFlow Studio");

    /// <summary>
    /// Muestra un mensaje de error.
    /// </summary>
    void ShowError(string message, string title = "Error");

    /// <summary>
    /// Muestra un cuadro de confirmación Sí/No.
    /// </summary>
    /// <returns><c>true</c> si el usuario confirmó Sí; de lo contrario, <c>false</c>.</returns>
    bool ShowConfirmation(string message, string title = "FileFlow Studio");

    /// <summary>
    /// Muestra un cuadro de diálogo con opciones Sí/No/Cancelar.
    /// </summary>
    DialogResult ShowYesNoCancel(string message, string title = "FileFlow Studio");

    /// <summary>
    /// La CONFIRMACIÓN ASÍNCRONA (hito 263): la vía de los hosts cuyo diálogo modal sólo existe en
    /// asíncrono —un host WinUI no tiene API síncrona y su hilo de UI no puede quedarse esperando—.
    /// Quien la usa se queda con la RESPUESTA REAL del usuario sin bloquear a nadie.
    ///
    /// <para>La implementación por defecto delega en <see cref="ShowConfirmation"/>, en un hilo de fondo:
    /// los hosts que ya saben confirmar en síncrono (la versión anterior, con su bomba anidada de mensajes) y los
    /// dobles de prueba siguen funcionando sin cambios, y un host moderno sólo tiene que reescribir ésta.</para>
    /// </summary>
    async Task<bool> ConfirmAsync(string message, string title = "FileFlow Studio")
    {
        return await Task.Run(() => ShowConfirmation(message, title)).ConfigureAwait(false);
    }
}
