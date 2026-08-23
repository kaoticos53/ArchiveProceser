using System.Globalization;
using System.Windows;
using FluentAssertions;
using FileFlow.App.Converters;
using FileFlow.Sdk;
using Xunit;

namespace FileFlow.Tests.Unit.App;

public class ValueConvertersExhaustiveTests
{
    [Theory]
    [InlineData(LogLevel.Critical, "CRITICAL")]
    [InlineData(LogLevel.Error, "ERROR")]
    [InlineData(LogLevel.Warning, "WARN")]
    [InlineData(LogLevel.Information, "INFO")]
    [InlineData(LogLevel.Debug, "DEBUG")]
    [InlineData(LogLevel.Trace, "TRACE")]
    public void LogLevelToBadgeConverter_ShouldReturnShortBadges(LogLevel level, string expectedBadge)
    {
        // Arrange
        var converter = new LogLevelToBadgeConverter();

        // Act
        var result = converter.Convert(level, typeof(string), null!, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(expectedBadge);
    }

    [Fact]
    public void EnumToBooleanConverter_ShouldConvertBidirectionally()
    {
        // Arrange
        var converter = new EnumToBooleanConverter();

        // Act & Assert: Convert (Enum -> Bool)
        converter.Convert(LogLevel.Warning, typeof(bool), "Warning", CultureInfo.InvariantCulture).Should().Be(true);
        converter.Convert(LogLevel.Warning, typeof(bool), "Information", CultureInfo.InvariantCulture).Should().Be(false);

        // Act & Assert: ConvertBack (Bool -> Enum)
        converter.ConvertBack(true, typeof(LogLevel), "Error", CultureInfo.InvariantCulture).Should().Be(LogLevel.Error);
    }

    [Fact]
    public void InverseBooleanToVisibilityConverter_ShouldInvertCorrectly()
    {
        // Arrange
        var converter = new InverseBooleanToVisibilityConverter();

        // Act & Assert
        converter.Convert(true, typeof(Visibility), null!, CultureInfo.InvariantCulture).Should().Be(Visibility.Collapsed);
        converter.Convert(false, typeof(Visibility), null!, CultureInfo.InvariantCulture).Should().Be(Visibility.Visible);
    }

    [Fact]
    public void BooleanToGridLengthConverter_ShouldReturnAppropriateWidth()
    {
        // Arrange
        var converter = new BooleanToGridLengthConverter { DefaultWidth = 320 };

        // Act & Assert
        var visibleLength = (GridLength)converter.Convert(true, typeof(GridLength), null!, CultureInfo.InvariantCulture);
        visibleLength.Value.Should().Be(320);

        var collapsedLength = (GridLength)converter.Convert(false, typeof(GridLength), null!, CultureInfo.InvariantCulture);
        collapsedLength.Value.Should().Be(0);
    }
}
