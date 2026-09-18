using FileFlow.App.ViewModels;

namespace FileFlow.App.Services.UndoRedo;

/// <summary>
/// Acción reversible para la creación de un cable / conexión entre dos puertos.
/// </summary>
public sealed class AddConnectionAction : IUndoableAction
{
    private readonly EditorViewModel _editor;
    private readonly ConnectionViewModel _connection;

    public string Description { get; }

    public AddConnectionAction(EditorViewModel editor, ConnectionViewModel connection, string? description = null)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        Description = description ?? $"Conectar {_connection.Source.NodeOwner.Title} -> {_connection.Target.NodeOwner.Title}";
    }

    public void Undo()
    {
        _editor.Connections.Remove(_connection);
        _editor.UpdatePortConnectionStates();
    }

    public void Redo()
    {
        if (!_editor.Connections.Contains(_connection))
        {
            _editor.Connections.Add(_connection);
        }
        _editor.UpdatePortConnectionStates();
    }
}

/// <summary>
/// Acción reversible para la eliminación de un cable / conexión.
/// </summary>
public sealed class DeleteConnectionAction : IUndoableAction
{
    private readonly EditorViewModel _editor;
    private readonly ConnectionViewModel _connection;

    public string Description { get; }

    public DeleteConnectionAction(EditorViewModel editor, ConnectionViewModel connection, string? description = null)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        Description = description ?? $"Eliminar conexión {_connection.Source.NodeOwner.Title} -> {_connection.Target.NodeOwner.Title}";
    }

    public void Undo()
    {
        if (!_editor.Connections.Contains(_connection))
        {
            _editor.Connections.Add(_connection);
        }
        _editor.UpdatePortConnectionStates();
    }

    public void Redo()
    {
        _editor.Connections.Remove(_connection);
        _editor.UpdatePortConnectionStates();
    }
}

