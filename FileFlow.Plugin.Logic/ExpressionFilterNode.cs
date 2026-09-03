using System.Text.RegularExpressions;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Plugin.Logic;

[NodeDefinition("ExpressionFilterNode_Name", "Logic", "ExpressionFilterNode_Desc", PipelineRole.Filter,
    "filtro", "condicion", "if", "regex", "comparar", "igual", "mayor", "filter", "logica")]
public class ExpressionFilterNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("ExpressionFilterNode_Name", "Filtro por Condición Lógica");
    public string Category => "Logic";
    public string Description => LocalizationManager.Instance.GetString("ExpressionFilterNode_Desc", "Evalúa condiciones numéricas o de texto sobre propiedades del archivo (ej. tamaño en MB, extensión, fecha, tags) y desvía el flujo por los puertos True o False.");


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
        ["Property"] = "SizeMB", // SizeMB, Extension, DaysOld, Tag
        ["Operator"] = ">", // >, <, ==, !=, Contains
        ["ComparisonValue"] = "10"
    };

    private static readonly Regex NumericRegex = new(@"[-+]?\d+(?:[\.,]\d+)?", RegexOptions.Compiled);

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string prop = Parameters.TryGetValue("Property", out var pVal) ? ParameterHelper.GetString(pVal, "SizeMB") : "SizeMB";
        string op = Parameters.TryGetValue("Operator", out var oVal) ? ParameterHelper.GetString(oVal, ">") : ">";
        string compVal = Parameters.TryGetValue("ComparisonValue", out var cVal) ? ParameterHelper.GetString(cVal, "10") : "10";

        string actualValue = VariableTemplateResolver.GetVariableValue(prop, item, null);
        bool result = EvaluateCondition(prop, op, compVal, item);

        string outcomePort = result ? "True" : "False";
        string detailsJson = $"{{\"property\": \"{prop}\", \"operator\": \"{op}\", \"targetValue\": \"{compVal}\", \"actualValue\": \"{actualValue}\", \"result\": {result.ToString().ToLowerInvariant()}}}";

        context.Log($"[Filtro Condicional] Condición '{prop} {op} {compVal}' evaluada como {result.ToString().ToUpperInvariant()} (Valor actual: '{actualValue}') -> Rama '{outcomePort}'", LogLevel.Information, item, durationMs: 0.0, detailsJson: detailsJson);

        item.AddLog($"ExpressionFilter ({prop} {op} {compVal} -> '{actualValue}') evaluated to {result}");
        if (result)
        {
            await context.EmitAsync("True", item);
        }
        else
        {
            await context.EmitAsync("False", item);
        }
    }

    private static bool EvaluateCondition(string prop, string op, string compVal, FileItemContext item)
    {
        string actualValue = VariableTemplateResolver.GetVariableValue(prop, item, null);

        if (op is ">" or ">=" or "<" or "<=" or "==" or "=" or "!=")
        {
            if (TryParseSmartNumeric(actualValue, out double numActual) &&
                TryParseSmartNumeric(compVal, out double numComp))
            {
                return op switch
                {
                    ">" => numActual > numComp,
                    ">=" => numActual >= numComp,
                    "<" => numActual < numComp,
                    "<=" => numActual <= numComp,
                    "==" or "=" => Math.Abs(numActual - numComp) < 0.0001,
                    "!=" => Math.Abs(numActual - numComp) >= 0.0001,
                    _ => false
                };
            }
        }

        return op switch
        {
            "==" or "=" => actualValue.Equals(compVal, StringComparison.OrdinalIgnoreCase),
            "!=" => !actualValue.Equals(compVal, StringComparison.OrdinalIgnoreCase),
            "Contains" => actualValue.Contains(compVal, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static bool TryParseSmartNumeric(string text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string t = text.Trim();

        double multiplier = 1.0;
        if (t.EndsWith("TB", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1024.0 * 1024.0 * 1024.0 * 1024.0;
        }
        else if (t.EndsWith("GB", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1024.0 * 1024.0 * 1024.0;
        }
        else if (t.EndsWith("MB", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1024.0 * 1024.0;
        }
        else if (t.EndsWith("KB", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1024.0;
        }
        else if (t.EndsWith("Bytes", StringComparison.OrdinalIgnoreCase) || t.EndsWith("B", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1.0;
        }

        var match = NumericRegex.Match(t);
        if (!match.Success) return false;

        string numStr = match.Value.Replace(',', '.');
        if (double.TryParse(numStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsed))
        {
            value = parsed * multiplier;
            return true;
        }

        return false;
    }
}
