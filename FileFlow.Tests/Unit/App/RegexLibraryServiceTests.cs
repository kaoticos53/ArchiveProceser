using System.IO;
using FileFlow.App.Services;
using FileFlow.Sdk.Renaming;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

public class RegexLibraryServiceTests : IDisposable
{
    private readonly string _tempFilePath;

    public RegexLibraryServiceTests()
    {
        _tempFilePath = Path.Combine(Path.GetTempPath(), $"regex_lib_test_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFilePath))
        {
            try { File.Delete(_tempFilePath); } catch { }
        }
    }

    [Fact]
    public void GetBuiltInPatterns_ShouldReturnCompleteCuratedList()
    {
        // Arrange
        var service = new RegexLibraryService(_tempFilePath);

        // Act
        var builtIn = service.GetBuiltInPatterns();

        // Assert
        builtIn.Should().NotBeNullOrEmpty();
        builtIn.Should().Contain(p => p.Category == "Series y Vídeo" && p.Pattern.Contains("xX"));
        builtIn.Should().Contain(p => p.Category == "Fechas y Tiempos");
        builtIn.Should().Contain(p => p.Category == "Limpieza de Nombres");
        builtIn.All(p => p.IsBuiltIn).Should().BeTrue();
    }

    [Fact]
    public void SaveUserPattern_And_DeleteUserPattern_ShouldPersistAndRemove()
    {
        // Arrange
        var service = new RegexLibraryService(_tempFilePath);
        var custom = new RegexPatternItem
        {
            Name = "Mi Patrón Personalizado",
            Category = "Proyectos",
            Pattern = @"PRJ-(\d{4})_(\w+)",
            Replacement = "PROYECTO_$1_$2",
            Description = "Patrón para códigos de proyecto"
        };

        // Act - Save
        service.SaveUserPattern(custom);
        var userPatterns = service.GetUserPatterns();

        // Assert - Save
        userPatterns.Should().HaveCount(1);
        userPatterns[0].Name.Should().Be("Mi Patrón Personalizado");
        userPatterns[0].Pattern.Should().Be(@"PRJ-(\d{4})_(\w+)");

        // Act - Delete
        bool deleted = service.DeleteUserPattern(custom.Id);
        var patternsAfterDelete = service.GetUserPatterns();

        // Assert - Delete
        deleted.Should().BeTrue();
        patternsAfterDelete.Should().BeEmpty();
    }

    [Fact]
    public void ExportAndImport_ShouldRestoreUserPatterns()
    {
        // Arrange
        var service1 = new RegexLibraryService(_tempFilePath);
        service1.SaveUserPattern(new RegexPatternItem
        {
            Name = "Facturas",
            Pattern = @"FAC_(\d+)"
        });

        // Act
        string json = service1.ExportToJson();
        string temp2 = Path.Combine(Path.GetTempPath(), $"regex_lib_test2_{Guid.NewGuid():N}.json");
        var service2 = new RegexLibraryService(temp2);
        int imported = service2.ImportFromJson(json);

        // Assert
        imported.Should().Be(1);
        var list = service2.GetUserPatterns();
        list.Should().HaveCount(1);
        list[0].Name.Should().Be("Facturas");
        list[0].Pattern.Should().Be(@"FAC_(\d+)");

        try { File.Delete(temp2); } catch { }
    }
}
