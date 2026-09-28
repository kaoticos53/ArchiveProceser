using System;
using System.Threading.Tasks;

namespace FileFlow.Sdk.Services;

/// <summary>
/// Claves canónicas de los diálogos que los ViewModels piden por nombre.
/// Cada host (Avalonia, Uno, …) mapea cada clave a su ventana concreta.
/// </summary>
public static class DialogKeys
{
    public const string UpdateDialog = "UpdateDialog";
    public const string WorkflowSettings = "WorkflowSettings";
    public const string VariablePicker = "VariablePicker";
    public const string TextEditor = "TextEditor";
    public const string VirtualFileSystemExplorer = "VirtualFileSystemExplorer";
    public const string About = "About";
    public const string WorkflowMetricsDashboard = "WorkflowMetricsDashboard";
    public const string ThemeCustomizer = "ThemeCustomizer";
    public const string AiModelUrlsConfig = "AiModelUrlsConfig";

    /// <summary>
    /// El Diseñador de Datasets Sintéticos: la superficie que declara el nodo de datos sintéticos del plugin
    /// de sistema de archivos (ver <see cref="FileFlow.Sdk.Descriptors.INodeDialogSurfaceProvider"/>). En el
    /// escritorio la monta el propio plugin con su toolkit; en un host sin el toolkit del escritorio se sirve
    /// con la vista de ESE host sobre el mismo view model portable que declara el nodo.
    /// </summary>
    public const string DataSetDesigner = "DataSetDesigner";

    /// <summary>
    /// El Gestor de Presets de Medios: la superficie que declara el nodo de transcodificación del plugin de
    /// integraciones (ver <see cref="FileFlow.Sdk.Descriptors.INodeDialogSurfaceProvider"/>). El escritorio la
    /// abre desde el botón «🎬» de la fila del parámetro «Preset»; un host sin el toolkit del escritorio la
    /// sirve con SU vista sobre el mismo view model portable que declara el nodo, que a su vez escribe en el
    /// mismo almacén de presets (el que lee el motor de transcodificación).
    /// </summary>
    public const string MediaPresetManager = "MediaPresetManager";
}

/// <summary>
/// Resultado genérico de un diálogo modal: el host empaqueta aquí lo que antes los ViewModels leían
/// de propiedades concretas de la ventana (<c>SelectedToken</c>, <c>ResultText</c>, un bool de confirmación…).
/// </summary>
public sealed record DialogResultPayload
{
    /// <summary>True si el usuario confirmó (el equivalente al bool de ShowDialog).</summary>
    public required bool Confirmed { get; init; }

    /// <summary>Valor libre del diálogo: el token elegido, el texto editado, etc.</summary>
    public string? Value { get; init; }

    /// <summary>Objeto de resultado tipado para diálogos que devuelven algo más rico.</summary>
    public object? Result { get; init; }
}

/// <summary>
/// Contrato para abrir ventanas y diálogos modales desde los ViewModels sin conocer el framework de UI.
/// El host (Avalonia, Uno, …) registra su implementación y resuelve la ventana anfitriona por sí mismo,
/// de modo que el ViewModel sólo declara <b>qué</b> diálogo quiere (<paramref name="dialogKey"/>) y
/// <b>qué contiene</b> (<paramref name="payload"/>). Las claves viven en <see cref="DialogKeys"/>,
/// iguales para todos los hosts.
/// </summary>
public interface IWindowService
{
    /// <summary>
    /// La ventana anfitriona del host en su representación nativa (un <c>Window</c> de Avalonia, uno de
    /// WinUI…), para las APIs que piden un propietario como <c>object?</c> —p. ej. las acciones
    /// personalizadas de los nodos—. Null cuando aún no hay ventana.
    /// </summary>
    object? MainWindowOwner { get; }

    /// <summary>Abre el diálogo <paramref name="dialogKey"/> de forma modal y devuelve su resultado.</summary>
    Task<DialogResultPayload?> ShowDialogAsync(string dialogKey, object? payload = null);

    /// <summary>Abre la ventana <paramref name="dialogKey"/> no modal (exploradores, dashboards).</summary>
    void ShowWindow(string dialogKey, object? payload = null);
}

/// <summary>
/// Implementación nula para pruebas y hosts sin ventanas: los diálogos se «cierran sin confirmar»
/// y las ventanas no se abren. Mantiene el mismo contrato sin construir UI.
/// </summary>
public sealed class NullWindowService : IWindowService
{
    public static NullWindowService Instance { get; } = new();

    public object? MainWindowOwner => null;

    public Task<DialogResultPayload?> ShowDialogAsync(string dialogKey, object? payload = null)
        => Task.FromResult<DialogResultPayload?>(new DialogResultPayload { Confirmed = false });

    public void ShowWindow(string dialogKey, object? payload = null)
    {
    }
}
