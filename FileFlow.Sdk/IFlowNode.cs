namespace FileFlow.Sdk;

public interface IFlowNode
{
    string Id { get; set; }
    string Name { get; }
    string Category { get; }
    string Description { get; }
    IReadOnlyList<NodePort> Inputs { get; }
    IReadOnlyList<NodePort> Outputs { get; }
    Dictionary<string, object?> Parameters { get; }

    /// <summary>
    /// Descriptores de esquema de parámetros para renderizado y ordenamiento automático en la UI.
    /// Si un nodo no los implementa, la UI renderizará los parámetros a partir del diccionario <see cref="Parameters"/>.
    /// </summary>
    IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => Array.Empty<NodeParameterDescriptor>();

    /// <summary>
    /// Descriptores de acciones de herramientas o botones modales avanzados expuestos por el nodo.
    /// </summary>
    IReadOnlyList<NodeActionDescriptor> CustomActions => Array.Empty<NodeActionDescriptor>();

    Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Hook invocado por el motor de orquestación cuando todos los elementos aguas arriba han completado su procesamiento.
    /// Permite a nodos acumuladores o agregadores (ej. reportes consolidados, archivadores batch) emitir resultados finales.
    /// </summary>
    Task OnWorkflowCompletedAsync(
        IFlowExecutionContext context,
        CancellationToken cancellationToken) => Task.CompletedTask;
}
