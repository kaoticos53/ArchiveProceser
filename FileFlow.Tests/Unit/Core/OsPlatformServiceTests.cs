using System.Runtime.InteropServices;
using FileFlow.Core.Platform;
using FileFlow.Sdk.Platform;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Core;

public class OsPlatformServiceTests
{
    [Fact]
    public void OsPlatformServiceFactory_ReturnsAppropriatePlatformService()
    {
        var platform = OsPlatformServiceFactory.Instance;

        platform.Should().NotBeNull();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            platform.IsWindows.Should().BeTrue();
            platform.Platform.Should().Be(OSPlatform.Windows);
            platform.Should().BeOfType<WindowsPlatformService>();
            platform.GetDefaultShellExecutable().Should().Be("cmd.exe");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            platform.IsLinux.Should().BeTrue();
            platform.Platform.Should().Be(OSPlatform.Linux);
            platform.Should().BeOfType<LinuxPlatformService>();
            platform.GetDefaultShellExecutable().Should().Be("/bin/bash");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            platform.IsMac.Should().BeTrue();
            platform.Platform.Should().Be(OSPlatform.OSX);
            platform.Should().BeOfType<MacPlatformService>();
            platform.GetDefaultShellExecutable().Should().Be("/bin/zsh");
        }
    }

    [Fact]
    public void ShellArgumentFormatting_AdaptsPerPlatform()
    {
        var win = WindowsPlatformService.Instance;
        win.GetDefaultShellArguments("Get-Process").Should().Be("/c Get-Process");

        var linux = LinuxPlatformService.Instance;
        linux.GetDefaultShellArguments("ls -la").Should().Be("-c \"ls -la\"");

        var mac = MacPlatformService.Instance;
        mac.GetDefaultShellArguments("echo test").Should().Be("-c \"echo test\"");
    }

    [Fact]
    public void NullOsPlatformService_ProvidesSafeNoOpDefaults()
    {
        var nullPlatform = NullOsPlatformService.Instance;

        nullPlatform.Platform.Should().NotBeNull();
        nullPlatform.GetDefaultShellExecutable().Should().NotBeNullOrWhiteSpace();
        nullPlatform.GetDefaultShellArguments("echo test").Should().Contain("test");

        // Should not throw
        var recycleResult = nullPlatform.MoveToTrash("non_existent_file.txt");
        recycleResult.Should().BeFalse();

        nullPlatform.TrimWorkingSet();
    }
}
