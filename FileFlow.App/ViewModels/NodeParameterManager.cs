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
        bool isRenamer = _nodeOwner.IsAdvancedRenamerNode;

        if (isRenamer)
        {
            // Migrar y limpiar parámetros legados (Pattern, NameTemplate, CaseTransformation)
            string legacyPattern = string.Empty;
            if (_nodeInstance.Parameters.TryGetValue("Pattern", out var pVal) && pVal != null && !string.IsNullOrWhiteSpace(pVal.ToString()))
            {
                legacyPattern = pVal.ToString()!;
                _nodeInstance.Parameters.Remove("Pattern");
            }
            else if (_nodeInstance.Parameters.TryGetValue("NameTemplate", out var ntVal) && ntVal != null && !string.IsNullOrWhiteSpace(ntVal.ToString()))
            {
                legacyPattern = ntVal.ToString()!;
                _nodeInstance.Parameters.Remove("NameTemplate");
            }

            string legacyCase = string.Empty;
            if (_nodeInstance.Parameters.TryGetValue("CaseTransformation", out var ctVal) && ctVal != null)
            {
                legacyCase = ctVal.ToString()!;
                _nodeInstance.Parameters.Remove("CaseTransformation");
            }

            // Si hay un patrón legado y MethodSteps está vacío, construir paso inicial
            bool hasSteps = _nodeInstance.Parameters.TryGetValue("MethodSteps", out var msVal) &&
                            msVal != null &&
                            !string.IsNullOrWhiteSpace(msVal.ToString());

            if (!hasSteps && !string.IsNullOrWhiteSpace(legacyPattern))
            {
                var steps = new List<FileFlow.Sdk.Renaming.RenameMethodStep>
                {
                    new()
                    {
                        MethodType = FileFlow.Sdk.Renaming.RenameMethodType.NewName,
                        ApplyTo = FileFlow.Sdk.Renaming.ApplyToTarget.FullName,
                        Pattern = legacyPattern,
                        IsEnabled = true,
                        Name = "Plantilla Inicial"
                    }
                };

                if (!string.IsNullOrWhiteSpace(legacyCase) &&
                    !legacyCase.Equals("None", StringComparison.OrdinalIgnoreCase) &&
                    Enum.TryParse<FileFlow.Sdk.Renaming.CaseTransformType>(legacyCase, true, out var ct))
                {
                    steps.Add(new FileFlow.Sdk.Renaming.RenameMethodStep
                    {
                        MethodType = FileFlow.Sdk.Renaming.RenameMethodType.CaseConversion,
                        ApplyTo = FileFlow.Sdk.Renaming.ApplyToTarget.FullName,
                        CaseType = ct,
                        IsEnabled = true,
                        Name = "Transformación de Mayúsculas"
                    });
                }

                _nodeInstance.Parameters["MethodSteps"] = FileFlow.Sdk.Renaming.RenamerPresetService.SerializeSteps(steps);
            }

            // Asegurar que PipelineName existe y tiene un nombre por defecto no vacío
            if (!_nodeInstance.Parameters.TryGetValue("PipelineName", out var pnVal) ||
                pnVal == null ||
                string.IsNullOrWhiteSpace(pnVal.ToString()))
            {
                _nodeInstance.Parameters["PipelineName"] = "Pipeline Predeterminado";
            }

            // Asegurar que CollisionStrategy existe
            if (!_nodeInstance.Parameters.TryGetValue("CollisionStrategy", out var csVal) ||
                csVal == null ||
                string.IsNullOrWhiteSpace(csVal.ToString()))
            {
                _nodeInstance.Parameters["CollisionStrategy"] = "AutoIncrement";
            }
        }

        bool isImageOptimizer = _nodeOwner.NodeTypeName.Contains("ImageOptimizerNode", StringComparison.OrdinalIgnoreCase) ||
                                _nodeInstance.GetType().Name.Contains("ImageOptimizerNode", StringComparison.OrdinalIgnoreCase);

        if (isImageOptimizer)
        {
            // Migrar MaxWidth / MaxHeight legados
            if (_nodeInstance.Parameters.TryGetValue("MaxWidth", out var mwVal) && mwVal != null)
            {
                if (!_nodeInstance.Parameters.ContainsKey("Width"))
                {
                    _nodeInstance.Parameters["Width"] = mwVal;
                }
                _nodeInstance.Parameters.Remove("MaxWidth");
            }

            if (_nodeInstance.Parameters.TryGetValue("MaxHeight", out var mhVal) && mhVal != null)
            {
                if (!_nodeInstance.Parameters.ContainsKey("Height"))
                {
                    _nodeInstance.Parameters["Height"] = mhVal;
                }
                _nodeInstance.Parameters.Remove("MaxHeight");
            }

            // Asegurar que todos los nuevos parámetros existen con valores por defecto
            if (!_nodeInstance.Parameters.ContainsKey("SizeMode")) _nodeInstance.Parameters["SizeMode"] = "Pixels";
            if (!_nodeInstance.Parameters.ContainsKey("Width")) _nodeInstance.Parameters["Width"] = 1920;
            if (!_nodeInstance.Parameters.ContainsKey("Height")) _nodeInstance.Parameters["Height"] = 1080;
            if (!_nodeInstance.Parameters.ContainsKey("ScalePercentage")) _nodeInstance.Parameters["ScalePercentage"] = 100.0;
            if (!_nodeInstance.Parameters.ContainsKey("ScalePercentageY")) _nodeInstance.Parameters["ScalePercentageY"] = 100.0;
            if (!_nodeInstance.Parameters.ContainsKey("MaintainAspectRatio")) _nodeInstance.Parameters["MaintainAspectRatio"] = true;
            if (!_nodeInstance.Parameters.ContainsKey("OnlyDownscale")) _nodeInstance.Parameters["OnlyDownscale"] = true;
            if (!_nodeInstance.Parameters.ContainsKey("TargetFormat")) _nodeInstance.Parameters["TargetFormat"] = "WebP";
            if (!_nodeInstance.Parameters.ContainsKey("Quality")) _nodeInstance.Parameters["Quality"] = 80;
            if (!_nodeInstance.Parameters.ContainsKey("OutputDirectory")) _nodeInstance.Parameters["OutputDirectory"] = @"{RelativeDir}\OptimizedImages";
        }

        foreach (var (k, v) in _nodeInstance.Parameters)
        {
            if (isSwitch && k.Equals("CasesJson", StringComparison.OrdinalIgnoreCase))
                continue;
            if (isRenamer && (k.Equals("MethodSteps", StringComparison.OrdinalIgnoreCase) ||
                              k.Equals("Pattern", StringComparison.OrdinalIgnoreCase) ||
                              k.Equals("NameTemplate", StringComparison.OrdinalIgnoreCase) ||
                              k.Equals("CaseTransformation", StringComparison.OrdinalIgnoreCase)))
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
