using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FileFlow.Core.Utils;

/// <summary>
/// Asistente centralizado para la recolección determinista de memoria,
/// compactación de montículos de objetos grandes (LOH) y recorte de Working Set del sistema operativo.
/// </summary>
public static class MemoryReclamationHelper
{
    private static readonly Lock _lock = new();
    private static readonly List<Action> _cleanupCallbacks = [];

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessWorkingSetSize(nint hProcess, nint dwMinimumWorkingSetSize, nint dwMaximumWorkingSetSize);

    /// <summary>
    /// Registra una acción de limpieza personalizada (por ejemplo, purga de pools de buffers o motores de inferencia).
    /// </summary>
    public static void RegisterCleanupCallback(Action callback)
    {
        if (callback == null) return;
        lock (_lock)
        {
            if (!_cleanupCallbacks.Contains(callback))
            {
                _cleanupCallbacks.Add(callback);
            }
        }
    }

    /// <summary>
    /// Ejecuta una liberación profunda de memoria en 3 fases:
    /// 1. Invocación de callbacks de limpieza registrados (pools de memoria, librerías gráficas, etc.).
    /// 2. Recolección de basura de Generación 2 con compactación forzada de montículo LOH.
    /// 3. Recorte de páginas de memoria no utilizadas devolviendo el Working Set a Windows.
    /// </summary>
    public static void ReclaimMemory(bool trimWorkingSet = true)
    {
        // Fase 1: Callbacks registrados
        Action[] callbacks;
        lock (_lock)
        {
            callbacks = [.. _cleanupCallbacks];
        }

        foreach (var action in callbacks)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MemoryReclamationHelper] Error en callback de limpieza: {ex.Message}");
            }
        }

        // Fase 2: Recolección y compactación de GC
        try
        {
            GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MemoryReclamationHelper] Error en recolección de GC: {ex.Message}");
        }

        // Fase 3: Recorte de Working Set en Windows
        if (trimWorkingSet && OperatingSystem.IsWindows())
        {
            try
            {
                using var currentProcess = Process.GetCurrentProcess();
                SetProcessWorkingSetSize(currentProcess.Handle, -1, -1);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MemoryReclamationHelper] Error al recortar Working Set en Windows: {ex.Message}");
            }
        }
    }
}
