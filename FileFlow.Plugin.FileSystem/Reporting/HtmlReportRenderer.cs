using System.Net;
using System.Text;

namespace FileFlow.Plugin.FileSystem.Reporting;

public class HtmlReportRenderer : IReportRenderer
{
    public string FileExtension => "html";

    public string Render(ReportSummaryData summary, string theme = "ModernDark", bool includeMetadata = true)
    {
        bool isDark = !string.Equals(theme, "CleanLight", StringComparison.OrdinalIgnoreCase);
        var sb = new StringBuilder();

        string bgBody = isDark ? "#0F172A" : "#F8FAFC";
        string bgCard = isDark ? "#1E293B" : "#FFFFFF";
        string bgCardAlt = isDark ? "#334155" : "#F1F5F9";
        string textPrimary = isDark ? "#F8FAFC" : "#0F172A";
        string textSecondary = isDark ? "#94A3B8" : "#64748B";
        string borderColor = isDark ? "#334155" : "#E2E8F0";
        string accentCyan = "#06B6D4";
        string accentSuccess = "#10B981";
        string accentWarning = "#F59E0B";
        string accentError = "#EF4444";
        string accentIndigo = "#6366F1";

        var groups = summary.Groups.Count > 0
            ? summary.Groups
            : ReportSummaryData.CreateGroups(summary.Items, summary.GroupBy, b => FormatBytes(b));

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"es\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"  <title>{WebUtility.HtmlEncode(summary.Title)}</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine($"    :root {{");
        sb.AppendLine($"      --bg-body: {bgBody}; --bg-card: {bgCard}; --bg-card-alt: {bgCardAlt};");
        sb.AppendLine($"      --text-primary: {textPrimary}; --text-secondary: {textSecondary};");
        sb.AppendLine($"      --border-color: {borderColor}; --accent-cyan: {accentCyan};");
        sb.AppendLine($"      --accent-success: {accentSuccess}; --accent-warning: {accentWarning};");
        sb.AppendLine($"      --accent-error: {accentError}; --accent-indigo: {accentIndigo};");
        sb.AppendLine("    }");
        sb.AppendLine("    * { box-sizing: border-box; margin: 0; padding: 0; }");
        sb.AppendLine("    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: var(--bg-body); color: var(--text-primary); line-height: 1.5; padding: 24px; }");
        sb.AppendLine("    .container { max-width: 1200px; margin: 0 auto; }");
        sb.AppendLine("    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; padding-bottom: 16px; border-bottom: 1px solid var(--border-color); flex-wrap: wrap; gap: 16px; }");
        sb.AppendLine("    .header-title { font-size: 24px; font-weight: 700; display: flex; align-items: center; gap: 10px; }");
        sb.AppendLine("    .header-meta { font-size: 13px; color: var(--text-secondary); text-align: right; }");
        sb.AppendLine("    .kpi-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 16px; margin-bottom: 24px; }");
        sb.AppendLine("    .kpi-card { background: var(--bg-card); border: 1px solid var(--border-color); border-radius: 12px; padding: 16px; box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1); }");
        sb.AppendLine("    .kpi-label { font-size: 12px; text-transform: uppercase; letter-spacing: 0.5px; color: var(--text-secondary); font-weight: 600; margin-bottom: 6px; }");
        sb.AppendLine("    .kpi-value { font-size: 24px; font-weight: 800; color: var(--text-primary); }");
        sb.AppendLine("    .toolbar { display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; gap: 12px; flex-wrap: wrap; }");
        sb.AppendLine("    .search-box { flex: 1; min-width: 260px; position: relative; }");
        sb.AppendLine("    .search-input { width: 100%; padding: 10px 14px; background: var(--bg-card); border: 1px solid var(--border-color); border-radius: 8px; color: var(--text-primary); font-size: 13px; outline: none; transition: border-color 0.2s; }");
        sb.AppendLine("    .search-input:focus { border-color: var(--accent-cyan); }");
        sb.AppendLine("    .btn-group { display: flex; gap: 8px; flex-wrap: wrap; }");
        sb.AppendLine("    .btn { padding: 9px 14px; background: var(--bg-card-alt); border: 1px solid var(--border-color); border-radius: 8px; color: var(--text-primary); font-size: 13px; font-weight: 600; cursor: pointer; transition: all 0.2s; display: inline-flex; align-items: center; gap: 6px; user-select: none; }");
        sb.AppendLine("    .btn:hover { background: var(--border-color); }");
        sb.AppendLine("    .folder-group { margin-bottom: 18px; border: 1px solid var(--border-color); border-radius: 12px; overflow: hidden; background: var(--bg-card); box-shadow: 0 2px 4px rgba(0,0,0,0.05); }");
        sb.AppendLine("    .folder-header { padding: 14px 18px; display: flex; justify-content: space-between; align-items: center; cursor: pointer; user-select: none; background: var(--bg-card-alt); border-bottom: 1px solid transparent; transition: background 0.15s; flex-wrap: wrap; gap: 10px; }");
        sb.AppendLine("    .folder-header:hover { filter: brightness(1.04); }");
        sb.AppendLine("    .folder-group.open .folder-header { border-bottom-color: var(--border-color); }");
        sb.AppendLine("    .folder-title { display: flex; align-items: center; gap: 10px; font-weight: 700; font-size: 14.5px; word-break: break-all; }");
        sb.AppendLine("    .folder-pill { font-size: 11.5px; font-weight: 600; background: rgba(6, 182, 212, 0.15); color: var(--accent-cyan); padding: 2px 8px; border-radius: 12px; white-space: nowrap; }");
        sb.AppendLine("    .folder-actions { display: flex; align-items: center; gap: 12px; }");
        sb.AppendLine("    .folder-chevron { font-size: 12px; transition: transform 0.2s ease; display: inline-block; color: var(--text-secondary); }");
        sb.AppendLine("    .folder-group.open .folder-chevron { transform: rotate(180deg); }");
        sb.AppendLine("    .folder-body { padding: 16px; display: none; }");
        sb.AppendLine("    .folder-group.open .folder-body { display: block; }");
        sb.AppendLine("    .file-card { background: var(--bg-card); border: 1px solid var(--border-color); border-radius: 10px; margin-bottom: 14px; overflow: hidden; transition: border-color 0.15s; }");
        sb.AppendLine("    .file-card:last-child { margin-bottom: 0; }");
        sb.AppendLine("    .file-card:hover { border-color: var(--accent-cyan); }");
        sb.AppendLine("    .file-header { padding: 12px 16px; display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid var(--border-color); background: rgba(255, 255, 255, 0.02); flex-wrap: wrap; gap: 10px; }");
        sb.AppendLine("    .file-name { font-weight: 700; font-size: 14px; display: flex; align-items: center; gap: 8px; }");
        sb.AppendLine("    .badge { font-size: 11px; font-weight: 700; padding: 3px 8px; border-radius: 6px; text-transform: uppercase; }");
        sb.AppendLine("    .badge-success { background: rgba(16, 185, 129, 0.15); color: var(--accent-success); border: 1px solid rgba(16, 185, 129, 0.3); }");
        sb.AppendLine("    .badge-error { background: rgba(239, 68, 68, 0.15); color: var(--accent-error); border: 1px solid rgba(239, 68, 68, 0.3); }");
        sb.AppendLine("    .file-body { padding: 14px 16px; }");
        sb.AppendLine("    .path-row { display: flex; align-items: center; gap: 8px; font-size: 12.5px; margin-bottom: 8px; word-break: break-all; }");
        sb.AppendLine("    .path-label { font-weight: 600; color: var(--text-secondary); min-width: 65px; }");
        sb.AppendLine("    .path-value { font-family: 'Cascadia Code', Consolas, monospace; background: var(--bg-card-alt); padding: 3px 8px; border-radius: 4px; }");
        sb.AppendLine("    .timeline-container { margin-top: 12px; padding-top: 10px; border-top: 1px dashed var(--border-color); }");
        sb.AppendLine("    .timeline-title { font-size: 11.5px; font-weight: 700; color: var(--text-secondary); text-transform: uppercase; margin-bottom: 8px; }");
        sb.AppendLine("    .timeline { display: flex; flex-direction: column; gap: 6px; }");
        sb.AppendLine("    .timeline-step { display: flex; align-items: baseline; gap: 10px; font-size: 12px; }");
        sb.AppendLine("    .timeline-dot { width: 7px; height: 7px; border-radius: 50%; background: var(--accent-cyan); flex-shrink: 0; }");
        sb.AppendLine("    .metadata-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 8px; margin-top: 10px; padding: 10px; background: var(--bg-card-alt); border-radius: 8px; font-size: 11px; font-family: monospace; }");
        sb.AppendLine("    .meta-item { display: flex; justify-content: space-between; overflow: hidden; text-overflow: ellipsis; }");
        sb.AppendLine("    .meta-key { font-weight: 600; color: var(--accent-indigo); }");
        sb.AppendLine("    .meta-val { color: var(--text-primary); }");
        sb.AppendLine("    @media print { body { background: #fff; color: #000; padding: 0; } .toolbar { display: none; } .folder-body { display: block !important; } .file-card { break-inside: avoid; border: 1px solid #ccc; box-shadow: none; } }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class=\"container\">");

        // Header
        sb.AppendLine("    <div class=\"header\">");
        sb.AppendLine("      <div>");
        sb.AppendLine($"        <div class=\"header-title\">⚡ {WebUtility.HtmlEncode(summary.Title)}</div>");
        sb.AppendLine($"        <div style=\"font-size: 13px; color: var(--text-secondary); margin-top: 4px;\">Generado automáticamente por FileFlow Studio v2.0</div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"header-meta\">");
        sb.AppendLine($"        <div><strong>Fecha:</strong> {summary.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC</div>");
        sb.AppendLine($"        <div><strong>Total Archivos:</strong> {summary.TotalFiles}</div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");

        // KPI Cards
        sb.AppendLine("    <div class=\"kpi-grid\">");
        sb.AppendLine("      <div class=\"kpi-card\">");
        sb.AppendLine("        <div class=\"kpi-label\">📁 Total Archivos</div>");
        sb.AppendLine($"        <div class=\"kpi-value\">{summary.TotalFiles}</div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"kpi-card\">");
        sb.AppendLine("        <div class=\"kpi-label\">✅ Exitosos</div>");
        sb.AppendLine($"        <div class=\"kpi-value\" style=\"color: var(--accent-success);\">{summary.SuccessCount}</div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"kpi-card\">");
        sb.AppendLine("        <div class=\"kpi-label\">⚠️ Errores / Alertas</div>");
        sb.AppendLine($"        <div class=\"kpi-value\" style=\"color: {(summary.ErrorCount > 0 ? "var(--accent-error)" : "var(--text-primary)")};\">{summary.ErrorCount}</div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"kpi-card\">");
        sb.AppendLine("        <div class=\"kpi-label\">💾 Volumen Total</div>");
        sb.AppendLine($"        <div class=\"kpi-value\" style=\"color: var(--accent-cyan);\">{WebUtility.HtmlEncode(summary.FormattedTotalBytes)}</div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");

        // Toolbar
        sb.AppendLine("    <div class=\"toolbar\">");
        sb.AppendLine("      <div class=\"search-box\">");
        sb.AppendLine("        <input type=\"text\" id=\"searchInput\" class=\"search-input\" placeholder=\"🔍 Filtrar por nombre de archivo, directorio o metadato...\" onkeyup=\"filterFiles()\">");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"btn-group\">");
        sb.AppendLine("        <button class=\"btn\" onclick=\"toggleAllFolders(true)\">📂 Expandir Todo</button>");
        sb.AppendLine("        <button class=\"btn\" onclick=\"toggleAllFolders(false)\">📁 Colapsar Todo</button>");
        sb.AppendLine("        <button class=\"btn\" onclick=\"window.print()\">🖨️ Imprimir / PDF</button>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");

