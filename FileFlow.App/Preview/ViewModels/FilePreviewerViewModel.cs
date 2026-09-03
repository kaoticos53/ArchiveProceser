using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.App.Preview.Core;

namespace FileFlow.App.Preview.ViewModels;

public record FileMetadataItem(string Key, string Value, bool IsAi = false);

public partial class FilePreviewerViewModel : ObservableObject
{
    [ObservableProperty]
    private FilePreviewContext? _currentContext;

    [ObservableProperty]
    private FrameworkElement? _activeVisualElement;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _currentIndex;

    [ObservableProperty]
    private int _totalItemsCount;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<FilePreviewContext> NavigationItems { get; } = [];
    public ObservableCollection<FileMetadataItem> MetadataItems { get; } = [];

    public bool CanNavigateNext => NavigationItems.Count > 1 && CurrentIndex < NavigationItems.Count - 1;
    public bool CanNavigatePrevious => NavigationItems.Count > 1 && CurrentIndex > 0;

    public async Task LoadContextAsync(FilePreviewContext context, IEnumerable<FilePreviewContext>? siblings = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        NavigationItems.Clear();
        if (siblings != null)
        {
            foreach (var s in siblings) NavigationItems.Add(s);
        }

        int foundIdx = -1;
        for (int i = 0; i < NavigationItems.Count; i++)
        {
            if (ReferenceEquals(NavigationItems[i], context) ||
                string.Equals(NavigationItems[i].CurrentPath, context.CurrentPath, StringComparison.OrdinalIgnoreCase))
            {
                foundIdx = i;
                break;
            }
        }

        if (foundIdx < 0)
        {
            NavigationItems.Add(context);
            foundIdx = NavigationItems.Count - 1;
        }

        CurrentIndex = foundIdx;
        TotalItemsCount = NavigationItems.Count;

        await SetCurrentContextInternalAsync(NavigationItems[CurrentIndex]).ConfigureAwait(false);
    }

    private async Task SetCurrentContextInternalAsync(FilePreviewContext context)
    {
        IsLoading = true;
        CurrentContext = context;
        StatusMessage = $"Cargando {context.FileName}...";

        // Actualizar metadatos
        MetadataItems.Clear();
        MetadataItems.Add(new FileMetadataItem("Archivo", context.FileName));
        MetadataItems.Add(new FileMetadataItem("Tamaño", $"{context.FileSizeBytes / 1024.0:F1} KB ({context.FileSizeBytes:N0} bytes)"));
        MetadataItems.Add(new FileMetadataItem("Ruta Completa", context.CurrentPath));

        if (!string.IsNullOrWhiteSpace(context.OriginalPath) && !context.OriginalPath.Equals(context.CurrentPath, StringComparison.OrdinalIgnoreCase))
        {
            MetadataItems.Add(new FileMetadataItem("Ruta Original", context.OriginalPath));
        }

        foreach (var (k, v) in context.Metadata)
        {
            bool isAi = k.StartsWith("AI:", StringComparison.OrdinalIgnoreCase) ||
                        k.StartsWith("Ocr:", StringComparison.OrdinalIgnoreCase) ||
                        k.StartsWith("Transcript", StringComparison.OrdinalIgnoreCase);
            MetadataItems.Add(new FileMetadataItem(k, v?.ToString() ?? string.Empty, isAi));
        }

        try
        {
            var provider = FilePreviewRegistry.Instance.GetProvider(context);
            if (provider != null)
            {
                var visual = await provider.CreateVisualElementAsync(context, CancellationToken.None).ConfigureAwait(false);
                ActiveVisualElement = visual;
                StatusMessage = $"Vista con {provider.ProviderName}";
            }
            else
            {
                ActiveVisualElement = null;
                StatusMessage = "Sin visualizador disponible.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(CanNavigateNext));
            OnPropertyChanged(nameof(CanNavigatePrevious));
        }
    }

    [RelayCommand]
    public async Task NavigateNextAsync()
    {
        if (CanNavigateNext)
        {
            CurrentIndex++;
            await SetCurrentContextInternalAsync(NavigationItems[CurrentIndex]).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    public async Task NavigatePreviousAsync()
    {
        if (CanNavigatePrevious)
        {
            CurrentIndex--;
            await SetCurrentContextInternalAsync(NavigationItems[CurrentIndex]).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    public void OpenInExplorer()
    {
        if (CurrentContext != null && File.Exists(CurrentContext.CurrentPath))
        {
            Process.Start("explorer.exe", $"/select,\"{CurrentContext.CurrentPath}\"");
        }
    }

    [RelayCommand]
    public void CopyPathToClipboard()
    {
        if (CurrentContext != null)
        {
            Clipboard.SetText(CurrentContext.CurrentPath);
        }
    }
}
