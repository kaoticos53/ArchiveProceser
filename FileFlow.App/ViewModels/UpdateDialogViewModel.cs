using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.App.Services;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Services;

namespace FileFlow.App.ViewModels;

/// <summary>
/// ViewModel reactivo para el diálogo modal de autoactualización.
/// </summary>
public partial class UpdateDialogViewModel : ObservableObject
{
    private readonly AppUpdateInfo _updateInfo;
    private readonly IAppUpdateService _updateService;

    public event Action<bool>? RequestClose;

    public string CurrentVersionText => _updateService.CurrentVersion.ToString();
    public string NewVersionText => _updateInfo.Version.ToString();
    public string ReleaseTitle => _updateInfo.Title;
    public string ReleaseDateText => _updateInfo.PublishedAtUtc.ToLocalTime().ToString("g");
    public string ReleaseNotes => _updateInfo.ReleaseNotesMarkdown;

    public string PackagingFormatText => _updateService.CurrentPackagingFormat switch
    {
        AppPackagingFormat.WindowsInstalled => "Windows Setup (.exe)",
        AppPackagingFormat.WindowsPortable => "Windows Portable (.zip)",
        AppPackagingFormat.LinuxAppImage => "Linux AppImage (.AppImage)",
        AppPackagingFormat.LinuxFlatpak => "Linux Flatpak (.flatpak)",
        AppPackagingFormat.LinuxDebPackage => "Debian / Ubuntu (.deb)",
        AppPackagingFormat.LinuxGenericTarball => "Linux Portable (.tar.gz)",
        _ => "Portable / Universal"
    };

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private string _downloadStatusText = string.Empty;

    [ObservableProperty]
    private bool _isChecksumVerified;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public UpdateDialogViewModel(AppUpdateInfo updateInfo, IAppUpdateService? updateService = null)
    {
        _updateInfo = updateInfo;
        _updateService = updateService ?? AppUpdateService.Instance;
    }

    [RelayCommand]
    private async Task InstallAndRestartAsync()
    {
        if (IsDownloading) return;

        IsDownloading = true;
        HasError = false;
        ErrorMessage = string.Empty;
        IsChecksumVerified = false;
        DownloadProgress = 0.0;
        DownloadStatusText = LocalizationManager.Instance.GetString("Update_StatusChecking", "Iniciando descarga segura...");

        var progress = new Progress<UpdateProgressReport>(report =>
        {
            DownloadProgress = report.Percentage;
            DownloadStatusText = report.StatusMessage;
            if (report.StatusMessage.Contains("Integridad") || report.StatusMessage.Contains("verified"))
            {
                IsChecksumVerified = true;
            }
        });

        try
        {
            string targetFile = await _updateService.DownloadAndPrepareUpdateAsync(_updateInfo, progress, CancellationToken.None);
            DownloadStatusText = LocalizationManager.Instance.GetString("Update_InstallAndRestart", "Aplicando actualización y reiniciando...");

            await _updateService.ApplyUpdateAndRestartAsync(_updateInfo, targetFile);
            RequestClose?.Invoke(true);
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
            IsDownloading = false;
        }
    }

    [RelayCommand]
    private void RemindLater()
    {
        RequestClose?.Invoke(false);
    }

    [RelayCommand]
    private void SkipVersion()
    {
        var prefs = UserPreferencesService.Instance.Preferences;
        prefs.IgnoredUpdateVersion = _updateInfo.VersionTag;
        UserPreferencesService.Instance.Save();
        RequestClose?.Invoke(false);
    }

    [RelayCommand]
    private void OpenGitHub()
    {
        if (!string.IsNullOrWhiteSpace(_updateInfo.HtmlUrl))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _updateInfo.HtmlUrl,
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }
}

