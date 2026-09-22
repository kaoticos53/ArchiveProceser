namespace FileFlow.Sdk;

/// <summary>
/// Contrato para nodos de subflujo compuesto que encapsulan un subgrafo DAG reutilizable.
/// </summary>
public interface ISubflowNode : IFlowNode
{
    public const string SubflowSinkKey = "__SubflowOutputSink__";

    /// <summary>
    /// Ruta al archivo de definición de subflujo (.flow o .subflow).
    /// </summary>
    string SubflowPath { get; set; }

    /// <summary>
    /// Definición serializada en JSON del subgrafo cuando está incrustado directamente.
    /// </summary>
    string SubflowDefinitionJson { get; set; }

    /// <summary>
    /// Indica si la definición del subflujo está incrustada en lugar de referenciar un archivo externo.
    /// </summary>
    bool EmbedDefinition { get; set; }

    /// <summary>
    /// Sincroniza y actualiza la lista dinámica de puertos de entrada y salida expuestos
    /// a partir de los nodos frontera (SubflowInputNode / SubflowOutputNode) del subgrafo.
    /// </summary>
    void RefreshDynamicPorts(IEnumerable<string> inputPortNames, IEnumerable<string> outputPortNames);

    /// <summary>
    /// Puertos que el contenedor expone hacia el exterior: lo último que se pudo leer de su definición.
    ///
    /// <para>
    /// El nodo lo guarda con él, así que viaja en el archivo del flujo y sobrevive a la sesión. Es lo que
    /// permite reconstruir las conexiones de un flujo guardado cuando la definición ya no está al alcance
    /// —el subflujo se movió de sitio, o el flujo se compartió sin él—: sin memoria, el contenedor
    /// volvería a sus puertos genéricos, y el editor revalida los cables contra los puertos vigentes, así
    /// que ese respaldo no degrada nada, los borra.
    /// </para>
    ///
    /// <para>
    /// Devuelve dos listas vacías mientras el contenedor no haya expuesto puertos propios; el respaldo de
    /// los genéricos es decisión de quien pregunta, no de este contrato.
    /// </para>
    ///
    /// <para>
    /// Es de lectura y escritura porque tiene dos escritores con motivos distintos: el nodo la actualiza al
    /// materializar sus puertos —el único momento en que cambian—, y quien reconstruye un archivo guardado
    /// por una versión del formato que no los guardaba la <b>siembra</b> con lo que ese archivo todavía dice
    /// antes de materializar, de modo que un contenedor cuya definición ya no está conserve sus puertos y
    /// sus cables. Una definición que resuelva manda sobre ella: la siembra sólo decide cuando no hay nada
    /// mejor que leer.
    /// </para>
    /// </summary>
    (IReadOnlyList<string> Inputs, IReadOnlyList<string> Outputs) RememberedPorts { get; set; }
}

/// <summary>
/// Contrato para nodos frontera de entrada o salida dentro de un subflujo.
/// </summary>
public interface ISubflowBoundaryNode : IFlowNode
{
    /// <summary>
    /// Lista delimitada por punto y coma de los nombres de puerto expuestos hacia el exterior.
    /// </summary>
    string PortNames { get; set; }
}
