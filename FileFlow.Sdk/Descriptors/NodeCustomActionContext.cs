namespace FileFlow.Sdk;

/// <summary>
/// Contexto de ejecución para acciones personalizadas de nodos (modales, diseñadores y asistentes).
/// Permite inyectar la ventana anfitriona y recibir notificaciones de finalización deterministas.
/// </summary>
/// <param name="ParentWindow">Ventana anfitriona (TopLevel / Window) para anclar diálogos modales.</param>
/// <param name="OnCompleted">Callback opcional invocado al cerrar el diálogo o finalizar la acción.</param>
public sealed record NodeCustomActionContext(object? ParentWindow = null, Action? OnCompleted = null);

