using CommunityToolkit.Mvvm.Messaging.Messages;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Messages;

/// <summary>
/// Mensaje emitido cuando un nodo es seleccionado o inspeccionado en el editor.
/// </summary>
public sealed class NodeSelectedMessage : ValueChangedMessage<NodeViewModel?>
{
    public bool AutoOpenInspector { get; }

    public NodeSelectedMessage(NodeViewModel? node, bool autoOpenInspector = false) : base(node)
    {
        AutoOpenInspector = autoOpenInspector;
    }
}

/// <summary>
/// Mensaje emitido cuando se solicita limpiar los estados visuales de depuración de todos los nodos.
/// </summary>
public sealed class ClearDebugStatesMessage
{
}
