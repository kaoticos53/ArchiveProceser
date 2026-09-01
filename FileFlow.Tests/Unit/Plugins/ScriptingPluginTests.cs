using System.IO;
using FileFlow.Plugin.Scripting;
using FileFlow.Plugin.Scripting.Engines;
using FileFlow.Plugin.Scripting.Services;
using FileFlow.Sdk;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

public class ScriptingPluginTests
{
    private sealed class MockFlowExecutionContext : IFlowExecutionContext
    {
        public bool IsDryRun => false;
        public List<(string Port, FileItemContext Item)> Emitted { get; } = [];
        public List<string> Logs { get; } = [];

        public Task EmitAsync(string outputPortName, FileItemContext item)
        {
            Emitted.Add((outputPortName, item));
            return Task.CompletedTask;
        }

        public void ReportProgress(double percentage, string statusMessage) { }

        public void Log(string message, LogLevel level)
        {
            Logs.Add($"[{level}] {message}");
        }

        public void RegisterPlannedAction(PlannedAction action) { }
        public void RecordJournalEntry(JournalEntry entry) { }
    }

    [Fact]
    public async Task RoslynCSharpEngine_ShouldExecuteAndModifyMetadata()
    {
        var item = new FileItemContext(Path.Combine(Path.GetTempPath(), "reporte_anual.pdf"))
        {
            FileSizeBytes = 25 * 1024 * 1024 // 25 MB
        };
        var mockContext = new MockFlowExecutionContext();
        var execContext = new ScriptExecutionContext
        {
            Item = item,
            FlowContext = mockContext,
            InputPortName = "In",
            CancellationToken = CancellationToken.None
        };

        string csharpCode = @"
            double mb = Item.FileSizeBytes / (1024.0 * 1024.0);
            Item.Metadata[""TamanoMB""] = mb;
            Item.Tags.Add(""DocumentoVerificado"");
            Log($""Archivo {Item.FileName} procesado con éxito"");

            if (mb > 10)
            {
                await EmitAsync(""Grandes"");
            }
            else
            {
                await EmitAsync(""Pequenos"");
            }
        ";

        await RoslynCSharpEngine.Instance.ExecuteAsync(csharpCode, execContext, CancellationToken.None);

        Assert.True(item.Metadata.ContainsKey("TamanoMB"));
        Assert.Equal(25.0, (double)item.Metadata["TamanoMB"]!, 1);
        Assert.Contains("DocumentoVerificado", item.Tags);
        Assert.Single(mockContext.Emitted);
        Assert.Equal("Grandes", mockContext.Emitted[0].Port);
    }

