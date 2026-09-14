using FileFlow.Server;
using Photino.NET;

namespace FileFlow.App.Photino;

public static class Program
{
    public static async Task Main(string[] args)
    {
        const int serverPort = 5000;
        var listenUrl = $"http://127.0.0.1:{serverPort}";

        Console.WriteLine($"[FileFlow.Photino] Iniciando motor y servidor ({listenUrl})...");
        var app = FileFlowServerRunner.BuildServer(args, listenUrl);

        try
        {
            await app.StartAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FileFlow.Photino] Error al iniciar servidor en {listenUrl}: {ex.Message}. Reintentando en puerto dinamico...");
            app = FileFlowServerRunner.BuildServer(args, "http://127.0.0.1:0");
            await app.StartAsync();
        }

        var serverUrl = app.Urls.FirstOrDefault() ?? listenUrl;
        Console.WriteLine($"[FileFlow.Photino] Servidor activo y escuchando en: {serverUrl}");

        Console.WriteLine("[FileFlow.Photino] Abriendo ventana nativa de escritorio...");
        var window = new PhotinoWindow()
            .SetTitle("FileFlow Studio - Visual Workflow Engine")
            .SetUseOsDefaultSize(false)
            .SetSize(1440, 920)
            .Center()
            .Load(new Uri(serverUrl));

        window.WaitForClose();

        await app.StopAsync();
    }
}
