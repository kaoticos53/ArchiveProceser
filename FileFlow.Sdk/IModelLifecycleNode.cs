namespace FileFlow.Sdk;

/// <summary>
/// Contrato opcional para nodos que gestionan o dependen de modelos pesados de inferencia (IA, redes neuronales, ONNX),
/// permitiendo inspeccionar el estado de carga en memoria (RAM/VRAM), precargarlos o liberarlos deterministamente.
/// </summary>
public interface IModelLifecycleNode : IFlowNode
{
    /// <summary>
    /// Indica si el modelo de inferencia asociado se encuentra actualmente cargado en memoria (RAM o VRAM).
    /// </summary>
    bool IsModelLoaded { get; }

    /// <summary>
    /// Nombre o identificador amigable del modelo configurado actualmente en el nodo.
    /// </summary>
    string? ModelIdentifier { get; }

    /// <summary>
    /// Indica si el modelo configurado o cargado aprovecha aceleración por hardware GPU (DirectML / CUDA).
    /// </summary>
    bool IsGpuAccelerated => false;

    /// <summary>
    /// Precarga o inicializa la sesión de inferencia en memoria en segundo plano.
    /// </summary>
    Task PreloadModelAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Libera la sesión de inferencia y descarga el modelo de la memoria RAM/VRAM inmediatamente.
    /// </summary>
    void UnloadModel();

    /// <summary>
    /// Evento notificado cuando cambia el estado de carga del modelo (cargado/descargado).
    /// </summary>
    event Action? ModelStatusChanged;
}
