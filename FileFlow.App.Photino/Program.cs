using FileFlow.Server;
using Photino.NET;

namespace FileFlow.App.Photino;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        const int serverPort = 5000;
        var listenUrl = $"http://127.0.0.1:{serverPort}";

        Console.WriteLine($"[FileFlow.Photino] Iniciando motor Kestrel ({listenUrl})...");
        var app = FileFlowServerRunner.BuildServer(args, listenUrl);

        try
        {
            Task.Run(async () => await app.StartAsync()).Wait();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FileFlow.Photino] Advertencia en puerto {serverPort}: {ex.Message}. Reintentando con puerto dinamico...");
            app = FileFlowServerRunner.BuildServer(args, "http://127.0.0.1:0");
            Task.Run(async () => await app.StartAsync()).Wait();
        }

        var serverUrl = app.Urls.FirstOrDefault() ?? listenUrl;
        Console.WriteLine($"[FileFlow.Photino] Servidor Kestrel activo y escuchando en: {serverUrl}");

        Console.WriteLine("[FileFlow.Photino] Creando ventana nativa Photino...");
        var window = new PhotinoWindow()
            .SetTitle("FileFlow Studio - Visual Workflow Engine")
            .SetUseOsDefaultSize(false)
            .SetSize(1440, 920)
            .Center()
            .SetDevToolsEnabled(true)
            .SetLogVerbosity(2);

        // Cargar SIEMPRE via HTTP (Kestrel) para que WebView2 permita la carga de ES Modules (Vite)
        Console.WriteLine($"[FileFlow.Photino] Cargando aplicacion via HTTP: {serverUrl}");
        window.Load(new Uri(serverUrl));

        Console.WriteLine("[FileFlow.Photino] Entrando en el bucle de mensajes de la ventana...");
        window.WaitForClose();

        Console.WriteLine("[FileFlow.Photino] Ventana cerrada. Deteniendo Kestrel...");
        Task.Run(async () => await app.StopAsync()).Wait();
    }
}
