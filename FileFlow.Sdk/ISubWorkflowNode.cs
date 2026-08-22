namespace FileFlow.Sdk;

/// <summary>
/// Contrato para nodos compuestos que albergan y ejecutan un sub-flujo (macro o sub-grafo anidado).
/// </summary>
public interface ISubWorkflowNode : IFlowNode
{
    /// <summary>
    /// Definición serializable o estructurada del sub-grafo interno (JSON o DTO).
    /// </summary>
    string InnerGraphJson { get; set; }

    /// <summary>
    /// Mapeo de puertos de entrada externos a nodos/puertos internos.
    /// </summary>
    IReadOnlyDictionary<string, string> InputMappings { get; }

    /// <summary>
    /// Mapeo de nodos/puertos de salida internos a puertos de salida externos.
    /// </summary>
    IReadOnlyDictionary<string, string> OutputMappings { get; }
}
