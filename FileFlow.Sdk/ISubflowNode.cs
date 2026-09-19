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
