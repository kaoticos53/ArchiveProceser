using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using FileFlow.Core.Engine;

namespace FileFlow.App;

public static class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args)
    {
        // Soporte de ejecución Headless / CLI en Linux, Windows y macOS
        if (args.Length > 0 && (args.Any(a => a.Equals("--run", StringComparison.OrdinalIgnoreCase) ||
                                              a.Equals("-r", StringComparison.OrdinalIgnoreCase) ||
                                              a.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
                                              a.Equals("-h", StringComparison.OrdinalIgnoreCase) ||
                                              (File.Exists(a) && a.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))))
        {
            var cliOptions = WorkflowCliOptions.Parse(args);
            return WorkflowCliRunner.RunAsync(cliOptions).GetAwaiter().GetResult();
        }

        return BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
