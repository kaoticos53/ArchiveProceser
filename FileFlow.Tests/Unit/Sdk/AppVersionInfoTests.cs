using System.Text.RegularExpressions;
using FileFlow.Sdk;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Sdk;

public class AppVersionInfoTests
{
    [Fact]
    public void AppVersionInfo_ShouldFollowSemVer2Format()
    {
        // Assert
        AppVersionInfo.InformationalVersion.Should().NotBeNullOrWhiteSpace();
        AppVersionInfo.DisplayVersion.Should().StartWith("v");

        // Format: MAJOR.MINOR.PATCH(-PRERELEASE)?+build.BUILDNUMBER
        string semVerPattern = @"^\d+\.\d+\.\d+(-[a-zA-Z0-9\.]+)?\+build\.\d+$";
        Regex.IsMatch(AppVersionInfo.InformationalVersion, semVerPattern).Should().BeTrue(
            $"porque InformationalVersion '{AppVersionInfo.InformationalVersion}' debe cumplir con SemVer 2.0 y metadatos de build.");
    }

    [Fact]
    public void AppVersionInfo_Properties_ShouldBeCorrectlyExtracted()
    {
        // Assert
        AppVersionInfo.Major.Should().Be(1);
        AppVersionInfo.Minor.Should().Be(0);
        AppVersionInfo.Patch.Should().Be(0);
        AppVersionInfo.PreRelease.Should().Be("beta");
        AppVersionInfo.BuildMetadata.Should().StartWith("build.");
    }
}
