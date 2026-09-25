using FileFlow.App.Core;
using FileFlow.App.Services;
using FileFlow.Sdk.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FileFlow.App.Services;

/// <summary>
/// Registro del host Avalonia: toma la base portable del núcleo (<c>AddFileFlowCoreServices</c>) y
/// añade los adaptadores de plataforma Avalonia (despachado, diálogos, portapapeles, ventanas,
/// menús, ficheros, selector de color). Debe instalarse ANTES de resolver ViewModels.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFileFlowServices(this IServiceCollection services)
    {
        services.AddFileFlowCoreServices();

        // Adaptadores de plataforma Avalonia (los únicos puntos acoplados a la UI de este host).
        // Los ViewModels del núcleo toman estos contratos por parámetro y caen a sus no-ops si el
        // host no los registró; aquí se registran todos.
        services.AddSingleton<IUiDispatcher, AvaloniaUiDispatcher>();
        services.AddSingleton<IDialogService, AvaloniaDialogService>();
        services.AddSingleton<IClipboardService, AvaloniaClipboardService>();
        services.AddSingleton<IWindowService, AvaloniaWindowService>();
        services.AddSingleton<IPopupMenuService, AvaloniaPopupMenuService>();
        services.AddSingleton<IFileDialogService, FileDialogService>();
        services.AddSingleton<IColorPickerService, ColorPickerService>();

        return services;
    }
}
