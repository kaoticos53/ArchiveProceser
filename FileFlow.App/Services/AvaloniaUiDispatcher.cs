using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using FileFlow.Sdk.Services;

namespace FileFlow.App.Services;

/// <summary>
/// Adaptador de despacho de UI basado en Avalonia.Threading.Dispatcher.
/// </summary>
public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    private static readonly Lazy<AvaloniaUiDispatcher> _instance = new(() => new AvaloniaUiDispatcher());
    public static AvaloniaUiDispatcher Instance => _instance.Value;

    /// <summary>
    /// Publicar en la interfaz. Si no hay aplicación, el trabajo se <b>descarta</b>.
    ///
    /// <para>Descartarlo —y no ejecutarlo en el hilo que llame— se midió a golpes durante el hito 176: los
    /// latidos dejaron de entregar el tick desde el bucle de la interfaz y lo hacen desde un hilo del grupo de
    /// hilos, así que un despacho contra una aplicación que no está arrancada <b>inicializaba el despachador de
    /// Avalonia en ese hilo</b> —y con él su bucle de render—, y la siguiente sesión de pruebas moría al montar su
    /// compositor con «The calling thread cannot access this object because a different thread owns it». Tocar la
    /// interfaz desde el hilo equivocado es peor que no hacerlo: lo que queda por publicar no es crítico.</para>
    /// </summary>
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (Application.Current is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(action);
    }

    public Task InvokeAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (!HasUiThread())
        {
            // A diferencia de Post, aquí hay alguien esperando el resultado: sin interfaz se ejecuta en línea en
            // lugar de dejarlo colgado.
            action();
            return Task.CompletedTask;
        }

        return Dispatcher.UIThread.InvokeAsync(action).GetTask();
    }

    public Task<T> InvokeAsync<T>(Func<T> function)
    {
        ArgumentNullException.ThrowIfNull(function);

        if (!HasUiThread())
        {
            return Task.FromResult(function());
        }

        return Dispatcher.UIThread.InvokeAsync(function).GetTask();
    }

    public bool CheckAccess() => !HasUiThread() || Dispatcher.UIThread.CheckAccess();

    /// <summary>¿Hay hilo de interfaz al que despachar? Sin aplicación no hay ninguno.</summary>
    private static bool HasUiThread() => Application.Current is not null && !Dispatcher.UIThread.CheckAccess();
}
