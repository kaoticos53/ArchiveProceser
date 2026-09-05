namespace FileFlow.App.Services;

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
/// Permite probar unitariamente los ViewModels sin depender de subsistemas de ventanas de WPF.
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
}
