using Avalonia;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Services.UndoRedo;

/// <summary>
/// Representa el desplazamiento individual de un nodo o elemento del lienzo.
/// </summary>
public readonly record struct NodeMoveItem(NodeViewModel Node, Point OldLocation, Point NewLocation);

/// <summary>
/// Acción reversible para el movimiento de uno o varios nodos en el lienzo.
/// </summary>
public sealed class MoveNodesAction : IUndoableAction
{
    private readonly List<NodeMoveItem> _moves;

    public string Description { get; }
    public IReadOnlyList<NodeMoveItem> Moves => _moves;

    public MoveNodesAction(IEnumerable<NodeMoveItem> moves, string? description = null)
    {
        _moves = moves?.Where(m => m.Node != null && m.OldLocation != m.NewLocation).ToList() ?? [];
        Description = description ?? (_moves.Count == 1 ? $"Mover {_moves[0].Node.Title}" : $"Mover {_moves.Count} nodos");
    }

    public void Undo()
    {
        foreach (var move in _moves)
        {
            move.Node.Location = move.OldLocation;
        }
    }

    public void Redo()
    {
        foreach (var move in _moves)
        {
            move.Node.Location = move.NewLocation;
        }
    }
}

