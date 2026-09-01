namespace FileFlow.Sdk.Renaming.Handlers;

/// <summary>
/// Utilidades puras para el cálculo de índices y posiciones en operaciones de inserción y eliminación.
/// </summary>
public static class RenameIndexCalculator
{
    public static int CalculateInsertIndex(CharacterPosition pos, int index, int length)
    {
        return pos switch
        {
            CharacterPosition.FromStart => Math.Clamp(index, 0, length),
            CharacterPosition.FromEnd => Math.Clamp(length - index, 0, length),
            CharacterPosition.AbsoluteIndex => Math.Clamp(index, 0, length),
            _ => length
        };
    }

    public static int CalculateRemoveStartIndex(CharacterPosition pos, int index, int length, int count)
    {
        return pos switch
        {
            CharacterPosition.FromStart => Math.Clamp(index, 0, length),
            CharacterPosition.FromEnd => Math.Clamp(length - index - count, 0, length),
            CharacterPosition.AbsoluteIndex => Math.Clamp(index, 0, length),
            _ => 0
        };
    }
}
