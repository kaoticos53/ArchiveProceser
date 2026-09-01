using System.Collections.ObjectModel;
using FileFlow.App.ViewModels;
using FileFlow.Sdk;

namespace FileFlow.App.Services;

/// <summary>
/// Coordinador de inicialización, sincronización y puertos dinámicos para nodos de bifurcación condicional (SwitchCaseNode).
/// </summary>
public static class NodeSwitchCaseCoordinator
{
    public static void InitializeSwitchCases(
        IFlowNode node,
        NodeViewModel nodeVm,
        ObservableCollection<PortViewModel> outputPorts,
        ObservableCollection<SwitchCaseItemViewModel> switchCases)
    {
        List<(string Name, string Pattern)> initialCases = [];
        if (node is FileFlow.Plugin.Logic.SwitchCaseNode switchNode)
        {
            initialCases = switchNode.GetCases().Select(c => (c.Name, c.Pattern)).ToList();
        }
        else if (node.Parameters.TryGetValue("CasesJson", out var jsonVal) && jsonVal != null)
        {
            string jsonStr = jsonVal.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(jsonStr))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(jsonStr);
                    foreach (var elem in doc.RootElement.EnumerateArray())
                    {
                        string cName = elem.TryGetProperty("Name", out var np) ? np.GetString() ?? "" : "";
                        string cPattern = elem.TryGetProperty("Pattern", out var pp) ? pp.GetString() ?? "" : "";
                        if (!string.IsNullOrWhiteSpace(cName))
                        {
                            initialCases.Add((cName, cPattern));
                        }
                    }
                }
                catch { }
            }
        }

        if (initialCases.Count == 0)
        {
            initialCases.Add(("Case 1", "jpg;jpeg;png;webp;gif"));
        }

        foreach (var c in initialCases)
        {
            var port = new PortViewModel(nodeVm, c.Name, c.Name, PortDirection.Output, typeof(FileItemContext));
            outputPorts.Add(port);
            switchCases.Add(new SwitchCaseItemViewModel(nodeVm, c.Name, c.Pattern) { Port = port });
        }
        outputPorts.Add(new PortViewModel(nodeVm, "Default", "Default", PortDirection.Output, typeof(FileItemContext)));
    }

    public static void AddCase(
        NodeViewModel nodeVm,
        ObservableCollection<PortViewModel> outputPorts,
        ObservableCollection<SwitchCaseItemViewModel> switchCases,
        Action syncCallback)
    {
        int count = switchCases.Count + 1;
        string caseName = $"Case {count}";
        while (switchCases.Any(c => c.Name.Equals(caseName, StringComparison.OrdinalIgnoreCase)) ||
               outputPorts.Any(p => p.Name.Equals(caseName, StringComparison.OrdinalIgnoreCase)))
        {
            count++;
            caseName = $"Case {count}";
        }

        var newPort = new PortViewModel(nodeVm, caseName, caseName, PortDirection.Output, typeof(FileItemContext));
        var caseItem = new SwitchCaseItemViewModel(nodeVm, caseName, "") { Port = newPort };
        switchCases.Add(caseItem);

        var defaultPort = outputPorts.FirstOrDefault(p => p.Name.Equals("Default", StringComparison.OrdinalIgnoreCase));
        int insertIndex = defaultPort != null ? outputPorts.IndexOf(defaultPort) : outputPorts.Count;
        outputPorts.Insert(insertIndex, newPort);

        syncCallback();
    }

    public static void RemoveCase(
        SwitchCaseItemViewModel caseItem,
        ObservableCollection<PortViewModel> outputPorts,
        ObservableCollection<SwitchCaseItemViewModel> switchCases,
        Action syncCallback)
    {
        if (caseItem == null) return;
        switchCases.Remove(caseItem);

        if (caseItem.Port != null)
        {
            outputPorts.Remove(caseItem.Port);
        }
        else
        {
            var port = outputPorts.FirstOrDefault(p => p.Name.Equals(caseItem.Name, StringComparison.OrdinalIgnoreCase));
            if (port != null)
            {
                outputPorts.Remove(port);
            }
        }

        syncCallback();
    }

    public static void RenameCase(
        string oldName,
        string newName,
        SwitchCaseItemViewModel item,
        ObservableCollection<PortViewModel> outputPorts,
        Action syncCallback)
    {
        if (item.Port != null)
        {
            item.Port.Name = newName;
            item.Port.DisplayName = newName;
        }
        else
        {
            var port = outputPorts.FirstOrDefault(p => p.Name.Equals(oldName, StringComparison.OrdinalIgnoreCase));
            if (port != null)
            {
                port.Name = newName;
                port.DisplayName = newName;
                item.Port = port;
            }
        }
        syncCallback();
    }

    public static void SyncCasesToNode(IFlowNode nodeInstance, IEnumerable<SwitchCaseItemViewModel> switchCases)
    {
        if (nodeInstance is FileFlow.Plugin.Logic.SwitchCaseNode switchNode)
        {
            var rules = switchCases.Select(c => new FileFlow.Plugin.Logic.SwitchCaseRule(c.Name, c.Pattern)).ToList();
            switchNode.SetCases(rules);
        }
        else
        {
            var rules = switchCases.Select(c => new { Name = c.Name, Pattern = c.Pattern }).ToList();
            lock (nodeInstance.Parameters)
            {
                nodeInstance.Parameters["CasesJson"] = System.Text.Json.JsonSerializer.Serialize(rules);
            }
        }
    }
}
