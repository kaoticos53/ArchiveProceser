using System.IO;
using System.Text.Json;
using FileFlow.Core.Plugins;
using FileFlow.Sdk;
using Xunit;

namespace FileFlow.Tests.Unit.Core;

public class WorkflowExamplesValidationTests
{
    [Fact]
    public void AllExampleFlows_ShouldLoadAndHaveValidNodesAndPorts()
    {
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.FileSystem.FolderSourceNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Archives.SmartUnpackNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Images.ImageOptimizerNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Logic.SwitchCaseNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Hashing.HashCalculatorNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Integrations.MediaTranscoderNode).Assembly);

        string examplesDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../..", "docs", "examples"));
        if (!Directory.Exists(examplesDir))
        {
            examplesDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "docs", "examples"));
        }

        Assert.True(Directory.Exists(examplesDir), $"Examples dir not found: {examplesDir}");

        var jsonFiles = Directory.GetFiles(examplesDir, "*.json", SearchOption.AllDirectories);
        Assert.NotEmpty(jsonFiles);

        var errors = new List<string>();

        foreach (var file in jsonFiles)
        {
            string fileName = Path.GetFileName(file);
            string json = File.ReadAllText(file);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("nodes", out var nodesElement))
            {
                errors.Add($"[{fileName}] Missing 'nodes' property.");
                continue;
            }

            var nodeMap = new Dictionary<string, (IFlowNode Node, List<string> InPorts, List<string> OutPorts)>();

            foreach (var nodeElem in nodesElement.EnumerateArray())
            {
                string id = nodeElem.GetProperty("id").GetString()!;
                string nodeTypeName = nodeElem.GetProperty("nodeTypeName").GetString()!;

                // Lookup node type in loader
                if (!loader.DiscoveredNodeTypes.TryGetValue(nodeTypeName, out var nodeType))
                {
                    // Try without namespace
                    string simpleName = nodeTypeName.Contains('.') ? nodeTypeName.Split('.').Last() : nodeTypeName;
                    if (!loader.DiscoveredNodeTypes.TryGetValue(simpleName, out nodeType))
                    {
                        errors.Add($"[{fileName}] Node type '{nodeTypeName}' (id: {id}) not found in discovered plugins.");
                        continue;
                    }
                }

                var instance = (IFlowNode)Activator.CreateInstance(nodeType)!;
                var inPorts = instance.Inputs.Select(p => p.Name).ToList();
                var outPorts = instance.Outputs.Select(p => p.Name).ToList();

                if (instance.GetType().Name.Contains("SwitchCaseNode", StringComparison.OrdinalIgnoreCase))
                {
                    // Dynamic ports for switch
                    if (nodeElem.TryGetProperty("parameters", out var pElem) && pElem.TryGetProperty("Cases", out var casesElem))
                    {
                        // Cases are handled
                    }
                }

                nodeMap[id] = (instance, inPorts, outPorts);

                // Validate parameters
                if (nodeElem.TryGetProperty("parameters", out var paramsElem))
                {
                    var validKeys = new HashSet<string>(instance.ParameterDescriptors.Select(d => d.Key), StringComparer.OrdinalIgnoreCase);
                    foreach (var prop in paramsElem.EnumerateObject())
                    {
                        // Check if key is known or a dynamic variable
                        if (instance.GetType().Name.Contains("VariableInjectorNode") || instance.GetType().Name.Contains("SwitchCaseNode"))
                        {
                            continue;
                        }
                        if (instance.GetType().Name.Contains("AdvancedRenamerNode") && (prop.Name.Equals("PipelineName", StringComparison.OrdinalIgnoreCase) || prop.Name.Equals("CollisionStrategy", StringComparison.OrdinalIgnoreCase) || prop.Name.Equals("MethodSteps", StringComparison.OrdinalIgnoreCase)))
                        {
                            continue;
                        }

                        if (!validKeys.Contains(prop.Name) && !instance.Parameters.ContainsKey(prop.Name))
                        {
                            errors.Add($"[{fileName}] Node '{nodeTypeName}' (id: {id}) has unknown parameter '{prop.Name}'. Valid keys: {string.Join(", ", validKeys)}");
                        }
                    }
                }
            }

            // Validate edges
            if (root.TryGetProperty("edges", out var edgesElement))
            {
                foreach (var edgeElem in edgesElement.EnumerateArray())
                {
                    string srcId = edgeElem.GetProperty("sourceNodeId").GetString()!;
                    string srcPort = edgeElem.GetProperty("sourcePortName").GetString()!;
                    string tgtId = edgeElem.GetProperty("targetNodeId").GetString()!;
                    string tgtPort = edgeElem.GetProperty("targetPortName").GetString()!;

                    if (!nodeMap.TryGetValue(srcId, out var srcInfo))
                    {
                        errors.Add($"[{fileName}] Edge source node '{srcId}' not found.");
                        continue;
                    }

                    if (!nodeMap.TryGetValue(tgtId, out var tgtInfo))
                    {
                        errors.Add($"[{fileName}] Edge target node '{tgtId}' not found.");
                        continue;
                    }

                    if (!srcInfo.OutPorts.Contains(srcPort) && !srcInfo.Node.GetType().Name.Contains("SwitchCaseNode"))
                    {
                        errors.Add($"[{fileName}] Edge source port '{srcPort}' not found on node '{srcInfo.Node.GetType().Name}' (id: {srcId}). Valid: {string.Join(", ", srcInfo.OutPorts)}");
                    }

                    if (!tgtInfo.InPorts.Contains(tgtPort))
                    {
                        errors.Add($"[{fileName}] Edge target port '{tgtPort}' not found on node '{tgtInfo.Node.GetType().Name}' (id: {tgtId}). Valid: {string.Join(", ", tgtInfo.InPorts)}");
                    }
                }
            }
        }

        Assert.True(errors.Count == 0, $"Validation errors found in example flows:\n" + string.Join("\n", errors));
    }
}
