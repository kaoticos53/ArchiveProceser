namespace FileFlow.Sdk;

/// <summary>
/// Descriptor inmutable que define las características, tipo de control UI, orden y restricciones de un parámetro de nodo.
/// </summary>
public sealed record NodeParameterDescriptor(
    string Key,
    ParameterEditorType EditorType = ParameterEditorType.Text,
    object? DefaultValue = null,
    int DisplayOrder = 0,
    IReadOnlyList<string>? Options = null,
    double? Min = null,
    double? Max = null,
    double? Step = null,
    string? HelpText = null
);
