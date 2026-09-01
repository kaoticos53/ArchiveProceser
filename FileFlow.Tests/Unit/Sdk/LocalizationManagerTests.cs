using FileFlow.Sdk.Localization;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Sdk;

[Collection("Localization")]
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

    [Fact]
    public void SetCulture_ShouldRaisePropertyChangedForIndexer_WhenCultureChanges()
    {
        // Arrange
        var manager = LocalizationManager.Instance;
        manager.SetCulture("fr-FR"); // Asegurar valor previo distinto

        var changedProperties = new List<string?>();
        manager.PropertyChanged += (sender, args) => changedProperties.Add(args.PropertyName);

        try
        {
            // Act
            manager.SetCulture("es-ES");

            // Assert
            changedProperties.Should().Contain(p => p == "Item[]");
            changedProperties.Should().Contain(p => p == string.Empty);
        }
        finally
        {
            manager.SetCulture("es-ES");
        }
    }
}
