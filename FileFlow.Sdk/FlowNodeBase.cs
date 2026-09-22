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
public abstract class FlowNodeBase : IFlowNode, IPortTopologyNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public abstract string Name { get; }
    public abstract string Category { get; }
    public abstract string Description { get; }

    private IReadOnlyList<NodePort> _portsForInputs = [];
    private IReadOnlyList<NodePort> _portsForOutputs = [];

    /// <summary>
    /// Puertos de entrada del nodo. Un nodo con puertos fijos los asigna en el constructor
    /// (<c>Inputs = [...]</c>); uno con puertos que dependen de la configuración del usuario sobrescribe
    /// <see cref="BuildInputPorts"/> en lugar de esta propiedad.
    ///
    /// Se evalúa en cada lectura, a propósito: el diseño de la UI, el portapapeles y la carga de un
    /// perfil escriben <see cref="Parameters"/> directamente, así que cualquier caché de puertos quedaría
    /// obsoleta sin que nadie se entere.
    /// </summary>
    public IReadOnlyList<NodePort> Inputs
    {
        get => BuildInputPorts();
        protected set => _portsForInputs = value;
    }

    /// <summary>Puertos de salida del nodo. Ver <see cref="Inputs"/>.</summary>
    public IReadOnlyList<NodePort> Outputs
    {
        get => BuildOutputPorts();
        protected set => _portsForOutputs = value;
    }

    /// <summary>
    /// Último conjunto de puertos anunciado (nombres de entrada y salida, en orden). Es lo que permite que
    /// <see cref="NotifyPortsChanged"/> sea silencioso cuando se le pide reevaluar y no ha cambiado nada:
    /// el inspector escribe parámetros en cada pulsación de tecla y el editor no debe reconstruir el lienzo
    /// por ruido.
    /// </summary>
    private string? _lastAnnouncedTopology;

    /// <summary>Se dispara cuando el conjunto de puertos del nodo cambia de verdad. Ver <see cref="IPortTopologyNode"/>.</summary>
    public event EventHandler<PortTopologyChangedEventArgs>? PortsChanged;

    /// <summary>
    /// Reevalúa la topología y la reanuncia si cambió. La base vuelve a leer <see cref="Inputs"/>/<see cref="Outputs"/>,
    /// lo que basta para los nodos cuyos puertos se calculan al leerlos; un nodo que los materialice al
    /// sincronizarse (como el de scripts, que los asigna a partir de sus parámetros) debe sobrescribirlo
    /// para rederivarlos antes de anunciar.
    /// </summary>
    public virtual void RefreshPortTopology() => NotifyPortsChanged();

    /// <summary>
    /// Anuncia la topología vigente a quien la esté observando, sólo si difiere de la última anunciada.
    /// A diferencia de las demás notificaciones de la jerarquía, es <b>pull</b>: el nodo no guarda un
    /// espejo de sus puertos que deba mantener en sincronía, se lee el estado actual en cada anuncio.
    /// </summary>
    protected void NotifyPortsChanged()
    {
        var inputNames = Inputs.Select(port => port.Name).ToList();
        var outputNames = Outputs.Select(port => port.Name).ToList();

        string topology = $"{string.Join('\u001f', inputNames)}\u001e{string.Join('\u001f', outputNames)}";

        if (string.Equals(topology, _lastAnnouncedTopology, StringComparison.Ordinal))
        {
            return;
        }

        _lastAnnouncedTopology = topology;
        PortsChanged?.Invoke(this, new PortTopologyChangedEventArgs(inputNames, outputNames));
    }

    /// <summary>
    /// Puertos de entrada a exponer. Por defecto, los asignados en el constructor; sobrescríbelo cuando
    /// dependan de los parámetros del usuario (puertos dinámicos, como los casos de un switch o los
    /// nombres configurados de un subflujo).
    /// Se invoca en cada lectura de <see cref="Inputs"/>, así que debe ser barato y no mutar el nodo.
    /// </summary>
    protected virtual IReadOnlyList<NodePort> BuildInputPorts() => _portsForInputs;

    /// <summary>Puertos de salida a exponer. Ver <see cref="BuildInputPorts"/>.</summary>
    protected virtual IReadOnlyList<NodePort> BuildOutputPorts() => _portsForOutputs;
    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase);
    public virtual int MaxConcurrency => 0;

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
    /// Obtiene un parámetro tipado del diccionario de parámetros, con valor por defecto de respaldo.
    /// Es equivalente a <c>ParameterHelper.GetInt32/GetDouble/GetBoolean/GetString</c>: ambas rutas
    /// delegan en <see cref="ParameterValueConverter"/>, así que entiende <c>JsonElement</c> (perfiles
    /// guardados), números embebidos en texto (<c>"50%"</c> → 50) y nunca desborda en silencio. Ver el
    /// contrato completo en <see cref="ParameterValueConverter"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// int quality = GetParameter("Quality", 80);
    /// bool onlyDownscale = GetParameter("OnlyDownscale", true);
    /// string pattern = GetParameter("OutputDirectory", "{FileName}");
    /// </code>
    /// </example>
    protected T GetParameter<T>(string key, T defaultValue = default!) =>
        Parameters.TryGetValue(key, out var value)
            ? ParameterValueConverter.ConvertTo(value, defaultValue)
            : defaultValue;

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

    /// <summary>
    /// Obtiene una cadena localizada por clave desde LocalizationManager.
    /// </summary>
    protected string GetLocalizedString(string key, string fallback = "")
    {
        return Localization.LocalizationManager.Instance.GetString(key, fallback);
    }

    /// <summary>
    /// Obtiene una cadena formateada con localización desde LocalizationManager.
    /// </summary>
    protected string GetLocalizedFormat(string key, string fallbackTemplate, params object?[] args)
    {
        return Localization.LocalizationManager.Instance.GetFormattedString(key, fallbackTemplate, args);
    }

    #endregion
}
