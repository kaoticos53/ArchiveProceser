using System;

namespace FileFlow.Sdk.Services;

/// <summary>
/// La FRONTERA de las superficies que un host NO puede montar.
///
/// <para><b>Para qué existe.</b> Un nodo de un plugin puede traer una superficie modal propia (una ventana,
/// un selector de archivos). Cuando el host en ejecución no tiene la pieza nativa con la que montarla, el nodo
/// no puede construirla a ciegas: <b>declara</b> que esa superficie no está disponible, con su nombre y su
/// motivo, por los diálogos de quien la abrió. Sin esta costura, el botón de un nodo no hacía NADA y tampoco
/// avisaba.</para>
///
/// <para><b>Qué NO es.</b> No es el camino de las superficies declaradas
/// (<see cref="FileFlow.Sdk.Descriptors.INodeDialogSurfaceProvider"/>): cuando un nodo declara su superficie,
/// cada host la sirve con SU vista sobre el view model portable del nodo y ninguna frontera se alcanza. Esto
/// es sólo para lo que no se puede declarar.</para>
///
/// <para><b>Por qué también escribe en la consola.</b> Un aviso que no se puede leer no se puede diagnosticar:
/// quien mide (una sonda, un registro de soporte) necesita la traza aunque el host no tenga ventana todavía.</para>
/// </summary>
public static class UnavailableSurface
{
    /// <summary>
    /// Declara que <paramref name="surfaceName"/> no se puede montar en este host: lo dice por los diálogos
    /// de quien lo abrió (<paramref name="dialogs"/>, que el nodo recibe en su contexto) y lo deja escrito en
    /// el canal de errores. Sin diálogos (una prueba, un host sin ventana) queda al menos la traza.
    /// </summary>
    /// <param name="dialogs">El servicio de diálogos del host que abrió la acción; puede ser nulo.</param>
    /// <param name="surfaceName">El nombre de la superficie en el idioma activo: lo lee el plugin de su diccionario.</param>
    /// <param name="title">El rótulo del aviso.</param>
    /// <param name="message">El texto del aviso, con el nombre de la superficie si lo necesita.</param>
    public static void Declare(IDialogService? dialogs, string surfaceName, string title, string message)
    {
        Console.Error.WriteLine($"[UnavailableSurface] «{surfaceName}» no se puede montar en este host: {message}");
        (dialogs ?? NullDialogService.Instance).ShowWarning(message, title);
    }
}
