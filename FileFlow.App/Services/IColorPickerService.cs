namespace FileFlow.App.Services;

/// <summary>
/// Contrato para el servicio de selección de color nativo/personalizado.
/// </summary>
public interface IColorPickerService
{
    string? PickColorHex();
}
