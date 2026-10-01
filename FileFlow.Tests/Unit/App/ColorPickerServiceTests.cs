using System;
using FileFlow.App.Services;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

public class ColorPickerServiceTests
{
    [Fact]
    public void Instance_ShouldNotBeNull()
    {
        ColorPickerService.Instance.Should().NotBeNull();
    }

    [Fact]
    public void PickColorHex_OnNonWindows_ShouldReturnNullWithoutThrowing()
    {
        if (!OperatingSystem.IsWindows())
        {
            var result = ColorPickerService.Instance.PickColorHex();
            result.Should().BeNull();
        }
    }
}
