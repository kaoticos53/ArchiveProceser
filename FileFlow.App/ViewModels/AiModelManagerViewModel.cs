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

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private bool _hasCustomUrls;

    [ObservableProperty]
    private int _configuredUrlsCount;

    public bool CanDownload => !IsDownloading;

    public void RefreshState()
    {
        bool available = AiModelManager.IsModelAvailable(ModelId);
        IsDownloaded = available;
        IsDownloading = false;
        Progress = available ? 100.0 : 0.0;
        HasCustomUrls = AiModelManager.HasCustomUrls(ModelId);
        ConfiguredUrlsCount = AiModelManager.GetConfiguredUrls(ModelId).Count;

        if (available)
        {
            ErrorMessage = null;
            HasError = false;
            ProgressText = string.Empty;
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
            if (HasError && !string.IsNullOrEmpty(ErrorMessage))
            {
                StatusIcon = "❌";
                StatusText = LocalizationManager.Instance.GetString("AiModelManager_StatusError", "Error");
            }
            else
            {
                StatusIcon = "⏳";
                StatusText = LocalizationManager.Instance.GetString("AiModelManager_StatusMissing", "No descargado");
                ProgressText = string.Empty;
            }
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

    [ObservableProperty]
    private string? _lastDownloadErrorMessage;

    [ObservableProperty]
    private bool _hasDownloadError;

    [RelayCommand]
    public void DismissError()
    {
        LastDownloadErrorMessage = null;
        HasDownloadError = false;
    }

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
        await DownloadModelInternalAsync(item, suppressSingleAlert: false);
    }

    public async Task DownloadModelInternalAsync(AiModelItemViewModel? item, bool suppressSingleAlert)
    {
        if (item == null || item.IsDownloading) return;

        IsBusy = true;
        item.IsDownloading = true;
        item.ErrorMessage = null;
        item.HasError = false;
        item.StatusIcon = "⬇️";
        item.StatusText = LocalizationManager.Instance.GetString("AiModelManager_StatusDownloading", "Descargando...");
        item.Progress = 0;
        item.ProgressText = "Conectando...";

        _downloadCts = new CancellationTokenSource();

        var progressReporter = new Progress<double>(p =>
        {
            void UpdateProgress()
            {
                item.Progress = Math.Clamp(p, 0.0, 100.0);
                item.ProgressText = $"{item.Progress:F0}%";
            }

            if (Application.Current?.Dispatcher != null)
                Application.Current.Dispatcher.Invoke(UpdateProgress);
            else
                UpdateProgress();
        });

        string? lastErrorCaptured = null;

        try
        {
            string? result = await AiModelManager.DownloadModelWithProgressAsync(
                item.ModelId,
                progressReporter,
                statusLogger: msg =>
                {
                    void UpdateText()
                    {
                        item.ProgressText = msg;
                        if (msg.StartsWith("❌") || msg.Contains("Error", StringComparison.OrdinalIgnoreCase))
                        {
                            lastErrorCaptured = msg;
                        }
                    }

                    if (Application.Current?.Dispatcher != null)
                        Application.Current.Dispatcher.Invoke(UpdateText);
                    else
                        UpdateText();
                },
                cancellationToken: _downloadCts.Token
            );

            if (result != null)
            {
                item.ErrorMessage = null;
                item.HasError = false;
                item.RefreshState();
            }
            else
            {
                string err = lastErrorCaptured ?? AiModelManager.LastError ?? "Error desconocido en la descarga del modelo.";
                item.ErrorMessage = err;
                item.HasError = true;
                item.StatusIcon = "❌";
                item.StatusText = LocalizationManager.Instance.GetString("AiModelManager_StatusError", "Error");
                item.ProgressText = err;

                LastDownloadErrorMessage = $"{item.Name}: {err}";
                HasDownloadError = true;

                if (!suppressSingleAlert && Application.Current != null)
                {
                    MessageBox.Show(
                        $"No se pudo descargar el modelo '{item.Name}':\n\n{err}",
                        "Error en Descarga de Modelo IA",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }
        catch (OperationCanceledException)
        {
            item.StatusIcon = "⏳";
            item.StatusText = "Cancelado";
            item.ProgressText = "Descarga cancelada por el usuario.";
        }
        catch (Exception ex)
        {
            string err = $"Excepción: {ex.Message}";
            item.ErrorMessage = err;
            item.HasError = true;
            item.StatusIcon = "❌";
            item.StatusText = LocalizationManager.Instance.GetString("AiModelManager_StatusError", "Error");
            item.ProgressText = err;

            LastDownloadErrorMessage = $"{item.Name}: {err}";
            HasDownloadError = true;

            if (!suppressSingleAlert && Application.Current != null)
            {
                MessageBox.Show(
                    $"Error inesperado al descargar '{item.Name}':\n\n{ex.Message}",
                    "Error en Descarga de Modelo IA",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
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
        DismissError();

        var failedList = new List<(string Name, string Error)>();

        foreach (var item in missing)
        {
            await DownloadModelInternalAsync(item, suppressSingleAlert: true);
            if (item.HasError && !string.IsNullOrEmpty(item.ErrorMessage))
            {
                failedList.Add((item.Name, item.ErrorMessage));
            }
        }

        IsBusy = false;

        if (failedList.Count > 0 && Application.Current != null)
        {
            string summary = string.Join("\n• ", failedList.Select(f => $"{f.Name}: {f.Error}"));
            LastDownloadErrorMessage = $"Falló la descarga de {failedList.Count} modelo(s).";
            HasDownloadError = true;

            MessageBox.Show(
                $"No se pudieron descargar {failedList.Count} de los {missing.Count} modelos solicitados:\n\n• {summary}\n\nPor favor, verifica la conexión a Internet o los detalles de red.",
                "Error en Descarga de Modelos IA",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
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

    [RelayCommand]
    public void ConfigureUrls(AiModelItemViewModel? item)
    {
        if (item == null) return;

        if (Application.Current != null)
        {
            var dialog = new Views.Components.AiModelUrlsConfigDialog(item.ModelId)
            {
                Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                item.RefreshState();
            }
        }
    }
}
