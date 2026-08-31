namespace FileFlow.Plugin.FileSystem.Reporting;

public interface IReportRenderer
{
    string FileExtension { get; }
    string Render(ReportSummaryData summary, string theme = "ModernDark", bool includeMetadata = true);
}
