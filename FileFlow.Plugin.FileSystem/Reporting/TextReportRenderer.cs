using System.Text;

namespace FileFlow.Plugin.FileSystem.Reporting;

public class TextReportRenderer : IReportRenderer
{
    public string FileExtension => "txt";

    public string Render(ReportSummaryData summary, string theme = "ModernDark", bool includeMetadata = true)
    {
        var sb = new StringBuilder();

        sb.AppendLine("================================================================================");
        sb.AppendLine($" FILEFLOW STUDIO - {summary.Title.ToUpperInvariant()}");
        sb.AppendLine("================================================================================");
        sb.AppendLine($"Fecha: {summary.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"Archivos: {summary.TotalFiles} | Exitosos: {summary.SuccessCount} | Errores: {summary.ErrorCount} | Tamaño: {summary.FormattedTotalBytes}");
        sb.AppendLine("================================================================================");
        sb.AppendLine();

        var groups = summary.Groups.Count > 0
            ? summary.Groups
            : ReportSummaryData.CreateGroups(summary.Items, summary.GroupBy, b => $"{b} B");

        int globalIndex = 1;
        foreach (var group in groups)
        {
            sb.AppendLine($"📁 {group.DisplayName} ({group.FileCount} archivos, {group.FormattedTotalBytes})");
            sb.AppendLine("│");

            for (int i = 0; i < group.Items.Count; i++)
            {
                var item = group.Items[i];
                bool isLast = (i == group.Items.Count - 1);
                string branch = isLast ? "└──" : "├──";
                string childPrefix = isLast ? "    " : "│   ";
                string status = item.IsSuccess ? "EXITO" : "ERROR";

                sb.AppendLine($"{branch} 📄 [{globalIndex}] {item.FileName} ({item.FormattedSize}) [{status}]");
                sb.AppendLine($"{childPrefix}    Origen : {item.OriginalPath}");
                sb.AppendLine($"{childPrefix}    Destino: {item.FinalPath}");

                if (!string.IsNullOrWhiteSpace(item.ErrorMessage))
                {
                    sb.AppendLine($"{childPrefix}    Error  : {item.ErrorMessage}");
                }

                if (item.Steps.Count > 0)
                {
                    sb.AppendLine($"{childPrefix}    Operaciones:");
                    foreach (var step in item.Steps)
                    {
                        sb.AppendLine($"{childPrefix}      * {step}");
                    }
                }

                if (includeMetadata && item.Metadata.Count > 0)
                {
                    sb.AppendLine($"{childPrefix}    Metadatos:");
                    foreach (var (k, v) in item.Metadata)
                    {
                        sb.AppendLine($"{childPrefix}      - {k}: {v}");
                    }
                }

                globalIndex++;
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
