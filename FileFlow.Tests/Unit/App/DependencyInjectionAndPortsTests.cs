using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Core.Telemetry;
using FileFlow.Sdk.Localization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FileFlow.Tests.Unit.App;

public class DependencyInjectionAndPortsTests
{
    [Fact]
    public void ServiceCollectionExtensions_RegistersAllRequiredServicesAndPorts()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddFileFlowServices();
        var provider = services.BuildServiceProvider();

        // Assert - Domain & Core Ports
        Assert.NotNull(provider.GetService<ILocalizationService>());
        Assert.NotNull(provider.GetService<ILogStore>());
        Assert.NotNull(provider.GetService<IFileRecycler>());
        Assert.NotNull(provider.GetService<IFolderWatcherService>());
        Assert.NotNull(provider.GetService<PluginLoader>());

        // Assert - Presentation & UI Ports
        Assert.NotNull(provider.GetService<IFileDialogService>());
        Assert.NotNull(provider.GetService<IWorkflowStorageService>());
        Assert.NotNull(provider.GetService<IVariableDiscoveryService>());
        Assert.NotNull(provider.GetService<INodeClipboardService>());
        Assert.NotNull(provider.GetService<ISystemPerformanceMonitor>());
        Assert.NotNull(provider.GetService<IThemeService>());
        Assert.NotNull(provider.GetService<IUserPreferencesService>());
        Assert.NotNull(provider.GetService<IDialogService>());
        Assert.NotNull(provider.GetService<IProcessLauncherService>());

        // Assert - ViewModels
        Assert.NotNull(provider.GetService<LogViewModel>());
        Assert.NotNull(provider.GetService<EditorViewModel>());
        Assert.NotNull(provider.GetService<ToolboxViewModel>());
        Assert.NotNull(provider.GetService<NodeInspectorViewModel>());
        Assert.NotNull(provider.GetService<ControlBarViewModel>());
        Assert.NotNull(provider.GetService<StatusBarViewModel>());
        Assert.NotNull(provider.GetService<MainViewModel>());
    }

    [Fact]
    public void MainViewModel_ResolvesSuccessfullyFromContainer()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddFileFlowServices();
        var provider = services.BuildServiceProvider();

        // Act
        var mainVm = provider.GetRequiredService<MainViewModel>();

        // Assert
        Assert.NotNull(mainVm);
        Assert.NotNull(mainVm.Editor);
        Assert.NotNull(mainVm.Toolbox);
        Assert.NotNull(mainVm.NodeInspector);
        Assert.NotNull(mainVm.ControlBar);
        Assert.NotNull(mainVm.LogConsole);
        Assert.NotNull(mainVm.StatusBar);
        Assert.NotNull(mainVm.PerformanceMonitor);
    }

    [Fact]
    public void StatusBarViewModel_WorksWithMockedPorts()
    {
        // Arrange
        var pluginLoader = new PluginLoader();
        var editor = new EditorViewModel(pluginLoader);
        var log = new LogViewModel();
        var inspector = new NodeInspectorViewModel(editor, new FileDialogService(), log);
        var controlBar = new ControlBarViewModel(editor, pluginLoader, log, inspector, new FileDialogService(), new WorkflowStorageService());

        var fakePerfMonitor = new FakePerformanceMonitor();
        var fakeDialog = new FakeDialogService();
        var fakeLauncher = new FakeProcessLauncherService();

        // Act
        var statusBar = new StatusBarViewModel(
            editor,
            controlBar,
            fakePerfMonitor,
            log,
            LocalizationManager.Instance,
            fakeDialog,
            fakeLauncher
        );

        // Assert
        Assert.NotNull(statusBar);
        statusBar.OpenGlobalOutputFolder();
        Assert.True(fakeLauncher.OpenedFolders.Count > 0 || fakeDialog.ErrorMessages.Count > 0);
    }

    private class FakePerformanceMonitor : ISystemPerformanceMonitor
    {
        public event Action<PerformanceMetrics>? PerformanceUpdated;

        public void Trigger(PerformanceMetrics metrics) => PerformanceUpdated?.Invoke(metrics);

        public void Dispose() { }
    }

    private class FakeDialogService : IDialogService
    {
        public List<string> InfoMessages { get; } = [];
        public List<string> ErrorMessages { get; } = [];

        public void ShowInformation(string message, string title = "FileFlow Studio") => InfoMessages.Add(message);
        public void ShowWarning(string message, string title = "FileFlow Studio") { }
        public void ShowError(string message, string title = "Error") => ErrorMessages.Add(message);
        public bool ShowConfirmation(string message, string title = "FileFlow Studio") => true;
        public DialogResult ShowYesNoCancel(string message, string title = "FileFlow Studio") => DialogResult.Yes;
    }

    private class FakeProcessLauncherService : IProcessLauncherService
    {
        public List<string> OpenedFolders { get; } = [];

        public bool OpenUrl(string url) => true;
        public bool OpenFolder(string folderPath)
        {
            OpenedFolders.Add(folderPath);
            return true;
        }
        public bool OpenFileInExplorer(string filePath) => true;
        public bool StartProcess(string fileName, string? arguments = null) => true;
    }
}
