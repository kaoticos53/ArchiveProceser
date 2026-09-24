using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Plugin.FileSystem;
using FileFlow.Sdk;
using FileFlow.Sdk.Storage;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

/// <summary>
/// <b>Las dos ramas que solo se recorren cuando el entorno falla</b>: el <c>Error</c> del limpiador de carpetas
/// vacías y el del informe de operaciones.
///
/// <para><b>El problema</b>: ninguna entrada de la configuración las alcanza. El limpiador solo falla si el
/// borrado falla —los permisos que lo hacen fallar en Linux no lo hacen fallar en Windows, así que una prueba
/// por permisos sería una prueba de un sistema operativo— y el informe solo falla si su contenido no se puede
/// volcar. Estaban declaradas «no forzables» en el inventario de ramas: un hueco escrito, que es mejor que un
/// hueco callado, pero hueco al fin y al cabo.</para>
///
/// <para><b>Lo que hacen estas pruebas</b>: inyectar el fallo. Al limpiador se le entrega un
/// <see cref="FailingStorageService"/> —el mismo contrato que el almacenamiento real, averiado— y al informe un
/// ítem cuyo registro no se puede serializar, que es lo que su volcado JSON no admite. La afirmación es siempre
/// la misma: <b>el ítem sale por el puerto declarado</b>, y el nombre de ese puerto lo ata al árbol la guardia
/// estática (<c>NodeEmissionPortGuardTests</c>), que exige que <c>Error</c> esté declarado por el nodo que lo
/// emite.</para>
///
/// <para><b>Lo que cada prueba añade al control negativo</b>: la misma disposición con un almacenamiento sano
/// tiene que salir por el camino feliz. Sin él, una prueba que solo mira el puerto de error pasaría también si
/// el nodo se hubiera averiado por otra razón.</para>
/// </summary>
public class InjectedFailureBranchTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Limpiador de carpetas vacías: la rama que solo se recorre si el borrado falla
    // ─────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(FailingStorageService.Fault.ReportsFailure, "el almacenamiento responde que no pudo")]
    [InlineData(FailingStorageService.Fault.Throws, "el almacenamiento revienta")]
    public async Task AnEmptyDirectoryCleanerWhoseStorageFailsToDelete_ShouldLeaveByTheDeclaredErrorPort(
        FailingStorageService.Fault fault,
        string reason)
    {
        // Arrange
        string root = CreateTemporaryDirectory();
        string emptyChild = Directory.CreateDirectory(Path.Combine(root, "vacia")).FullName;

        try
        {
            var storage = fault == FailingStorageService.Fault.Throws
                ? FailingStorageService.DeleteThrows()
                : FailingStorageService.DeleteReportsFailure();

            var context = new ProbeFlowContext { Storage = storage };
            var node = new EmptyDirectoryCleanerNode();
            node.Parameters["TargetDirectory"] = root;
            node.Parameters["Recursive"] = true;

            var item = new FileItemContext(root, isDirectory: true);

            // Act
            await node.ExecuteAsync("TriggerIn", item, context, CancellationToken.None);

            // Assert
            context.EmittedPorts.Should().ContainSingle(reason)
                .Which.Should().Be("Error", $"un borrado que no se pudo hacer no es un éxito: {reason}");

            // La carpeta sigue ahí: el nodo no puede dar por borrado lo que no borró.
            Directory.Exists(emptyChild).Should().BeTrue("el almacenamiento no borró nada");

            // Y no puede apuntar en el diario un borrado que no ocurrió: el diario deshace operaciones reales.
            context.JournalEntries.Should().NotContain(
                e => e.OperationType == JournalOperationType.DeletedPermanently,
                "no se borró nada, así que no hay nada que registrar para poder deshacerlo");

            // El fallo se pidió al almacenamiento, que es lo que lo hace inyectable y portable.
            storage.DeletedPaths.Should().Contain(emptyChild, "el nodo pidió el borrado al almacenamiento");

            // El registro del ítem marca el fallo (AddLog lo precede de la marca de tiempo, así que se busca dentro).
            item.ExecutionLog.Should().Contain(log => log.Contains("Empty directory cleaner failed:", StringComparison.Ordinal));
            context.Logs.Should().Contain(log => log.StartsWith("[Error]", StringComparison.Ordinal));
        }
        finally
        {
            DeleteTemporaryDirectory(root);
        }
    }

    /// <summary>
    /// El control negativo: la misma disposición con un almacenamiento sano borra lo que tiene que borrar y sale
    /// por <c>Out</c>. Es lo que convierte la prueba anterior en una prueba sobre el fallo y no sobre el nodo.
    /// </summary>
    [Fact]
    public async Task AnEmptyDirectoryCleanerWhoseStorageWorks_ShouldDeleteAndLeaveByItsHappyPort()
    {
        // Arrange
        string root = CreateTemporaryDirectory();
        string emptyChild = Directory.CreateDirectory(Path.Combine(root, "vacia")).FullName;

        try
        {
            // Por omisión, el contexto entrega el mismo almacenamiento que una ejecución sin averías.
            var context = new ProbeFlowContext();
            var node = new EmptyDirectoryCleanerNode();
            node.Parameters["TargetDirectory"] = root;
            node.Parameters["Recursive"] = true;

            var item = new FileItemContext(root, isDirectory: true);

            // Act
            await node.ExecuteAsync("TriggerIn", item, context, CancellationToken.None);

            // Assert
            context.EmittedPorts.Should().ContainSingle().Which.Should().Be("Out");
            Directory.Exists(emptyChild).Should().BeFalse("el almacenamiento sano borra la carpeta vacía");
            context.JournalEntries.Should().Contain(e => e.OperationType == JournalOperationType.DeletedPermanently);
        }
        finally
        {
            DeleteTemporaryDirectory(root);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Informe de operaciones: la rama que solo se recorre si el informe no se puede materializar
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// El informe se genera en memoria y se entrega dentro del ítem, así que lo único que puede fallar mientras
    /// el nodo trabaja es <b>su propio volcado</b>: el registro del ítem viaja al informe, y un registro que no
    /// se puede serializar —una referencia circular— revienta el volcado JSON. El ítem tiene que salir por
    /// <c>Error</c> y no quedarse sin salida.
    /// </summary>
    [Fact]
    public async Task AReportWhoseContentCannotBeMaterialized_ShouldLeaveByTheDeclaredErrorPort()
    {
        // Arrange
        string source = CreateTemporaryFile("informe_origen.txt");

        try
        {
            var context = new ProbeFlowContext();
            var node = new OperationReportNode();
            node.Parameters["ReportFormat"] = "JSON";
            node.Parameters["ReportScope"] = "PerFile";
            node.Parameters["IncludeMetadata"] = true;

            var item = new FileItemContext(source, isDirectory: false);

            // Un registro que se refiere a sí mismo: System.Text.Json no lo admite.
            var circular = new Dictionary<string, object?>();
            circular["self"] = circular;
            item.Metadata["Circular"] = circular;

            // Act
            await node.ExecuteAsync("In", item, context, CancellationToken.None);

            // Assert
            context.EmittedPorts.Should().ContainSingle()
                .Which.Should().Be("Error", "un informe que no se pudo materializar no es un informe generado");

            item.ExecutionLog.Should().Contain(log => log.Contains("OperationReportNode failed:", StringComparison.Ordinal));
            context.Logs.Should().Contain(log => log.StartsWith("[Error]", StringComparison.Ordinal));
        }
        finally
        {
            DeleteTemporaryFile(source);
        }
    }

    /// <summary>
    /// Y la rama es <b>para un informe que falló</b>, no para una ejecución cancelada: si el motor cancela justo
    /// mientras el nodo entrega el informe, la cancelación se propaga —el motor la trata como cancelación— en vez
    /// de convertirse en un ítem de error de una ejecución que el usuario acaba de detener.
    /// </summary>
    [Fact]
    public async Task ACancelledReport_ShouldPropagateTheCancellationInsteadOfLeavingByTheErrorPort()
    {
        // Arrange
        string source = CreateTemporaryFile("informe_cancelado.txt");

        try
        {
            // El informe individual se entrega por 'Report' antes de reenviar el ítem por 'Out'.
            var context = new ProbeFlowContext { CancelledPort = "Report" };
            var node = new OperationReportNode();
            node.Parameters["ReportFormat"] = "HTML";
            node.Parameters["ReportScope"] = "PerFile";

            var item = new FileItemContext(source, isDirectory: false);

            // Act
            Func<Task> act = () => node.ExecuteAsync("In", item, context, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
            context.EmittedPorts.Should().BeEmpty(
                "la cancelación no es un fallo del informe: no puede salir un ítem por 'Error'");
        }
        finally
        {
            DeleteTemporaryFile(source);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "FF_InjectedFault_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateTemporaryFile(string name)
    {
        string path = Path.Combine(Path.GetTempPath(), "FF_InjectedFault_" + Guid.NewGuid().ToString("N"), name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "contenido");
        return path;
    }

    private static void DeleteTemporaryDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
        catch (IOException)
        {
            // Mejor esfuerzo: es una carpeta temporal y el fallo del borrado es justo lo que prueba este fichero.
        }
    }

    private static void DeleteTemporaryFile(string path) => DeleteTemporaryDirectory(Path.GetDirectoryName(path)!);
}
