using System.Globalization;

namespace FileFlow.Sdk.TemplateEngine.Functions;

public sealed class DateTimeFunctionsEvaluator : ITemplateFunctionEvaluator
{
    private static readonly HashSet<string> SupportedFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "year", "month", "day", "formatdate", "fileagedays"
    };

    public bool CanEvaluate(string functionName) => SupportedFunctions.Contains(functionName);

    public string Evaluate(string functionName, IReadOnlyList<string> args, FileItemContext item, string? sourceRootPath)
    {
        string arg0 = args.Count > 0 ? args[0] : string.Empty;

        switch (functionName.ToLowerInvariant())
        {
            case "year":
                return ExtractDatePart(arg0, "yyyy");

            case "month":
                return ExtractDatePart(arg0, "MM");

            case "day":
                return ExtractDatePart(arg0, "dd");

            case "formatdate":
                string fmt = args.Count > 1 ? args[1] : "yyyy-MM-dd";
                return ExtractDatePart(arg0, fmt);

            case "fileagedays":
                return CalculateDaysElapsed(arg0);

            default:
                return arg0;
        }
    }

    private static string CalculateDaysElapsed(string dateInput)
    {
        if (DateTime.TryParse(dateInput, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime dt) ||
            DateTime.TryParse(dateInput, out dt))
        {
            int days = (int)(DateTime.UtcNow - dt.ToUniversalTime()).TotalDays;
            return days.ToString(CultureInfo.InvariantCulture);
        }
        return "0";
    }

    private static string ExtractDatePart(string dateInput, string format)
    {
        if (DateTime.TryParse(dateInput, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt) ||
            DateTime.TryParse(dateInput, out dt))
        {
            return dt.ToString(format, CultureInfo.InvariantCulture);
        }
        return dateInput;
    }
}
