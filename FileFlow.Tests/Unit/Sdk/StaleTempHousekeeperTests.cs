using System.IO;
using FileFlow.Sdk.Storage;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Sdk;

[Collection("AppPaths")]
public class StaleTempHousekeeperTests
{
    [Fact]
    public void CleanupStaleTempDirectories_ShouldRemoveOldDirectoriesAndKeepRecentOnes()
    {
        // Arrange
        AppPaths.EnsureDirectories();
        string runsDir = AppPaths.RunsDirectory;
        Directory.CreateDirectory(runsDir);

        string staleDir = Path.Combine(runsDir, "StaleRun_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staleDir);
        File.WriteAllText(Path.Combine(staleDir, "dummy.txt"), "old run");

        string freshDir = Path.Combine(runsDir, "FreshRun_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(freshDir);
        File.WriteAllText(Path.Combine(freshDir, "dummy.txt"), "fresh run");

        // Backdate staleDir creation and write time by 3 hours
        Directory.SetLastWriteTime(staleDir, DateTime.Now.AddHours(-3));
        Directory.SetCreationTime(staleDir, DateTime.Now.AddHours(-3));

        // Act: cleanup anything older than 1 hour
        long cleaned = AppPaths.CleanupStaleTempDirectories(TimeSpan.FromHours(1));

        // Assert
        cleaned.Should().BeGreaterOrEqualTo(1);
        Directory.Exists(staleDir).Should().BeFalse();
        Directory.Exists(freshDir).Should().BeTrue();

        // Cleanup freshDir
        try { Directory.Delete(freshDir, true); } catch { }
    }

    [Fact]
    public void CleanupStaleTempDirectories_WithZeroTimeSpan_ShouldRemoveAllInactiveRuns()
    {
        // Arrange
        AppPaths.EnsureDirectories();
        string runsDir = AppPaths.RunsDirectory;
        Directory.CreateDirectory(runsDir);

        string testRunDir = Path.Combine(runsDir, "ManualCleanRun_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRunDir);
        File.WriteAllText(Path.Combine(testRunDir, "test.tmp"), "some data");

        // Act: cleanup with TimeSpan.Zero
        long cleaned = AppPaths.CleanupStaleTempDirectories(TimeSpan.Zero);

        // Assert
        cleaned.Should().BeGreaterOrEqualTo(1);
        Directory.Exists(testRunDir).Should().BeFalse();
    }
}
