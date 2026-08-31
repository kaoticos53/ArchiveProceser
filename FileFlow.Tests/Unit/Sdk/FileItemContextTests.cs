using System;
using System.IO;
using FileFlow.Sdk;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Sdk;

/// <summary>
/// Pruebas unitarias para el modelo fundamental de datos <see cref="FileItemContext"/> del SDK.
/// </summary>
public class FileItemContextTests
{
    /// <summary>
    /// OBJETO: Constructor de <see cref="FileItemContext"/> con archivo existente en disco.
    /// QUÉ:    Verifica la inicialización correcta de rutas, cálculo automático de tamaño en bytes, generación de GUID y colecciones vacías.
    /// CÓMO:  Crea un archivo temporal con contenido de 25 bytes, instancia el contexto y comprueba que FileSizeBytes sea 25 y los tags/metadatos no sean nulos.
    /// </summary>
    [Fact]
    public void Constructor_ShouldInitializeProperties_WhenValidFilePathGiven()
    {
        // Arrange
        string tempFile = Path.Combine(Path.GetTempPath(), $"FileItemTest_{Guid.NewGuid()}.tmp");
        File.WriteAllText(tempFile, "Hello FileFlow Studio SDK");

        try
        {
            // Act
            var context = new FileItemContext(tempFile, isDirectory: false);

            // Assert
            context.CurrentPath.Should().Be(tempFile);
            context.OriginalPath.Should().Be(tempFile);
            context.IsDirectory.Should().BeFalse();
            context.FileSizeBytes.Should().Be(25); // "Hello FileFlow Studio SDK" is 25 bytes
            context.Id.Should().NotBeEmpty();
            context.Metadata.Should().BeEmpty();
            context.Tags.Should().BeEmpty();
            context.ExecutionLog.Should().BeEmpty();
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    /// <summary>
    /// OBJETO: Constructor de <see cref="FileItemContext"/> para directorios.
    /// QUÉ:    Comprueba que cuando se crea un contexto de tipo carpeta, IsDirectory sea true y el tamaño inicial en bytes sea 0.
    /// CÓMO:  Crea un directorio temporal, instancia el contexto con isDirectory = true y verifica las propiedades.
    /// </summary>
    [Fact]
    public void Constructor_ShouldSetSizeToZero_WhenPathIsDirectory()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), $"FileItemDirTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Act
            var context = new FileItemContext(tempDir, isDirectory: true);

            // Assert
            context.CurrentPath.Should().Be(tempDir);
            context.IsDirectory.Should().BeTrue();
            context.FileSizeBytes.Should().Be(0);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// OBJETO: Clonado profundo e inmutabilidad mediante <see cref="FileItemContext.DeepClone"/>.
    /// QUÉ:    Garantiza que al duplicar un contexto para ramificaciones paralelas del grafo DAG, las colecciones de metadatos, tags y trazas se copien por valor y no por referencia.
    /// CÓMO:  Prepara un contexto con tags, trazas y metadatos, genera un clon, muta el clon y verifica que el objeto original permanezca completamente inalterado.
    /// </summary>
    [Fact]
    public void DeepClone_ShouldCreateIndependentCopy_WhenMetadataTagsAndLogsArePresent()
    {
        // Arrange
        var original = new FileItemContext(@"C:\Path\To\File.txt", isDirectory: false)
        {
            FileSizeBytes = 1024
        };
        original.Metadata["Category"] = "Document";
        original.Metadata["ProcessCount"] = 3;
        original.Tags.Add("Urgent");
        original.Tags.Add("PDF");
        original.AddLog("Initial creation log entry.");

        // Act
        var clone = original.DeepClone();

        // Mutate clone to verify independence
        clone.CurrentPath = @"C:\Path\To\NewFile.txt";
        clone.Metadata["Category"] = "Archived";
        clone.Metadata["NewKey"] = "NewValue";
        clone.Tags.Add("Processed");
        clone.AddLog("Clone modification log entry.");

        // Assert
        clone.Id.Should().Be(original.Id);
        original.CurrentPath.Should().Be(@"C:\Path\To\File.txt");
        original.Metadata["Category"].Should().Be("Document");
        original.Metadata.Should().NotContainKey("NewKey");
        original.Tags.Should().NotContain("Processed");
        original.ExecutionLog.Should().HaveCount(1);

        clone.CurrentPath.Should().Be(@"C:\Path\To\NewFile.txt");
        clone.Metadata["Category"].Should().Be("Archived");
        clone.Metadata["NewKey"].Should().Be("NewValue");
        clone.Tags.Should().Contain("Processed");
        clone.ExecutionLog.Should().HaveCount(2);
    }

    /// <summary>
    /// OBJETO: Registro de trazas cronológicas con <see cref="FileItemContext.AddLog"/>.
    /// QUÉ:    Valida que cada mensaje añadido al log de ejecución contenga una marca de tiempo formateada entre corchetes.
    /// CÓMO:  Instancia un contexto, invoca AddLog con un mensaje y comprueba que la primera entrada empiece con '[' y contenga el texto.
    /// </summary>
    [Fact]
    public void AddLog_ShouldAppendTimestampedLogEntry()
    {
        // Arrange
        var context = new FileItemContext(@"C:\Test\File.txt");

        // Act
        context.AddLog("Step 1 executed successfully.");

        // Assert
        context.ExecutionLog.Should().HaveCount(1);
        context.ExecutionLog[0].Should().Contain("Step 1 executed successfully.");
        context.ExecutionLog[0].Should().StartWith("[");
    }

    /// <summary>
    /// OBJETO: Resiliencia ante rutas inexistentes en el constructor de <see cref="FileItemContext"/>.
    /// QUÉ:    Asegura que si se pasa una ruta de archivo que no existe físicamente en disco, no se lancen excepciones no controladas y el tamaño se asigne a 0.
    /// CÓMO:  Genera una ruta sintética inexistente, instancia el contexto y verifica que CurrentPath coincida y FileSizeBytes sea 0.
    /// </summary>
    [Fact]
    public void Constructor_ShouldHandleNonExistentFileGracefully_WithoutThrowingException()
    {
        // Arrange
        string nonExistentPath = @"C:\NonExistentDirectory\FakeFile_" + Guid.NewGuid() + ".txt";

        // Act
        var context = new FileItemContext(nonExistentPath, isDirectory: false);

        // Assert
        context.CurrentPath.Should().Be(nonExistentPath);
        context.FileSizeBytes.Should().Be(0);
        context.IsDirectory.Should().BeFalse();
    }
}
