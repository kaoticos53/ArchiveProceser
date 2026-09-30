#if __WASM__
using Uno.UI.Hosting;

namespace FileFlow.App.Uno;

/// <summary>Punto de entrada del host para WebAssembly (navegador).</summary>
public static class WasmProgram
{
    public static void Main(string[] args)
    {
        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseWebAssembly()
            .Build();

        host.Run();
    }
}
#endif
