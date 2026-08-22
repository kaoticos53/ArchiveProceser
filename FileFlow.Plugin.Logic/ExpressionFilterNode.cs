using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Plugin.Logic;

[NodeDefinition("ExpressionFilterNode_Name", "Logic", "ExpressionFilterNode_Desc")]
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

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string prop = Parameters.TryGetValue("Property", out var pVal) ? ParameterHelper.GetString(pVal, "SizeMB") : "SizeMB";
        string op = Parameters.TryGetValue("Operator", out var oVal) ? ParameterHelper.GetString(oVal, ">") : ">";
        string compVal = Parameters.TryGetValue("ComparisonValue", out var cVal) ? ParameterHelper.GetString(cVal, "10") : "10";

        bool result = EvaluateCondition(prop, op, compVal, item);

        item.AddLog($"ExpressionFilter ({prop} {op} {compVal}) evaluated to {result}");
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

        if (double.TryParse(actualValue, System.Globalization.CultureInfo.InvariantCulture, out double numActual) &&
            double.TryParse(compVal, System.Globalization.CultureInfo.InvariantCulture, out double numComp))
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

        return op switch
        {
            "==" or "=" => actualValue.Equals(compVal, StringComparison.OrdinalIgnoreCase),
            "!=" => !actualValue.Equals(compVal, StringComparison.OrdinalIgnoreCase),
            "Contains" => actualValue.Contains(compVal, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }
}
