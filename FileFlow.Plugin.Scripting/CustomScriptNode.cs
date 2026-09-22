using System.Text.Json;
using FileFlow.Plugin.Scripting.Engines;
using FileFlow.Plugin.Scripting.UI.Views;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.Scripting;

[NodeDefinition("CustomScriptNode_Name", "Logic", "CustomScriptNode_Desc", PipelineRole.Control,
    "script", "c#", "csharp", "javascript", "js", "codigo", "programar", "roslyn", "custom", "logica")]
public sealed class CustomScriptNode : FlowNodeBase, INodeCustomActionProvider
{
    public override string Name => LocalizationManager.Instance.GetString("CustomScriptNode_Name", "Script Personalizado (C# / JavaScript)");
    public override string Category => "Logic";
    public override string Description => LocalizationManager.Instance.GetString("CustomScriptNode_Desc", "Ejecuta lógica a medida en C# (Roslyn) o JavaScript con editor de código, puertos dinámicos configurables y biblioteca de scripts.");

    private void SetDefaultParameters()
    {
        Parameters["Language"] = "CSharp"; // "CSharp" o "JavaScript"
        Parameters["ScriptCode"] = @"// Script en C#
// Disponibles: Item (o File), Context (o Flow), EmitAsync(port), Log(msg)

Log($""Procesando archivo: {Item.FileName} ({Item.FileSizeBytes} bytes)"");
Item.Metadata[""ProcesadoPorScript""] = true;

await EmitAsync(""Out"");";
        Parameters["InputPorts"] = "In";
        Parameters["OutputPorts"] = "Out";
        Parameters["TimeoutSeconds"] = 30;
    }

    public override IReadOnlyList<NodeActionDescriptor> CustomActions =>
    [
        new(
            "OpenScriptStudio",
            LocalizationManager.Instance.GetString("ScriptStudio_Button", "💻 Editor de Scripts..."),
            "💻",
            LocalizationManager.Instance.GetString("ScriptStudio_Tooltip", "Abrir el estudio de programación de scripts con editor de código, plantillas y probador"))
    ];

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("Language", ParameterEditorType.Dropdown, "CSharp", 0, ["CSharp", "JavaScript"], null, null, null, "Lenguaje de programación a ejecutar"),
        new("TimeoutSeconds", ParameterEditorType.Number, 30, 1, null, 1, 300, 1, "Límite de tiempo en segundos por archivo"),
        new("InputPorts", ParameterEditorType.Text, "In", 2, null, null, null, null, "Nombres de los puertos de entrada separados por comas"),
        new("OutputPorts", ParameterEditorType.Text, "Out", 3, null, null, null, null, "Nombres de los puertos de salida separados por comas")
    ];

    public CustomScriptNode()
    {
        SetDefaultParameters();
        SyncPortsFromParameters();
    }

    /// <summary>
    /// Puertos dinámicos: los define el usuario desde los parámetros InputPorts/OutputPorts. Se asignan con
    /// los setters protegidos de la base en lugar de sobrescribir las propiedades de puertos.
    ///
    /// Este nodo <b>materializa</b> sus puertos (a diferencia de los que los calculan al leerlos), así que
    /// reevaluar la topología es volver a derivarlos de los parámetros: por eso sobrescribe
    /// <see cref="FlowNodeBase.RefreshPortTopology"/>. Anunciar sin rederivar dejaría al editor con los
    /// puertos viejos, que es exactamente el fallo que la notificación viene a evitar.
    /// </summary>
    public override void RefreshPortTopology() => SyncPortsFromParameters();

    public void SyncPortsFromParameters()
    {
        Inputs = ParsePorts(GetParameter("InputPorts", "In"), PortDirection.Input, "In");
        Outputs = ParsePorts(GetParameter("OutputPorts", "Out"), PortDirection.Output, "Out");

        NotifyPortsChanged();
    }

    private static List<NodePort> ParsePorts(string names, PortDirection direction, string fallback)
    {
        string[] portNames = names.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (portNames.Length == 0) portNames = [fallback];

        return portNames.Select(name => new NodePort(name, typeof(FileItemContext), direction, name)).ToList();
    }

    public override async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string language = GetParameter("Language", "CSharp");
        string code = GetParameter("ScriptCode", "");
        int timeoutSec = GetParameter("TimeoutSeconds", 30);

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

    public async void ExecuteCustomAction(string actionId, object? context = null)
    {
        try
        {
            if (actionId.Equals("OpenScriptStudio", StringComparison.OrdinalIgnoreCase))
            {
                Action? onCompleted = null;
                object? parentWindow = context;

                if (context is NodeCustomActionContext customCtx)
                {
                    parentWindow = customCtx.ParentWindow;
                    onCompleted = customCtx.OnCompleted;
                }
                else if (context is Action callback)
                {
                    onCompleted = callback;
                }

                string language = GetParameter("Language", "CSharp");
                string code = GetParameter("ScriptCode", "");
                string inputsStr = GetParameter("InputPorts", "In");
                string outputsStr = GetParameter("OutputPorts", "Out");

                var window = new ScriptStudioWindow(language, code, inputsStr, outputsStr);
                Avalonia.Controls.Window? owner = parentWindow as Avalonia.Controls.Window;
                if (owner == null && Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    owner = desktop.MainWindow;
                }

                bool result = false;
                if (owner != null)
                {
                    result = await window.ShowDialog<bool>(owner);
                }
                else
                {
                    window.Show();
                }

                if (result)
                {
                    Parameters["Language"] = window.SelectedLanguage;
                    Parameters["ScriptCode"] = window.ScriptCode;
                    Parameters["InputPorts"] = window.InputPortsString;
                    Parameters["OutputPorts"] = window.OutputPortsString;

                    SyncPortsFromParameters();
                    onCompleted?.Invoke();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CustomScriptNode] Error executing custom action '{actionId}': {ex}");
        }
    }
}
