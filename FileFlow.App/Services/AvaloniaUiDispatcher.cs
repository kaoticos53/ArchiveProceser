using System;
using System.Threading.Tasks;
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

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Dispatcher.UIThread.Post(action);
    }

    public Task InvokeAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }
        return Dispatcher.UIThread.InvokeAsync(action).GetTask();
    }

    public Task<T> InvokeAsync<T>(Func<T> function)
    {
        ArgumentNullException.ThrowIfNull(function);
        if (Dispatcher.UIThread.CheckAccess())
        {
            return Task.FromResult(function());
        }
        return Dispatcher.UIThread.InvokeAsync(function).GetTask();
    }

    public bool CheckAccess()
    {
        return Dispatcher.UIThread.CheckAccess();
    }
}
