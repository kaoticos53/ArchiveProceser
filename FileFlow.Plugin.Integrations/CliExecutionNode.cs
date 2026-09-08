using System.Diagnostics;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Platform;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Plugin.Integrations;

[NodeDefinition("CliExecutionNode_Name", "Integrations", "CliExecutionNode_Desc", PipelineRole.Control,
    "cli", "comando", "ejecutable", "cmd", "powershell", "proceso", "terminal", "bash", "execute")]
public sealed class CliExecutionNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("CliExecutionNode_Name", "Ejecutor de Comandos y Procesos CLI");
    public string Category => "Integrations";
    public string Description => LocalizationManager.Instance.GetString("CliExecutionNode_Desc", "Lanza ejecutables externos y scripts de sistema (FFmpeg, PowerShell, Python, Node.js) inyectando la ruta y metadatos del archivo mediante argumentos con tokens.");

    public IReadOnlyList<NodePort> Inputs { get; } = new[]
    {
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    };

    public IReadOnlyList<NodePort> Outputs { get; } = new[]
    {
        new NodePort("Success", typeof(FileItemContext), PortDirection.Output, "Success"),
        new NodePort("Failed", typeof(FileItemContext), PortDirection.Output, "Failed")
    };

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ExecutablePath"] = "cmd.exe",
        ["ArgumentsTemplate"] = "/c echo Processing {FileName}",
        ["TimeoutSeconds"] = 60,
        ["CaptureOutputToMetadata"] = true
    };

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        var platform = context.Platform ?? NullOsPlatformService.Instance;
        string defaultShell = platform.GetDefaultShellExecutable();
        string exe = Parameters.TryGetValue("ExecutablePath", out var eVal) ? ParameterHelper.GetString(eVal, defaultShell) : defaultShell;
        if (string.Equals(exe, "cmd.exe", StringComparison.OrdinalIgnoreCase) && !platform.IsWindows)
        {
            exe = defaultShell;
        }

        string argsTemplate = Parameters.TryGetValue("ArgumentsTemplate", out var aVal) ? ParameterHelper.GetString(aVal, "") : "";
        int timeoutSec = Parameters.TryGetValue("TimeoutSeconds", out var tVal) ? ParameterHelper.GetInt32(tVal, 60) : 60;
        bool captureOutput = Parameters.TryGetValue("CaptureOutputToMetadata", out var cVal) && ParameterHelper.GetBoolean(cVal, true);

        string resolvedArgs = VariableTemplateResolver.Resolve(argsTemplate, item);
        if (!platform.IsWindows && resolvedArgs.StartsWith("/c ", StringComparison.OrdinalIgnoreCase))
        {
            resolvedArgs = "-c \"" + resolvedArgs[3..].Replace("\"", "\\\"") + "\"";
        }
        string resolvedExe = VariableTemplateResolver.Resolve(exe, item);

        try
        {

            if (context.IsDryRun)
            {
                context.RegisterPlannedAction(new PlannedAction(
                    Guid.NewGuid(),
                    Id,
                    Name,
                    PlannedOperationType.ExecuteCommand,
                    item.CurrentPath,
                    null,
                    $"Run command: {resolvedExe} {resolvedArgs}"
                ));
                item.AddLog($"[DryRun] Planned CLI Execution: {resolvedExe} {resolvedArgs}");
                await context.EmitAsync("Success", item);
                return;
            }

            var runner = context.ProcessRunner ?? ProcessRunner.Instance;
            var runnerResult = await runner.RunAsync(new ProcessExecutionRequest
            {
                FileName = resolvedExe,
                Arguments = resolvedArgs,
                WorkingDirectory = null,
                Timeout = TimeSpan.FromSeconds(timeoutSec),
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }, cancellationToken).ConfigureAwait(false);

            if (runnerResult.TimedOut)
            {
                throw new TimeoutException($"CLI Execution timed out after {timeoutSec} seconds: {resolvedExe}");
            }

            string stdOut = runnerResult.StandardOutput;
            string stdErr = runnerResult.StandardError;
            int exitCode = runnerResult.ExitCode;
            double durationMs = runnerResult.Duration.TotalMilliseconds;

            if (captureOutput)
            {
                item.Metadata["Cli:StdOut"] = stdOut.Trim();
                item.Metadata["Cli:StdErr"] = stdErr.Trim();
                item.Metadata["Cli:ExitCode"] = exitCode;
            }

            string detailsJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                executable = resolvedExe,
                arguments = resolvedArgs,
                exitCode = exitCode,
                stdOutLength = stdOut.Length,
                stdErrLength = stdErr.Length,
                stdOutSample = stdOut.Length > 200 ? stdOut[..200] + "..." : stdOut,
                stdErrSample = stdErr.Length > 200 ? stdErr[..200] + "..." : stdErr
            });

            if (exitCode == 0)
            {
                item.AddLog($"CLI command succeeded (ExitCode=0): {resolvedExe}");
                context.Log($"[Ejecutor CLI] Proceso finalizado exitosamente (ExitCode=0): '{Path.GetFileName(resolvedExe)}'", LogLevel.Information, item, durationMs: durationMs, detailsJson: detailsJson);
                await context.EmitAsync("Success", item);
            }
            else
            {
                item.AddLog($"CLI command failed (ExitCode={exitCode}): {stdErr}");
                context.Log($"[Ejecutor CLI] Proceso falló con código {exitCode}: {stdErr.Trim()}", LogLevel.Warning, item, durationMs: durationMs, detailsJson: detailsJson);
                await context.EmitAsync("Failed", item);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            string errJson = $"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\", \"executable\": \"{resolvedExe.Replace("\\", "\\\\")}\"}}";
            context.Log($"[Ejecutor CLI] Error al ejecutar comando: {ex.Message}", LogLevel.Error, item, durationMs: 0.0, detailsJson: errJson);
            item.AddLog($"CLI Execution Exception: {ex.Message}");
            await context.EmitAsync("Failed", item);
        }
    }
}
