namespace FileFlow.Sdk;

/// <summary>
/// Contrato para nodos cuyos puertos no son fijos: dependen de sus parámetros (los casos de un switch,
/// los nombres configurados de un subflujo, los puertos declarados de un script) y cambian en tiempo de
/// diseño desde el editor.
///
/// <para>
/// El editor no puede adivinar cuándo cambian —los puertos se calculan al leerlos y no hay ninguna
/// invalidación observable—, así que el nodo lo anuncia: <see cref="PortsChanged"/> se dispara cuando la
/// topología <b>ya</b> cambió, y <see cref="RefreshPortTopology"/> es la vía por la que el entorno pide al
/// nodo que la reevalúe (por ejemplo, tras escribir un parámetro desde el inspector).
/// </para>
///
/// <para>
/// Contrato de hilo: el anuncio se emite en el hilo que muta la topología (el de la interfaz en la
/// aplicación). El consumidor reconstruye puertos y conexiones de forma síncrona, así que un nodo que
/// cambiara sus puertos desde un hilo de ejecución rompería esa afinidad; hoy ningún camino lo hace.
/// </para>
/// </summary>
public interface IPortTopologyNode : IFlowNode
{
    /// <summary>
    /// Notificado cuando el conjunto de puertos del nodo ha cambiado de verdad (no cuando se ha pedido una
    /// reevaluación que no cambió nada). El argumento lleva los nombres vigentes en ese momento.
    /// </summary>
    event EventHandler<PortTopologyChangedEventArgs>? PortsChanged;

    /// <summary>
    /// Reevalúa la topología desde el estado actual (normalmente los parámetros) y la reanuncia si cambió.
    /// Es idempotente: un nodo cuyos puertos no dependan de lo que acaba de cambiar no emite nada.
    /// </summary>
    void RefreshPortTopology();
}

/// <summary>
/// Estado de la topología de puertos en el momento del anuncio: los nombres de los puertos de entrada y de
/// salida, en el orden en que el nodo los expone.
/// </summary>
public sealed class PortTopologyChangedEventArgs : EventArgs
{
    public PortTopologyChangedEventArgs(
        IReadOnlyList<string> inputPortNames,
        IReadOnlyList<string> outputPortNames)
    {
        ArgumentNullException.ThrowIfNull(inputPortNames);
        ArgumentNullException.ThrowIfNull(outputPortNames);

        InputPortNames = inputPortNames;
        OutputPortNames = outputPortNames;
    }

    /// <summary>Nombres de los puertos de entrada vigentes tras el cambio.</summary>
    public IReadOnlyList<string> InputPortNames { get; }

    /// <summary>Nombres de los puertos de salida vigentes tras el cambio.</summary>
    public IReadOnlyList<string> OutputPortNames { get; }

    public override string ToString() =>
        $"Entradas: [{string.Join(", ", InputPortNames)}] · Salidas: [{string.Join(", ", OutputPortNames)}]";
}
