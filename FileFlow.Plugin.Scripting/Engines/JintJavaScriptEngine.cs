using FileFlow.Sdk;
using Jint;
using Jint.Native;

namespace FileFlow.Plugin.Scripting.Engines;

public sealed class JintJavaScriptEngine : IScriptExecutionEngine
{
    private static readonly Lazy<JintJavaScriptEngine> _instance = new(() => new JintJavaScriptEngine());
    public static JintJavaScriptEngine Instance => _instance.Value;

    public async Task ExecuteAsync(string code, ScriptExecutionContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            await context.EmitAsync("Out").ConfigureAwait(false);
            return;
        }

        var emittedTasks = new List<Task>();

        var engine = new Engine(cfg =>
        {
            cfg.CancellationToken(cancellationToken);
            cfg.TimeoutInterval(TimeSpan.FromSeconds(30));
            cfg.LimitMemory(64 * 1024 * 1024); // 64 MB max memory
            cfg.MaxStatements(500_000);
        });

        // Configurar objetos y funciones globales de JavaScript
        engine.SetValue("item", context.Item);
        engine.SetValue("file", context.Item);
        engine.SetValue("context", context.FlowContext);

        // Funciones de resolución de variables y plantillas implícitas
        engine.SetValue("resolve", new Func<string, string>(tpl => 
            FileFlow.Sdk.TemplateEngine.VariableTemplateResolver.Resolve(tpl, context.Item)));
        engine.SetValue("getVar", new Func<string, string>(varName => 
            FileFlow.Sdk.TemplateEngine.VariableTemplateResolver.GetVariableValue(varName, context.Item, null)));

        // Funciones de emisión
        engine.SetValue("emit", new Action<string, object?>((portName, fileObj) =>
        {
            FileItemContext targetItem = fileObj is FileItemContext fic ? fic : context.Item;
            emittedTasks.Add(context.EmitAsync(portName, targetItem));
        }));

        // Logging
        engine.SetValue("log", new Action<string>(msg => context.Log(msg, LogLevel.Information)));

        var consoleObj = new
        {
            log = new Action<object?>(msg => context.Log(msg?.ToString() ?? "null", LogLevel.Information)),
            warn = new Action<object?>(msg => context.Log(msg?.ToString() ?? "null", LogLevel.Warning)),
            error = new Action<object?>(msg => context.Log(msg?.ToString() ?? "null", LogLevel.Error)),
            info = new Action<object?>(msg => context.Log(msg?.ToString() ?? "null", LogLevel.Information))
        };
        engine.SetValue("console", consoleObj);

        try
        {
            engine.Execute(code);

            // Si se llamaron métodos emit() dentro de JS, esperar a que todas las emisiones asíncronas completen
            if (emittedTasks.Count > 0)
            {
                await Task.WhenAll(emittedTasks).ConfigureAwait(false);
            }
        }
        catch (Jint.Runtime.JavaScriptException ex)
        {
            context.Log($"[JavaScript Error] Línea {ex.Location.Start.Line}: {ex.Message}", LogLevel.Error);
            throw new InvalidOperationException($"Error en script JavaScript (Línea {ex.Location.Start.Line}): {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            context.Log($"[JavaScript Error de Ejecución] {ex.Message}", LogLevel.Error);
            throw;
        }
    }
}
