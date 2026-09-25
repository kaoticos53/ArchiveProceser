namespace FileFlow.App.Services.UndoRedo;

/// <summary>
/// Acción compuesta que agrupa una secuencia de acciones atómicas bajo una única unidad transaccional.
/// </summary>
public sealed class CompositeAction : IUndoableAction
{
    private readonly List<IUndoableAction> _actions = [];

    public string Description { get; }
    public IReadOnlyList<IUndoableAction> Actions => _actions;

    public CompositeAction(string description)
    {
        Description = description;
    }

    public CompositeAction(string description, IEnumerable<IUndoableAction> actions)
    {
        Description = description;
        _actions.AddRange(actions);
    }

    public void Add(IUndoableAction action)
    {
        if (action != null)
        {
            _actions.Add(action);
        }
    }

    public void Undo()
    {
        for (int i = _actions.Count - 1; i >= 0; i--)
        {
            _actions[i].Undo();
        }
    }

    public void Redo()
    {
        for (int i = 0; i < _actions.Count; i++)
        {
            _actions[i].Redo();
        }
    }
}

