using FileFlow.Sdk.Services;

namespace FileFlow.App.Core;

/// <summary>
/// Resolución del servicio de diálogos para ViewModels construidos sin contenedor (los que se hacen
/// con <c>new</c> dentro de otros ViewModels o de una ventana). El host deja su contenedor aquí en el
/// arranque; las pruebas pueden dejar el nulo y caen al doble nulo del Sdk.
/// </summary>
public static class CoreDialogHost
{
    private static IServiceProvider? s_services;

    /// <summary>Contenedor de servicios del host; nulo en pruebas y hosts sin contenedor.</summary>
    public static IServiceProvider? Services
    {
        get => s_services;
        set => s_services = value;
    }

    /// <summary>
    /// Resuelve el <see cref="IDialogService"/> del host cuando el contenedor ya está levantado.
    /// El constructor sin argumentos es el que usa el estudio al abrirse por su cuenta, así que no puede
    /// quedarse con el doble nulo: sin diálogo real, eliminar un tema no pide confirmación y un error al
    /// importar o exportar no se le cuenta a nadie. Con el doble nulo como último recurso.
    /// </summary>
    public static IDialogService ResolveDialogService() =>
        s_services?.GetService(typeof(IDialogService)) as IDialogService ?? NullDialogService.Instance;
}
