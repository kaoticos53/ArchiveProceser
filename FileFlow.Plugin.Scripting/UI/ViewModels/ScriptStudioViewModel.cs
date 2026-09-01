using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.Plugin.Scripting.Engines;
using FileFlow.Plugin.Scripting.Services;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.Scripting.UI.ViewModels;

public sealed partial class ScriptStudioViewModel : ObservableObject
{
    [ObservableProperty]
    private string _selectedLanguage = "CSharp";

    [ObservableProperty]
    private string _scriptCode = string.Empty;

    [ObservableProperty]
    private string _newInputPortName = string.Empty;

    [ObservableProperty]
    private string _newOutputPortName = string.Empty;

    [ObservableProperty]
    private ScriptDefinition? _selectedPreset;

    [ObservableProperty]
    private string _newPresetName = string.Empty;

    [ObservableProperty]
    private string _newPresetDescription = string.Empty;

    [ObservableProperty]
    private string _testFileName = "documento_test.pdf";

    [ObservableProperty]
    private long _testFileSizeBytes = 15_728_640; // 15 MB

    [ObservableProperty]
    private string _testStatus = LocalizationManager.Instance.GetString("ScriptStudio_ReadyToTest", "Listo para probar");

    public ObservableCollection<string> InputPorts { get; } = [];
    public ObservableCollection<string> OutputPorts { get; } = [];
    public ObservableCollection<ScriptDefinition> Presets { get; } = [];
    public ObservableCollection<string> TestLogs { get; } = [];
    public ObservableCollection<string> TestEmittedPorts { get; } = [];

    public ScriptStudioViewModel(string initialLanguage, string initialCode, string initialInputs, string initialOutputs)
    {
        SelectedLanguage = string.IsNullOrWhiteSpace(initialLanguage) ? "CSharp" : initialLanguage;
        ScriptCode = initialCode;

        var inputs = initialInputs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (inputs.Length == 0) inputs = ["In"];
        foreach (var p in inputs) InputPorts.Add(p);

        var outputs = initialOutputs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (outputs.Length == 0) outputs = ["Out"];
        foreach (var p in outputs) OutputPorts.Add(p);

        RefreshPresets();
    }

    public void RefreshPresets()
    {
        Presets.Clear();
        foreach (var script in ScriptLibraryService.Instance.GetAllScripts())
        {
            Presets.Add(script);
        }
    }

    [RelayCommand]
    private void AddInputPort()
    {
        if (string.IsNullOrWhiteSpace(NewInputPortName)) return;
        string name = NewInputPortName.Trim();
        if (!InputPorts.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            InputPorts.Add(name);
            NewInputPortName = string.Empty;
        }
    }

    [RelayCommand]
    private void RemoveInputPort(string portName)
    {
        if (InputPorts.Count > 1 && InputPorts.Contains(portName))
        {
            InputPorts.Remove(portName);
        }
    }

    [RelayCommand]
    private void AddOutputPort()
    {
        if (string.IsNullOrWhiteSpace(NewOutputPortName)) return;
        string name = NewOutputPortName.Trim();
        if (!OutputPorts.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            OutputPorts.Add(name);
            NewOutputPortName = string.Empty;
        }
    }

    [RelayCommand]
    private void RemoveOutputPort(string portName)
    {
        if (OutputPorts.Count > 1 && OutputPorts.Contains(portName))
        {
            OutputPorts.Remove(portName);
        }
    }

    [RelayCommand]
    private void LoadSelectedPreset()
    {
        if (SelectedPreset == null) return;
        SelectedLanguage = SelectedPreset.Language;
        ScriptCode = SelectedPreset.ScriptCode;

        InputPorts.Clear();
        foreach (var p in SelectedPreset.InputPorts) InputPorts.Add(p);

        OutputPorts.Clear();
        foreach (var p in SelectedPreset.OutputPorts) OutputPorts.Add(p);

        TestLogs.Add($"[Preset Cargado] '{SelectedPreset.Name}' ({SelectedPreset.Language})");
    }

