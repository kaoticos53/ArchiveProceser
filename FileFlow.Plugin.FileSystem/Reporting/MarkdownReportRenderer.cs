using System.Text;

namespace FileFlow.Plugin.FileSystem.Reporting;

public class MarkdownReportRenderer : IReportRenderer
{
    public string FileExtension => "md";

    public string Render(ReportSummaryData summary, string theme = "ModernDark", bool includeMetadata = true)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"# 📊 {summary.Title}");
        sb.AppendLine();
        sb.AppendLine($"> **Generado por FileFlow Studio v2.0** — {summary.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine("## 📈 Resumen Ejecutivo");
        sb.AppendLine();
        sb.AppendLine("| Métrica | Valor |");
        sb.AppendLine("| :--- | :--- |");
        sb.AppendLine($"| **Total Archivos** | `{summary.TotalFiles}` |");
        sb.AppendLine($"| **Exitosos** | `{summary.SuccessCount}` |");
        sb.AppendLine($"| **Errores** | `{summary.ErrorCount}` |");
        sb.AppendLine($"| **Volumen Total** | `{summary.FormattedTotalBytes}` |");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## 📁 Estructura y Detalle de Operaciones");
        sb.AppendLine();

        var groups = summary.Groups.Count > 0
            ? summary.Groups
            : ReportSummaryData.CreateGroups(summary.Items, summary.GroupBy, b => $"{b} B");

        int globalIndex = 1;
        foreach (var group in groups)
        {
            sb.AppendLine($"### 📂 Directorio: `{group.DisplayName}` ({group.FileCount} archivos, {group.FormattedTotalBytes})");
            sb.AppendLine();
            sb.AppendLine("<details open>");
            sb.AppendLine($"<summary><strong>Ver archivos de {group.DisplayName} ({group.FileCount})</strong></summary>");
            sb.AppendLine();

            foreach (var item in group.Items)
            {
                string statusIcon = item.IsSuccess ? "✅ Éxito" : "❌ Error";
                sb.AppendLine($"#### {globalIndex}. `{item.FileName}` ({item.FormattedSize}) — {statusIcon}");
                sb.AppendLine();
                sb.AppendLine($"- **Ruta Origen:** `{item.OriginalPath}`");
                sb.AppendLine($"- **Ruta Destino:** `{item.FinalPath}`");

                if (!string.IsNullOrWhiteSpace(item.ErrorMessage))
                {
                    sb.AppendLine($"- **Error:** `{item.ErrorMessage}`");
                }

                if (item.Steps.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("**Historial de Operaciones:**");
                    foreach (var step in item.Steps)
                    {
                        sb.AppendLine($"- {step}");
                    }
                }

                if (includeMetadata && item.Metadata.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("<details>");
                    sb.AppendLine("<summary><em>Metadatos</em></summary>");
                    sb.AppendLine();
                    sb.AppendLine("| Atributo | Valor |");
                    sb.AppendLine("| :--- | :--- |");
                    foreach (var (k, v) in item.Metadata)
                    {
                        sb.AppendLine($"| `{k}` | `{v?.ToString() ?? "null"}` |");
                    }
                    sb.AppendLine();
                    sb.AppendLine("</details>");
                }

                sb.AppendLine();
                globalIndex++;
            }

            sb.AppendLine("</details>");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
