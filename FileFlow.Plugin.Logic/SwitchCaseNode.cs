using System.Text.Json;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Plugin.Logic;

public record SwitchCaseRule(string Name, string Pattern);

[NodeDefinition("SwitchCaseNode_Name", "Logic", "SwitchCaseNode_Desc")]
public class SwitchCaseNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("SwitchCaseNode_Name", "Enrutador Condicional (Switch / Case)");
    public string Category => "Logic";
    public string Description => LocalizationManager.Instance.GetString("SwitchCaseNode_Desc", "Evalúa una propiedad o extensión del archivo y lo enruta dinámicamente hacia uno de varios puertos de salida (Case 1, 2, 3 o Default) según listas de patrones configurables.");

    public IReadOnlyList<NodePort> Inputs { get; } = new[]
    {
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    };

    public IReadOnlyList<NodePort> Outputs
    {
        get
        {
            var cases = GetCases();
            var list = new List<NodePort>();
            foreach (var c in cases)
            {
                list.Add(new NodePort(c.Name, typeof(FileItemContext), PortDirection.Output, c.Name));
            }
            list.Add(new NodePort("Default", typeof(FileItemContext), PortDirection.Output, "Default"));
            return list;
        }
    }

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Expression"] = "{Ext}",
        ["CasesJson"] = "[{\"Name\":\"Case 1\",\"Pattern\":\"jpg;jpeg;png;webp;gif\"}]"
    };

    public List<SwitchCaseRule> GetCases()
    {
        if (Parameters.TryGetValue("CasesJson", out var jsonVal) && jsonVal != null)
        {
            string jsonStr = jsonVal.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(jsonStr))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<List<SwitchCaseRule>>(jsonStr);
                    if (parsed != null && parsed.Count > 0)
                    {
                        return parsed;
                    }
                }
                catch { }
            }
        }

        // Check legacy parameters if any
        var legacy = new List<SwitchCaseRule>();
        if (Parameters.TryGetValue("Case1Pattern", out var c1) && !string.IsNullOrWhiteSpace(c1?.ToString()))
            legacy.Add(new SwitchCaseRule("Case 1", c1.ToString()!));
        if (Parameters.TryGetValue("Case2Pattern", out var c2) && !string.IsNullOrWhiteSpace(c2?.ToString()))
            legacy.Add(new SwitchCaseRule("Case 2", c2.ToString()!));
        if (Parameters.TryGetValue("Case3Pattern", out var c3) && !string.IsNullOrWhiteSpace(c3?.ToString()))
            legacy.Add(new SwitchCaseRule("Case 3", c3.ToString()!));

        if (legacy.Count > 0) return legacy;

        // Default 1 case
        return [new SwitchCaseRule("Case 1", "jpg;jpeg;png;webp;gif")];
    }

    public void SetCases(IEnumerable<SwitchCaseRule> cases)
    {
        var list = cases.ToList();
        Parameters["CasesJson"] = JsonSerializer.Serialize(list);
    }

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string expr = Parameters.TryGetValue("Expression", out var eVal) ? ParameterHelper.GetString(eVal, "{Ext}") : "{Ext}";
        string evaluated = VariableTemplateResolver.Resolve(expr, item).Trim();

        var cases = GetCases();
        foreach (var c in cases)
        {
            if (MatchesPattern(evaluated, c.Pattern))
            {
                item.AddLog($"SwitchCase routed to {c.Name} (matched: {evaluated})");
                await context.EmitAsync(c.Name, item);
                return;
            }
        }

        item.AddLog($"SwitchCase routed to Default ({evaluated})");
        await context.EmitAsync("Default", item);
    }

    private static bool MatchesPattern(string value, string patternList)
    {
        if (string.IsNullOrWhiteSpace(patternList)) return false;
        var tokens = patternList.Split([';', '|', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var tok in tokens)
        {
            if (tok.StartsWith('.') && !value.StartsWith('.'))
            {
                if (("." + value).Equals(tok, StringComparison.OrdinalIgnoreCase)) return true;
            }
            if (value.Equals(tok, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}