    [RelayCommand]
    private void SaveCurrentAsPreset()
    {
        if (string.IsNullOrWhiteSpace(NewPresetName)) return;

        var newPreset = new ScriptDefinition
        {
            Name = NewPresetName.Trim(),
            Description = NewPresetDescription.Trim(),
            Language = SelectedLanguage,
            ScriptCode = ScriptCode,
            InputPorts = InputPorts.ToList(),
            OutputPorts = OutputPorts.ToList(),
            IsBuiltIn = false
        };

        ScriptLibraryService.Instance.SaveUserScript(newPreset);
        RefreshPresets();
        NewPresetName = string.Empty;
        NewPresetDescription = string.Empty;
        TestLogs.Add($"[Preset Guardado] '{newPreset.Name}'");
    }

    [RelayCommand]
    private async Task RunTestAsync()
    {
        TestLogs.Clear();
        TestEmittedPorts.Clear();
        TestStatus = LocalizationManager.Instance.GetString("ScriptStudio_Running", "Ejecutando...");

        var syntheticItem = new FileItemContext(Path.Combine(Path.GetTempPath(), TestFileName))
        {
            FileSizeBytes = TestFileSizeBytes
        };

        var mockFlowContext = new TestFlowExecutionContext((port, item) =>
        {
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                TestEmittedPorts.Add($"⚡ Emitido por '{port}' ({item.FileName})");
            });
        },
        (msg, lvl) =>
        {
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                TestLogs.Add($"[{lvl}] {msg}");
            });
        });

        var execContext = new ScriptExecutionContext
        {
            Item = syntheticItem,
            FlowContext = mockFlowContext,
            InputPortName = InputPorts.FirstOrDefault() ?? "In",
            CancellationToken = CancellationToken.None
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (SelectedLanguage.Equals("JavaScript", StringComparison.OrdinalIgnoreCase))
            {
                await JintJavaScriptEngine.Instance.ExecuteAsync(ScriptCode, execContext, CancellationToken.None);
            }
            else
            {
                await RoslynCSharpEngine.Instance.ExecuteAsync(ScriptCode, execContext, CancellationToken.None);
            }

            sw.Stop();
            string successMsg = LocalizationManager.Instance.GetString("ScriptStudio_Success", "Éxito");
            TestStatus = $"✓ {successMsg} ({sw.ElapsedMilliseconds} ms)";
            TestLogs.Add($"[Resultado] Ejecución completada en {sw.ElapsedMilliseconds} ms.");
            if (syntheticItem.Metadata.Count > 0)
            {
                TestLogs.Add($"[Metadatos Resultantes]:");
                foreach (var kvp in syntheticItem.Metadata)
                {
                    TestLogs.Add($"  • {kvp.Key} = {kvp.Value}");
                }
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            string errorMsg = LocalizationManager.Instance.GetString("ScriptStudio_Error", "Error");
            TestStatus = $"✗ {errorMsg} ({sw.ElapsedMilliseconds} ms)";
            TestLogs.Add($"[Error de Ejecución] {ex.Message}");
        }
    }

    private sealed class TestFlowExecutionContext(
        Action<string, FileItemContext> onEmit,
        Action<string, LogLevel> onLog) : IFlowExecutionContext
    {
        public bool IsDryRun => false;

        public Task EmitAsync(string outputPortName, FileItemContext item)
        {
            onEmit(outputPortName, item);
            return Task.CompletedTask;
        }

        public void ReportProgress(double percentage, string statusMessage)
        {
            onLog($"[Progreso {percentage:P0}] {statusMessage}", LogLevel.Information);
        }

        public void Log(string message, LogLevel level)
        {
            onLog(message, level);
        }

        public void RegisterPlannedAction(PlannedAction action) { }
        public void RecordJournalEntry(JournalEntry entry) { }
    }
}
