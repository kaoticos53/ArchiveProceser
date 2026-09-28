using FileFlow.Sdk.Services;

namespace FileFlow.Sdk;

/// <summary>
/// Contexto de ejecución para acciones personalizadas de nodos (modales, diseñadores y asistentes).
/// Permite inyectar la ventana anfitriona y recibir notificaciones de finalización deterministas.
/// </summary>
/// <param name="ParentWindow">Ventana anfitriona (TopLevel / Window) para anclar diálogos modales.</param>
/// <param name="OnCompleted">Callback opcional invocado al cerrar el diálogo o finalizar la acción.</param>
/// <param name="Dialogs">
/// Los avisos de quien pide la acción (el host que la sirve), para que el nodo construya el contenido de su
/// superficie con los diálogos de VERDAD de ese host en vez de con el nulo declarado. El nodo no puede
/// resolverlos por sí mismo —vive en un ensamblado de plugin y no conoce la UI del host— así que se los pasa
/// quien abre: el mismo servicio con el que ese host avisa en el resto de sus superficies. Sin él, el
/// contenido cae al <see cref="NullDialogService"/> declarado (que no avisa), nunca a un no-op inventado.
/// </param>
public sealed record NodeCustomActionContext(object? ParentWindow = null, Action? OnCompleted = null, IDialogService? Dialogs = null);

