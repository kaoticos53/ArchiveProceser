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

    private readonly ConcurrentDictionary<string, (ScriptRunner<object> Runner, long LastUsedTicks)> _cachedRunners = new();
    private const int MaxCachedScripts = 256;

    /// <summary>
    /// Tiempo máximo de ejecución de un script de usuario (seguridad). Configurable si se desea.
    /// </summary>
    private static readonly TimeSpan ScriptExecutionTimeout = TimeSpan.FromSeconds(60);

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
            if (!_cachedRunners.TryGetValue(hash, out var entry))
            {
                var script = CSharpScript.Create<object>(code, DefaultScriptOptions, typeof(RoslynScriptGlobals));
                var runner = script.CreateDelegate(cancellationToken);
                entry = (runner, Environment.TickCount64);
                _cachedRunners[hash] = entry;

                // LOW-01: Evicción LRU si se supera el límite de caché
                if (_cachedRunners.Count > MaxCachedScripts)
                {
                    var oldest = _cachedRunners.OrderBy(kv => kv.Value.LastUsedTicks).First();
                    _cachedRunners.TryRemove(oldest.Key, out _);
                }
            }
            else
            {
                _cachedRunners[hash] = entry with { LastUsedTicks = Environment.TickCount64 };
            }

            var globals = new RoslynScriptGlobals
            {
                Item = context.Item,
                Context = context.FlowContext,
                ScriptContext = context,
                CancellationToken = cancellationToken
            };

            // CRIT-02: Timeout de seguridad para evitar scripts infinitos o maliciosos
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(ScriptExecutionTimeout);

            try
            {
                await entry.Runner(globals, timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                context.Log($"[Roslyn C#] El script excedió el tiempo límite de ejecución ({ScriptExecutionTimeout.TotalSeconds}s).", LogLevel.Error);
                throw new TimeoutException($"El script C# excedió el tiempo máximo de ejecución de {ScriptExecutionTimeout.TotalSeconds} segundos.");
            }
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
