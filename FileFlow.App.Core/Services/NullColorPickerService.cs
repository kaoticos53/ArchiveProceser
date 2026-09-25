using FileFlow.App.Services;

namespace FileFlow.App.Core;

/// <summary>
/// Implementación nula de la selección de color: sin host que muestre un selector no hay color,
/// y el llamador ya trata el <c>null</c> como «el usuario canceló».
/// </summary>
public sealed class NullColorPickerService : IColorPickerService
{
    public static NullColorPickerService Instance { get; } = new();
    public string? PickColorHex() => null;
}
