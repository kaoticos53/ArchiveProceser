using System.Text;

namespace FileFlow.Plugin.FileSystem.Reporting;

public class CsvReportRenderer : IReportRenderer
{
    public string FileExtension => "csv";

    public string Render(ReportSummaryData summary, string theme = "ModernDark", bool includeMetadata = true)
    {
        var sb = new StringBuilder();

        // CSV Header
        sb.AppendLine("Id,FileName,Directory,OriginalPath,FinalPath,FileSizeBytes,FormattedSize,Status,ErrorMessage,StepsCount,StepsSummary");

        foreach (var item in summary.Items)
        {
            string status = item.IsSuccess ? "Success" : "Error";
            string stepsSummary = string.Join(" | ", item.Steps);
            string dir = Path.GetDirectoryName(!string.IsNullOrWhiteSpace(item.OriginalPath) ? item.OriginalPath : item.FinalPath) ?? string.Empty;

            sb.AppendLine(string.Join(",",
                EscapeCsv(item.Id),
                EscapeCsv(item.FileName),
                EscapeCsv(dir),
                EscapeCsv(item.OriginalPath),
                EscapeCsv(item.FinalPath),
                item.FileSizeBytes.ToString(),
                EscapeCsv(item.FormattedSize),
                status,
                EscapeCsv(item.ErrorMessage ?? string.Empty),
                item.Steps.Count.ToString(),
                EscapeCsv(stepsSummary)
            ));
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string field)
    {
        if (string.IsNullOrEmpty(field)) return "\"\"";
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return $"\"{field}\"";
    }
}
