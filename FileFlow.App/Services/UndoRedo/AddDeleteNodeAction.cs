using FileFlow.App.ViewModels;

namespace FileFlow.App.Services.UndoRedo;

/// <summary>
/// Acción reversible para la inserción de uno o más nodos en el lienzo.
/// </summary>
public sealed class AddNodesAction : IUndoableAction
{
    private readonly EditorViewModel _editor;
    private readonly List<NodeViewModel> _nodes;
    private readonly List<ConnectionViewModel> _connections;

    public string Description { get; }

    public AddNodesAction(EditorViewModel editor, IEnumerable<NodeViewModel> nodes, IEnumerable<ConnectionViewModel>? connections = null, string? description = null)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _nodes = nodes?.Where(n => n != null).ToList() ?? [];
        _connections = connections?.Where(c => c != null).ToList() ?? [];
        Description = description ?? (_nodes.Count == 1 ? $"Añadir {_nodes[0].Title}" : $"Añadir {_nodes.Count} nodos");
    }

    public void Undo()
    {
        foreach (var conn in _connections)
        {
            _editor.Connections.Remove(conn);
        }

        foreach (var node in _nodes)
        {
            var related = _editor.Connections.Where(c => c.Source.NodeOwner == node || c.Target.NodeOwner == node).ToList();
            foreach (var r in related)
            {
                _editor.Connections.Remove(r);
            }
            _editor.Nodes.Remove(node);
        }

        _editor.UpdatePortConnectionStates();
    }

    public void Redo()
    {
        foreach (var node in _nodes)
        {
            if (!_editor.Nodes.Contains(node))
            {
                _editor.Nodes.Add(node);
                _editor.BringToFront(node);
            }
        }

        foreach (var conn in _connections)
        {
            if (!_editor.Connections.Contains(conn))
            {
                _editor.Connections.Add(conn);
            }
        }

        _editor.UpdatePortConnectionStates();
    }
}

/// <summary>
/// Acción reversible para la eliminación de uno o más nodos y sus conexiones incidentes.
/// </summary>
public sealed class DeleteNodesAction : IUndoableAction
{
    private readonly EditorViewModel _editor;
    private readonly List<NodeViewModel> _nodes;
    private readonly List<ConnectionViewModel> _connections;

    public string Description { get; }

    public DeleteNodesAction(EditorViewModel editor, IEnumerable<NodeViewModel> nodes, IEnumerable<ConnectionViewModel>? connections = null, string? description = null)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _nodes = nodes?.Where(n => n != null).ToList() ?? [];
        _connections = connections?.Where(c => c != null).ToList() ?? [];
        Description = description ?? (_nodes.Count == 1 ? $"Eliminar {_nodes[0].Title}" : $"Eliminar {_nodes.Count} nodos");
    }

    public void Undo()
    {
        foreach (var node in _nodes)
        {
            if (!_editor.Nodes.Contains(node))
            {
                _editor.Nodes.Add(node);
                _editor.BringToFront(node);
            }
        }

        foreach (var conn in _connections)
        {
            if (!_editor.Connections.Contains(conn))
            {
                _editor.Connections.Add(conn);
            }
        }

        _editor.UpdatePortConnectionStates();
    }

    public void Redo()
    {
        foreach (var conn in _connections)
        {
            _editor.Connections.Remove(conn);
        }

        foreach (var node in _nodes)
        {
            var related = _editor.Connections.Where(c => c.Source.NodeOwner == node || c.Target.NodeOwner == node).ToList();
            foreach (var r in related)
            {
                _editor.Connections.Remove(r);
            }
            _editor.Nodes.Remove(node);
        }

        _editor.UpdatePortConnectionStates();
    }
}

