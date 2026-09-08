using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Plugin.Logic;

[NodeDefinition("VersionRouterNode_Name", "Logic", "VersionRouterNode_Desc", PipelineRole.Filter,
    "router", "version", "enrutador", "condicion", "if", "branch", "autopurga", "desviar")]
public sealed class VersionRouterNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("VersionRouterNode_Name", "Enrutador de Versiones");
    public string Category => "Logic";
    public string Description => LocalizationManager.Instance.GetString("VersionRouterNode_Desc", "Evalúa una condición entre versiones o variables (ej. tamaño optimizado < tamaño original) y desvía el flujo por True o False activando el archivo deseado y autopurgando el descarte.");

    public IReadOnlyList<NodePort> Inputs { get; } = new[]
    {
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    };

    public IReadOnlyList<NodePort> Outputs { get; } = new[]
    {
        new NodePort("True", typeof(FileItemContext), PortDirection.Output, "True"),
        new NodePort("False", typeof(FileItemContext), PortDirection.Output, "False")
    };

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Property"] = "FileSize:Optimized",
        ["Operator"] = "<",
        ["ComparisonValue"] = "{FileSize:Original}",
        ["TrueFile"] = "{CurrentPath}",
        ["FalseFile"] = "{OriginalPath}",
        ["PurgeUnselectedTemps"] = true
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => [
        new("Property", ParameterEditorType.Text, DefaultValue: "FileSize:Optimized", DisplayOrder: 1, HelpText: "Propiedad o variable a evaluar"),
        new("Operator", ParameterEditorType.Dropdown, DefaultValue: "<", DisplayOrder: 2, Options: ["<", "<=", ">", ">=", "==", "!=", "Contains", "StartsWith", "EndsWith"], HelpText: "Operador de comparación"),
        new("ComparisonValue", ParameterEditorType.Text, DefaultValue: "{FileSize:Original}", DisplayOrder: 3, HelpText: "Valor objetivo o variable con la que comparar"),
        new("TrueFile", ParameterEditorType.FileVersionSelector, DefaultValue: "{CurrentPath}", DisplayOrder: 4, HelpText: "Versión del archivo a activar si la condición es Verdadera (True)"),
        new("FalseFile", ParameterEditorType.FileVersionSelector, DefaultValue: "{OriginalPath}", DisplayOrder: 5, HelpText: "Versión del archivo a activar si la condición es Falsa (False)"),
        new("PurgeUnselectedTemps", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 6, HelpText: "Autopurga el archivo intermedio de la rama descartada si no es el archivo original")
    ];

    private static readonly Regex NumericRegex = new(@"[-+]?\d+(?:[\.,]\d+)?", RegexOptions.Compiled);

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string prop = Parameters.TryGetValue("Property", out var pVal) ? ParameterHelper.GetString(pVal, "FileSize:Optimized") : "FileSize:Optimized";
        string op = Parameters.TryGetValue("Operator", out var oVal) ? ParameterHelper.GetString(oVal, "<") : "<";
        string compValPattern = Parameters.TryGetValue("ComparisonValue", out var cVal) ? ParameterHelper.GetString(cVal, "{FileSize:Original}") : "{FileSize:Original}";
        string trueFilePattern = Parameters.TryGetValue("TrueFile", out var tf) ? ParameterHelper.GetString(tf, "{CurrentPath}") : "{CurrentPath}";
        string falseFilePattern = Parameters.TryGetValue("FalseFile", out var ff) ? ParameterHelper.GetString(ff, "{OriginalPath}") : "{OriginalPath}";
        bool purgeUnselectedTemps = Parameters.TryGetValue("PurgeUnselectedTemps", out var pu) ? ParameterHelper.GetBoolean(pu, true) : true;

        string actualValue = VariableTemplateResolver.GetVariableValue(prop, item, null);
        string comparisonValue = VariableTemplateResolver.Resolve(compValPattern, item);

        bool result = EvaluateCondition(actualValue, op, comparisonValue);
        string outcomePort = result ? "True" : "False";
        string activePattern = result ? trueFilePattern : falseFilePattern;
        string unselectedPattern = result ? falseFilePattern : trueFilePattern;

        string activePath = ResolveVersionOrPath(activePattern, item);
        string unselectedPath = ResolveVersionOrPath(unselectedPattern, item);

        if (purgeUnselectedTemps && !string.IsNullOrWhiteSpace(unselectedPath) && File.Exists(unselectedPath))
        {
            if (!string.Equals(Path.GetFullPath(unselectedPath), Path.GetFullPath(item.OriginalPath), StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    File.Delete(unselectedPath);
                    context.Log($"[VersionRouter] Auto-purged unselected intermediate file: '{unselectedPath}'", LogLevel.Debug, item);
                }
                catch (Exception ex)
                {
                    context.Log($"[VersionRouter] Could not auto-purge unselected intermediate file '{unselectedPath}': {ex.Message}", LogLevel.Warning, item);
                }
            }
            else
            {
                context.Log($"[VersionRouter] Preserved unselected file because it is the original file: '{unselectedPath}'", LogLevel.Debug, item);
            }
        }

        if (!string.IsNullOrWhiteSpace(activePath))
        {
            item.CurrentPath = activePath;
            item.PhysicalPath = activePath;
            if (File.Exists(activePath))
            {
                item.FileSizeBytes = new FileInfo(activePath).Length;
            }
        }

        item.Metadata["RouterConditionResult"] = result;
        item.Metadata["RouterActiveFile"] = activePath;
        item.AddLog($"[VersionRouter] '{prop}' ({actualValue}) {op} '{compValPattern}' ({comparisonValue}) -> {result} (Active: '{activePath}')");

        string detailsJson = $"{{\"property\": \"{prop}\", \"operator\": \"{op}\", \"targetValue\": \"{comparisonValue}\", \"actualValue\": \"{actualValue}\", \"result\": {result.ToString().ToLowerInvariant()}, \"activePath\": \"{activePath.Replace("\\", "\\\\")}\"}}";
        context.Log($"[VersionRouter] Condition '{prop} {op} {comparisonValue}' evaluated to {result.ToString().ToUpperInvariant()} -> Port '{outcomePort}'", LogLevel.Information, item, detailsJson: detailsJson);

        await context.EmitAsync(outcomePort, item).ConfigureAwait(false);
    }

    private static string ResolveVersionOrPath(string input, FileItemContext item)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return item.CurrentPath;
        }

        string resolved = VariableTemplateResolver.Resolve(input, item);
        if (File.Exists(resolved))
        {
            return resolved;
        }

        string? versionPath = item.GetVersionPath(input);
        if (!string.IsNullOrWhiteSpace(versionPath) && File.Exists(versionPath))
        {
            return versionPath;
        }

        return resolved;
    }

    private static bool EvaluateCondition(string actualStr, string op, string compStr)
    {
        if (TryExtractDouble(actualStr, out double actualNum) && TryExtractDouble(compStr, out double compNum))
        {
            return op switch
            {
                ">" => actualNum > compNum,
                ">=" => actualNum >= compNum,
                "<" => actualNum < compNum,
                "<=" => actualNum <= compNum,
                "==" => Math.Abs(actualNum - compNum) < 0.000001,
                "!=" => Math.Abs(actualNum - compNum) >= 0.000001,
                _ => false
            };
        }

        return op switch
        {
            "==" => string.Equals(actualStr, compStr, StringComparison.OrdinalIgnoreCase),
            "!=" => !string.Equals(actualStr, compStr, StringComparison.OrdinalIgnoreCase),
            "Contains" => actualStr.Contains(compStr, StringComparison.OrdinalIgnoreCase),
            "StartsWith" => actualStr.StartsWith(compStr, StringComparison.OrdinalIgnoreCase),
            "EndsWith" => actualStr.EndsWith(compStr, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static bool TryExtractDouble(string input, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var match = NumericRegex.Match(input);
        if (!match.Success) return false;

        string normalized = match.Value.Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }
}
