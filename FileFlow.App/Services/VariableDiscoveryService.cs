using FileFlow.App.Models;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Services;

/// <summary>
/// Servicio responsable de construir el catálogo de variables disponibles para un nodo específico en el grafo.
/// </summary>
public class VariableDiscoveryService : IVariableDiscoveryService
{
    public List<VariableGroupItem> GetAvailableVariables(NodeViewModel targetNode, IEnumerable<ConnectionViewModel> connections)
    {
        ArgumentNullException.ThrowIfNull(targetNode);
        ArgumentNullException.ThrowIfNull(connections);

        var result = new List<VariableGroupItem>();

        // 1. Built-in System & Environment Variables (Always available)
        var systemGroup = new VariableGroupItem("🌐 System & Environment");
        systemGroup.Variables.Add(new VariableItem("FileName", "{FileName}", "Full file name (e.g. photo.jpg)"));
        systemGroup.Variables.Add(new VariableItem("FileNameNoExt", "{FileNameNoExt}", "File name without extension (e.g. photo)"));
        systemGroup.Variables.Add(new VariableItem("Extension", "{Extension}", "File extension (e.g. jpg)"));
        systemGroup.Variables.Add(new VariableItem("CurrentPath", "{CurrentPath}", "Current absolute item path"));
        systemGroup.Variables.Add(new VariableItem("OriginalPath", "{OriginalPath}", "Original source item path"));
        systemGroup.Variables.Add(new VariableItem("RelativePath", "{RelativePath}", "Relative subfolder path from source root"));
        systemGroup.Variables.Add(new VariableItem("DateNow", "{DateNow}", "Current execution date (yyyy-MM-dd)"));
        systemGroup.Variables.Add(new VariableItem("TimeNow", "{TimeNow}", "Current execution time (HH-mm-ss)"));
        systemGroup.Variables.Add(new VariableItem("DateTimeNow", "{DateTimeNow}", "Combined timestamp (yyyy-MM-dd_HH-mm-ss)"));
        systemGroup.Variables.Add(new VariableItem("Counter", "{Counter}", "Item sequence index in batch (e.g. 1, 2, 3)"));
        systemGroup.Variables.Add(new VariableItem("SizeMB", "{SizeMB}", "File size in Megabytes (e.g. 4.25MB)"));
        systemGroup.Variables.Add(new VariableItem("SizeKB", "{SizeKB}", "File size in Kilobytes"));
        systemGroup.Variables.Add(new VariableItem("UserName", "{UserName}", "Windows user name"));
        systemGroup.Variables.Add(new VariableItem("MachineName", "{MachineName}", "Environment host computer name"));
        result.Add(systemGroup);

        // 2. Upstream Traversal
        var visitedNodes = new HashSet<NodeViewModel>();
        var queue = new Queue<NodeViewModel>();
        queue.Enqueue(targetNode);

        var connectionsList = connections.ToList();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var incomingConns = connectionsList.Where(c => c.Target.NodeOwner == current).ToList();

            foreach (var conn in incomingConns)
            {
                var upstreamNode = conn.Source.NodeOwner;
                if (visitedNodes.Add(upstreamNode))
                {
                    queue.Enqueue(upstreamNode);

                    string typeName = upstreamNode.NodeTypeName;
                    var upstreamGroup = new VariableGroupItem($"🔗 {upstreamNode.Title}");

                    if (typeName.Contains("ExifMetadataNode", StringComparison.OrdinalIgnoreCase))
                    {
                        upstreamGroup.Variables.Add(new VariableItem("DateTaken", "{DateTaken}", "Date/Time Original EXIF"));
                        upstreamGroup.Variables.Add(new VariableItem("Year", "{Year(DateTaken)}", "4-Digit Year"));
                        upstreamGroup.Variables.Add(new VariableItem("Month", "{Month(DateTaken)}", "2-Digit Month"));
                        upstreamGroup.Variables.Add(new VariableItem("Day", "{Day(DateTaken)}", "2-Digit Day"));
                        upstreamGroup.Variables.Add(new VariableItem("CameraModel", "{CameraModel}", "Camera Model EXIF"));
                        upstreamGroup.Variables.Add(new VariableItem("CameraMake", "{CameraMake}", "Camera Make EXIF"));
                        upstreamGroup.Variables.Add(new VariableItem("ImageWidth", "{ImageWidth}", "Image width in pixels"));
                        upstreamGroup.Variables.Add(new VariableItem("ImageHeight", "{ImageHeight}", "Image height in pixels"));
                        upstreamGroup.Variables.Add(new VariableItem("Orientation", "{Orientation}", "Landscape, Portrait, or Square"));
                        upstreamGroup.Variables.Add(new VariableItem("AspectRatio", "{AspectRatio}", "Calculated Aspect Ratio (e.g. 16:9)"));
                        upstreamGroup.Variables.Add(new VariableItem("Megapixels", "{Megapixels}", "Image resolution in Megapixels"));
                    }
                    else if (typeName.Contains("VariableInjectorNode", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var param in upstreamNode.Parameters)
                        {
                            string keyName = param.Key;
                            if (!string.IsNullOrWhiteSpace(keyName))
                            {
                                upstreamGroup.Variables.Add(new VariableItem(keyName, $"{{{keyName}}}", $"Injected by {upstreamNode.Title}"));
                            }
                        }
                    }
                    else if (typeName.Contains("SmartUnpackNode", StringComparison.OrdinalIgnoreCase))
                    {
                        upstreamGroup.Variables.Add(new VariableItem("UnpackedFrom", "{UnpackedFrom}", "Original Archive Path"));
                        upstreamGroup.Variables.Add(new VariableItem("ArchiveFormat", "{ArchiveFormat}", "Archive format (ZIP/7Z/RAR)"));
                        upstreamGroup.Variables.Add(new VariableItem("UnpackedFileCount", "{UnpackedFileCount}", "Total extracted file count"));
                    }
                    else if (typeName.Contains("ImageOptimizerNode", StringComparison.OrdinalIgnoreCase))
                    {
                        upstreamGroup.Variables.Add(new VariableItem("OptimizedFormat", "{OptimizedFormat}", "Output Format (WebP/Jpeg/Png)"));
                    }

                    if (upstreamGroup.Variables.Count > 0)
                    {
                        result.Add(upstreamGroup);
                    }
                }
            }
        }

