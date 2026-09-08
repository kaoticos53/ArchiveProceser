using FileFlow.App.Services;
using FileFlow.App.Themes;
using FileFlow.App.ViewModels;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Services;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.App;

public class WorkflowSettingsViewModelTests
{
    private readonly Mock<IUserPreferencesService> _prefsMock = new();
    private readonly Mock<IExternalToolsService> _toolsMock = new();
    private readonly Mock<IThemeService> _themeMock = new();
    private readonly Mock<ILocalizationService> _locMock = new();
    private readonly Mock<IFileDialogService> _fileDialogMock = new();
    private readonly Mock<IDialogService> _dialogMock = new();

    public WorkflowSettingsViewModelTests()
    {
        _prefsMock.Setup(p => p.Preferences).Returns(new UserPreferencesData
        {
            DefaultGlobalOutputDir = @"C:\DefaultOutput",
            TemporaryDirectory = @"C:\Temp",
            DefaultConflictStrategy = "Overwrite",
            EnableAutoSave = true,
            AutoSaveIntervalMinutes = 10,
            ActiveTheme = "dark_fluent",
            MaxLogEntries = 500,
            MaxParallelThreads = 4,
            DefaultDryRunState = false,
            DefaultLogLevel = "Warning",
            EnableCheckpointing = false,
            AutoUnloadAiModelsOnCompletion = false
        });

        _toolsMock.Setup(t => t.Config).Returns(new ExternalToolsConfig
        {
            FfmpegPath = @"C:\Tools\ffmpeg.exe",
            FfprobePath = @"C:\Tools\ffprobe.exe",
            SevenZipPath = @"C:\Tools\7z.exe",
            PythonPath = @"C:\Tools\python.exe"
        });

        _locMock.Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string key, string def) => def);
    }

    [Fact]
    public void Initialize_LoadsPreferencesAndToolsCorrectly()
    {
        var vm = new WorkflowSettingsViewModel(
            _prefsMock.Object,
            _toolsMock.Object,
            _themeMock.Object,
            _locMock.Object,
            _fileDialogMock.Object,
            _dialogMock.Object,
            new AiModelManagerViewModel(_locMock.Object));

        vm.Initialize(@"C:\CustomOutput");

        Assert.Equal(@"C:\CustomOutput", vm.GlobalOutputDir);
        Assert.Equal(@"C:\Temp", vm.TempWorkingDir);
        Assert.Equal("Overwrite", vm.SelectedConflictStrategy);
        Assert.Equal(10, vm.AutoSaveIntervalMinutes);
        Assert.Equal("dark_fluent", vm.SelectedThemeId);
        Assert.Equal(500, vm.MaxLogEntries);
        Assert.Equal(4, vm.MaxParallelThreads);
        Assert.Equal("Warning", vm.SelectedLogLevel);
        Assert.False(vm.EnableCheckpointing);
        Assert.False(vm.AutoUnloadAiModelsOnCompletion);

        Assert.Equal(@"C:\Tools\ffmpeg.exe", vm.FfmpegPath);
        Assert.Equal(@"C:\Tools\ffprobe.exe", vm.FfprobePath);
        Assert.Equal(@"C:\Tools\7z.exe", vm.SevenZipPath);
        Assert.Equal(@"C:\Tools\python.exe", vm.PythonPath);
    }

    [Fact]
    public void BrowseCommands_UpdatePathsWhenFolderOrFileSelected()
    {
        var vm = new WorkflowSettingsViewModel(
            _prefsMock.Object,
            _toolsMock.Object,
            _themeMock.Object,
            _locMock.Object,
            _fileDialogMock.Object,
            _dialogMock.Object,
            new AiModelManagerViewModel(_locMock.Object));

        _fileDialogMock.Setup(f => f.ShowFolderBrowserDialog(It.IsAny<string>()))
            .Returns(@"D:\SelectedFolder");
        _fileDialogMock.Setup(f => f.ShowOpenFileDialog(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(@"D:\SelectedTool.exe");

        vm.BrowseGlobalOutputCommand.Execute(null);
        Assert.Equal(@"D:\SelectedFolder", vm.GlobalOutputDir);

        vm.BrowseTempWorkingDirCommand.Execute(null);
        Assert.Equal(@"D:\SelectedFolder", vm.TempWorkingDir);

        vm.BrowseFfmpegCommand.Execute(null);
        Assert.Equal(@"D:\SelectedTool.exe", vm.FfmpegPath);

        vm.BrowseFfprobeCommand.Execute(null);
        Assert.Equal(@"D:\SelectedTool.exe", vm.FfprobePath);

        vm.BrowseSevenZipCommand.Execute(null);
        Assert.Equal(@"D:\SelectedTool.exe", vm.SevenZipPath);

        vm.BrowsePythonCommand.Execute(null);
        Assert.Equal(@"D:\SelectedTool.exe", vm.PythonPath);
    }

    [Fact]
    public async Task AutoDetectToolsAsync_UpdatesToolsWhenDetected()
    {
        var vm = new WorkflowSettingsViewModel(
            _prefsMock.Object,
            _toolsMock.Object,
            _themeMock.Object,
            _locMock.Object,
            _fileDialogMock.Object,
            _dialogMock.Object,
            new AiModelManagerViewModel(_locMock.Object));

        _toolsMock.Setup(t => t.AutoDetectToolsAsync()).ReturnsAsync(new ExternalToolsConfig
        {
            FfmpegPath = @"C:\Detected\ffmpeg.exe",
            FfprobePath = @"C:\Detected\ffprobe.exe",
            SevenZipPath = @"C:\Detected\7z.exe",
            PythonPath = @"C:\Detected\python.exe"
        });

        await vm.AutoDetectToolsCommand.ExecuteAsync(null);

        Assert.Equal(@"C:\Detected\ffmpeg.exe", vm.FfmpegPath);
        Assert.Equal(@"C:\Detected\ffprobe.exe", vm.FfprobePath);
        Assert.Equal(@"C:\Detected\7z.exe", vm.SevenZipPath);
        Assert.Equal(@"C:\Detected\python.exe", vm.PythonPath);
        _dialogMock.Verify(d => d.ShowInformation(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void SaveCommand_UpdatesPreferencesAndTriggersRequestCloseWithTrue()
    {
        var vm = new WorkflowSettingsViewModel(
            _prefsMock.Object,
            _toolsMock.Object,
            _themeMock.Object,
            _locMock.Object,
            _fileDialogMock.Object,
            _dialogMock.Object,
            new AiModelManagerViewModel(_locMock.Object));

        vm.Initialize();
        vm.GlobalOutputDir = @"E:\NewOutput";
        vm.FfmpegPath = @"E:\Bin\ffmpeg.exe";

        bool? closeResult = null;
        vm.RequestClose += res => closeResult = res;

        vm.SaveCommand.Execute(null);

        Assert.True(closeResult);
        _prefsMock.Verify(p => p.UpdatePreferences(It.IsAny<Action<UserPreferencesData>>()), Times.Once);
        _toolsMock.Verify(t => t.SaveConfig(It.Is<ExternalToolsConfig>(c => c.FfmpegPath == @"E:\Bin\ffmpeg.exe")), Times.Once);
    }

    [Fact]
    public void CancelCommand_TriggersRequestCloseWithFalse()
    {
        var vm = new WorkflowSettingsViewModel(
            _prefsMock.Object,
            _toolsMock.Object,
            _themeMock.Object,
            _locMock.Object,
            _fileDialogMock.Object,
            _dialogMock.Object,
            new AiModelManagerViewModel(_locMock.Object));

        bool? closeResult = null;
        vm.RequestClose += res => closeResult = res;

        vm.CancelCommand.Execute(null);

        Assert.False(closeResult);
    }
}
