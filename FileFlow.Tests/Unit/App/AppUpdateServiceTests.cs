using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using FileFlow.App.Services;
using FileFlow.Sdk.Services;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

public class AppUpdateServiceTests
{
    [Theory]
    [InlineData("1.0.0", "1.0.0", 0)]
    [InlineData("1.1.0", "1.0.0", 1)]
    [InlineData("1.0.0", "1.1.0", -1)]
    [InlineData("2.0.0", "1.99.99", 1)]
    [InlineData("1.0.1", "1.0.0", 1)]
    [InlineData("1.0.0", "1.0.0-beta", 1)] // Final release is higher than pre-release
    [InlineData("1.0.0-beta.2", "1.0.0-beta.1", 1)]
    [InlineData("v1.0.0+build.200", "1.0.0+build.100", 1)]
    [InlineData("v2.5.1-rc.1+build.500", "v2.5.0", 1)]
    public void SemVersion_Comparison_ShouldMatchExpectedOrder(string v1, string v2, int expectedSign)
    {
        var sem1 = SemVersion.Parse(v1);
        var sem2 = SemVersion.Parse(v2);

        int cmp = sem1.CompareTo(sem2);
        int sign = Math.Sign(cmp);

        sign.Should().Be(expectedSign, $"comparing '{v1}' with '{v2}'");
    }

    [Fact]
    public void SemVersion_ToString_ShouldFormatCorrectly()
    {
        var sem = new SemVersion(1, 2, 3, "beta.1", 456);
        sem.ToString().Should().Be("1.2.3-beta.1+build.456");

        var clean = new SemVersion(2, 0, 0);
        clean.ToString().Should().Be("2.0.0");
    }

    [Fact]
    public void ResolveAssetForPlatform_ShouldMatchCorrectAssetForEveryPlatform()
    {
        // Arrange
        var assets = new List<AppReleaseAssetInfo>
        {
            new("FileFlowStudio-Setup-1.5.0.exe", "https://github.com/.../Setup.exe", 85000000, ""),
            new("FileFlowStudio-Portable-v1.5.0-win-x64.zip", "https://github.com/.../Portable.zip", 75000000, ""),
            new("FileFlow-v1.5.0-x86_64.AppImage", "https://github.com/.../AppImage", 95000000, ""),
            new("FileFlow-v1.5.0-x86_64.flatpak", "https://github.com/.../flatpak", 92000000, ""),
            new("fileflow_1.5.0_amd64.deb", "https://github.com/.../deb", 90000000, ""),
            new("fileflow-linux-x64-v1.5.0.tar.gz", "https://github.com/.../tar.gz", 88000000, ""),
            new("fileflow-linux-x64-v1.5.0.AppDir.tar.gz", "https://github.com/.../AppDir.tar.gz", 88000000, ""),
            new("checksums.txt", "https://github.com/.../checksums.txt", 1024, "")
        };

        var checksumTable = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["FileFlowStudio-Setup-1.5.0.exe"] = "hash_setup_exe",
            ["FileFlowStudio-Portable-v1.5.0-win-x64.zip"] = "hash_portable_zip",
            ["FileFlow-v1.5.0-x86_64.AppImage"] = "hash_appimage",
            ["FileFlow-v1.5.0-x86_64.flatpak"] = "hash_flatpak",
            ["fileflow_1.5.0_amd64.deb"] = "hash_deb",
            ["fileflow-linux-x64-v1.5.0.tar.gz"] = "hash_targz"
        };

        // Act & Assert
        var winInstalled = AppUpdateService.ResolveAssetForPlatform(assets, AppPackagingFormat.WindowsInstalled, checksumTable);
        winInstalled.Should().NotBeNull();
        winInstalled!.Name.Should().Be("FileFlowStudio-Setup-1.5.0.exe");
        winInstalled.Sha256Hash.Should().Be("hash_setup_exe");

        var winPortable = AppUpdateService.ResolveAssetForPlatform(assets, AppPackagingFormat.WindowsPortable, checksumTable);
        winPortable.Should().NotBeNull();
        winPortable!.Name.Should().Be("FileFlowStudio-Portable-v1.5.0-win-x64.zip");
        winPortable.Sha256Hash.Should().Be("hash_portable_zip");

        var linuxAppImage = AppUpdateService.ResolveAssetForPlatform(assets, AppPackagingFormat.LinuxAppImage, checksumTable);
        linuxAppImage.Should().NotBeNull();
        linuxAppImage!.Name.Should().Be("FileFlow-v1.5.0-x86_64.AppImage");
        linuxAppImage.Sha256Hash.Should().Be("hash_appimage");

        var linuxFlatpak = AppUpdateService.ResolveAssetForPlatform(assets, AppPackagingFormat.LinuxFlatpak, checksumTable);
        linuxFlatpak.Should().NotBeNull();
        linuxFlatpak!.Name.Should().Be("FileFlow-v1.5.0-x86_64.flatpak");

        var linuxDeb = AppUpdateService.ResolveAssetForPlatform(assets, AppPackagingFormat.LinuxDebPackage, checksumTable);
        linuxDeb.Should().NotBeNull();
        linuxDeb!.Name.Should().Be("fileflow_1.5.0_amd64.deb");