        // Folder Groups Accordion
        sb.AppendLine("    <div id=\"filesList\">");
        int globalIndex = 1;

        foreach (var group in groups)
        {
            string groupStatusBadge = group.ErrorCount == 0
                ? "<span class=\"badge badge-success\">✅ OK</span>"
                : $"<span class=\"badge badge-error\">⚠️ {group.ErrorCount} Errores</span>";

            sb.AppendLine($"      <div class=\"folder-group open\" data-folder-name=\"{WebUtility.HtmlEncode(group.DisplayName.ToLowerInvariant())}\">");
            sb.AppendLine("        <div class=\"folder-header\" onclick=\"toggleFolder(this)\">");
            sb.AppendLine("          <div class=\"folder-title\">");
            sb.AppendLine($"            <span>📁</span>");
            sb.AppendLine($"            <span>{WebUtility.HtmlEncode(group.DisplayName)}</span>");
            sb.AppendLine($"            <span class=\"folder-pill\">{group.FileCount} archivos • {WebUtility.HtmlEncode(group.FormattedTotalBytes)}</span>");
            sb.AppendLine("          </div>");
            sb.AppendLine("          <div class=\"folder-actions\">");
            sb.AppendLine($"            {groupStatusBadge}");
            sb.AppendLine("            <span class=\"folder-chevron\">▼</span>");
            sb.AppendLine("          </div>");
            sb.AppendLine("        </div>");
            sb.AppendLine("        <div class=\"folder-body\">");

            foreach (var item in group.Items)
            {
                string statusBadge = item.IsSuccess
                    ? "<span class=\"badge badge-success\">Completado</span>"
                    : "<span class=\"badge badge-error\">Error</span>";

                sb.AppendLine($"          <div class=\"file-card\" data-search=\"{WebUtility.HtmlEncode((item.FileName + " " + item.OriginalPath + " " + item.FinalPath + " " + group.DisplayName).ToLowerInvariant())}\">");
                sb.AppendLine("            <div class=\"file-header\">");
                sb.AppendLine($"              <div class=\"file-name\">📄 #{globalIndex} {WebUtility.HtmlEncode(item.FileName)} <span style=\"font-size:12px; font-weight:normal; color:var(--text-secondary);\">({WebUtility.HtmlEncode(item.FormattedSize)})</span></div>");
                sb.AppendLine($"              <div>{statusBadge}</div>");
                sb.AppendLine("            </div>");
                sb.AppendLine("            <div class=\"file-body\">");

                sb.AppendLine("              <div class=\"path-row\">");
                sb.AppendLine("                <span class=\"path-label\">Origen:</span>");
                sb.AppendLine($"                <span class=\"path-value\">{WebUtility.HtmlEncode(item.OriginalPath)}</span>");
                sb.AppendLine("              </div>");

                sb.AppendLine("              <div class=\"path-row\">");
                sb.AppendLine("                <span class=\"path-label\">Destino:</span>");
                sb.AppendLine($"                <span class=\"path-value\" style=\"color: var(--accent-success);\">{WebUtility.HtmlEncode(item.FinalPath)}</span>");
                sb.AppendLine("              </div>");

                if (!string.IsNullOrWhiteSpace(item.ErrorMessage))
                {
                    sb.AppendLine("              <div class=\"path-row\">");
                    sb.AppendLine("                <span class=\"path-label\" style=\"color: var(--accent-error);\">Fallo:</span>");
                    sb.AppendLine($"                <span class=\"path-value\" style=\"color: var(--accent-error);\">{WebUtility.HtmlEncode(item.ErrorMessage)}</span>");
                    sb.AppendLine("              </div>");
                }

                // Timeline of execution steps
                if (item.Steps.Count > 0)
                {
                    sb.AppendLine("              <div class=\"timeline-container\">");
                    sb.AppendLine($"                <div class=\"timeline-title\">Historial de Transformaciones ({item.Steps.Count} pasos)</div>");
                    sb.AppendLine("                <div class=\"timeline\">");
                    foreach (var step in item.Steps)
                    {
                        sb.AppendLine("                  <div class=\"timeline-step\">");
                        sb.AppendLine("                    <div class=\"timeline-dot\"></div>");
                        sb.AppendLine($"                    <div>{WebUtility.HtmlEncode(step)}</div>");
                        sb.AppendLine("                  </div>");
                    }
                    sb.AppendLine("                </div>");
                    sb.AppendLine("              </div>");
                }

                // Metadata grid
                if (includeMetadata && item.Metadata.Count > 0)
                {
                    sb.AppendLine("              <div class=\"timeline-container\">");
                    sb.AppendLine($"                <div class=\"timeline-title\">Metadatos Asociados ({item.Metadata.Count} atributos)</div>");
                    sb.AppendLine("                <div class=\"metadata-grid\">");
                    foreach (var (k, v) in item.Metadata)
                    {
                        sb.AppendLine("                  <div class=\"meta-item\">");
                        sb.AppendLine($"                    <span class=\"meta-key\">{WebUtility.HtmlEncode(k)}:</span>");
                        sb.AppendLine($"                    <span class=\"meta-val\">{WebUtility.HtmlEncode(v?.ToString() ?? "null")}</span>");
                        sb.AppendLine("                  </div>");
                    }
                    sb.AppendLine("                </div>");
                    sb.AppendLine("              </div>");
                }

                sb.AppendLine("            </div>");
                sb.AppendLine("          </div>");
                globalIndex++;
            }

            sb.AppendLine("        </div>");
            sb.AppendLine("      </div>");
        }

