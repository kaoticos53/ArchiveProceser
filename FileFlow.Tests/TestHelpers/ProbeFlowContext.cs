using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Sdk;
using FileFlow.Sdk.Storage;
using FileFlow.Sdk.VirtualFileSystem;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// <b>Contexto de prueba con la costura del almacenamiento</b>: todo lo que el nodo necesita para ejecutarse
/// —puertos, bitácora, diario, acciones planificadas— y una dependencia inyectable, que es lo que este
/// contexto aporta al suite.
///
/// <para><b>Para qué</b>: hay ramas del producto que solo se recorren cuando el entorno falla, y el fallo no se
/// puede provocar desde la configuración: el limpiador de carpetas vacías no tiene ninguna entrada que le haga
/// fallar, porque lo único que puede fallar es el borrado. Con este contexto, la prueba le entrega un
/// <see cref="FailingStorageService"/> y el nodo camina por su rama de error sin depender de permisos, del
/// sistema de archivos ni del sistema operativo.</para>
///
/// <para><b>Qué no es</b>: no sustituye al andamiaje del motor (<see cref="BranchPortHarness"/>), que existe
/// para otra pregunta —si el motor entrega el ítem cuando el nodo emite por esa rama—. Aquí el nodo se ejecuta
/// solo: lo que se afirma es <i>por qué puerto sale cuando su almacenamiento no responde</i>, y el nombre del
/// puerto lo ata al árbol la guardia estática de puertos, que exige que <c>Error</c> esté declarado.</para>
/// </summary>
public sealed class ProbeFlowContext : IFlowExecutionContext
{
    /// <summary>El almacenamiento que verá el nodo. Por omisión, el mismo que en una ejecución sin averías.</summary>
    public IStorageService Storage { get; set; } = NullStorageService.Instance;

    public bool IsDryRun { get; set; }

    public IVirtualFileSystemStore? VirtualFileSystem => null;

    /// <summary>
    /// Puerto cuya emisión se cancela: simula que la ejecución se cancela justo mientras el nodo entrega el
    /// ítem. Es el disparador que distingue «la cancelación se propaga» de «la cancelación se convierte en un
    /// ítem de error», que es lo que esta clase de manejador decide sin querer.
    /// </summary>
    public string? CancelledPort { get; set; }

    public List<string> EmittedPorts { get; } = [];
    public List<FileItemContext> EmittedItems { get; } = [];
    public List<string> Logs { get; } = [];
    public List<PlannedAction> PlannedActions { get; } = [];
    public List<JournalEntry> JournalEntries { get; } = [];

    public Task EmitAsync(string outputPortName, FileItemContext item)
    {
        if (CancelledPort is not null && string.Equals(outputPortName, CancelledPort, StringComparison.Ordinal))
        {
            throw new OperationCanceledException($"Emisión cancelada en el puerto '{outputPortName}' (inyectado).");
        }

        EmittedPorts.Add(outputPortName);
        EmittedItems.Add(item);
        return Task.CompletedTask;
    }

    public void Log(string message, LogLevel level) => Logs.Add($"[{level}] {message}");

    public void ReportProgress(double percentage, string statusMessage) { }

    public void SetTotalExpectedItems(long totalExpectedItems) { }

    public void RegisterPlannedAction(PlannedAction action) => PlannedActions.Add(action);

    public void RecordJournalEntry(JournalEntry entry) => JournalEntries.Add(entry);
}
