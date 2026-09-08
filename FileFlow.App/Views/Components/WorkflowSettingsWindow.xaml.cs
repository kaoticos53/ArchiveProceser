using System.Windows;
using FileFlow.App.Services;
using FileFlow.App.Themes;
using FileFlow.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FileFlow.App.Views.Components;

/// <summary>
/// Ventana modal de ajustes y configuración del espacio de trabajo y herramientas.
/// Sigue el patrón MVVM delegando toda la lógica y estado en <see cref="WorkflowSettingsViewModel"/>.
/// </summary>
public partial class WorkflowSettingsWindow : Window
{
    public WorkflowSettingsViewModel ViewModel { get; }

    public string GlobalOutputDir => ViewModel.GlobalOutputDir;

    public WorkflowSettingsWindow(string currentGlobalOutputDir, WorkflowSettingsViewModel? viewModel = null)
    {
        InitializeComponent();
        WindowThemeHelper.ApplyThemeToWindow(this);

        ViewModel = viewModel
            ?? App.Services?.GetService<WorkflowSettingsViewModel>()
            ?? new WorkflowSettingsViewModel(
                UserPreferencesService.Instance,
                FileFlow.Core.Services.ExternalToolsService.Instance,
                ThemeManager.Instance,
                FileFlow.Sdk.Localization.LocalizationManager.Instance,
                new FileDialogService(),
                new WpfDialogService(),
                App.Services?.GetService<AiModelManagerViewModel>() ?? new AiModelManagerViewModel());

        DataContext = ViewModel;
        ViewModel.Initialize(currentGlobalOutputDir);

        ViewModel.RequestClose += result =>
        {
            DialogResult = result;
            Close();
        };
    }

    private void OpenThemeCustomizer_Click(object sender, RoutedEventArgs e)
    {
        var win = new ThemeCustomizerWindow
        {
            Owner = this
        };
        win.ShowDialog();
        ViewModel.ReloadThemes(ThemeManager.Instance.CurrentThemeId);
    }

    private void ConfigureAiModelUrls_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement btn && btn.Tag is AiModelItemViewModel item)
        {
            var dialog = new AiModelUrlsConfigDialog(item.ModelId)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                item.RefreshState();
            }
        }
    }
}
