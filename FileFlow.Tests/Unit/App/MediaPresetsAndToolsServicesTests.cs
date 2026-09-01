using FileFlow.App.Services;
using FileFlow.Plugin.Integrations.UI.Services;
using Xunit;

namespace FileFlow.Tests.Unit.App;

public class MediaPresetsAndToolsServicesTests
{
    [Fact]
    public void MediaPresetManagerService_ShouldContainDefaultPresets()
    {
        var presets = MediaPresetManagerService.Instance.GetPresets();
        Assert.NotEmpty(presets);
        Assert.True(presets.Count >= 10);
    }

    [Fact]
    public void MediaPresetManagerService_GetPresetByName_ReturnsValidPreset()
    {
        var preset = MediaPresetManagerService.Instance.GetPresetByName("Extraer Audio MP3");
        Assert.NotNull(preset);
        Assert.Equal(".mp3", preset.OutputExtension);
        Assert.Contains("libmp3lame", preset.FfmpegArguments);
    }

    [Fact]
    public void ExternalToolsService_Instance_ShouldLoadConfigWithoutExceptions()
    {
        var config = ExternalToolsService.Instance.Config;
        Assert.NotNull(config);
    }

    [Fact]
    public void UserPreferencesService_ShouldContainValidPersistentDefaults()
    {
        var prefs = UserPreferencesService.Instance.Preferences;
        Assert.NotNull(prefs);
        Assert.False(string.IsNullOrWhiteSpace(prefs.DefaultGlobalOutputDir));
        Assert.False(string.IsNullOrWhiteSpace(prefs.ActiveTheme));
        Assert.True(prefs.MaxParallelThreads > 0);
    }
}
