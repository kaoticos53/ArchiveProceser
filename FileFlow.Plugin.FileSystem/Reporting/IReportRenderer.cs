namespace FileFlow.Plugin.FileSystem.Reporting;

internal interface IReportRenderer
{
    string FileExtension { get; }
    string Render(ReportSummaryData summary, string theme = "ModernDark", bool includeMetadata = true);
}
