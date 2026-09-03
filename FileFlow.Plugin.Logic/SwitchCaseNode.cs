using System.Text.Json;
using System.Text.RegularExpressions;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Plugin.Logic;

public record SwitchCaseRule(string Name, string Pattern);

[NodeDefinition("SwitchCaseNode_Name", "Logic", "SwitchCaseNode_Desc", PipelineRole.Filter,
    "switch", "case", "bifurcacion", "enrutar", "multiples", "router", "branch", "logica")]
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

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => [
        new("Expression", ParameterEditorType.Text, DefaultValue: "{Ext}", DisplayOrder: 1)
    ];

    public IReadOnlyList<NodeActionDescriptor> CustomActions => [
        new("AddSwitchCase", "➕ Caso", "➕", "Añadir nuevo caso / puerto de salida")
    ];

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

    private static readonly Regex NumericRegex = new(@"[-+]?\d+(?:[\.,]\d+)?", RegexOptions.Compiled);

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
                string detailsJson = $"{{\"expression\": \"{expr}\", \"evaluatedValue\": \"{evaluated}\", \"matchedCase\": \"{c.Name}\", \"pattern\": \"{c.Pattern}\"}}";
                context.Log($"[SwitchCase] Enrutado a '{c.Name}' (Valor '{evaluated}' coincidió con '{c.Pattern}')", LogLevel.Information, item, durationMs: 0.0, detailsJson: detailsJson);
                item.AddLog($"SwitchCase routed to {c.Name} (matched: {evaluated})");
                await context.EmitAsync(c.Name, item);
                return;
            }
        }

        string defaultDetails = $"{{\"expression\": \"{expr}\", \"evaluatedValue\": \"{evaluated}\", \"matchedCase\": \"Default\"}}";
        context.Log($"[SwitchCase] Enrutado a 'Default' (Valor '{evaluated}' no coincidió con ningún caso)", LogLevel.Information, item, durationMs: 0.0, detailsJson: defaultDetails);
        item.AddLog($"SwitchCase routed to Default ({evaluated})");
        await context.EmitAsync("Default", item);
    }

    private static bool MatchesPattern(string value, string patternList)
    {
        if (string.IsNullOrWhiteSpace(patternList)) return false;

        var tokens = patternList.Split([';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var tok in tokens)
        {
            if (EvaluateSinglePattern(value, tok))
            {
                return true;
            }
        }
        return false;
    }

    private static bool EvaluateSinglePattern(string value, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return false;
        string p = pattern.Trim();

        // 1. Range syntax: "10 MB..1 GB" or "100..500" or "2025-01-01..2025-12-31" or "10MB - 1GB"
        if (p.Contains("..") || (p.Contains(" - ") && !p.StartsWith("-")))
        {
            string[] rangeParts = p.Contains("..")
                ? p.Split("..", 2, StringSplitOptions.TrimEntries)
                : p.Split(" - ", 2, StringSplitOptions.TrimEntries);

            if (rangeParts.Length == 2)
            {
                // Date Range comparison
                if (DateTime.TryParse(value, out DateTime valDate) &&
                    DateTime.TryParse(rangeParts[0], out DateTime startDate) &&
                    DateTime.TryParse(rangeParts[1], out DateTime endDate))
                {
                    return valDate >= startDate && valDate <= endDate;
                }

                // Smart Numeric & Size Range comparison
                double targetMult = DetectUnitMultiplier(rangeParts[0]);
                if (targetMult == 1.0) targetMult = DetectUnitMultiplier(rangeParts[1]);

                if (TryParseSmartNumeric(value, out double valNum, targetMult) &&
                    TryParseSmartNumeric(rangeParts[0], out double startNum) &&
                    TryParseSmartNumeric(rangeParts[1], out double endNum))
                {
                    return valNum >= startNum && valNum <= endNum;
                }
            }
        }

        // 2. Numeric / Size Operator comparison: "< 10 MB", ">= 1 GB", "<= 500", "!= 0"
        if (p.StartsWith("<=") || p.StartsWith(">=") || p.StartsWith("==") || p.StartsWith("!=") || p.StartsWith('<') || p.StartsWith('>'))
        {
            string op;
            string targetStr;
            if (p.StartsWith("<=") || p.StartsWith(">=") || p.StartsWith("==") || p.StartsWith("!="))
            {
                op = p[..2];
                targetStr = p[2..].Trim();
            }
            else
            {
                op = p[..1];
                targetStr = p[1..].Trim();
            }

            // Date comparison
            if (DateTime.TryParse(value, out DateTime valDate) && DateTime.TryParse(targetStr, out DateTime targetDate))
            {
                return op switch
                {
                    "<" => valDate < targetDate,
                    "<=" => valDate <= targetDate,
                    ">" => valDate > targetDate,
                    ">=" => valDate >= targetDate,
                    "==" => valDate == targetDate,
                    "!=" => valDate != targetDate,
                    _ => false
                };
            }

            // Smart Numeric & Size comparison
            double targetMult = DetectUnitMultiplier(targetStr);
            if (TryParseSmartNumeric(value, out double valNum, targetMult) && TryParseSmartNumeric(targetStr, out double targetNum))
            {
                return op switch
                {
                    "<" => valNum < targetNum,
                    "<=" => valNum <= targetNum,
                    ">" => valNum > targetNum,
                    ">=" => valNum >= targetNum,
                    "==" => Math.Abs(valNum - targetNum) < 0.0001,
                    "!=" => Math.Abs(valNum - targetNum) >= 0.0001,
                    _ => false
                };
            }
        }

        // 3. Extension / String equality check (legacy & text match)
        if (p.StartsWith('.') && !value.StartsWith('.'))
        {
            if (("." + value).Equals(p, StringComparison.OrdinalIgnoreCase)) return true;
        }
        if (value.Equals(p, StringComparison.OrdinalIgnoreCase)) return true;

        if (p.Contains(','))
        {
            var subTokens = p.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var sub in subTokens)
            {
                if (sub.StartsWith('.') && !value.StartsWith('.'))
                {
                    if (("." + value).Equals(sub, StringComparison.OrdinalIgnoreCase)) return true;
                }
                if (value.Equals(sub, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }

        return false;
    }

    private static double DetectUnitMultiplier(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 1.0;
        string t = text.Trim();

        if (t.EndsWith("TB", StringComparison.OrdinalIgnoreCase)) return 1024.0 * 1024.0 * 1024.0 * 1024.0;
        if (t.EndsWith("GB", StringComparison.OrdinalIgnoreCase)) return 1024.0 * 1024.0 * 1024.0;
        if (t.EndsWith("MB", StringComparison.OrdinalIgnoreCase)) return 1024.0 * 1024.0;
        if (t.EndsWith("KB", StringComparison.OrdinalIgnoreCase)) return 1024.0;
        if (t.EndsWith("Bytes", StringComparison.OrdinalIgnoreCase) || t.EndsWith("B", StringComparison.OrdinalIgnoreCase)) return 1.0;
        return 1.0;
    }

    private static bool TryParseSmartNumeric(string text, out double value, double defaultMultiplier = 1.0)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string t = text.Trim();

        double multiplier = 1.0;
        bool hasExplicitUnit = false;

        if (t.EndsWith("TB", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1024.0 * 1024.0 * 1024.0 * 1024.0;
            hasExplicitUnit = true;
        }
        else if (t.EndsWith("GB", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1024.0 * 1024.0 * 1024.0;
            hasExplicitUnit = true;
        }
        else if (t.EndsWith("MB", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1024.0 * 1024.0;
            hasExplicitUnit = true;
        }
        else if (t.EndsWith("KB", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1024.0;
            hasExplicitUnit = true;
        }
        else if (t.EndsWith("Bytes", StringComparison.OrdinalIgnoreCase) || t.EndsWith("B", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1.0;
            hasExplicitUnit = true;
        }

        var match = NumericRegex.Match(t);
        if (!match.Success) return false;

        string numStr = match.Value.Replace(',', '.');
        if (double.TryParse(numStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsed))
        {
            if (!hasExplicitUnit && parsed < 10000.0 && defaultMultiplier > 1.0)
            {
                multiplier = defaultMultiplier;
            }

            value = parsed * multiplier;
            return true;
        }

        return false;
    }
}

