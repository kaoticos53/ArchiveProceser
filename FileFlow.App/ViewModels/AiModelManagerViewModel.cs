using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.Plugin.AI;
using FileFlow.Sdk.Localization;

namespace FileFlow.App.ViewModels;

public partial class AiModelItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _modelId = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _category = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _expectedSizeLabel = string.Empty;

    [ObservableProperty]
    private string _diskSizeLabel = string.Empty;

    [ObservableProperty]
    private bool _isDownloaded;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private string _statusIcon = "⏳";

    [ObservableProperty]
    private string _statusText = string.Empty;

    public bool CanDownload => !IsDownloading;

    public void RefreshState()
    {
        bool available = AiModelManager.IsModelAvailable(ModelId);
        IsDownloaded = available;
        IsDownloading = false;
        Progress = available ? 100.0 : 0.0;
        ProgressText = string.Empty;

        if (available)
        {
            StatusIcon = "✅";
            StatusText = LocalizationManager.Instance.GetString("AiModelManager_StatusInstalled", "Descargado");
            long? size = AiModelManager.GetModelDiskSizeBytes(ModelId);
            if (size.HasValue)
            {
                DiskSizeLabel = $"{size.Value / 1_048_576.0:F1} MB";
            }
            else
            {
                DiskSizeLabel = ExpectedSizeLabel;
            }
        }
        else
        {
            StatusIcon = "⏳";
            StatusText = LocalizationManager.Instance.GetString("AiModelManager_StatusMissing", "No descargado");
            DiskSizeLabel = string.Empty;
        }

        OnPropertyChanged(nameof(CanDownload));
    }
}

public partial class AiModelManagerViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<AiModelItemViewModel> _models = [];

    [ObservableProperty]
    private string _modelsDirectory = string.Empty;

    [ObservableProperty]
    private string _installedSummary = string.Empty;

    [ObservableProperty]
    private bool _hasMissingModels;

    [ObservableProperty]
    private bool _isBusy;

    private CancellationTokenSource? _downloadCts;

    public AiModelManagerViewModel()
    {
        ModelsDirectory = AiModelManager.ModelsDirectory;
        InitializeModels();
        RefreshStatus();
    }

    private void InitializeModels()
    {
        Models.Clear();
        foreach (var (id, info) in AiModelManager.Catalog)
        {
            string friendlyName = !string.IsNullOrWhiteSpace(info.FriendlyName)
                ? info.FriendlyName
                : info.Id;

            string expectedSize;
            int parenOpen = info.Description.LastIndexOf('(');
            int parenClose = info.Description.LastIndexOf(')');
            if (parenOpen >= 0 && parenClose > parenOpen)
            {
                expectedSize = info.Description.Substring(parenOpen + 1, parenClose - parenOpen - 1);
            }
            else
            {
                expectedSize = $"~{info.MinSizeBytes / 1_048_576.0:F1} MB";
            }

            var item = new AiModelItemViewModel
            {
                ModelId = id,
                Name = friendlyName,
                Category = info.Category,
                Description = info.Description,
                FileName = Path.GetFileName(info.FileName),
                ExpectedSizeLabel = expectedSize,
            };

            Models.Add(item);
        }
    }

    [RelayCommand]
    public void RefreshStatus()
    {
        int installedCount = 0;
        long totalBytesOnDisk = 0;

        foreach (var item in Models)
        {
            item.RefreshState();
            if (item.IsDownloaded)
            {
                installedCount++;
                long? size = AiModelManager.GetModelDiskSizeBytes(item.ModelId);
                if (size.HasValue) totalBytesOnDisk += size.Value;
            }
        }

        int totalCount = Models.Count;
        HasMissingModels = installedCount < totalCount;
        double totalMb = totalBytesOnDisk / 1_048_576.0;

        InstalledSummary = $"{installedCount} de {totalCount} modelos instalados ({totalMb:F1} MB en disco)";
    }

    [RelayCommand]
    public async Task DownloadModelAsync(AiModelItemViewModel? item)
    {
        if (item == null || item.IsDownloading) return;

        IsBusy = true;
        item.IsDownloading = true;
        item.StatusIcon = "⬇️";
        item.StatusText = LocalizationManager.Instance.GetString("AiModelManager_StatusDownloading", "Descargando...");
        item.Progress = 0;

        _downloadCts = new CancellationTokenSource();

        var progressReporter = new Progress<double>(p =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                item.Progress = Math.Clamp(p, 0.0, 100.0);
                item.ProgressText = $"{item.Progress:F0}%";
            });
        });

        try
        {
            string? result = await AiModelManager.DownloadModelWithProgressAsync(
                item.ModelId,
                progressReporter,
                statusLogger: msg =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        item.ProgressText = msg;
                    });
                },
                cancellationToken: _downloadCts.Token
            );

            if (result != null)
            {
                item.RefreshState();
            }
            else
            {
                item.StatusIcon = "❌";
                item.StatusText = "Error en descarga";
            }
        }
        catch (OperationCanceledException)
        {
            item.StatusIcon = "⏳";
            item.StatusText = "Cancelado";
        }
        catch (Exception ex)
        {
            item.StatusIcon = "❌";
            item.StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            item.IsDownloading = false;
            IsBusy = Models.Any(m => m.IsDownloading);
            RefreshStatus();
        }
    }

    [RelayCommand]
    public async Task DownloadMissingModelsAsync()
    {
        if (IsBusy) return;

        var missing = Models.Where(m => !m.IsDownloaded).ToList();
        if (missing.Count == 0)
        {
            RefreshStatus();
            return;
        }

        IsBusy = true;
        foreach (var item in missing)
        {
            await DownloadModelAsync(item);
        }
        IsBusy = false;
    }

    [RelayCommand]
    public void DeleteModel(AiModelItemViewModel? item)
    {
        if (item == null) return;

        var result = MessageBox.Show(
            $"¿Estás seguro de que deseas eliminar el modelo '{item.Name}' del disco local?",
            "Eliminar Modelo",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            AiModelManager.DeleteModel(item.ModelId);
            RefreshStatus();
        }
    }

    [RelayCommand]
    public void OpenModelsFolder()
    {
        try
        {
            string path = ModelsDirectory;
            if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
        }
        catch { }
    }
}
