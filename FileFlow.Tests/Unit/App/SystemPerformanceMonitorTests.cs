using FileFlow.App.Services;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

public class SystemPerformanceMonitorTests
{
    [Theory]
    [InlineData(0.0, "0%")]
    [InlineData(5.4, "5%")]
    [InlineData(99.9, "100%")]
    public void PerformanceMetrics_GpuFormatted_ShouldFormatCorrectly(double gpuPercent, string expected)
    {
        var metrics = new PerformanceMetrics
        {
            GpuPercentage = gpuPercent
        };

        metrics.GpuFormatted.Should().Be(expected);
    }

    [Theory]
    [InlineData(1048576, "1,0 MB", "1.0 MB")]
    [InlineData(1073741824, "1,00 GB", "1.00 GB")]
    public void PerformanceMetrics_RamFormatted_ShouldFormatMbAndGb(long bytes, string expectedComma, string expectedDot)
    {
        var metrics = new PerformanceMetrics
        {
            WorkingSetBytes = bytes
        };

        metrics.RamFormatted.Should().BeOneOf(expectedComma, expectedDot);
    }

    [Fact]
    public void SystemPerformanceMonitor_CanInstantiateAndDisposeWithoutErrors()
    {
        using var monitor = new SystemPerformanceMonitor();
        monitor.Should().NotBeNull();
    }
}
