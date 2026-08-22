using System.Collections.Concurrent;
using FileFlow.Sdk;

namespace FileFlow.Core.Engine;

/// <summary>
/// Servicio de registro transaccional que rastrea todas las operaciones realizadas por los nodos y permite deshacerlas en orden LIFO.
/// </summary>
public sealed class ExecutionJournalService
{
    private readonly List<JournalEntry> _entries = [];
    private readonly Lock _syncLock = new();

    public IReadOnlyList<JournalEntry> Entries
    {
        get
        {
            lock (_syncLock)
            {
                return [.. _entries];
            }
        }
    }

    public void Record(JournalEntry entry)
    {
        lock (_syncLock)
        {
            _entries.Add(entry);
        }
    }

    public void Clear()
    {
        lock (_syncLock)
        {
            _entries.Clear();
        }
    }

    /// <summary>
    /// Ejecuta las acciones inversas (Undo) de todas las operaciones registradas en orden LIFO.
    /// </summary>
    public async Task<int> RollbackAsync(CancellationToken cancellationToken = default)
    {
        List<JournalEntry> toRollback;
        lock (_syncLock)
        {
            toRollback = [.. _entries];
            _entries.Clear();
        }

        toRollback.Reverse(); // Orden LIFO
        int successCount = 0;

        foreach (var entry in toRollback)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry.UndoAction != null)
            {
                try
                {
                    bool ok = await entry.UndoAction(cancellationToken).ConfigureAwait(false);
                    if (ok) successCount++;
                }
                catch
                {
                    // Continuar con el resto de operaciones de rollback
                }
            }
            else
            {
                // Estrategia por defecto según el tipo de operación
                bool ok = PerformDefaultUndo(entry);
                if (ok) successCount++;
            }
        }

        return successCount;
    }

    private static bool PerformDefaultUndo(JournalEntry entry)
    {
        try
        {
            switch (entry.OperationType)
            {
                case JournalOperationType.Moved:
                case JournalOperationType.Renamed:
                    if (!string.IsNullOrEmpty(entry.DestinationPath) && File.Exists(entry.DestinationPath))
                    {
                        string? origDir = Path.GetDirectoryName(entry.SourcePath);
                        if (!string.IsNullOrEmpty(origDir) && !Directory.Exists(origDir))
                        {
                            Directory.CreateDirectory(origDir);
                        }
                        File.Move(entry.DestinationPath, entry.SourcePath, true);
                        return true;
                    }
                    break;

                case JournalOperationType.Copied:
                    if (!string.IsNullOrEmpty(entry.DestinationPath) && File.Exists(entry.DestinationPath))
                    {
                        File.Delete(entry.DestinationPath);
                        return true;
                    }
                    break;

                case JournalOperationType.CreatedDirectory:
                    if (Directory.Exists(entry.SourcePath) && !Directory.EnumerateFileSystemEntries(entry.SourcePath).Any())
                    {
                        Directory.Delete(entry.SourcePath);
                        return true;
                    }
                    break;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }
}
