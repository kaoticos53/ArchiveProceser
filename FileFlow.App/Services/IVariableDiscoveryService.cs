using FileFlow.App.Models;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Services;

/// <summary>
/// Contrato para el servicio de descubrimiento de variables de sistema, funciones y variables upstream de nodos.
/// </summary>
public interface IVariableDiscoveryService
{
    List<VariableGroupItem> GetAvailableVariables(NodeViewModel targetNode, IEnumerable<ConnectionViewModel> connections);
}
