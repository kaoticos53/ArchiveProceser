using System.IO;
using System.Text.Json;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Logic;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Core;

public class WorkflowCliRunnerTests
{
    [Fact]
    public void WorkflowCliOptions_Parse_ReadsArgumentsCorrectly()
    {
        string[] args = [
            "--run", "test_flow.json",
            "--input", "C:\\Source",
            "--output", "C:\\Dest",
            "--dryrun",
            "--silent",
            "--watch",
            "--var", "EnvMode=Staging",
            "--param", "renamer-1.Prefix=NEW_",
            "--summary", "summary.json"
        ];

        var options = WorkflowCliOptions.Parse(args);

        options.WorkflowPath.Should().Be("test_flow.json");
        options.OverrideInputPath.Should().Be("C:\\Source");
        options.OverrideOutputPath.Should().Be("C:\\Dest");
        options.IsDryRun.Should().BeTrue();
        options.IsSilent.Should().BeTrue();
        options.IsWatchMode.Should().BeTrue();
        options.JsonSummaryPath.Should().Be("summary.json");
        options.Variables.Should().ContainKey("EnvMode").WhoseValue.Should().Be("Staging");
        options.ParameterOverrides.Should().ContainKey("renamer-1.Prefix").WhoseValue.Should().Be("NEW_");
        options.ShowHelp.Should().BeFalse();
    }

    [Fact]
    public void WorkflowCliOptions_ParseHelp_SetsShowHelpFlag()
    {
        string[] args = ["--help"];

        var options = WorkflowCliOptions.Parse(args);

        options.ShowHelp.Should().BeTrue();
    }

    [Fact]
    public async Task WorkflowCliRunner_ShowHelp_ReturnsZero()
    {
        var options = new WorkflowCliOptions { ShowHelp = true };
        using var sw = new StringWriter();

        int exitCode = await WorkflowCliRunner.RunAsync(options, writer: sw);

        exitCode.Should().Be(0);
        sw.ToString().Should().Contain("Headless CLI");
    }

    [Fact]
    public async Task WorkflowCliRunner_NonExistentFile_ReturnsOne()
    {
        var options = new WorkflowCliOptions { WorkflowPath = "non_existent_flow_9999.json" };
        using var sw = new StringWriter();

        int exitCode = await WorkflowCliRunner.RunAsync(options, writer: sw);

        exitCode.Should().Be(1);
        sw.ToString().Should().Contain("ERROR");
    }

    [Fact]
    public async Task WorkflowCliRunner_ValidWorkflow_WithJsonSummaryAndOverrides_ExecutesSuccessfully()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "FileFlowCliTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string sampleFile = Path.Combine(tempDir, "sample.txt");
        await File.WriteAllTextAsync(sampleFile, "CLI Test Content");

        string workflowJsonPath = Path.Combine(tempDir, "workflow.json");
        string summaryReportPath = Path.Combine(tempDir, "report.json");

        var graph = new WorkflowGraph
        {
            Name = "CLI Test Workflow",
            GlobalOutputDir = Path.Combine(tempDir, "Output")
        };

        var sourceNode = new WorkflowNode
        {
            Id = "source-1",
            NodeTypeName = typeof(FolderSourceNode).FullName!,
            Parameters = new Dictionary<string, object?>
            {
                ["SourcePath"] = tempDir,
                ["Recursive"] = false
            }
        };

        var throttleNode = new WorkflowNode
        {
            Id = "throttle-1",
            NodeTypeName = typeof(ThrottleDelayNode).FullName!,
            Parameters = new Dictionary<string, object?>
            {
                ["DelayMilliseconds"] = 10
            }
        };

        graph.Nodes.Add(sourceNode);
        graph.Nodes.Add(throttleNode);
        graph.Edges.Add(new WorkflowEdge
        {
            SourceNodeId = "source-1",
            SourcePortName = "Out",
            TargetNodeId = "throttle-1",
            TargetPortName = "In"
        });

        await File.WriteAllTextAsync(workflowJsonPath, graph.ToJson());

        try
        {
            var options = new WorkflowCliOptions
            {
                WorkflowPath = workflowJsonPath,
                IsDryRun = true,
                IsSilent = false,
                JsonSummaryPath = summaryReportPath
            };
            options.ParameterOverrides["throttle-1.DelayMilliseconds"] = "5";

            using var sw = new StringWriter();
            var pluginLoader = new PluginLoader();
            pluginLoader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
            pluginLoader.RegisterNodeTypesFromAssembly(typeof(ThrottleDelayNode).Assembly);

            int exitCode = await WorkflowCliRunner.RunAsync(options, pluginLoader, sw);

            exitCode.Should().Be(0);
            sw.ToString().Should().Contain("Flujo completado con éxito");

            // Validar reporte JSON generado
            File.Exists(summaryReportPath).Should().BeTrue();
            string jsonSummary = await File.ReadAllTextAsync(summaryReportPath);
            using var doc = JsonDocument.Parse(jsonSummary);
            doc.RootElement.GetProperty("Succeeded").GetBoolean().Should().BeTrue();
            doc.RootElement.GetProperty("TotalItemsProcessed").GetInt64().Should().BeGreaterThanOrEqualTo(1);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}
