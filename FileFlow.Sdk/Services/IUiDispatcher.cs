namespace FileFlow.Sdk.Services;

/// <summary>
/// Contrato abstracto para el despacho de acciones hacia el hilo principal de la interfaz de usuario.
/// Permite que ViewModels, plugins y servicios interactúen con la UI de forma agnóstica de WPF o Avalonia.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>
    /// Despacha una acción al hilo de UI de forma asíncrona (fire-and-forget).
    /// </summary>
    void Post(Action action);

    /// <summary>
    /// Ejecuta una acción en el hilo de UI esperando su finalización.
    /// </summary>
    Task InvokeAsync(Action action);

    /// <summary>
    /// Ejecuta una función en el hilo de UI esperando su resultado.
    /// </summary>
    Task<T> InvokeAsync<T>(Func<T> function);

    /// <summary>
    /// Indica si el hilo actual es el hilo de la interfaz de usuario.
    /// </summary>
    bool CheckAccess();
}
