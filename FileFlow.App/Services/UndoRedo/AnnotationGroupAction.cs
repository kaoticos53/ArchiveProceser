using FileFlow.App.ViewModels;

namespace FileFlow.App.Services.UndoRedo;

/// <summary>
/// Acción reversible para la inserción de una nota adhesiva / anotación.
/// </summary>
public sealed class AddAnnotationAction : IUndoableAction
{
    private readonly EditorViewModel _editor;
    private readonly AnnotationViewModel _annotation;

    public string Description => $"Añadir Nota: {_annotation.Title}";

    public AddAnnotationAction(EditorViewModel editor, AnnotationViewModel annotation)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _annotation = annotation ?? throw new ArgumentNullException(nameof(annotation));
    }

    public void Undo()
    {
        _editor.Annotations.Remove(_annotation);
        _editor.CanvasDecorators.Remove(_annotation);
    }

    public void Redo()
    {
        if (!_editor.Annotations.Contains(_annotation))
        {
            _editor.Annotations.Add(_annotation);
        }
        if (!_editor.CanvasDecorators.Contains(_annotation))
        {
            _editor.CanvasDecorators.Add(_annotation);
        }
    }
}

/// <summary>
/// Acción reversible para la eliminación de una nota adhesiva / anotación.
/// </summary>
public sealed class DeleteAnnotationAction : IUndoableAction
{
    private readonly EditorViewModel _editor;
    private readonly AnnotationViewModel _annotation;

    public string Description => $"Eliminar Nota: {_annotation.Title}";

    public DeleteAnnotationAction(EditorViewModel editor, AnnotationViewModel annotation)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _annotation = annotation ?? throw new ArgumentNullException(nameof(annotation));
    }

    public void Undo()
    {
        if (!_editor.Annotations.Contains(_annotation))
        {
            _editor.Annotations.Add(_annotation);
        }
        if (!_editor.CanvasDecorators.Contains(_annotation))
        {
            _editor.CanvasDecorators.Add(_annotation);
        }
    }

    public void Redo()
    {
        _editor.Annotations.Remove(_annotation);
        _editor.CanvasDecorators.Remove(_annotation);
    }
}

/// <summary>
/// Acción reversible para la inserción de un grupo visual de nodos.
/// </summary>
public sealed class AddGroupAction : IUndoableAction
{
    private readonly EditorViewModel _editor;
    private readonly GroupViewModel _group;

    public string Description => $"Añadir Grupo: {_group.Title}";

    public AddGroupAction(EditorViewModel editor, GroupViewModel group)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _group = group ?? throw new ArgumentNullException(nameof(group));
    }

    public void Undo()
    {
        _editor.Groups.Remove(_group);
        _editor.CanvasDecorators.Remove(_group);
    }

    public void Redo()
    {
        if (!_editor.Groups.Contains(_group))
        {
            _editor.Groups.Add(_group);
        }
        if (!_editor.CanvasDecorators.Contains(_group))
        {
            _editor.CanvasDecorators.Insert(0, _group);
        }
    }
}

/// <summary>
/// Acción reversible para la eliminación de un grupo visual de nodos.
/// </summary>
public sealed class DeleteGroupAction : IUndoableAction
{
    private readonly EditorViewModel _editor;
    private readonly GroupViewModel _group;

    public string Description => $"Eliminar Grupo: {_group.Title}";

    public DeleteGroupAction(EditorViewModel editor, GroupViewModel group)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _group = group ?? throw new ArgumentNullException(nameof(group));
    }

    public void Undo()
    {
        if (!_editor.Groups.Contains(_group))
        {
            _editor.Groups.Add(_group);
        }
        if (!_editor.CanvasDecorators.Contains(_group))
        {
            _editor.CanvasDecorators.Insert(0, _group);
        }
    }

    public void Redo()
    {
        _editor.Groups.Remove(_group);
        _editor.CanvasDecorators.Remove(_group);
    }
}