        var linuxTarGz = AppUpdateService.ResolveAssetForPlatform(assets, AppPackagingFormat.LinuxGenericTarball, checksumTable);
        linuxTarGz.Should().NotBeNull();
        linuxTarGz!.Name.Should().Be("fileflow-linux-x64-v1.5.0.tar.gz");
    }

    [Fact]
    public async Task ComputeSha256Async_ShouldCalculateAccurateHash()
    {
        // Arrange
        string tempFile = Path.Combine(Path.GetTempPath(), $"sha_test_{Guid.NewGuid():N}.txt");
        string testContent = "FileFlow Studio Cryptographic Integrity Test 2026";
        var utf8NoBom = new UTF8Encoding(false);
        await File.WriteAllBytesAsync(tempFile, utf8NoBom.GetBytes(testContent));

        try
        {
            // Expected
            using var sha = SHA256.Create();
            byte[] expectedBytes = sha.ComputeHash(utf8NoBom.GetBytes(testContent));
            string expectedHex = Convert.ToHexStringLower(expectedBytes);

            // Act
            string actualHex = await AppUpdateService.ComputeSha256Async(tempFile);

            // Assert
            actualHex.Should().Be(expectedHex);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WithNewerRelease_ShouldReturnUpdateAvailable()
    {
        // Arrange
        string mockReleasesJson = """
        [
          {
            "tag_name": "v99.0.0",
            "name": "FileFlow Studio v99.0.0",
            "body": "## Novedades\n- Increíbles mejoras en DAG Engine",
            "draft": false,
            "prerelease": false,
            "published_at": "2026-09-19T12:00:00Z",
            "html_url": "https://github.com/kaoticos53/ArchiveProceser/releases/tag/v99.0.0",
            "assets": [
              {
                "name": "FileFlowStudio-Setup-99.0.0.exe",
                "browser_download_url": "https://github.com/kaoticos53/ArchiveProceser/releases/download/v99.0.0/FileFlowStudio-Setup-99.0.0.exe",
                "size": 85000000
              },
              {
                "name": "FileFlowStudio-Portable-v99.0.0-win-x64.zip",
                "browser_download_url": "https://github.com/kaoticos53/ArchiveProceser/releases/download/v99.0.0/FileFlowStudio-Portable-v99.0.0-win-x64.zip",
                "size": 75000000
              },
              {
                "name": "FileFlow-v99.0.0-x86_64.AppImage",
                "browser_download_url": "https://github.com/kaoticos53/ArchiveProceser/releases/download/v99.0.0/FileFlow-v99.0.0-x86_64.AppImage",
                "size": 95000000
              },
              {
                "name": "checksums.txt",
                "browser_download_url": "https://github.com/kaoticos53/ArchiveProceser/releases/download/v99.0.0/checksums.txt",
                "size": 128
              }
            ]
          }
        ]
        """;

        string mockChecksums = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 *FileFlowStudio-Setup-99.0.0.exe\n" +
                               "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 *FileFlowStudio-Portable-v99.0.0-win-x64.zip\n" +
                               "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 *FileFlow-v99.0.0-x86_64.AppImage\n";

        var handler = new MockHttpMessageHandler((req) =>
        {
            if (req.RequestUri?.ToString().Contains("checksums.txt") == true)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(mockChecksums, Encoding.UTF8, "text/plain")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(mockReleasesJson, Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler);
        var service = new AppUpdateService(httpClient, "kaoticos53", "ArchiveProceser");

        // Act
        var result = await service.CheckForUpdatesAsync(UpdateChannel.Stable, force: true);

        // Assert
        result.UpdateAvailable.Should().BeTrue();
        result.UpdateInfo.Should().NotBeNull();
        result.UpdateInfo!.VersionTag.Should().Be("v99.0.0");
        result.UpdateInfo.Title.Should().Be("FileFlow Studio v99.0.0");
        result.UpdateInfo.ReleaseNotesMarkdown.Should().Contain("Increíbles mejoras");
        result.UpdateInfo.MatchedAsset.Should().NotBeNull();
        result.UpdateInfo.MatchedAsset!.Sha256Hash.Should().Be("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenPrereleaseAndChannelIsStable_ShouldSkipPrerelease()
    {
        // Arrange
        string mockReleasesJson = """
        [
          {
            "tag_name": "v99.0.0-beta.1",
            "name": "FileFlow Studio v99.0.0 Beta",
            "body": "Versión preliminar",
            "draft": false,
            "prerelease": true,
            "published_at": "2026-09-19T12:00:00Z",
            "html_url": "https://github.com/kaoticos53/ArchiveProceser/releases/tag/v99.0.0-beta.1",
            "assets": []
          }
        ]
        """;

        var handler = new MockHttpMessageHandler((req) =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(mockReleasesJson, Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler);
        var service = new AppUpdateService(httpClient, "kaoticos53", "ArchiveProceser");

        // Act for Stable
        var stableResult = await service.CheckForUpdatesAsync(UpdateChannel.Stable, force: true);
        stableResult.UpdateAvailable.Should().BeFalse();

        // Act for Beta
        var betaResult = await service.CheckForUpdatesAsync(UpdateChannel.Beta, force: true);
        betaResult.UpdateAvailable.Should().BeTrue();
        betaResult.UpdateInfo!.VersionTag.Should().Be("v99.0.0-beta.1");
    }

    private class MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handlerFunc) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handlerFunc(request));
        }
    }
}

