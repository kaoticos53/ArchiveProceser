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
        if (string.IsNullOrEmpty(template))
        {
            return template;
        }

        // Convertir sintaxis clásica de Advanced Renamer <Tag> a {Tag} para procesamiento unificado
        string normalizedTemplate = NormalizeTagSyntax(template);

        if (!normalizedTemplate.AsSpan().ContainsAny(OpenBraceSearch))
        {
            return normalizedTemplate;
        }

        var sb = new System.Text.StringBuilder(normalizedTemplate.Length + 32);
        int i = 0;
        while (i < normalizedTemplate.Length)
        {
            int openIdx = normalizedTemplate.IndexOf('{', i);
            if (openIdx < 0)
            {
                sb.Append(normalizedTemplate, i, normalizedTemplate.Length - i);
                break;
            }

            int closeIdx = FindMatchingClosingBrace(normalizedTemplate, openIdx);
            if (closeIdx < 0)
            {
                sb.Append(normalizedTemplate, i, normalizedTemplate.Length - i);
                break;
            }

            // Si la llave está precedida por '$' a nivel raíz (ej. ${1}, ${name}), incluir el '$' en el consumo
            if (openIdx > i && normalizedTemplate[openIdx - 1] == '$')
            {
                sb.Append(normalizedTemplate, i, openIdx - 1 - i);
            }
            else
            {
                sb.Append(normalizedTemplate, i, openIdx - i);
            }

            string expr = normalizedTemplate.Substring(openIdx + 1, closeIdx - openIdx - 1).Trim();
            string resolved = EvaluateExpression(expr, item, sourceRootPath);
            sb.Append(resolved);

            i = closeIdx + 1;
        }

        return sb.ToString();
    }

    private static int FindMatchingClosingBrace(string s, int openIndex)
    {
        int depth = 0;
        bool inQuotes = false;
        char quoteChar = '\0';

        for (int i = openIndex; i < s.Length; i++)
        {
            char c = s[i];
            if ((c == '"' || c == '\'') && !inQuotes)
            {
                inQuotes = true;
                quoteChar = c;
            }
            else if (c == quoteChar && inQuotes)
            {
                inQuotes = false;
            }
            else if (!inQuotes)
            {
                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return i;
                    }
                }
            }
        }

        return -1;
    }

    private static string NormalizeTagSyntax(string template)
    {
        if (!template.Contains('<') || !template.Contains('>'))
        {
            return template;
        }

        var sb = new System.Text.StringBuilder(template.Length);
        int i = 0;
        while (i < template.Length)
        {
            int openIdx = template.IndexOf('<', i);
            if (openIdx < 0)
            {
                sb.Append(template, i, template.Length - i);
                break;
            }

            int closeIdx = template.IndexOf('>', openIdx + 1);
            if (closeIdx < 0)
            {
                sb.Append(template, i, template.Length - i);
                break;
            }

            // Preservar construcciones sintácticas de Regex como (?<name> o \k<name> o (?<=
            bool isRegexConstruct = (openIdx >= 2 && template[openIdx - 1] == '?' && template[openIdx - 2] == '(') ||
                                    (openIdx >= 1 && template[openIdx - 1] == 'k' && (openIdx == 1 || template[openIdx - 2] == '\\')) ||
                                    (openIdx >= 2 && template[openIdx - 1] == '<' && template[openIdx - 2] == '?');

            if (isRegexConstruct)
            {
                sb.Append(template, i, closeIdx + 1 - i);
                i = closeIdx + 1;
                continue;
            }

            sb.Append(template, i, openIdx - i);
            string inner = template.Substring(openIdx + 1, closeIdx - openIdx - 1);
            sb.Append('{').Append(inner).Append('}');
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

    public static string ApplyRegexReplacement(Regex regex, string input, string rawReplacementTemplate, FileItemContext item, bool replaceAll = true)
    {
        if (string.IsNullOrEmpty(input)) return input;
        rawReplacementTemplate ??= string.Empty;

        string Evaluator(Match match)
        {
            if (!match.Success) return string.Empty;

            var tempKeys = new List<string>();
            try
            {
                item.Metadata["0"] = match.Value;
                item.Metadata["Match"] = match.Value;
                item.Metadata["Regex:0"] = match.Value;
                item.Metadata["Regex:Match"] = match.Value;
                tempKeys.Add("0");
                tempKeys.Add("Match");
                tempKeys.Add("Regex:0");
                tempKeys.Add("Regex:Match");

                for (int g = 1; g < match.Groups.Count; g++)
                {
                    var group = match.Groups[g];
                    string gIndex = g.ToString(CultureInfo.InvariantCulture);
                    item.Metadata[gIndex] = group.Value;
                    item.Metadata[$"Regex:{gIndex}"] = group.Value;
                    tempKeys.Add(gIndex);
                    tempKeys.Add($"Regex:{gIndex}");
                }

                foreach (var groupName in regex.GetGroupNames())
                {
                    if (groupName != "0" && !int.TryParse(groupName, out _))
                    {
                        var group = match.Groups[groupName];
                        if (group.Success)
                        {
                            item.Metadata[groupName] = group.Value;
                            item.Metadata[$"Regex:{groupName}"] = group.Value;
                            tempKeys.Add(groupName);
                            tempKeys.Add($"Regex:{groupName}");
                        }
                    }
                }

                // 1. Evaluar variables y funciones de plantilla ({Upper($1)}, {PadLeft($2, 3, 0)}, {ShowPrefix}, {Year})
                string resolvedTemplate = Resolve(rawReplacementTemplate, item);

                // 2. Sustituir coincidencias y grupos no envueltos en funciones ($0..$N, ${name}, <1>..<N>)
                return SubstituteRegexMatchGroups(resolvedTemplate, match, regex);
            }
            finally
            {
                foreach (var key in tempKeys)
                {
                    item.Metadata.Remove(key);
                }
            }
        }

        return replaceAll ? regex.Replace(input, Evaluator) : regex.Replace(input, Evaluator, 1);
    }

    public static string SubstituteRegexMatchGroups(string template, Match match, Regex? regex = null)
    {
        if (string.IsNullOrEmpty(template) || !match.Success) return template;

        string result = template;

        // Grupos nombrados (${name}, <name>)
        if (regex != null)
        {
            foreach (var groupName in regex.GetGroupNames())
            {
                if (groupName != "0" && !int.TryParse(groupName, out _))
                {
                    var grp = match.Groups[groupName];
                    if (grp.Success)
                    {
                        result = result.Replace($"${{{groupName}}}", grp.Value, StringComparison.Ordinal)
                                       .Replace($"<{groupName}>", grp.Value, StringComparison.Ordinal);
                    }
                }
            }
        }

        // Coincidencia completa
        result = result.Replace("$0", match.Value, StringComparison.Ordinal)
                       .Replace("$&", match.Value, StringComparison.Ordinal)
                       .Replace("<0>", match.Value, StringComparison.Ordinal)
                       .Replace("<Match>", match.Value, StringComparison.Ordinal);

        // Grupos numerados en orden descendente para evitar colisiones de prefijo ($10 antes de $1)
        for (int g = match.Groups.Count - 1; g >= 1; g--)
        {
            var grp = match.Groups[g];
            string gStr = g.ToString(CultureInfo.InvariantCulture);
            string gVal = grp.Value;

            result = result.Replace($"${{{gStr}}}", gVal, StringComparison.Ordinal)
                           .Replace($"${gStr}", gVal, StringComparison.Ordinal)
                           .Replace($"\\{gStr}", gVal, StringComparison.Ordinal)
                           .Replace($"<{gStr}>", gVal, StringComparison.Ordinal)
                           .Replace($"<Match:{gStr}>", gVal, StringComparison.Ordinal)
                           .Replace($"<Regex:{gStr}>", gVal, StringComparison.Ordinal);
        }

        return result;
    }
}
