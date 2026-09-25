using FileFlow.App.ViewModels;

namespace FileFlow.App.Services.UndoRedo;

/// <summary>
/// Acción reversible para el cambio de valor de un parámetro de configuración en un nodo.
/// </summary>
public sealed class ChangeParameterAction : IUndoableAction
{
    private readonly NodeParameterViewModel _parameter;
    private readonly object? _oldValue;
    private readonly object? _newValue;

    public string Description { get; }

    public ChangeParameterAction(NodeParameterViewModel parameter, object? oldValue, object? newValue, string? description = null)
    {
        _parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
        _oldValue = oldValue;
        _newValue = newValue;
        string nodeTitle = _parameter.NodeOwner?.Title ?? "Nodo";
        Description = description ?? $"Cambiar {parameter.DisplayName} en {nodeTitle}";
    }

    public void Undo()
    {
        _parameter.Value = _oldValue;
    }

    public void Redo()
    {
        _parameter.Value = _newValue;
    }
}

