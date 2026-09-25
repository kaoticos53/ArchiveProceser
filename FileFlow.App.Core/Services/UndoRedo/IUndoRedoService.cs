using System.ComponentModel;

namespace FileFlow.App.Services.UndoRedo;

/// <summary>
/// Contrato de servicio para la gestión de la pila de deshacer/rehacer (Undo/Redo) en el editor de grafos.
/// </summary>
public interface IUndoRedoService : INotifyPropertyChanged
{
    /// <summary>Indica si hay acciones disponibles para deshacer.</summary>
    bool CanUndo { get; }

    /// <summary>Indica si hay acciones disponibles para rehacer.</summary>
    bool CanRedo { get; }

    /// <summary>Descripción de la próxima acción a deshacer.</summary>
    string? NextUndoDescription { get; }

    /// <summary>Descripción de la próxima acción a rehacer.</summary>
    string? NextRedoDescription { get; }

    /// <summary>Indica si el servicio está ejecutando actualmente un Undo o Redo.</summary>
    bool IsExecuting { get; }

    /// <summary>Indica si hay una transacción abierta agrupando varias acciones atómicas.</summary>
    bool IsInTransaction { get; }

    /// <summary>
    /// Registra y almacena una acción realizada en la pila de Undo, vaciando la pila de Redo.
    /// </summary>
    void Record(IUndoableAction action);

    /// <summary>
    /// Deshace la última acción o lote de acciones.
    /// </summary>
    void Undo();

    /// <summary>
    /// Rehace la última acción o lote de acciones deshechas.
    /// </summary>
    void Redo();

    /// <summary>
    /// Limpia todas las pilas de deshacer y rehacer.
    /// </summary>
    void Clear();

    /// <summary>
    /// Inicia una transacción compuesta que agrupa múltiples acciones bajo una sola operación de deshacer/rehacer.
    /// </summary>
    IDisposable BeginTransaction(string description);
}

