using System.Collections.ObjectModel;
using FileFlow.Sdk;

namespace FileFlow.App.ViewModels;

public sealed class NodeParameterManager
{
    private readonly IFlowNode _nodeInstance;
    private readonly NodeViewModel _nodeOwner;

    public ObservableCollection<NodeParameterViewModel> Parameters { get; } = [];

    public NodeParameterManager(IFlowNode nodeInstance, NodeViewModel nodeOwner)
    {
        _nodeInstance = nodeInstance;
        _nodeOwner = nodeOwner;
        InitializeParameters();
    }

    private void InitializeParameters()
    {
        bool isSwitch = _nodeOwner.IsSwitchCaseNode;

        foreach (var (k, v) in _nodeInstance.Parameters)
        {
            if (isSwitch && k.Equals("CasesJson", StringComparison.OrdinalIgnoreCase))
                continue;
            Parameters.Add(new NodeParameterViewModel(k, v, nodeOwner: _nodeOwner));
        }
    }

    public void OnParameterValueChanged(string key, object? value)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        lock (_nodeInstance.Parameters)
        {
            _nodeInstance.Parameters[key] = value;
        }

        if (_nodeOwner.NodeTypeName.Contains("MediaTranscoderNode", StringComparison.OrdinalIgnoreCase) &&
            key.Equals("Preset", StringComparison.OrdinalIgnoreCase) && value != null)
        {
            UpdateMediaPresetArguments(value.ToString()!);
        }
    }

    public void OnParameterKeyRenamed(string oldKey, string newKey, object? value)
    {
        lock (_nodeInstance.Parameters)
        {
            if (!string.IsNullOrWhiteSpace(oldKey) && _nodeInstance.Parameters.ContainsKey(oldKey))
            {
                _nodeInstance.Parameters.Remove(oldKey);
            }
            if (!string.IsNullOrWhiteSpace(newKey))
            {
                _nodeInstance.Parameters[newKey] = value;
            }
        }
    }

    public void AddVariable()
    {
        int count = Parameters.Count(p => p.Key.StartsWith("Variable", StringComparison.OrdinalIgnoreCase)) + 1;
        string key = $"Var_{count}";
        string val = "Value";

        lock (_nodeInstance.Parameters)
        {
            _nodeInstance.Parameters[key] = val;
        }

        Parameters.Add(new NodeParameterViewModel(key, val, nodeOwner: _nodeOwner));
    }

    public void RemoveParameter(NodeParameterViewModel parameter)
    {
        if (parameter == null) return;
        lock (_nodeInstance.Parameters)
        {
            if (!string.IsNullOrWhiteSpace(parameter.Key) && _nodeInstance.Parameters.ContainsKey(parameter.Key))
            {
                _nodeInstance.Parameters.Remove(parameter.Key);
            }
        }

        parameter.Dispose();
        Parameters.Remove(parameter);
    }

    private void UpdateMediaPresetArguments(string presetName)
    {
        var preset = Services.MediaPresetManagerService.Instance.GetPresetByName(presetName);
        if (preset != null)
        {
            var customArgsParam = Parameters.FirstOrDefault(p => p.Key.Equals("CustomArguments", StringComparison.OrdinalIgnoreCase));
            if (customArgsParam != null)
            {
                customArgsParam.Value = preset.FfmpegArguments;
            }
            else
            {
                _nodeInstance.Parameters["CustomArguments"] = preset.FfmpegArguments;
                Parameters.Add(new NodeParameterViewModel("CustomArguments", preset.FfmpegArguments, nodeOwner: _nodeOwner));
            }
        }
    }

    public void Dispose()
    {
        foreach (var p in Parameters)
        {
            p.Dispose();
        }
        Parameters.Clear();
    }
}
