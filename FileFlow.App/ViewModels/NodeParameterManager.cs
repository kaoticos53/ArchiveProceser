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

        var descriptors = _nodeInstance.ParameterDescriptors;
        if (descriptors != null && descriptors.Count > 0)
        {
            var orderedDescriptors = descriptors.OrderBy(d => d.DisplayOrder).ToList();
            foreach (var desc in orderedDescriptors)
            {
                object? val = desc.DefaultValue;
                if (_nodeInstance.Parameters.TryGetValue(desc.Key, out var existingVal) && existingVal != null)
                {
                    val = existingVal;
                }
                else
                {
                    _nodeInstance.Parameters[desc.Key] = desc.DefaultValue;
                }

                Parameters.Add(new NodeParameterViewModel(desc, val, nodeOwner: _nodeOwner));
            }
            UpdateVisibilityConditions();
            return;
        }

        // Fallback genérico para nodos sin descriptores explícitos
        foreach (var (k, v) in _nodeInstance.Parameters)
        {
            if (isSwitch && k.Equals("CasesJson", StringComparison.OrdinalIgnoreCase))
                continue;

            // Claves legadas o internas de renombrado que se gestionan vía Pipeline de Métodos
            if (k.Equals("Pattern", StringComparison.OrdinalIgnoreCase) ||
                k.Equals("NameTemplate", StringComparison.OrdinalIgnoreCase) ||
                k.Equals("CaseTransformation", StringComparison.OrdinalIgnoreCase) ||
                k.Equals("MethodSteps", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Parameters.Add(new NodeParameterViewModel(k, v, nodeOwner: _nodeOwner));
        }
        UpdateVisibilityConditions();
    }

    public void UpdateVisibilityConditions()
    {
        foreach (var param in Parameters)
        {
            if (param.Descriptor == null || string.IsNullOrWhiteSpace(param.Descriptor.DependsOnKey))
            {
                param.IsVisible = true;
                continue;
            }

            string targetKey = param.Descriptor.DependsOnKey;
            var targetParam = Parameters.FirstOrDefault(p => p.Key.Equals(targetKey, StringComparison.OrdinalIgnoreCase));
            string? currentValStr = targetParam?.Value?.ToString();

            if (param.Descriptor.DependsOnValues != null && param.Descriptor.DependsOnValues.Count > 0)
            {
                param.IsVisible = !string.IsNullOrEmpty(currentValStr) &&
                                  param.Descriptor.DependsOnValues.Any(v => v.Equals(currentValStr, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                param.IsVisible = !string.IsNullOrWhiteSpace(currentValStr) && !currentValStr.Equals("false", StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    public void OnParameterValueChanged(string key, object? value)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        lock (_nodeInstance.Parameters)
        {
            _nodeInstance.Parameters[key] = value;
        }
        UpdateVisibilityConditions();
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

        Parameters.Add(new NodeParameterViewModel(new NodeParameterDescriptor(key, ParameterEditorType.Text, DefaultValue: val), val, nodeOwner: _nodeOwner));
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

    public void Dispose()
    {
        foreach (var p in Parameters)
        {
            p.Dispose();
        }
        Parameters.Clear();
    }
}