        sb.AppendLine("    </div>");

        // JS Interactive Controls
        sb.AppendLine("  </div>");
        sb.AppendLine("  <script>");
        sb.AppendLine("    function toggleFolder(headerElement) {");
        sb.AppendLine("      var group = headerElement.parentElement;");
        sb.AppendLine("      group.classList.toggle('open');");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    function toggleAllFolders(open) {");
        sb.AppendLine("      var groups = document.getElementsByClassName('folder-group');");
        sb.AppendLine("      for (var i = 0; i < groups.length; i++) {");
        sb.AppendLine("        if (open) { groups[i].classList.add('open'); }");
        sb.AppendLine("        else { groups[i].classList.remove('open'); }");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    function filterFiles() {");
        sb.AppendLine("      var input = document.getElementById('searchInput');");
        sb.AppendLine("      var filter = input.value.toLowerCase().trim();");
        sb.AppendLine("      var groups = document.getElementsByClassName('folder-group');");
        sb.AppendLine();
        sb.AppendLine("      for (var g = 0; g < groups.length; g++) {");
        sb.AppendLine("        var group = groups[g];");
        sb.AppendLine("        var cards = group.getElementsByClassName('file-card');");
        sb.AppendLine("        var folderName = group.getAttribute('data-folder-name') || '';");
        sb.AppendLine("        var groupMatches = filter.length > 0 && folderName.indexOf(filter) > -1;");
        sb.AppendLine("        var hasVisibleCards = false;");
        sb.AppendLine();
        sb.AppendLine("        for (var c = 0; c < cards.length; c++) {");
        sb.AppendLine("          var searchData = cards[c].getAttribute('data-search') || '';");
        sb.AppendLine("          if (filter.length === 0 || groupMatches || searchData.indexOf(filter) > -1) {");
        sb.AppendLine("            cards[c].style.display = '';");
        sb.AppendLine("            hasVisibleCards = true;");
        sb.AppendLine("          } else {");
        sb.AppendLine("            cards[c].style.display = 'none';");
        sb.AppendLine("          }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (filter.length === 0 || hasVisibleCards || groupMatches) {");
        sb.AppendLine("          group.style.display = '';");
        sb.AppendLine("          if (filter.length > 0) { group.classList.add('open'); }");
        sb.AppendLine("        } else {");
        sb.AppendLine("          group.style.display = 'none';");
        sb.AppendLine("        }");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine("  </script>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 0) return "0 B";
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1 && counter < suffixes.Length - 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }
}
