namespace FileFlow.Sdk.Services;

/// <summary>
/// Implementación neutra (Null Object) de <see cref="IUiDispatcher"/> para pruebas unitarias y ejecución headless/CLI.
/// Ejecuta las acciones sincrónicamente en el hilo que realiza la llamada.
/// </summary>
public sealed class NullUiDispatcher : IUiDispatcher
{
    private static readonly Lazy<NullUiDispatcher> _instance = new(() => new NullUiDispatcher());
    public static NullUiDispatcher Instance => _instance.Value;

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
    }

    public Task InvokeAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
        return Task.CompletedTask;
    }

    public Task<T> InvokeAsync<T>(Func<T> function)
    {
        ArgumentNullException.ThrowIfNull(function);
        return Task.FromResult(function());
    }

    public bool CheckAccess() => true;
}
