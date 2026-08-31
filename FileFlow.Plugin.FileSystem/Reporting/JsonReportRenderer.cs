using System.Text.Encodings.Web;
using System.Text.Json;

namespace FileFlow.Plugin.FileSystem.Reporting;

public class JsonReportRenderer : IReportRenderer
{
    public string FileExtension => "json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public string Render(ReportSummaryData summary, string theme = "ModernDark", bool includeMetadata = true)
    {
        if (!includeMetadata)
        {
            var cleanSummary = summary with
            {
                Items = summary.Items.Select(i => i with { Metadata = new Dictionary<string, object?>() }).ToList()
            };
            return JsonSerializer.Serialize(cleanSummary, Options);
        }

        return JsonSerializer.Serialize(summary, Options);
    }
}
