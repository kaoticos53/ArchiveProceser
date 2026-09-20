using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Logic;
using FileFlow.Plugin.Subflows;
using FileFlow.Sdk;
using FileFlow.Sdk.Common;
using FileFlow.Sdk.Services;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit;

/// <summary>
/// Pruebas unitarias para el ciclo de vida y ejecución jerárquica de Subflujos en el motor DAG.
/// </summary>
public class SubflowExecutionTests
{
    [Fact]
    public async Task Subflow_SingleInputAndOutput_ShouldProcessAndEmitItems()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "FF_Subflow_Test_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        string srcFile = Path.Combine(tempDir, "document.txt");
        File.WriteAllText(srcFile, "Subflow test content");

        try
        {
            var loader = new PluginLoader();
            loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
            loader.RegisterNodeTypesFromAssembly(typeof(SubflowNode).Assembly);

            // 1. Construir subgrafo interno (SubflowInput -> VariableInjector -> SubflowOutput)
            var innerGraph = new WorkflowGraph { Name = "Inner Subflow" };
            innerGraph.Nodes.Add(new WorkflowNode
            {
                Id = "sub-in",
                NodeTypeName = "SubflowInputNode",
                Parameters = new(StringComparer.OrdinalIgnoreCase) { ["PortNames"] = "In" }
            });
            innerGraph.Nodes.Add(new WorkflowNode
            {
                Id = "sub-injector",
                NodeTypeName = "VariableInjectorNode",
                Parameters = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["VariableName"] = "SubflowProcessed",
                    ["ExpressionValue"] = "True"
                }
            });
            innerGraph.Nodes.Add(new WorkflowNode
            {
                Id = "sub-out",
                NodeTypeName = "SubflowOutputNode",
                Parameters = new(StringComparer.OrdinalIgnoreCase) { ["PortNames"] = "Out" }
            });

            innerGraph.Edges.Add(new WorkflowEdge
            {
                SourceNodeId = "sub-in",
                SourcePortName = "In",
                TargetNodeId = "sub-injector",
                TargetPortName = WellKnownPorts.In
            });
            innerGraph.Edges.Add(new WorkflowEdge
            {
                SourceNodeId = "sub-injector",
                SourcePortName = WellKnownPorts.Out,
                TargetNodeId = "sub-out",
                TargetPortName = "Out"
            });

            string innerJson = innerGraph.ToJson();

            // 2. Construir grafo principal (FolderSource -> SubflowNode -> DestinationSink)
            string outDir = Path.Combine(tempDir, "Output");
            Directory.CreateDirectory(outDir);

