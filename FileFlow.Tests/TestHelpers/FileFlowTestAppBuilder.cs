using Avalonia;
using Avalonia.Headless;

// La sesión headless construye la aplicación con este AppBuilder (es el mecanismo que usa
// AvaloniaTestApplicationAttribute). Es lo que decide si hay renderizado REAL o simulado:
// sin '.UseSkia()' y sin desactivar 'UseHeadlessDrawing', 'CaptureRenderedFrame' no puede devolver
// una imagen y las pruebas de render no tendrían nada que comparar.
[assembly: AvaloniaTestApplication(typeof(FileFlow.Tests.TestHelpers.FileFlowTestAppBuilder))]

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// Configuración de la aplicación de pruebas: Skia real sobre la plataforma headless.
/// </summary>
public static class FileFlowTestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<FileFlow.App.App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions
        {
            UseHeadlessDrawing = false,
            FrameBufferFormat = Avalonia.Platform.PixelFormat.Rgba8888,
            ShouldRenderOnUIThread = true
        });
}
