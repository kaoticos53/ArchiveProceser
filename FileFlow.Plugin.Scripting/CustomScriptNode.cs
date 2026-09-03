using System.Text.Json;
using FileFlow.Plugin.Scripting.Engines;
using FileFlow.Plugin.Scripting.UI.Views;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.Scripting;

[NodeDefinition("CustomScriptNode_Name", "Logic", "CustomScriptNode_Desc", PipelineRole.Control,
    "script", "c#", "csharp", "javascript", "js", "codigo", "programar", "roslyn", "custom", "logica")]
public class CustomScriptNode : IFlowNode, INodeCustomActionProvider
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("CustomScriptNode_Name", "Script Personalizado (C# / JavaScript)");
    public string Category => "Logic";
    public string Description => LocalizationManager.Instance.GetString("CustomScriptNode_Desc", "Ejecuta lógica a medida en C# (Roslyn) o JavaScript con editor de código, puertos dinámicos configurables y biblioteca de scripts.");

    private List<NodePort> _inputs = [new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")];
    private List<NodePort> _outputs = [new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out")];

    public IReadOnlyList<NodePort> Inputs => _inputs.AsReadOnly();
    public IReadOnlyList<NodePort> Outputs => _outputs.AsReadOnly();

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Language"] = "CSharp", // "CSharp" o "JavaScript"
        ["ScriptCode"] = @"// Script en C#
// Disponibles: Item (o File), Context (o Flow), EmitAsync(port), Log(msg)

Log($""Procesando archivo: {Item.FileName} ({Item.FileSizeBytes} bytes)"");
Item.Metadata[""ProcesadoPorScript""] = true;

await EmitAsync(""Out"");",
        ["InputPorts"] = "In",
        ["OutputPorts"] = "Out",
        ["TimeoutSeconds"] = 30
    };

    public IReadOnlyList<NodeActionDescriptor> CustomActions =>
    [
        new(
            "OpenScriptStudio",
            LocalizationManager.Instance.GetString("ScriptStudio_Button", "💻 Editor de Scripts..."),
            "💻",
            LocalizationManager.Instance.GetString("ScriptStudio_Tooltip", "Abrir el estudio de programación de scripts con editor de código, plantillas y probador"))
    ];

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("Language", ParameterEditorType.Dropdown, "CSharp", 0, ["CSharp", "JavaScript"], null, null, null, "Lenguaje de programación a ejecutar"),
        new("TimeoutSeconds", ParameterEditorType.Number, 30, 1, null, 1, 300, 1, "Límite de tiempo en segundos por archivo"),
        new("InputPorts", ParameterEditorType.Text, "In", 2, null, null, null, null, "Nombres de los puertos de entrada separados por comas"),
        new("OutputPorts", ParameterEditorType.Text, "Out", 3, null, null, null, null, "Nombres de los puertos de salida separados por comas")
    ];

    public CustomScriptNode()
    {
        SyncPortsFromParameters();
    }

    public void SyncPortsFromParameters()
    {
        string inputsStr = Parameters.TryGetValue("InputPorts", out var inVal) ? ParameterHelper.GetString(inVal, "In") : "In";
        string outputsStr = Parameters.TryGetValue("OutputPorts", out var outVal) ? ParameterHelper.GetString(outVal, "Out") : "Out";

        var inPortNames = inputsStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (inPortNames.Length == 0) inPortNames = ["In"];

        var outPortNames = outputsStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (outPortNames.Length == 0) outPortNames = ["Out"];

        _inputs = inPortNames.Select(name => new NodePort(name, typeof(FileItemContext), PortDirection.Input, name)).ToList();
        _outputs = outPortNames.Select(name => new NodePort(name, typeof(FileItemContext), PortDirection.Output, name)).ToList();
    }

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string language = Parameters.TryGetValue("Language", out var lVal) ? ParameterHelper.GetString(lVal, "CSharp") : "CSharp";
        string code = Parameters.TryGetValue("ScriptCode", out var cVal) ? ParameterHelper.GetString(cVal, "") : "";
        int timeoutSec = Parameters.TryGetValue("TimeoutSeconds", out var tVal) ? ParameterHelper.GetInt32(tVal, 30) : 30;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, timeoutSec)));

        var execContext = new ScriptExecutionContext
        {
            Item = item,
            FlowContext = context,
            InputPortName = inputPortName,
            CancellationToken = cts.Token
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (language.Equals("JavaScript", StringComparison.OrdinalIgnoreCase) || language.Equals("JS", StringComparison.OrdinalIgnoreCase))
            {
                await JintJavaScriptEngine.Instance.ExecuteAsync(code, execContext, cts.Token).ConfigureAwait(false);
            }
            else
            {
                await RoslynCSharpEngine.Instance.ExecuteAsync(code, execContext, cts.Token).ConfigureAwait(false);
            }

            sw.Stop();
            context.Log($"[Script {language}] Ejecutado exitosamente en {sw.ElapsedMilliseconds} ms ({item.FileName})", LogLevel.Information, item, durationMs: sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            context.Log($"[Script {language} Timeout] Tiempo límite de {timeoutSec}s excedido para '{item.FileName}'", LogLevel.Error, item);
            throw new TimeoutException($"El script excedió el tiempo límite de {timeoutSec} segundos.");
        }
        catch (Exception ex)
        {
            context.Log($"[Script {language} Error] {ex.Message}", LogLevel.Error, item);
            throw;
        }
    }

    public void ExecuteCustomAction(string actionId, object? context = null)
    {
        if (actionId.Equals("OpenScriptStudio", StringComparison.OrdinalIgnoreCase))
        {
            string language = Parameters.TryGetValue("Language", out var lVal) ? ParameterHelper.GetString(lVal, "CSharp") : "CSharp";
            string code = Parameters.TryGetValue("ScriptCode", out var cVal) ? ParameterHelper.GetString(cVal, "") : "";
            string inputsStr = Parameters.TryGetValue("InputPorts", out var inVal) ? ParameterHelper.GetString(inVal, "In") : "In";
            string outputsStr = Parameters.TryGetValue("OutputPorts", out var outVal) ? ParameterHelper.GetString(outVal, "Out") : "Out";

            var window = new ScriptStudioWindow(language, code, inputsStr, outputsStr);
            if (System.Windows.Application.Current?.MainWindow != null)
            {
                window.Owner = System.Windows.Application.Current.MainWindow;
            }

            if (window.ShowDialog() == true)
            {
                Parameters["Language"] = window.SelectedLanguage;
                Parameters["ScriptCode"] = window.ScriptCode;
                Parameters["InputPorts"] = window.InputPortsString;
                Parameters["OutputPorts"] = window.OutputPortsString;

                SyncPortsFromParameters();
            }
        }
    }
}
