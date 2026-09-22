using FileFlow.App.ViewModels;

namespace FileFlow.App.Services;

/// <summary>
/// La <b>única</b> regla con la que se reconstruyen conexiones: emparejar por nombre el puerto de salida de
/// un nodo con el puerto de entrada de otro.
///
/// Abrir un flujo guardado y pegar o duplicar nodos la comparten a propósito. Es la lección de 2E-P7, donde
/// el guardado y el portapapeles divergieron por no compartir una regla y el mismo cable se comportaba
/// distinto según por dónde entrara; aquí los dos caminos entran por el mismo sitio, así que no pueden
/// divergir ni en qué se reconstruye ni en qué se cuenta cuando no se puede.
///
/// Lo que devuelve cuando falla no es un booleano: es el diagnóstico de una conexión perdida
/// (<see cref="DroppedConnection"/>), con sus dos extremos y el motivo por extremo, que es lo que hace
/// falta para poder volver a conectarla.
/// </summary>
public static class ConnectionReconstructor
{
    /// <summary>
    /// Reconstruye una conexión y la entrega a <paramref name="register"/>, o devuelve por qué no pudo.
    /// </summary>
    /// <param name="nodeLookup">
    /// Nodos que sí llegaron a crearse, por el identificador con el que el origen los nombra. El informe
    /// devuelve el identificador <b>con el que el nodo existe en el grafo reconstruido</b> —que no es el del
    /// origen cuando lo que se reconstruye es un pegado, porque los nodos pegados son otros—, que es lo que
    /// permite señalar el nodo del informe sin riesgo de señalar a su original.
    /// </param>
    /// <param name="uncreatedNodeNames">
    /// Nombre con el que reconocer un nodo que <b>no</b> llegó a crearse —el título que el usuario le puso, o
    /// su tipo, que es lo que se busca para saber qué plugin falta—, por el mismo identificador. Un nodo que
    /// no esté aquí se describe por su identificador, que es lo último que queda.
    /// </param>
    /// <param name="register">Dónde va la conexión reconstruida; no se llama si no se pudo reconstruir.</param>
    public static DroppedConnection? TryRebuild(
        string sourceNodeId,
        string sourcePortName,
        string targetNodeId,
        string targetPortName,
        IReadOnlyDictionary<string, NodeViewModel> nodeLookup,
        IReadOnlyDictionary<string, string> uncreatedNodeNames,
        Action<ConnectionViewModel> register)
    {
        ArgumentNullException.ThrowIfNull(nodeLookup);
        ArgumentNullException.ThrowIfNull(uncreatedNodeNames);
        ArgumentNullException.ThrowIfNull(register);

        if (nodeLookup.TryGetValue(sourceNodeId, out var sourceNode)
            && nodeLookup.TryGetValue(targetNodeId, out var targetNode))
        {
            var sourcePort = sourceNode.OutputPorts.FirstOrDefault(port => port.Name.Equals(sourcePortName, StringComparison.OrdinalIgnoreCase));
            var targetPort = targetNode.InputPorts.FirstOrDefault(port => port.Name.Equals(targetPortName, StringComparison.OrdinalIgnoreCase));

            if (sourcePort != null && targetPort != null)
            {
                register(new ConnectionViewModel(sourcePort, targetPort));
                return null;
            }
        }

        return new DroppedConnection(
            DescribeEnd(sourceNodeId, sourcePortName, input: false, nodeLookup, uncreatedNodeNames),
            DescribeEnd(targetNodeId, targetPortName, input: true, nodeLookup, uncreatedNodeNames));
    }

    /// <summary>
    /// Diagnóstico de un extremo: si su nodo no llegó a crearse, si el nodo está pero no expone ese puerto,
    /// o nada —porque el problema estaba en el otro extremo—.
    /// </summary>
    private static DroppedConnectionEnd DescribeEnd(
        string nodeId,
        string portName,
        bool input,
        IReadOnlyDictionary<string, NodeViewModel> nodeLookup,
        IReadOnlyDictionary<string, string> uncreatedNodeNames)
    {
        if (!nodeLookup.TryGetValue(nodeId, out var nodeVm))
        {
            return new DroppedConnectionEnd(
                nodeId, DescribeUncreatedNode(nodeId, uncreatedNodeNames), portName, DroppedConnectionEndProblem.MissingNode);
        }

        var ports = input ? nodeVm.InputPorts : nodeVm.OutputPorts;
        bool exposed = ports.Any(port => port.Name.Equals(portName, StringComparison.OrdinalIgnoreCase));

        return new DroppedConnectionEnd(
            nodeVm.Id,
            nodeVm.Title,
            portName,
            exposed ? DroppedConnectionEndProblem.None : DroppedConnectionEndProblem.MissingPort);
    }

    /// <summary>
    /// Nombre con el que reconocer un nodo que no se pudo crear: el título que el usuario le puso, si lo
    /// tenía, y si no su tipo —que es lo que se busca para saber qué plugin falta—. Sin nada de eso queda el
    /// identificador, que al menos permite encontrarlo en el origen.
    /// </summary>
    private static string DescribeUncreatedNode(string nodeId, IReadOnlyDictionary<string, string> uncreatedNodeNames)
    {
        if (!uncreatedNodeNames.TryGetValue(nodeId, out var name) || string.IsNullOrWhiteSpace(name))
        {
            return nodeId;
        }

        return name;
    }
}
