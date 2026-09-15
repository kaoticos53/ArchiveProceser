using Avalonia.Controls;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FileFlow.App.Views.Components;

public partial class WorkflowSettingsWindow : Window
{
    public WorkflowSettingsViewModel ViewModel { get; }

    public string GlobalOutputDir => ViewModel.GlobalOutputDir;

    public WorkflowSettingsWindow() : this(string.Empty) { }

    public WorkflowSettingsWindow(string currentGlobalOutputDir, WorkflowSettingsViewModel? viewModel = null)
    {
        InitializeComponent();

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
}
