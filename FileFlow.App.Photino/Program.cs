using FileFlow.Server;
using Photino.NET;

const int serverPort = 5000;
var serverUrl = $"http://127.0.0.1:{serverPort}";

Console.WriteLine($"[FileFlow.Photino] Iniciando motor en segundo plano ({serverUrl})...");
var app = FileFlowServerRunner.BuildServer(args, serverUrl);
_ = Task.Run(() => app.RunAsync());

await Task.Delay(400);

Console.WriteLine("[FileFlow.Photino] Abriendo ventana nativa de escritorio...");
var window = new PhotinoWindow()
    .SetTitle("FileFlow Studio - Visual Workflow Engine")
    .SetUseOsDefaultSize(false)
    .SetSize(1440, 920)
    .Center()
    .Load(new Uri(serverUrl));

window.WaitForClose();
