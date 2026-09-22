using System.IO;
using System.Text.Json;
using FileFlow.App.Services;
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

    /// <summary>
    /// Un archivo sin nodos no es un flujo: ejecutarlo no hace nada. Terminar en verde con cero elementos
    /// procesados era la peor forma de fallar —parecía un éxito—, así que el comando falla y lo dice en los dos
    /// sitios donde alguien lo lee: la salida y el resumen.
    /// </summary>
    [Fact]
    public async Task WorkflowCliRunner_WorkflowWithoutNodes_FailsInsteadOfEndingGreen()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "FileFlowCliEmpty_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string workflowJsonPath = Path.Combine(tempDir, "vacio.json");
        string summaryReportPath = Path.Combine(tempDir, "report.json");
        await File.WriteAllTextAsync(workflowJsonPath, new WorkflowGraph { Name = "Vacío" }.ToJson());

        try
        {
            var options = new WorkflowCliOptions
            {
                WorkflowPath = workflowJsonPath,
                JsonSummaryPath = summaryReportPath
            };

            using var sw = new StringWriter();
            int exitCode = await WorkflowCliRunner.RunAsync(options, new PluginLoader(), sw);

            exitCode.Should().Be(1, "no hay nada que ejecutar, así que no puede ser un éxito");
            sw.ToString().Should().Contain("ningún nodo");
            sw.ToString().Should().NotContain("completado con éxito");

            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(summaryReportPath));
            doc.RootElement.GetProperty("Succeeded").GetBoolean().Should().BeFalse(
                "el resumen es lo que lee quien automatiza, y no puede decir que fue bien");
            doc.RootElement.GetProperty("ErrorMessage").GetString().Should().Contain("ningún nodo");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    /// <summary>
    /// Un flujo sin nodos no es lo mismo que un flujo sin trabajo: si la carpeta de origen está vacía, el flujo
    /// se ejecutó y no encontró nada, y eso es un éxito. La frontera importa para que el aviso no acabe
    /// fallando ejecuciones legítimas.
    /// </summary>
    [Fact]
    public async Task WorkflowCliRunner_WorkflowWhoseSourceFindsNothing_StillSucceeds()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "FileFlowCliNoWork_" + Guid.NewGuid().ToString("N"));
        string inputDir = Path.Combine(tempDir, "Entrada");
        Directory.CreateDirectory(inputDir);
        string workflowJsonPath = Path.Combine(tempDir, "sin-trabajo.json");
        string summaryReportPath = Path.Combine(tempDir, "report.json");

        var graph = new WorkflowGraph
        {
            Name = "Origen sin archivos",
            GlobalOutputDir = Path.Combine(tempDir, "Salida")
        };
        graph.Nodes.Add(new WorkflowNode
        {
            Id = "source-1",
            NodeTypeName = typeof(FolderSourceNode).FullName!,
            Parameters = new Dictionary<string, object?>
            {
                ["SourcePath"] = inputDir,
                ["Recursive"] = false
            }
        });
        await File.WriteAllTextAsync(workflowJsonPath, graph.ToJson());

        try
        {
            var options = new WorkflowCliOptions
            {
                WorkflowPath = workflowJsonPath,
                IsDryRun = true,
                JsonSummaryPath = summaryReportPath
            };

            using var sw = new StringWriter();
            var pluginLoader = new PluginLoader();
            pluginLoader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);

            int exitCode = await WorkflowCliRunner.RunAsync(options, pluginLoader, sw);

            exitCode.Should().Be(0, "el flujo tiene un nodo y se ejecutó: que no encontrara archivos no es un fallo");

            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(summaryReportPath));
            doc.RootElement.GetProperty("Succeeded").GetBoolean().Should().BeTrue();
            doc.RootElement.GetProperty("TotalItemsProcessed").GetInt64().Should().Be(0);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    /// <summary>
    /// La otra mitad del diagnóstico: un aviso <b>no</b> bloquea. Este flujo tiene un nodo y no escribe en
    /// ninguna parte, así que el diagnóstico dirá que el resultado no llega a ningún destino —y el flujo se
    /// ejecuta igual—, porque un diagnóstico que impide ejecutar cosas legítimas es un diagnóstico que se
    /// desactiva.
    /// </summary>
    [Fact]
    public async Task WorkflowCliRunner_FlowThatWritesNowhere_WarnsAndStillRuns()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "FileFlowCliWarn_" + Guid.NewGuid().ToString("N"));
        string inputDir = Path.Combine(tempDir, "Entrada");
        Directory.CreateDirectory(inputDir);
        string workflowJsonPath = Path.Combine(tempDir, "sin-destino.json");
        string summaryReportPath = Path.Combine(tempDir, "report.json");

        var graph = new WorkflowGraph
        {
            Name = "Origen sin destino",
            GlobalOutputDir = Path.Combine(tempDir, "Salida")
        };
        graph.Nodes.Add(new WorkflowNode
        {
            Id = "source-1",
            NodeTypeName = typeof(FolderSourceNode).FullName!,
            Parameters = new Dictionary<string, object?>
            {
                ["SourcePath"] = inputDir,
                ["Recursive"] = false
            }
        });
        await File.WriteAllTextAsync(workflowJsonPath, graph.ToJson());

        try
        {
            var options = new WorkflowCliOptions
            {
                WorkflowPath = workflowJsonPath,
                IsDryRun = true,
                JsonSummaryPath = summaryReportPath
            };

            using var sw = new StringWriter();
            var pluginLoader = new PluginLoader();
            pluginLoader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);

            int exitCode = await WorkflowCliRunner.RunAsync(options, pluginLoader, sw);

            exitCode.Should().Be(0, "el aviso cuenta lo que conviene saber: no es un motivo para no ejecutar");
            sw.ToString().Should().Contain("AVISO").And.Contain("no llega a ningún destino",
                "y se dice antes de arrancar, que es cuando sirve de algo");

            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(summaryReportPath));
            doc.RootElement.GetProperty("Succeeded").GetBoolean().Should().BeTrue();
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
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

    /// <summary>
    /// El flujo puede estar guardado por la app o por el propio Core, y el CLI tiene que ejecutar los dos:
    /// no son el mismo archivo —los nombres van en cajas distintas— y durante un tiempo el lector del CLI sólo
    /// entendía el suyo. Lo que delataba el fallo no era un error sino un resumen en verde con cero elementos
    /// procesados, así que la prueba comprueba que el trabajo se hizo, no que el comando terminara bien.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WorkflowCliRunner_ValidWorkflow_WithJsonSummaryAndOverrides_ExecutesSuccessfully(bool savedByTheApp)
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

        await File.WriteAllTextAsync(workflowJsonPath, savedByTheApp
            ? new WorkflowStorageService().SerializeGraph(graph)
            : graph.ToJson());

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
