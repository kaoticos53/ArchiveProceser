using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FileFlow.App.Views.Components;

public partial class WorkflowSettingsWindow : Window
{
    public WorkflowSettingsViewModel ViewModel { get; }

    public string GlobalOutputDir => ViewModel.GlobalOutputDir;

    public WorkflowSettingsWindow() : this(string.Empty)
    {
    }

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
                new AvaloniaDialogService(),
                App.Services?.GetService<AiModelManagerViewModel>() ?? new AiModelManagerViewModel());

        DataContext = ViewModel;
        ViewModel.Initialize(currentGlobalOutputDir);

        ViewModel.RequestClose += result =>
        {
            Close(result);
        };
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void OpenThemeCustomizer_Click(object? sender, RoutedEventArgs e)
    {
        var win = new ThemeCustomizerWindow();
        await win.ShowDialog(this);
        ViewModel.ReloadThemes(ThemeManager.Instance.CurrentThemeId);
    }

    private async void ConfigureAiModelUrls_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control btn && btn.Tag is AiModelItemViewModel item)
        {
            var dialog = new AiModelUrlsConfigDialog(item.ModelId);
            bool? res = await dialog.ShowDialog<bool>(this);
            if (res == true)
            {
                item.RefreshState();
            }
        }
    }
}