        // 3. Transformation Functions Group
        var fnGroup = new VariableGroupItem("🔤 Expression Functions");
        fnGroup.Variables.Add(new VariableItem("Sanitize", "{Sanitize(CameraModel)}", "Clean illegal Windows path characters"));
        fnGroup.Variables.Add(new VariableItem("PadLeft", "{PadLeft(Counter, 4, \"0\")}", "Pad number with leading characters"));
        fnGroup.Variables.Add(new VariableItem("Upper", "{Upper(FileNameNoExt)}", "Convert text to uppercase"));
        fnGroup.Variables.Add(new VariableItem("Lower", "{Lower(Extension)}", "Convert text to lowercase"));
        fnGroup.Variables.Add(new VariableItem("FormatDate", "{FormatDate(DateTaken, \"yyyy-MM\")}", "Custom Date Format"));
        fnGroup.Variables.Add(new VariableItem("Substring", "{Substring(FileNameNoExt, 0, 8)}", "Extract text substring"));
        fnGroup.Variables.Add(new VariableItem("RegexMatch", "{RegexMatch(FileNameNoExt, \"[0-9]+\")}", "Extract regular expression match"));
        fnGroup.Variables.Add(new VariableItem("RegexReplace", "{RegexReplace(FileNameNoExt, \"[^a-zA-Z0-9]\", \"_\")}", "Replace regex pattern"));
        fnGroup.Variables.Add(new VariableItem("Coalesce", "{Coalesce(DateTaken, FileCreatedDate, DateNow)}", "First non-empty value in list"));
        fnGroup.Variables.Add(new VariableItem("FileAgeDays", "{FileAgeDays(DateTaken)}", "Days elapsed since date"));
        fnGroup.Variables.Add(new VariableItem("Default", "{Default(DateTaken, \"2026-01-01\")}", "Fallback value if empty"));
        result.Add(fnGroup);

        return result;
    }
}
