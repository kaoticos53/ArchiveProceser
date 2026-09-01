using System.Windows;
using FileFlow.App.ViewModels;
using FileFlow.Sdk;
using FileFlow.Sdk.Renaming;

namespace FileFlow.App.Services;

/// <summary>
/// Servicio de cálculo y generación de previsualización en vivo para AdvancedRenamer.
/// </summary>
public sealed class RenamerLivePreviewService
{
    private readonly IRenameTransformEngine _transformEngine;

    public RenamerLivePreviewService(IRenameTransformEngine? transformEngine = null)
    {
        _transformEngine = transformEngine ?? new RenameTransformEngine();
    }

    public List<PreviewRowItem> GeneratePreview(
        NodeViewModel nodeViewModel,
        IReadOnlyList<RenameMethodStep> steps,
        out string sourceDescription)
    {
        var sampleItems = RenamerSampleDataProvider.GetSampleItems(nodeViewModel, out sourceDescription);
        var previewItems = new List<PreviewRowItem>(sampleItems.Count);
        var batch = new RenameBatchContext();

        // Inyectar variables personalizadas de nodos VariableInjectorNode del flujo
        var editorNodes = nodeViewModel.ParentEditor?.Nodes?.ToList()
            ?? (Application.Current?.MainWindow?.DataContext is MainViewModel mainVm ? mainVm.Editor.Nodes.ToList() : new List<NodeViewModel>());

        var injectorNodes = editorNodes.Where(n => n.IsVariableInjectorNode).ToList();

        foreach (var item in sampleItems)
        {
            foreach (var injectorNode in injectorNodes)
            {
                foreach (var param in injectorNode.Parameters)
                {
                    if (!string.IsNullOrWhiteSpace(param.Key))
                    {
                        item.Metadata[param.Key] = param.Value?.ToString() ?? string.Empty;
                    }
                }
            }

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
