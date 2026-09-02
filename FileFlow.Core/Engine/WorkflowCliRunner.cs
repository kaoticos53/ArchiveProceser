using System.Diagnostics;
using System.IO;
using System.Text.Json;
using FileFlow.Core.Plugins;
using FileFlow.Sdk;
using FileFlow.Sdk.Telemetry;

namespace FileFlow.Core.Engine;

public class WorkflowCliOptions
{
    public string WorkflowPath { get; set; } = string.Empty;
    public string? OverrideInputPath { get; set; }
    public string? OverrideOutputPath { get; set; }
    public bool IsDryRun { get; set; }
    public bool IsSilent { get; set; }
    public bool ShowHelp { get; set; }
    public bool IsWatchMode { get; set; }
    public bool EnableCheckpointResume { get; set; } = true;
    public string? JsonSummaryPath { get; set; }
    public Dictionary<string, string> Variables { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> ParameterOverrides { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsValid => !string.IsNullOrWhiteSpace(WorkflowPath) && File.Exists(WorkflowPath);

    public static WorkflowCliOptions Parse(string[] args)
    {
        var options = new WorkflowCliOptions();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase) || arg.Equals("-h", StringComparison.OrdinalIgnoreCase) || arg.Equals("/?"))
            {
                options.ShowHelp = true;
            }
            else if ((arg.Equals("--run", StringComparison.OrdinalIgnoreCase) || arg.Equals("-r", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
            {
                options.WorkflowPath = args[++i];
            }
            else if ((arg.Equals("--input", StringComparison.OrdinalIgnoreCase) || arg.Equals("-i", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
            {
                options.OverrideInputPath = args[++i];
            }
            else if ((arg.Equals("--output", StringComparison.OrdinalIgnoreCase) || arg.Equals("-o", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
            {
                options.OverrideOutputPath = args[++i];
            }
            else if (arg.Equals("--dryrun", StringComparison.OrdinalIgnoreCase) || arg.Equals("-d", StringComparison.OrdinalIgnoreCase))
            {
                options.IsDryRun = true;
            }
            else if (arg.Equals("--no-checkpoint", StringComparison.OrdinalIgnoreCase))
            {
                options.EnableCheckpointResume = false;
            }
            else if (arg.Equals("--resume", StringComparison.OrdinalIgnoreCase) || arg.Equals("-c", StringComparison.OrdinalIgnoreCase))
            {
                options.EnableCheckpointResume = true;
            }
            else if (arg.Equals("--silent", StringComparison.OrdinalIgnoreCase) || arg.Equals("-s", StringComparison.OrdinalIgnoreCase))
            {
                options.IsSilent = true;
            }
            else if (arg.Equals("--watch", StringComparison.OrdinalIgnoreCase) || arg.Equals("-w", StringComparison.OrdinalIgnoreCase))
            {
                options.IsWatchMode = true;
            }
            else if ((arg.Equals("--json-summary", StringComparison.OrdinalIgnoreCase) || arg.Equals("--summary", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
            {
                options.JsonSummaryPath = args[++i];
            }
            else if ((arg.Equals("--var", StringComparison.OrdinalIgnoreCase) || arg.Equals("-v", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
            {
                string pair = args[++i];
                int eqIdx = pair.IndexOf('=');
                if (eqIdx > 0)
                {
                    options.Variables[pair[..eqIdx].Trim()] = pair[(eqIdx + 1)..].Trim();
                }
            }
            else if ((arg.Equals("--param", StringComparison.OrdinalIgnoreCase) || arg.Equals("-p", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
            {
                string pair = args[++i];
                int eqIdx = pair.IndexOf('=');
                if (eqIdx > 0)
                {
                    options.ParameterOverrides[pair[..eqIdx].Trim()] = pair[(eqIdx + 1)..].Trim();
                }
            }
            else if (File.Exists(arg) && string.IsNullOrWhiteSpace(options.WorkflowPath) && arg.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                options.WorkflowPath = arg;
            }
        }

        return options;
    }
}

public class WorkflowExecutionSummary
{
    public string WorkflowPath { get; set; } = string.Empty;
    public bool Succeeded { get; set; }
    public bool IsDryRun { get; set; }
    public bool IsWatchMode { get; set; }
    public double DurationMs { get; set; }
    public long TotalItemsProcessed { get; set; }
    public long ProcessedBytes { get; set; }
    public double ItemsPerSecond { get; set; }
    public double MegabytesPerSecond { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, NodeTelemetryStats> NodeStats { get; set; } = [];
}

/// <summary>
/// Ejecutor desatendido de flujos de trabajo por línea de comandos (CLI / Headless Mode).
/// </summary>
public static class WorkflowCliRunner
{
    public static void PrintHelp(TextWriter? writer = null)
    {
        var outWriter = writer ?? Console.Out;
        outWriter.WriteLine("==================================================================");
        outWriter.WriteLine(" FileFlow Studio - Headless CLI Workflow Runner (.NET 9)");
        outWriter.WriteLine("==================================================================");
        outWriter.WriteLine("Uso: FileFlow.App.exe --run <workflow.json> [opciones]");
        outWriter.WriteLine();
        outWriter.WriteLine("Opciones:");
        outWriter.WriteLine("  -r, --run <archivo.json>       Ruta al archivo JSON del flujo de trabajo a ejecutar.");
        outWriter.WriteLine("  -i, --input <carpeta>          Sobrescribe la ruta de entrada de los nodos origen.");
        outWriter.WriteLine("  -o, --output <carpeta>         Sobrescribe la ruta de salida global (GlobalOutputDir).");
        outWriter.WriteLine("  -d, --dryrun                   Ejecuta en modo de simulación seguro sin modificar disco.");
        outWriter.WriteLine("  -w, --watch                    Ejecuta en Modo Vigilante en tiempo real escuchando carpetas.");
        outWriter.WriteLine("  -v, --var <clave=valor>        Inyecta variables dinámicas en el contexto del flujo.");
        outWriter.WriteLine("  -p, --param <Nodo.Param=valor> Sobrescribe un parámetro específico de un nodo.");
        outWriter.WriteLine("      --summary <reporte.json>   Exporta un resumen de ejecución y métricas en formato JSON.");
        outWriter.WriteLine("  -s, --silent                   Modo silencioso: suprime la telemetría informativa.");
        outWriter.WriteLine("  -h, --help                     Muestra este mensaje de ayuda.");
        outWriter.WriteLine();
        outWriter.WriteLine("Ejemplos:");
        outWriter.WriteLine("  FileFlow.App.exe --run C:\\Flujos\\Optimizar.json");
        outWriter.WriteLine("  FileFlow.App.exe -r flow.json -i \"D:\\Fotos\" -o \"D:\\Salida\" --dryrun --summary report.json");
        outWriter.WriteLine("  FileFlow.App.exe -r flow.json --var Quality=90 -p ImageResizeNode.TargetWidth=1920");
    }

    public static async Task<int> RunAsync(WorkflowCliOptions options, PluginLoader? pluginLoader = null, TextWriter? writer = null, CancellationToken cancellationToken = default)
    {
        var outWriter = writer ?? Console.Out;

        if (options.ShowHelp)
        {
            PrintHelp(outWriter);
            return 0;
        }

        if (!options.IsValid)
        {
            outWriter.WriteLine($"[ERROR CLI] Archivo de flujo no encontrado o no especificado: '{options.WorkflowPath}'");
            PrintHelp(outWriter);
            return 1;
        }

        var loader = pluginLoader ?? new PluginLoader();
        loader.ScanCurrentAppDomain();
        string pluginsDir = Path.Combine(AppContext.BaseDirectory, "plugins");
        if (Directory.Exists(pluginsDir))
        {
            loader.LoadPluginDirectory(pluginsDir);
        }

        if (!options.IsSilent)
        {
            outWriter.WriteLine("==================================================================");
            outWriter.WriteLine($" FileFlow Studio - Ejecución de Flujo: {Path.GetFileName(options.WorkflowPath)}");
            outWriter.WriteLine($" Modo: {(options.IsDryRun ? "SIMULACIÓN (Dry Run)" : (options.IsWatchMode ? "VIGILANTE (Watch Mode)" : "PRODUCCIÓN"))}");
            outWriter.WriteLine("==================================================================");
        }

        var summary = new WorkflowExecutionSummary
        {
            WorkflowPath = options.WorkflowPath,
            IsDryRun = options.IsDryRun,
            IsWatchMode = options.IsWatchMode
        };

        var sw = Stopwatch.StartNew();

        try
        {
            string json = await File.ReadAllTextAsync(options.WorkflowPath, cancellationToken);
            var graph = WorkflowGraph.FromJson(json);

            if (!string.IsNullOrWhiteSpace(options.OverrideOutputPath))
            {
                graph.GlobalOutputDir = options.OverrideOutputPath;
            }

            // Aplicar variables globales
            foreach (var (varKey, varVal) in options.Variables)
            {
                Environment.SetEnvironmentVariable(varKey, varVal);
            }

            // Aplicar reemplazos de variables y parámetros sobre los nodos
            foreach (var nodeDto in graph.Nodes)
            {
                if (!string.IsNullOrWhiteSpace(options.OverrideInputPath))
                {
                    if (nodeDto.Parameters.ContainsKey("SourcePath")) nodeDto.Parameters["SourcePath"] = options.OverrideInputPath;
                    if (nodeDto.Parameters.ContainsKey("SourceDirectory")) nodeDto.Parameters["SourceDirectory"] = options.OverrideInputPath;
                    if (nodeDto.Parameters.ContainsKey("FolderPath")) nodeDto.Parameters["FolderPath"] = options.OverrideInputPath;
                }

                // Inyección de parámetros específicos --param NodeId.Param=Val o --param NodeTypeName.Param=Val
                foreach (var (pKey, pVal) in options.ParameterOverrides)
                {
                    int dotIdx = pKey.IndexOf('.');
                    if (dotIdx > 0)
                    {
                        string targetNodeIdentifier = pKey[..dotIdx].Trim();
                        string paramName = pKey[(dotIdx + 1)..].Trim();

                        if (nodeDto.Id.Equals(targetNodeIdentifier, StringComparison.OrdinalIgnoreCase) ||
                            nodeDto.NodeTypeName.EndsWith(targetNodeIdentifier, StringComparison.OrdinalIgnoreCase))
                        {
                            nodeDto.Parameters[paramName] = pVal;
                        }
                    }
                }
            }

            var executor = new WorkflowExecutor
            {
                IsDryRun = options.IsDryRun,
                GlobalOutputDir = graph.GlobalOutputDir,
                EnableCheckpointing = options.EnableCheckpointResume
            };

            if (!options.IsSilent)
            {
                executor.ProgressChanged += (pct, msg) =>
                {
                    outWriter.WriteLine($"[{pct:00.0}%] {msg}");
                };

                executor.LogEmitted += (msg, lvl) =>
                {
                    if (lvl >= LogLevel.Information)
                    {
                        outWriter.WriteLine($"[{lvl}] {msg}");
                    }
                };
            }

            if (options.IsWatchMode)
            {
                var watchFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var node in graph.Nodes)
                {
                    foreach (var param in node.Parameters)
                    {
                        if (param.Value == null) continue;
                        string valStr = param.Value.ToString() ?? string.Empty;
                        if ((param.Key.Contains("Folder", StringComparison.OrdinalIgnoreCase) ||
                             param.Key.Contains("Directory", StringComparison.OrdinalIgnoreCase) ||
                             param.Key.Contains("Path", StringComparison.OrdinalIgnoreCase)) &&
                            !param.Key.Contains("Output", StringComparison.OrdinalIgnoreCase) &&
                            !param.Key.Contains("Destination", StringComparison.OrdinalIgnoreCase))
                        {
                            string expanded = Environment.ExpandEnvironmentVariables(valStr);
                            if (Directory.Exists(expanded))
                            {
                                watchFolders.Add(expanded);
                            }
                        }
                    }
                }

                if (watchFolders.Count == 0)
                {
                    outWriter.WriteLine("❌ [ERROR CLI] No se encontraron carpetas de entrada existentes para escuchar en Modo Vigilante.");
                    return 1;
                }

                using var watcher = new FolderWatcherService();
                watcher.Start(watchFolders, filter: "*.*", includeSubdirectories: true);

                outWriter.WriteLine($"👁️ [Watchdog] Escuchando {watchFolders.Count} carpetas: {string.Join(", ", watchFolders)}");
                await executor.ExecuteWatchModeAsync(graph, loader, watcher, cancellationToken);
            }
            else
            {
                await executor.ExecuteAsync(graph, loader, cancellationToken);
            }

            sw.Stop();
            var snapshot = executor.GetTelemetrySnapshot();
            var nodeStats = executor.GetNodeTelemetryStats();

            summary.Succeeded = true;
            summary.DurationMs = sw.ElapsedMilliseconds;
            summary.TotalItemsProcessed = snapshot.ProcessedItems;
            summary.ProcessedBytes = snapshot.ProcessedBytes;
            summary.ItemsPerSecond = snapshot.ItemsPerSecond;
            summary.MegabytesPerSecond = snapshot.MegabytesPerSecond;
            summary.NodeStats = new Dictionary<string, NodeTelemetryStats>(nodeStats, StringComparer.OrdinalIgnoreCase);

            if (!options.IsSilent)
            {
                outWriter.WriteLine("------------------------------------------------------------------");
                outWriter.WriteLine($"✅ Flujo completado con éxito en {sw.ElapsedMilliseconds} ms. ({snapshot.ProcessedItems:N0} elementos procesados)");
                outWriter.WriteLine("==================================================================");
            }

            if (!string.IsNullOrWhiteSpace(options.JsonSummaryPath))
            {
                string summaryDir = Path.GetDirectoryName(options.JsonSummaryPath) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(summaryDir) && !Directory.Exists(summaryDir))
                {
                    Directory.CreateDirectory(summaryDir);
                }
                string summaryJson = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(options.JsonSummaryPath, summaryJson, cancellationToken);
            }

            return 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            summary.Succeeded = false;
            summary.DurationMs = sw.ElapsedMilliseconds;
            summary.ErrorMessage = ex.Message;

            outWriter.WriteLine($"❌ [ERROR FATAL] Falló la ejecución del flujo:\n{ex.Message}\n{ex.StackTrace}");

            if (!string.IsNullOrWhiteSpace(options.JsonSummaryPath))
            {
                try
                {
                    string summaryJson = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(options.JsonSummaryPath, summaryJson, cancellationToken);
                }
                catch { }
            }

            return 1;
        }
    }
}
