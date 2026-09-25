namespace FileFlow.App.Services.UndoRedo;

/// <summary>
/// Representa una acción reversible ejecutada sobre el grafo o lienzo de diseño.
/// </summary>
public interface IUndoableAction
{
    /// <summary>
    /// Descripción legible de la acción para tooltips o historial.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Revierte la acción efectuada.
    /// </summary>
    void Undo();

    /// <summary>
    /// Vuelve a aplicar la acción previamente revertida.
    /// </summary>
    void Redo();
}