            var mainGraph = new WorkflowGraph { Name = "Main Pipeline" };
            mainGraph.Nodes.Add(new WorkflowNode
            {
                Id = "main-src",
                NodeTypeName = "FolderSourceNode",
                Parameters = new(StringComparer.OrdinalIgnoreCase) { ["SourcePath"] = tempDir }
            });
            mainGraph.Nodes.Add(new WorkflowNode
            {
                Id = "main-subflow",
                NodeTypeName = "SubflowNode",
                Parameters = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["EmbedDefinition"] = true,
                    ["SubflowDefinitionJson"] = innerJson,
                    ["SubflowName"] = "MySubflow"
                }
            });
            mainGraph.Nodes.Add(new WorkflowNode
            {
                Id = "main-sink",
                NodeTypeName = "DestinationSinkNode",
                Parameters = new(StringComparer.OrdinalIgnoreCase) { ["DestinationRoot"] = outDir }
            });

            mainGraph.Edges.Add(new WorkflowEdge
            {
                SourceNodeId = "main-src",
                SourcePortName = WellKnownPorts.Out,
                TargetNodeId = "main-subflow",
                TargetPortName = "In"
            });
            mainGraph.Edges.Add(new WorkflowEdge
            {
                SourceNodeId = "main-subflow",
                SourcePortName = "Out",
                TargetNodeId = "main-sink",
                TargetPortName = WellKnownPorts.In
            });

            // Act
            var executor = new WorkflowExecutor();
            await executor.ExecuteAsync(mainGraph, loader, CancellationToken.None);

            // Assert
            string expectedDest = Path.Combine(outDir, "document.txt");
            File.Exists(expectedDest).Should().BeTrue();
            File.ReadAllText(expectedDest).Should().Be("Subflow test content");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task Subflow_CircularRecursion_ShouldDetectAndThrowInvalidOperationException()
    {
        // Arrange
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(SubflowNode).Assembly);

        // Crear un subflujo A que llama a subflujo A
        var loopGraph = new WorkflowGraph { Name = "Recursive Subflow" };
        loopGraph.Nodes.Add(new WorkflowNode
        {
            Id = "in-1",
            NodeTypeName = "SubflowInputNode",
            Parameters = new(StringComparer.OrdinalIgnoreCase) { ["PortNames"] = "In" }
        });
        loopGraph.Nodes.Add(new WorkflowNode
        {
            Id = "sub-self",
            NodeTypeName = "SubflowNode",
            Parameters = new(StringComparer.OrdinalIgnoreCase)
            {
                ["EmbedDefinition"] = true,
                ["SubflowName"] = "RecursiveSubflow"
            }
        });
        loopGraph.Nodes.Add(new WorkflowNode
        {
            Id = "out-1",
            NodeTypeName = "SubflowOutputNode",
            Parameters = new(StringComparer.OrdinalIgnoreCase) { ["PortNames"] = "Out" }
        });

        loopGraph.Edges.Add(new WorkflowEdge
        {
            SourceNodeId = "in-1",
            SourcePortName = "In",
            TargetNodeId = "sub-self",
            TargetPortName = "In"
        });
        loopGraph.Edges.Add(new WorkflowEdge
        {
            SourceNodeId = "sub-self",
            SourcePortName = "Out",
            TargetNodeId = "out-1",
            TargetPortName = "Out"
        });

        string innerJson = loopGraph.ToJson();
        loopGraph.Nodes[1].Parameters["SubflowDefinitionJson"] = innerJson;
        string loopJson = loopGraph.ToJson();

        var subflowNode = new SubflowNode
        {
            Id = "sub-self",
            EmbedDefinition = true,
            SubflowDefinitionJson = loopJson,
            SubflowName = "RecursiveSubflow"
        };

        var service = new WorkflowSubflowExecutionService(loader);
        var previousService = ISubflowExecutionService.Instance;
        ISubflowExecutionService.Instance = service;

        var item = new FileItemContext("test_" + Guid.NewGuid().ToString("N") + ".txt");
        item.Metadata["__SubflowExecutionService__"] = service;
        var context = new WorkflowExecutionContext("dummy", new WorkflowExecutor(), CancellationToken.None);

        try
        {
            // Act & Assert
            var act = async () => await service.ExecuteSubflowAsync(subflowNode, "In", item, context, CancellationToken.None);
            var ex = await Assert.ThrowsAnyAsync<Exception>(act);
            bool hasRecursionError = ex is InvalidOperationException || (ex is AggregateException agg && agg.Flatten().InnerExceptions.Any(i => i is InvalidOperationException));
            hasRecursionError.Should().BeTrue();
        }
        finally
        {
            ISubflowExecutionService.Instance = previousService;
        }
    }

    [Fact]
    public void SubflowPortsDiscovery_ShouldFindConfiguredInputAndOutputPorts()
    {
        // Arrange
        var subGraph = new WorkflowGraph { Name = "MultiPort Subflow" };
        subGraph.Nodes.Add(new WorkflowNode
        {
            Id = "in",
            NodeTypeName = "SubflowInputNode",
            Parameters = new(StringComparer.OrdinalIgnoreCase) { ["PortNames"] = "In;Alternate" }
        });
        subGraph.Nodes.Add(new WorkflowNode
        {
            Id = "out",
            NodeTypeName = "SubflowOutputNode",
            Parameters = new(StringComparer.OrdinalIgnoreCase) { ["PortNames"] = "Out;Errors;Logs" }
        });

        var subflowNode = new SubflowNode
        {
            EmbedDefinition = true,
            SubflowDefinitionJson = subGraph.ToJson()
        };

        var service = new WorkflowSubflowExecutionService();

        // Act
        var (inputs, outputs) = service.DiscoverSubflowPorts(subflowNode);

        // Assert
        inputs.Should().BeEquivalentTo(["In", "Alternate"]);
        outputs.Should().BeEquivalentTo(["Out", "Errors", "Logs"]);
    }
}
