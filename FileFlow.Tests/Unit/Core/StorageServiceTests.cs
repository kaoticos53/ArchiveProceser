using System;
using System.IO;
using System.Linq;
using System.Text;
using FileFlow.Core.Engine;
using FileFlow.Core.Platform;
using FileFlow.Core.Storage;
using FileFlow.Sdk;
using FileFlow.Sdk.Storage;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Core;

public class StorageServiceTests
{
    [Fact]
    public async Task PhysicalStorageService_FileAndDirectoryOperations_WorkCorrectly()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "StorageTest_" + Guid.NewGuid().ToString("N"));
        var storage = new PhysicalStorageService();

        try
        {
            (await storage.DirectoryExistsAsync(tempDir)).Should().BeFalse();
            await storage.CreateDirectoryAsync(tempDir);
            (await storage.DirectoryExistsAsync(tempDir)).Should().BeTrue();

            string testFile = Path.Combine(tempDir, "sample.txt");
            (await storage.FileExistsAsync(testFile)).Should().BeFalse();

            byte[] data = Encoding.UTF8.GetBytes("FileFlow Physical Storage Test");
            await storage.WriteAllBytesAsync(testFile, data);

            (await storage.FileExistsAsync(testFile)).Should().BeTrue();
            var readData = await storage.ReadAllBytesAsync(testFile);
            readData.Should().BeEquivalentTo(data);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task PhysicalStorageService_CopyAsync_RenameIncrementalStrategy_ResolvesCollisions()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "StorageTest_" + Guid.NewGuid().ToString("N"));
        var storage = new PhysicalStorageService();

        try
        {
            await storage.CreateDirectoryAsync(tempDir);
            string src = Path.Combine(tempDir, "document.pdf");
            string dst = Path.Combine(tempDir, "out", "document.pdf");

            await storage.WriteAllBytesAsync(src, Encoding.UTF8.GetBytes("Source Original"));
            await storage.CreateDirectoryAsync(Path.GetDirectoryName(dst)!);
            await storage.WriteAllBytesAsync(dst, Encoding.UTF8.GetBytes("Destination Conflict"));

            var result = await storage.CopyAsync(src, dst, StorageCollisionStrategy.RenameIncremental);

            result.IsSuccess.Should().BeTrue();
            result.FinalPath.Should().NotBe(dst);
            result.FinalPath.Should().Contain("document_1.pdf");
            (await storage.FileExistsAsync(dst)).Should().BeTrue();
            (await storage.FileExistsAsync(result.FinalPath)).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task PhysicalStorageService_DryRun_RegistersPlannedAction_WithoutModifyingDisk()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "StorageTest_" + Guid.NewGuid().ToString("N"));
        var plannedActions = new List<PlannedAction>();
        var storage = new PhysicalStorageService(
            platform: null,
            isDryRun: true,
            onRegisterPlannedAction: action => plannedActions.Add(action));

        try
        {
            Directory.CreateDirectory(tempDir);
            string src = Path.Combine(tempDir, "dryrun.txt");
            string dst = Path.Combine(tempDir, "dryrun_moved.txt");
            await File.WriteAllTextAsync(src, "dry run test content");

            var moveResult = await storage.MoveAsync(src, dst);

            moveResult.IsSuccess.Should().BeTrue();
            plannedActions.Should().HaveCount(1);
            plannedActions[0].OperationType.Should().Be(PlannedOperationType.Move);
            plannedActions[0].SourcePath.Should().Be(src);
            plannedActions[0].DestinationPath.Should().Be(dst);
            File.Exists(dst).Should().BeFalse();
            File.Exists(src).Should().BeTrue(); // Original preserved in dry-run
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    /// <summary>
    /// La enumeración del almacén físico: <b>contenido inmediato</b>, sin recursión, en orden determinista y con
    /// una carpeta inexistente que devuelve vacío en vez de lanzar. Es la mitad que el limpiador de carpetas
    /// vacías necesita para saber si una carpeta está vacía sin mirar el disco por su cuenta.
    /// </summary>
    [Fact]
    public async Task PhysicalStorageService_Enumeration_ShouldListImmediateContentOnly()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "StorageEnum_" + Guid.NewGuid().ToString("N"));
        var storage = new PhysicalStorageService();

        try
        {
            await storage.CreateDirectoryAsync(Path.Combine(tempDir, "beta"));
            await storage.CreateDirectoryAsync(Path.Combine(tempDir, "alfa", "anidada"));
            await storage.WriteAllTextAsync(Path.Combine(tempDir, "nota.txt"), "contenido");

            var directories = await storage.EnumerateDirectoriesAsync(tempDir);
            directories.Should().BeEquivalentTo(
                [Path.Combine(tempDir, "alfa"), Path.Combine(tempDir, "beta")],
                "sólo las carpetas inmediatas: la anidada pertenece a otra carpeta");
            directories.Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase,
                "el orden es parte del contrato: dos recorridos del mismo árbol tienen que coincidir");

            var entries = await storage.EnumerateFileSystemEntriesAsync(tempDir);
            entries.Should().HaveCount(3, "dos carpetas y un archivo");
            entries.Should().Contain(Path.Combine(tempDir, "nota.txt"));

            (await storage.EnumerateDirectoriesAsync(Path.Combine(tempDir, "no-existe"))).Should().BeEmpty(
                "una carpeta que no existe no es un error: es una carpeta sin contenido");
            (await storage.EnumerateFileSystemEntriesAsync(Path.Combine(tempDir, "no-existe"))).Should().BeEmpty();

            // El control que separa «listar» de «recorrer»: la carpeta anidada sí aparece al preguntar por su madre.
            (await storage.EnumerateDirectoriesAsync(Path.Combine(tempDir, "alfa")))
                .Should().ContainSingle().Which.Should().Be(Path.Combine(tempDir, "alfa", "anidada"));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    /// <summary>
    /// Y la del almacén virtual, que es la que hace que un recorrido de árbol funcione <b>dentro</b> de una
    /// ejecución virtual: los hijos salen del almacén, un archivo borrado deja de ser contenido —así que su
    /// carpeta queda vacía, que es el estado que el limpiador viene a limpiar— y una carpeta se puede borrar
    /// cuando está vacía y no cuando tiene archivos dentro.
    /// </summary>
    [Fact]
    public async Task VirtualStorageService_EnumerationAndDirectoryDeletion_ShouldOperateInTheStore()
    {
        var vfs = new VirtualFileSystemStore();
        var storage = new FileFlow.Sdk.Storage.VirtualStorageService(vfs);

        await storage.WriteAllBytesAsync(@"C:\out\a\uno.bin", [1, 2, 3]);
        await storage.WriteAllBytesAsync(@"C:\out\ab\dos.bin", [4]);

        (await storage.EnumerateDirectoriesAsync(@"C:\out")).Should().BeEquivalentTo([@"C:\out\a", @"C:\out\ab"],
            "'a' y 'ab' son hermanas: contar prefijos convertiría a 'ab' en hija de 'a'");
        (await storage.EnumerateDirectoriesAsync(@"C:\out\a")).Should().BeEmpty(
            "la hermana con el prefijo común no cuelga de aquí");
        (await storage.EnumerateFileSystemEntriesAsync(@"C:\out\a")).Should().Equal(@"C:\out\a\uno.bin");

        // Una carpeta con archivos activos dentro no es una carpeta vacía, y el contrato lo dice en vez de vaciarla.
        var notEmpty = await storage.DeleteAsync(@"C:\out\a", permanent: true);
        notEmpty.IsSuccess.Should().BeFalse();
        (await storage.DirectoryExistsAsync(@"C:\out\a")).Should().BeTrue();

        // Al borrar el archivo, la carpeta se queda vacía: es la secuencia que ejecuta el limpiador.
        (await storage.DeleteAsync(@"C:\out\a\uno.bin", permanent: true)).IsSuccess.Should().BeTrue();
        (await storage.EnumerateFileSystemEntriesAsync(@"C:\out\a")).Should().BeEmpty();

        var emptied = await storage.DeleteAsync(@"C:\out\a", permanent: true);
        emptied.IsSuccess.Should().BeTrue(
            "borrar una carpeta vacía es una operación del contrato: sin esto, una ejecución virtual no puede limpiar");
        (await storage.DirectoryExistsAsync(@"C:\out\a")).Should().BeFalse();
        vfs.GetAllDirectories().Should().NotContain(@"C:\out\a");
    }

    [Fact]
    public async Task VirtualStorageService_FullLifecycle_OperatesInMemory()
    {
        var vfs = new VirtualFileSystemStore();
        var storage = new FileFlow.Sdk.Storage.VirtualStorageService(vfs);

        string virtualPath = "vfs://documents/report.docx";
        byte[] payload = Encoding.UTF8.GetBytes("Confidential VFS Report");

        // Write
        await storage.WriteAllBytesAsync(virtualPath, payload);
        (await storage.FileExistsAsync(virtualPath)).Should().BeTrue();

        // Read
        var readBytes = await storage.ReadAllBytesAsync(virtualPath);
        readBytes.Should().BeEquivalentTo(payload);

        // Copy with RenameIncremental
        var copyResult = await storage.CopyAsync(virtualPath, virtualPath, StorageCollisionStrategy.RenameIncremental);
        copyResult.IsSuccess.Should().BeTrue();
        copyResult.FinalPath.Should().NotBe(virtualPath);
        copyResult.FinalPath.Should().Contain("report_1.docx");

        // Move
        string movedPath = "vfs://archive/report_final.docx";
        var moveResult = await storage.MoveAsync(virtualPath, movedPath);
        moveResult.IsSuccess.Should().BeTrue();
        (await storage.FileExistsAsync(virtualPath)).Should().BeFalse();
        (await storage.FileExistsAsync(movedPath)).Should().BeTrue();

        // Delete
        var deleteResult = await storage.DeleteAsync(movedPath);
        deleteResult.IsSuccess.Should().BeTrue();
        (await storage.FileExistsAsync(movedPath)).Should().BeFalse();
    }
}
