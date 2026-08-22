using System.Diagnostics;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Plugin.Integrations;

[NodeDefinition("CliExecutionNode_Name", "Integrations", "CliExecutionNode_Desc")]
public class CliExecutionNode : IFlowNode
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
        try
        {
            string exe = Parameters.TryGetValue("ExecutablePath", out var eVal) ? ParameterHelper.GetString(eVal, "cmd.exe") : "cmd.exe";
            string argsTemplate = Parameters.TryGetValue("ArgumentsTemplate", out var aVal) ? ParameterHelper.GetString(aVal, "") : "";
            int timeoutSec = Parameters.TryGetValue("TimeoutSeconds", out var tVal) ? ParameterHelper.GetInt32(tVal, 60) : 60;
            bool captureOutput = Parameters.TryGetValue("CaptureOutputToMetadata", out var cVal) && ParameterHelper.GetBoolean(cVal, true);

            string resolvedArgs = VariableTemplateResolver.Resolve(argsTemplate, item);
            string resolvedExe = VariableTemplateResolver.Resolve(exe, item);

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

            var startInfo = new ProcessStartInfo
            {
                FileName = resolvedExe,
                Arguments = resolvedArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSec));

            string stdOut = await process.StandardOutput.ReadToEndAsync(cts.Token).ConfigureAwait(false);
            string stdErr = await process.StandardError.ReadToEndAsync(cts.Token).ConfigureAwait(false);

            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);

            if (captureOutput)
            {
                item.Metadata["Cli:StdOut"] = stdOut.Trim();
                item.Metadata["Cli:StdErr"] = stdErr.Trim();
                item.Metadata["Cli:ExitCode"] = process.ExitCode;
            }

            if (process.ExitCode == 0)
            {
                item.AddLog($"CLI command succeeded (ExitCode=0): {resolvedExe}");
                context.Log($"[CliExecutionNode] Process completed: {resolvedExe} ({stdOut.Length} chars output)", LogLevel.Information);
                await context.EmitAsync("Success", item);
            }
            else
            {
                item.AddLog($"CLI command failed (ExitCode={process.ExitCode}): {stdErr}");
                context.Log($"[CliExecutionNode] Process failed with ExitCode={process.ExitCode}: {stdErr}", LogLevel.Warning);
                await context.EmitAsync("Failed", item);
            }
        }
        catch (Exception ex)
        {
            context.Log($"[CliExecutionNode] Exception: {ex.Message}", LogLevel.Error);
            item.AddLog($"CLI Execution Exception: {ex.Message}");
            await context.EmitAsync("Failed", item);
        }
    }
}
