using System;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using FileFlow.Sdk.Services;

namespace FileFlow.App.Uno.Platform;

/// <summary>
/// Adaptador de despachado al hilo de UI implementado con el DispatcherQueue de WinUI.
/// Cumple el contrato <see cref="IUiDispatcher"/> de FileFlow.Sdk, que ya era agnóstico de framework.
/// </summary>
public sealed class UnoUiDispatcher : IUiDispatcher
{
    private readonly DispatcherQueue _queue;

    public UnoUiDispatcher()
    {
        // En el host de escritorio el proceso ya tiene su DispatcherQueue en el hilo principal.
        _queue = DispatcherQueue.GetForCurrentThread();
    }

    public void Post(Action action) => _queue.TryEnqueue(() => action());

    public Task InvokeAsync(Action action)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.TryEnqueue(() =>
        {
            try { action(); tcs.SetResult(); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    public Task<T> InvokeAsync<T>(Func<T> function)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.TryEnqueue(() =>
        {
            try { tcs.SetResult(function()); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    public bool CheckAccess() => _queue.HasThreadAccess;
}
