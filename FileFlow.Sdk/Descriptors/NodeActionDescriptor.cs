namespace FileFlow.Sdk;

/// <summary>
/// Descriptor inmutable para botones y herramientas modales de acción personalizada en la tarjeta del nodo o inspector.
/// </summary>
public sealed record NodeActionDescriptor(
    string ActionId,
    string Title,
    string Icon = "⚙️",
    string? Tooltip = null
);
