using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FileFlow.Sdk;

/// <summary>
/// Clase base abstracta recomendada para la creación de nuevos nodos y plugins en FileFlow Studio.
/// Proporciona inicialización de puertos, manejo estándar de parámetros con tipado seguro,
/// y emisión simplificada a través del contexto de ejecución.
/// </summary>
public abstract class FlowNodeBase : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public abstract string Name { get; }
    public abstract string Category { get; }
    public abstract string Description { get; }

    public virtual IReadOnlyList<NodePort> Inputs { get; protected set; } = [];
    public virtual IReadOnlyList<NodePort> Outputs { get; protected set; } = [];
    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase);

    public virtual IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => [];
    public virtual IReadOnlyList<NodeActionDescriptor> CustomActions => [];

    public abstract Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken);

    public virtual Task OnWorkflowCompletedAsync(
        IFlowExecutionContext context,
        CancellationToken cancellationToken) => Task.CompletedTask;

    #region Métodos de ayuda para desarrollo ágil y limpio de nodos

    /// <summary>
    /// Obtiene un parámetro tipado del diccionario de parámetros con valor por defecto de respaldo.
    /// </summary>
    protected T GetParameter<T>(string key, T defaultValue = default!)
    {
        if (Parameters.TryGetValue(key, out var val) && val is not null)
        {
            if (val is T typed) return typed;
            try
            {
                return (T)Convert.ChangeType(val, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }
        return defaultValue;
    }

    /// <summary>
    /// Establece un parámetro en el diccionario de parámetros.
    /// </summary>
    protected void SetParameter<T>(string key, T value)
    {
        Parameters[key] = value;
    }

    /// <summary>
    /// Emite un elemento a través de un puerto de salida determinado (por defecto "Out").
    /// </summary>
    protected Task EmitAsync(
        IFlowExecutionContext context,
        FileItemContext item,
        string portName = "Out")
    {
        return context.EmitAsync(portName, item);
    }

    /// <summary>
    /// Emite un registro de log estandarizado para este nodo.
    /// </summary>
    protected void Log(
        IFlowExecutionContext context,
        string message,
        LogLevel level = LogLevel.Information,
        FileItemContext? item = null)
    {
        context.Log(message, level, item);
    }

    #endregion
}
