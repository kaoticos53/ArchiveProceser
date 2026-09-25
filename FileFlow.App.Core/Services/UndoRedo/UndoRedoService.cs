using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FileFlow.App.Services.UndoRedo;

/// <summary>
/// Implementación estándar de la pila de deshacer y rehacer (Undo/Redo) para el editor DAG.
/// Soporta transacciones atómicas, límite máximo de historial y notificación reactiva.
/// </summary>
public sealed class UndoRedoService : IUndoRedoService
{
    private readonly int _maxCapacity;
    private readonly List<IUndoableAction> _undoStack = [];
    private readonly Stack<IUndoableAction> _redoStack = new();
    private readonly Stack<CompositeAction> _transactionStack = new();
    private bool _isExecuting;

    public event PropertyChangedEventHandler? PropertyChanged;

    public UndoRedoService(int maxCapacity = 100)
    {
        _maxCapacity = maxCapacity > 0 ? maxCapacity : 100;
    }

    public bool CanUndo => _undoStack.Count > 0 && !IsExecuting;
    public bool CanRedo => _redoStack.Count > 0 && !IsExecuting;

    public string? NextUndoDescription => _undoStack.Count > 0 ? _undoStack[^1].Description : null;
    public string? NextRedoDescription => _redoStack.Count > 0 ? _redoStack.Peek().Description : null;

    public bool IsExecuting
    {
        get => _isExecuting;
        private set
        {
            if (_isExecuting != value)
            {
                _isExecuting = value;
                NotifyStateChanged();
            }
        }
    }

    public bool IsInTransaction => _transactionStack.Count > 0;

    public void Record(IUndoableAction action)
    {
        if (action == null || IsExecuting) return;

        if (_transactionStack.Count > 0)
        {
            _transactionStack.Peek().Add(action);
            return;
        }

        _undoStack.Add(action);
        if (_undoStack.Count > _maxCapacity)
        {
            _undoStack.RemoveAt(0);
        }

        _redoStack.Clear();
        NotifyStateChanged();
    }

    public void Undo()
    {
        if (!CanUndo) return;

        IsExecuting = true;
        try
        {
            var action = _undoStack[^1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            action.Undo();
            _redoStack.Push(action);
        }
        finally
        {
            IsExecuting = false;
        }
    }

    public void Redo()
    {
        if (!CanRedo) return;

        IsExecuting = true;
        try
        {
            var action = _redoStack.Pop();
            action.Redo();
            _undoStack.Add(action);
        }
        finally
        {
            IsExecuting = false;
        }
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        _transactionStack.Clear();
        NotifyStateChanged();
    }

    public IDisposable BeginTransaction(string description)
    {
        var composite = new CompositeAction(description);
        _transactionStack.Push(composite);
        return new TransactionScope(this, composite);
    }

    private void EndTransaction(CompositeAction composite)
    {
        if (_transactionStack.Count == 0) return;

        var current = _transactionStack.Pop();
        if (!ReferenceEquals(current, composite))
        {
            return;
        }

        if (composite.Actions.Count == 0)
        {
            return;
        }

        if (_transactionStack.Count > 0)
        {
            _transactionStack.Peek().Add(composite);
        }
        else
        {
            _undoStack.Add(composite);
            if (_undoStack.Count > _maxCapacity)
            {
                _undoStack.RemoveAt(0);
            }
            _redoStack.Clear();
            NotifyStateChanged();
        }
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(NextUndoDescription));
        OnPropertyChanged(nameof(NextRedoDescription));
        OnPropertyChanged(nameof(IsExecuting));
        OnPropertyChanged(nameof(IsInTransaction));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed class TransactionScope : IDisposable
    {
        private readonly UndoRedoService _service;
        private readonly CompositeAction _composite;
        private bool _disposed;

        public TransactionScope(UndoRedoService service, CompositeAction composite)
        {
            _service = service;
            _composite = composite;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _service.EndTransaction(_composite);
        }
    }
}

