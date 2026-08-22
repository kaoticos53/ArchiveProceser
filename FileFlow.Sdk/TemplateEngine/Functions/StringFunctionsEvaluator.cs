using System.Globalization;
using System.Text.RegularExpressions;

namespace FileFlow.Sdk.TemplateEngine.Functions;

public sealed class StringFunctionsEvaluator : ITemplateFunctionEvaluator
{
    private static readonly System.Buffers.SearchValues<char> InvalidCharsSearch =
        System.Buffers.SearchValues.Create(Path.GetInvalidFileNameChars().Union(Path.GetInvalidPathChars()).Distinct().ToArray());

    private static readonly HashSet<string> SupportedFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "upper", "lower", "trim", "replace", "default", "sanitize", "padleft", "substring", "regexmatch", "regexreplace", "coalesce"
    };

    public bool CanEvaluate(string functionName) => SupportedFunctions.Contains(functionName);

    public string Evaluate(string functionName, IReadOnlyList<string> args, FileItemContext item, string? sourceRootPath)
    {
        string arg0 = args.Count > 0 ? args[0] : string.Empty;

        switch (functionName.ToLowerInvariant())
        {
            case "upper":
                return arg0.ToUpperInvariant();

            case "lower":
                return arg0.ToLowerInvariant();

            case "trim":
                return arg0.Trim();

            case "replace":
                return args.Count >= 3 ? arg0.Replace(args[1], args[2], StringComparison.OrdinalIgnoreCase) : arg0;

            case "default":
                return string.IsNullOrWhiteSpace(arg0) && args.Count > 1 ? args[1] : arg0;

            case "sanitize":
                return SanitizeFileName(arg0);

            case "padleft":
                if (args.Count >= 2 && int.TryParse(args[1], out int len))
                {
                    char padChar = args.Count >= 3 && args[2].Length > 0 ? args[2][0] : '0';
                    return arg0.PadLeft(len, padChar);
                }
                return arg0;

            case "substring":
                if (args.Count >= 2 && int.TryParse(args[1], out int startIndex))
                {
                    if (startIndex < 0 || startIndex >= arg0.Length) return string.Empty;
                    if (args.Count >= 3 && int.TryParse(args[2], out int length))
                    {
                        length = Math.Min(length, arg0.Length - startIndex);
                        return arg0.Substring(startIndex, length);
                    }
                    return arg0[startIndex..];
                }
                return arg0;

            case "regexmatch":
                if (args.Count >= 2)
                {
                    try
                    {
                        var match = Regex.Match(arg0, args[1], RegexOptions.None, TimeSpan.FromSeconds(1));
                        return match.Success ? match.Value : string.Empty;
                    }
                    catch
                    {
                        return string.Empty;
                    }
                }
                return arg0;

            case "regexreplace":
                if (args.Count >= 3)
                {
                    try
                    {
                        return Regex.Replace(arg0, args[1], args[2], RegexOptions.None, TimeSpan.FromSeconds(1));
                    }
                    catch
                    {
                        return arg0;
                    }
                }
                return arg0;

            case "coalesce":
                foreach (var arg in args)
                {
                    if (!string.IsNullOrWhiteSpace(arg)) return arg;
                }
                return string.Empty;

            default:
                return arg0;
        }
    }

    public static string SanitizeFileName(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        if (!input.AsSpan().ContainsAny(InvalidCharsSearch))
        {
            return input;
        }

        char[] buffer = input.ToCharArray();
        for (int i = 0; i < buffer.Length; i++)
        {
            if (InvalidCharsSearch.Contains(buffer[i]))
            {
                buffer[i] = '-';
            }
        }
        return new string(buffer);
    }
}
