using System.Buffers;
using System.Globalization;
using System.Text.RegularExpressions;
using FileFlow.Sdk.TemplateEngine.Functions;

namespace FileFlow.Sdk.TemplateEngine;

public static class VariableTemplateResolver
{
    private static readonly Regex FunctionRegex = new(@"^(?<fn>\w+)\((?<args>.*)\)$", RegexOptions.Compiled);
    private static readonly SearchValues<char> OpenBraceSearch = SearchValues.Create(['{']);

    private static readonly List<ITemplateFunctionEvaluator> FunctionEvaluators = new()
    {
        new StringFunctionsEvaluator(),
        new DateTimeFunctionsEvaluator()
    };

    public static string Resolve(string template, FileItemContext item, string? sourceRootPath = null)
    {
        if (string.IsNullOrEmpty(template) || !template.AsSpan().ContainsAny(OpenBraceSearch))
        {
            return template;
        }

        var sb = new System.Text.StringBuilder(template.Length + 32);
        int i = 0;
        while (i < template.Length)
        {
            int openIdx = template.IndexOf('{', i);
            if (openIdx < 0)
            {
                sb.Append(template, i, template.Length - i);
                break;
            }

            int closeIdx = template.IndexOf('}', openIdx + 1);
            if (closeIdx < 0)
            {
                sb.Append(template, i, template.Length - i);
                break;
            }

            sb.Append(template, i, openIdx - i);

            string expr = template.Substring(openIdx + 1, closeIdx - openIdx - 1).Trim();
            string resolved = EvaluateExpression(expr, item, sourceRootPath);
            sb.Append(resolved);

            i = closeIdx + 1;
        }

        return sb.ToString();
    }

    private static string EvaluateExpression(string expr, FileItemContext item, string? sourceRootPath)
    {
        var fnMatch = FunctionRegex.Match(expr);
        if (fnMatch.Success)
        {
            string fnName = fnMatch.Groups["fn"].Value;
            string argsStr = fnMatch.Groups["args"].Value;

            var args = ParseArguments(argsStr, item, sourceRootPath);
            return ExecuteFunction(fnName, args, item, sourceRootPath);
        }

        return GetVariableValue(expr, item, sourceRootPath);
    }

    public static string GetVariableValue(string varName, FileItemContext item, string? sourceRootPath)
    {
        return SystemVariablesResolver.GetVariableValue(varName, item, sourceRootPath);
    }

    private static List<string> ParseArguments(string argsStr, FileItemContext item, string? sourceRootPath)
    {
        if (string.IsNullOrWhiteSpace(argsStr)) return [];

        var result = new List<string>();
        var parts = SplitArguments(argsStr);
        foreach (var p in parts)
        {
            string trimmed = p.Trim();
            if (trimmed.Length >= 2 && ((trimmed.StartsWith('"') && trimmed.EndsWith('"')) || (trimmed.StartsWith('\'') && trimmed.EndsWith('\''))))
            {
                result.Add(trimmed[1..^1]);
            }
            else if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ||
                     double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                result.Add(trimmed);
            }
            else
            {
                string token = (trimmed.StartsWith('{') && trimmed.EndsWith('}')) ? trimmed[1..^1] : trimmed;
                result.Add(GetVariableValue(token, item, sourceRootPath));
            }
        }
        return result;
    }

    private static List<string> SplitArguments(string argsStr)
    {
        var list = new List<string>();
        var sb = new System.Text.StringBuilder();
        bool inQuotes = false;
        char quoteChar = '\0';

        foreach (char c in argsStr)
        {
            if ((c == '"' || c == '\'') && !inQuotes)
            {
                inQuotes = true;
                quoteChar = c;
                sb.Append(c);
            }
            else if (c == quoteChar && inQuotes)
            {
                inQuotes = false;
                sb.Append(c);
            }
            else if (c == ',' && !inQuotes)
            {
                list.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }
        if (sb.Length > 0)
        {
            list.Add(sb.ToString());
        }
        return list;
    }

    private static string ExecuteFunction(string fnName, List<string> args, FileItemContext item, string? sourceRootPath)
    {
        foreach (var evaluator in FunctionEvaluators)
        {
            if (evaluator.CanEvaluate(fnName))
            {
                return evaluator.Evaluate(fnName, args, item, sourceRootPath);
            }
        }

        return args.Count > 0 ? args[0] : string.Empty;
    }
}
