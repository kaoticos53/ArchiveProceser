using System;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace FileFlow.App.Uno;

/// <summary>
/// El sondeo en runtime del host Uno (<c>FileFlow.App.Uno.exe --selfcheck</c>): arranca la aplicación real
/// (DI completa, plugins descubiertos, ejemplo cargado) y recorre el árbol visual de la tarjeta para
/// confirmar que los bindings, recursos y conversores del host se resuelven —la prueba que la compilación no
/// puede dar—. Imprime el inventario en consola y termina con código 0 (verificado) o 1 (alguna expectativa
/// vacía), sin interacción.
///
/// <para>Lo que confirma y lo que no: el sondeo valida que cada pieza enlazada del árbol tiene valor real
/// (título, categoría, color de acento, icono con geometría, sockets con borde, telemetría con texto) y que
/// los cables del lienzo están dibujados; no valida el píxel (la comparación visual con la versión anterior queda
/// sin demostrar en este entorno) ni la interacción con puntero real (fases 3.2/3.3).</para>
///
/// <para><b>Este archivo es el DESPACHADOR, y nada más</b>: aquí vive el bucle de reintentos —la
/// materialización de las plantillas ocurre en el pase de layout, después del Activate— y el veredicto por
/// código de salida. Los modos viven en su propio archivo y éste no los contiene: el LIENZO en
/// <c>SelfCheckCanvas.cs</c> (con lo que el puntero no puede recorrer en <c>SelfCheckPointerless.cs</c>), los
/// ajustes en <c>SelfCheckSettings.cs</c>, la barra de control y su cajón en <c>SelfCheckControlBar.cs</c> y
/// los paneles de nodo en <c>SelfCheckDialogs.cs</c>. Lo que todos comparten —recorrer el árbol visual y
/// describir un fallo— es el cinturón de <c>SelfCheckTree.cs</c>; y cada preocupación con entidad propia
/// recibe el comprobador de quien la llama: el marco del host en <c>SelfCheckFrame.cs</c>, los dos paneles
/// (catálogo y ficha) en <c>SelfCheckPanels.cs</c> y la sonda UIA externa en <c>SelfCheckUia.cs</c>.</para>
///
/// <para><b>Las capas, una por afirmación</b>: la SONDA mide el comportamiento en runtime y afirma
/// (<c>[OK]</c>/<c>[FALLO]</c>); la GUARDIA de fuente fija la regla en el árbol de pruebas (y es el testigo de
/// su mutación); la MUTACIÓN demuestra que la guardia muerde. Ninguna afirmación debería repetirse dentro de
/// la misma capa: si dos casos de xunit dicen lo mismo, uno sobra.</para>
/// </summary>
public static class RuntimeSelfCheck
{
    /// <summary>
    /// Corre el sondeo en un hilo de fondo (nunca bloquea el hilo de UI): reintenta en el dispatcher
    /// hasta ver las tarjetas materializadas o agotar la ventana de espera, y termina el proceso con el
    /// veredicto. Devuelve -1 (el proceso termina dentro del sondeo).
    /// </summary>
    public static int Run(Window window, DispatcherQueue dispatcher)
    {
        new Thread(() =>
        {
            var lastReport = new StringBuilder("[sin intento completado]");
            var ok = false;

            // La materialización de las plantillas ocurre en el pase de layout, después del Activate.
            for (int attempt = 0; attempt < 30 && !ok && !SelfCheckCanvas.PerformanceProbeRan; attempt++)
            {
                Thread.Sleep(attempt == 0 ? 300 : 200);

                // Cada intento parte de un bloque limpio: selfcheck-report.txt cuenta SIEMPRE lo que el
                // último intento vio, no la historia de los intentos de espera («lienzo sin tamaño»,
                // «grafo sin cargar»), que era ruido de diagnóstico. La consola recibe el bloque final.
                lastReport.Clear();

                var completed = new ManualResetEventSlim(false);
                dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        ok = SelfCheckCanvas.Run(window, lastReport);
                    }
                    catch (Exception ex)
                    {
                        lastReport.AppendLine("EXCEPCIÓN en el sondeo: " + ex.GetType().Name + ": " + ex.Message
                            + Environment.NewLine + ex.StackTrace);
                    }
                    finally
                    {
                        completed.Set();
                    }
                });

                completed.Wait(TimeSpan.FromSeconds(10));

                // Escritura POR intento: si el proceso muere a mitad del sondeo, el fichero cuenta el
                // último intento completo y no queda a medias con una mezcla de épocas.
                try
                {
                    File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "selfcheck-report.txt"), lastReport.ToString());
                }
                catch { }
            }

            // Exit desde un hilo de fondo no purga buffers ni ejecuta finalizers: imprimir el bloque final
            // y forzar el flush antes de salir.
            Console.Out.Flush();
            Console.WriteLine(lastReport.ToString());
            Console.Out.Flush();
            Environment.Exit(ok ? 0 : 1);
        })
        {
            IsBackground = true,
            Name = "RuntimeSelfCheck"
        }.Start();

        return -1; // el proceso termina por Environment.Exit dentro del sondeo
    }
}
