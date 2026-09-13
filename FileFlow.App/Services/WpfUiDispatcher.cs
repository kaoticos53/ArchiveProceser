using System.Windows;
using System.Windows.Threading;
using FileFlow.Sdk.Services;

namespace FileFlow.App.Services;

/// <summary>
/// Adaptador de <see cref="IUiDispatcher"/> para el Dispatcher nativo de WPF.
/// </summary>
public sealed class WpfUiDispatcher : IUiDispatcher
{
    private static readonly Lazy<WpfUiDispatcher> _instance = new(() => new WpfUiDispatcher());
    public static WpfUiDispatcher Instance => _instance.Value;

    private readonly Dispatcher _dispatcher;

    public WpfUiDispatcher(Dispatcher? dispatcher = null)
    {
        _dispatcher = dispatcher ?? Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
    }

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (_dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            _dispatcher.BeginInvoke(action);
        }
    }

    public async Task InvokeAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (_dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            await _dispatcher.InvokeAsync(action);
        }
    }

    public async Task<T> InvokeAsync<T>(Func<T> function)
    {
        ArgumentNullException.ThrowIfNull(function);
        if (_dispatcher.CheckAccess())
        {
            return function();
        }
        return await _dispatcher.InvokeAsync(function);
    }

    public bool CheckAccess() => _dispatcher.CheckAccess();
}
