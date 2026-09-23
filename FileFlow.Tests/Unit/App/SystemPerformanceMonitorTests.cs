using System.Collections.Generic;
using System.Threading.Tasks;
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

    // ─────────────────────────────────────────────────────────────────────────────
    // El latido del muestreo: el camino que sólo corría en la aplicación
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Hasta ahora sólo se probaba el formateo de las métricas y que el monitor se pueda construir: el tick —con
    /// su guarda de reentrada, su captura de excepciones y su comprobación de desecho— no se ejecutaba nunca en
    /// el suite. Estos tres casos son sus tres salidas posibles.
    /// </summary>
    [Fact]
    public async Task TheHeartbeat_ShouldPublishAPlausibleSample()
    {
        using var monitor = new SystemPerformanceMonitor();
        var samples = new List<PerformanceMetrics>();
        monitor.PerformanceUpdated += metrics => samples.Add(metrics);

        await monitor.SampleNowAsync();

        samples.Should().ContainSingle("un latido publica una muestra");
        samples[0].WorkingSetBytes.Should().BeGreaterThan(0, "el proceso que la mide ocupa memoria");
        samples[0].CpuPercentage.Should().BeInRange(0, 100, "el porcentaje se acota antes de publicarse");
    }

    [Fact]
    public async Task TheHeartbeat_ShouldNotOverlap_WhenTwoTicksCoincide()
    {
        using var monitor = new SystemPerformanceMonitor();
        int published = 0;
        monitor.PerformanceUpdated += _ => published++;

        await Task.WhenAll(monitor.SampleNowAsync(), monitor.SampleNowAsync());

        published.Should().Be(1,
            "la guarda se levanta antes del primer await: dos ticks solapados no pueden muestrear dos veces");
    }

    [Fact]
    public async Task TheHeartbeat_ShouldStaySilent_AfterDispose()
    {
        var monitor = new SystemPerformanceMonitor();
        int published = 0;
        monitor.PerformanceUpdated += _ => published++;

        monitor.Dispose();
        await monitor.SampleNowAsync();

        published.Should().Be(0,
            "un latido que llegue con el monitor ya desechado no puede publicar métricas de un proceso liberado");
    }
}
