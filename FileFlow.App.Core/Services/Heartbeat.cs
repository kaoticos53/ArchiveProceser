using System;
using System.Diagnostics;
using FileFlow.Sdk.Services;

namespace FileFlow.App.Services;

/// <summary>
/// Entrega el trabajo de un <b>latido</b>: despacha el paso al hilo de la interfaz y, pase lo que pase, no deja
/// escapar la excepción.
///
/// <para><b>Por qué existe</b>: desde el hito 176 el tick lo entrega el reloj inyectable en un hilo del grupo de
/// hilos, no un <c>DispatcherTimer</c> ya sobre el hilo de la interfaz. Ahí una excepción sin capturar no la
/// recoge nadie —no hay bucle que la registre ni tarea a la que culpar— y <b>tumba el proceso</b>: medido, un
/// despacho contra un despachador ya desmontado (una ventana de apagado, la sesión de pruebas cerrando) dejaba
/// el host de pruebas muerto con un <c>NullReferenceException</c> dentro de Avalonia y la serie anulada. El
/// <c>DispatcherTimer</c> anterior lo tapaba por construcción: sólo late cuando el bucle lo atiende.</para>
///
/// <para>Un latido no es una tarea de la que dependa nada: si su entrega falla, lo correcto es dejar constancia y
/// seguir vivo —la aplicación debe seguir funcionando aunque un fotograma, un vaciado de consola o un muestreo
/// no llegue—. Los <b>efectos</b> siguen siendo responsabilidad del paso, que ya se protege por dentro cuando
/// tiene algo que proteger (ver <c>SampleNowAsync</c>).</para>
///
/// <para><b>Quién lo usa</b>: sólo el registro de latidos (<see cref="HeartbeatService"/>). Desde el hito 178 un
/// latido es una declaración en el servicio, así que ningún componente entrega sus propios ticks: si aparece una
/// llamada a esta entrega fuera del servicio, la fontanería ha vuelto a copiarse y el lint de latidos lo dice.</para>
/// </summary>
public static class Heartbeat
{
    /// <summary>
    /// Publica el paso de un latido con nombre. El nombre lo pone el registro
    /// (<see cref="HeartbeatService"/>), de modo que el aviso diga <b>qué</b> latido no llegó: el nombre del
    /// método del paso no sirve cuando el paso es una lambda, y el de una lambda no dice nada.
    /// </summary>
    public static void Post(IUiDispatcher dispatcher, string name, Action step)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(step);

        try
        {
            dispatcher.Post(step);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            Debug.WriteLine($"[Heartbeat] No se pudo entregar el latido '{name}': {ex.Message}");
        }
    }
}
