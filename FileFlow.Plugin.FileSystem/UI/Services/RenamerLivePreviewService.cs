using FileFlow.Plugin.FileSystem.UI.Models;
using FileFlow.Sdk;
using FileFlow.Sdk.Renaming;

namespace FileFlow.Plugin.FileSystem.UI.Services;

/// <summary>
/// Servicio de cálculo y generación de previsualización en vivo para AdvancedRenamer dentro del plugin.
/// </summary>
public sealed class RenamerLivePreviewService
{
    private readonly IRenameTransformEngine _transformEngine;

    public RenamerLivePreviewService(IRenameTransformEngine? transformEngine = null)
    {
        _transformEngine = transformEngine ?? new RenameTransformEngine();
    }

    public List<PreviewRowItem> GeneratePreview(
        IReadOnlyList<RenameMethodStep> steps,
        out string sourceDescription)
    {
        var sampleItems = RenamerSampleDataProvider.GetSampleItems(out sourceDescription);
        var previewItems = new List<PreviewRowItem>(sampleItems.Count);
        var batch = new RenameBatchContext();

        foreach (var item in sampleItems)
        {
            var res = _transformEngine.Transform(item.FileName, item, steps, batch);
            bool isModified = !string.Equals(item.FileName, res.ResultFileName, StringComparison.Ordinal);
            string status = isModified ? "✓ Modificado" : "= Sin cambios";

            if (!string.IsNullOrEmpty(res.ErrorMessage))
            {
                status = $"⚠️ {res.ErrorMessage}";
            }

            previewItems.Add(new PreviewRowItem(item.FileName, res.ResultFileName, isModified, status));
        }

        return previewItems;
    }
}
