using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using FileFlow.Sdk;
using FileFlow.Sdk.TemplateEngine;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace FileFlow.Plugin.Scripting.Engines;

public sealed class RoslynScriptGlobals
{
    public FileItemContext Item { get; init; } = null!;
    public FileItemContext File => Item; // Alias para mayor comodidad
    public IFlowExecutionContext Context { get; init; } = null!;
    public IFlowExecutionContext Flow => Context; // Alias
    public ScriptExecutionContext ScriptContext { get; init; } = null!;
    public CancellationToken CancellationToken { get; init; }

    public Task EmitAsync(string portName, FileItemContext? fileItem = null) =>
        ScriptContext.EmitAsync(portName, fileItem);

    public void Log(string message, LogLevel level = LogLevel.Information) =>
        ScriptContext.Log(message, level);

    public string Resolve(string template) =>
        VariableTemplateResolver.Resolve(template, Item);
}

public sealed class RoslynCSharpEngine : IScriptExecutionEngine
{
    private static readonly Lazy<RoslynCSharpEngine> _instance = new(() => new RoslynCSharpEngine());
    public static RoslynCSharpEngine Instance => _instance.Value;

    private readonly ConcurrentDictionary<string, ScriptRunner<object>> _cachedRunners = new();

    private static readonly ScriptOptions DefaultScriptOptions = ScriptOptions.Default
        .WithImports(
            "System",
            "System.IO",
            "System.Collections.Generic",
            "System.Linq",
            "System.Text",
            "System.Text.RegularExpressions",
            "System.Text.Json",
            "System.Threading",
            "System.Threading.Tasks",
            "FileFlow.Sdk",
            "FileFlow.Sdk.Localization",
            "FileFlow.Sdk.TemplateEngine")
        .WithReferences(
            typeof(FileItemContext).Assembly,
            typeof(System.Text.Json.JsonSerializer).Assembly,
            typeof(System.Text.RegularExpressions.Regex).Assembly,
            typeof(System.Linq.Enumerable).Assembly);

    public async Task ExecuteAsync(string code, ScriptExecutionContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            // Si el script está vacío, emitir por defecto a "Out"
            await context.EmitAsync("Out").ConfigureAwait(false);
            return;
        }

        string hash = ComputeHash(code);

        try
        {
            if (!_cachedRunners.TryGetValue(hash, out var runner))
            {
                var script = CSharpScript.Create<object>(code, DefaultScriptOptions, typeof(RoslynScriptGlobals));
                runner = script.CreateDelegate(cancellationToken);
                _cachedRunners[hash] = runner;
            }

            var globals = new RoslynScriptGlobals
            {
                Item = context.Item,
                Context = context.FlowContext,
                ScriptContext = context,
                CancellationToken = cancellationToken
            };

            await runner(globals, cancellationToken).ConfigureAwait(false);
        }
        catch (CompilationErrorException ex)
        {
            string formattedErrors = string.Join(Environment.NewLine, ex.Diagnostics);
            context.Log($"[Roslyn C# Error de Compilación] {formattedErrors}", LogLevel.Error);
            throw new InvalidOperationException($"Error al compilar el script C#: {formattedErrors}", ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not InvalidOperationException)
        {
            context.Log($"[Roslyn C# Error de Ejecución] {ex.Message}", LogLevel.Error);
            throw;
        }
    }

    private static string ComputeHash(string input)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
