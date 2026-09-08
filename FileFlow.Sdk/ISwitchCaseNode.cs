namespace FileFlow.Sdk;

/// <summary>
/// Definición de una regla o caso de bifurcación condicional.
/// </summary>
public record SwitchCaseRuleDefinition(string Name, string Pattern);

/// <summary>
/// Contrato para nodos de bifurcación condicional que exponen casos y puertos dinámicos configurables.
/// Permite desacoplar la UI de la implementación concreta del nodo de lógica.
/// </summary>
public interface ISwitchCaseNode : IFlowNode
{
    IReadOnlyList<SwitchCaseRuleDefinition> GetCases();
    void SetCases(IEnumerable<SwitchCaseRuleDefinition> cases);
}
