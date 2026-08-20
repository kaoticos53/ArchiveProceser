using FileFlow.Sdk.Localization;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Sdk;

public class LocalizationManagerTests
{
    [Fact]
    public void Instance_ShouldReturnSingleton_WhenAccessedMultipleTimes()
    {
        // Act
        var instance1 = LocalizationManager.Instance;
        var instance2 = LocalizationManager.Instance;

        // Assert
        instance1.Should().NotBeNull();
        instance1.Should().BeSameAs(instance2);
    }

    [Fact]
    public void SetCulture_ShouldFireLanguageChangedEvent_WhenCultureChanges()
    {
        // Arrange
        var manager = LocalizationManager.Instance;
        bool eventFired = false;
        manager.LanguageChanged += (sender, args) => eventFired = true;

        // Act
        manager.SetCulture("en-US");

        // Assert
        eventFired.Should().BeTrue();
    }
}