    [Fact]
    public async Task RoslynCSharpEngine_ShouldThrowOnSyntaxError()
    {
        var item = new FileItemContext(Path.Combine(Path.GetTempPath(), "test.txt"));
        var mockContext = new MockFlowExecutionContext();
        var execContext = new ScriptExecutionContext
        {
            Item = item,
            FlowContext = mockContext,
            InputPortName = "In",
            CancellationToken = CancellationToken.None
        };

        string brokenCode = "esto no es codigo valido csharp;;;";

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await RoslynCSharpEngine.Instance.ExecuteAsync(brokenCode, execContext, CancellationToken.None);
        });
    }

    [Fact]
    public async Task JintJavaScriptEngine_ShouldExecuteAndEmitPorts()
    {
        var item = new FileItemContext(Path.Combine(Path.GetTempPath(), "pelicula.mkv"))
        {
            FileSizeBytes = 800 * 1024 * 1024 // 800 MB
        };
        var mockContext = new MockFlowExecutionContext();
        var execContext = new ScriptExecutionContext
        {
            Item = item,
            FlowContext = mockContext,
            InputPortName = "In",
            CancellationToken = CancellationToken.None
        };

        string jsCode = @"
            var fname = item.FileName.toLowerCase();
            if (fname.endsWith('.mkv') || fname.endsWith('.mp4')) {
                item.Metadata['EsVideo'] = true;
                item.Tags.Add('VideoHD');
                emit('Videos', item);
            } else {
                emit('Otros', item);
            }
        ";

        await JintJavaScriptEngine.Instance.ExecuteAsync(jsCode, execContext, CancellationToken.None);

        Assert.True(item.Metadata.ContainsKey("EsVideo"));
        Assert.Equal(true, item.Metadata["EsVideo"]);
        Assert.Contains("VideoHD", item.Tags);
        Assert.Single(mockContext.Emitted);
        Assert.Equal("Videos", mockContext.Emitted[0].Port);
    }

    [Fact]
    public async Task RoslynAndJint_ShouldSupportVariableTemplateResolution()
    {
        var item = new FileItemContext(Path.Combine(Path.GetTempPath(), "reporte_mensual.pdf"))
        {
            FileSizeBytes = 10 * 1024 * 1024
        };
        item.Metadata["Hash:SHA256"] = "abcdef1234567890";

        var mockContext = new MockFlowExecutionContext();
        var execContext = new ScriptExecutionContext
        {
            Item = item,
            FlowContext = mockContext,
            InputPortName = "In",
            CancellationToken = CancellationToken.None
        };

        // 1. Probar en C#
        string csharpCode = @"
            string resolved = Resolve(""{FileNameNoExt}_[OK]"");
            Item.Metadata[""NombreFormateadoCS""] = resolved;
            string hash = Item.Metadata[""Hash:SHA256""]?.ToString() ?? """";
            Item.Metadata[""HashLeidoCS""] = hash;
            await EmitAsync(""Out"");
        ";
        await RoslynCSharpEngine.Instance.ExecuteAsync(csharpCode, execContext, CancellationToken.None);

        Assert.Equal("reporte_mensual_[OK]", item.Metadata["NombreFormateadoCS"]);
        Assert.Equal("abcdef1234567890", item.Metadata["HashLeidoCS"]);

        // 2. Probar en JS
        string jsCode = @"
            var res = resolve('{FileNameNoExt}_[JS]');
            item.Metadata['NombreFormateadoJS'] = res;
            var hash = item.Metadata['Hash:SHA256'];
            item.Metadata['HashLeidoJS'] = hash;
            emit('Out', item);
        ";
        await JintJavaScriptEngine.Instance.ExecuteAsync(jsCode, execContext, CancellationToken.None);

        Assert.Equal("reporte_mensual_[JS]", item.Metadata["NombreFormateadoJS"]);
        Assert.Equal("abcdef1234567890", item.Metadata["HashLeidoJS"]);
    }

    [Fact]
    public async Task CustomScriptNode_ShouldSupportDynamicPortsAndExecution()
    {
        var node = new CustomScriptNode();
        node.Parameters["Language"] = "CSharp";
        node.Parameters["InputPorts"] = "In, SecondaryIn";
        node.Parameters["OutputPorts"] = "Aprobados, Rechazados";
        node.Parameters["ScriptCode"] = @"
            Item.Metadata[""ProcesadoPorNodo""] = true;
            await EmitAsync(""Aprobados"");
        ";
        node.SyncPortsFromParameters();

        Assert.Equal(2, node.Inputs.Count);
        Assert.Equal("In", node.Inputs[0].Name);
        Assert.Equal("SecondaryIn", node.Inputs[1].Name);

        Assert.Equal(2, node.Outputs.Count);
        Assert.Equal("Aprobados", node.Outputs[0].Name);
        Assert.Equal("Rechazados", node.Outputs[1].Name);

        var item = new FileItemContext(Path.Combine(Path.GetTempPath(), "foto.jpg"));
        var mockContext = new MockFlowExecutionContext();

        await node.ExecuteAsync("In", item, mockContext, CancellationToken.None);

        Assert.True(item.Metadata.ContainsKey("ProcesadoPorNodo"));
        Assert.Single(mockContext.Emitted);
        Assert.Equal("Aprobados", mockContext.Emitted[0].Port);
    }

    [Fact]
    public void ScriptLibraryService_ShouldProvideBuiltInPresets()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "FF_Scripts_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var service = new ScriptLibraryService(tempDir);
            var builtIn = service.GetBuiltInScripts();

            Assert.NotEmpty(builtIn);
            Assert.Contains(builtIn, s => s.Language == "CSharp");
            Assert.Contains(builtIn, s => s.Language == "JavaScript");

            // Guardar script de usuario
            var userScript = new ScriptDefinition
            {
                Name = "Mi Script Personalizado",
                Description = "Prueba de guardado",
                Language = "CSharp",
                ScriptCode = "Log(\"Hola Mundo\"); await EmitAsync(\"Out\");",
                InputPorts = ["In"],
                OutputPorts = ["Out"]
            };

            service.SaveUserScript(userScript);
            var loaded = service.GetUserScripts();

            Assert.Single(loaded);
            Assert.Equal("Mi Script Personalizado", loaded[0].Name);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}
