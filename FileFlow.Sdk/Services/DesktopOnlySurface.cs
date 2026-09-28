using System;

namespace FileFlow.Sdk.Services;

/// <summary>
/// La FRONTERA de las ventanas del toolkit de escritorio (hito 268).
///
/// <para><b>Para qué existe.</b> Un nodo de un plugin puede traer su propia ventana modal: la monta el host
/// que TIENE el toolkit con el que el plugin la escribió (el host de escritorio, con Avalonia) y no puede
/// montarla un host que no lo tiene (el host Uno, con WinUI). Cuando el nodo construía la ventana a ciegas,
/// un host sin ese toolkit recibía una ventana de otro framework —o, peor, el botón no hacía NADA y tampoco
/// avisaba—. Esta es la costura que cierra ese hueco: el nodo compilado para un host sin el toolkit
/// <b>declara</b> que esa superficie pertenece al escritorio, con el nombre de la ventana y su motivo, por
/// los diálogos del host que lo abrió.</para>
///
/// <para><b>Qué NO es.</b> No es el camino de las superficies declaradas
/// (<see cref="FileFlow.Sdk.Descriptors.INodeDialogSurfaceProvider"/>): cuando un nodo declara su superficie,
/// cada host la sirve con SU vista sobre el view model portable del nodo y ninguna frontera se alcanza. Esto
/// es sólo para lo que no se puede declarar —la ventana del plugin y sus selectores de archivo—.</para>
///
/// <para><b>Por qué también escribe en la consola.</b> Un aviso que no se puede leer no se puede diagnosticar:
/// quien mide (una sonda, un registro de soporte) necesita la traza aunque el host no tenga ventana todavía.
/// Es el mismo criterio con el que el catálogo de diálogos del host Uno anota lo que declina.</para>
/// </summary>
public static class DesktopOnlySurface
{
    /// <summary>
    /// Declara que <paramref name="surfaceName"/> pertenece al host de escritorio: lo dice por los diálogos
    /// de quien abrió (<paramref name="dialogs"/>, que el nodo recibe en su contexto) y lo deja escrito en el
    /// canal de errores. Sin diálogos (una prueba, un host sin ventana) queda al menos la traza.
    /// </summary>
    /// <param name="surfaceName">El nombre de la superficie en el idioma activo: lo lee el plugin de su diccionario.</param>
    /// <param name="title">El rótulo del aviso.</param>
    /// <param name="message">El texto del aviso, con el nombre de la superficie si lo necesita.</param>
    public static void Declare(IDialogService? dialogs, string surfaceName, string title, string message)
    {
        Console.Error.WriteLine($"[DesktopOnlySurface] «{surfaceName}» no se puede montar en este host: {message}");
        (dialogs ?? NullDialogService.Instance).ShowWarning(message, title);
    }
}
