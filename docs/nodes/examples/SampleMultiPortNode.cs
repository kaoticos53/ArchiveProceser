using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.Examples;

/// <summary>
/// Ejemplo completo de nodo personalizado para FileFlow Studio.
/// Demuestra el uso de múltiples puertos de entrada, múltiples puertos de salida,
/// configuración de parámetros y manejo asíncrono de errores.
/// </summary>
[NodeDefinition("SampleMultiPortNode_Name", "General", "SampleMultiPortNode_Desc")]
public class SampleMultiPortNode : IFlowNode
{
    /// <summary>
    /// Identificador único de instancia del nodo.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Nombre visible del nodo (soporta traducción multilingüe dinámica).
    /// </summary>
    public string Name => LocalizationManager.Instance.GetString("SampleMultiPortNode_Name", "Filtro Multi-Puerto de Archivos");

    /// <summary>
    /// Categoría del nodo dentro del catálogo lateral.
    /// </summary>
    public string Category => "General";

    /// <summary>
    /// Descripción del nodo expuesta en las tarjetas informativas.
    /// </summary>
    public string Description => LocalizationManager.Instance.GetString(
        "SampleMultiPortNode_Desc",
        "Clasifica elementos recibidos de múltiples entradas según su tamaño y extensión hacia 3 puertos de salida.");

    /// <summary>
    /// Definición de los puertos de entrada (Entrada Principal y Entrada Secundaria).
    /// </summary>
    public IReadOnlyList<NodePort> Inputs { get; } = new[]
    {
        new NodePort("MainInput", typeof(FileItemContext), PortDirection.Input, "Entrada Principal"),
        new NodePort("SecondaryInput", typeof(FileItemContext), PortDirection.Input, "Entrada Secundaria")
    };

    /// <summary>
    /// Definición de los puertos de salida (Aprobados, Rechazados y Error).
    /// </summary>
    public IReadOnlyList<NodePort> Outputs { get; } = new[]
    {
        new NodePort("Approved", typeof(FileItemContext), PortDirection.Output, "Aprobados"),
        new NodePort("Rejected", typeof(FileItemContext), PortDirection.Output, "Rechazados"),
        new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
    };

    /// <summary>
    /// Parámetros editables expuestos en la interfaz de usuario.
    /// </summary>
    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MaxFileSizeMB"] = 25,
        ["TargetExtension"] = ".zip",
        ["StrictValidation"] = true,
        ["OutputDirectoryPath"] = @"C:\Procesados"
    };

    /// <summary>
    /// Ejecución asíncrona principal del nodo.
    /// </summary>
    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        // 1. Obtener y validar parámetros de configuración
        int maxMb = Parameters.TryGetValue("MaxFileSizeMB", out var mbVal) ? Convert.ToInt32(mbVal) : 25;
        string targetExt = Parameters.TryGetValue("TargetExtension", out var extVal) ? extVal?.ToString() ?? ".zip" : ".zip";
        bool isStrict = Parameters.TryGetValue("StrictValidation", out var stVal) && Convert.ToBoolean(stVal);

        string filePath = item.CurrentPath;

        // Comprobación previa de existencia del archivo
        if (string.IsNullOrWhiteSpace(filePath) || (!File.Exists(filePath) && !Directory.Exists(filePath)))
        {
            context.Log($"[SampleMultiPortNode] El archivo o carpeta '{filePath}' no fue encontrado.", LogLevel.Warning);
            item.AddLog($"SampleMultiPortNode error: ruta '{filePath}' no válida.");
            await context.EmitAsync("Error", item);
            return;
        }

        try
        {
            context.Log($"[SampleMultiPortNode] Procesando '{filePath}' (Recibido por '{inputPortName}')...", LogLevel.Information);

            // Verificación de cancelación solicitada por el usuario
            cancellationToken.ThrowIfCancellationRequested();

            long maxSizeBytes = (long)maxMb * 1024 * 1024;
            string actualExt = Path.GetExtension(filePath);

            // 2. Lógica de clasificación por puertos
            bool extensionMatches = actualExt.Equals(targetExt, StringComparison.OrdinalIgnoreCase);
            bool sizeMatches = item.FileSizeBytes <= maxSizeBytes;

            if (extensionMatches && (sizeMatches || !isStrict))
            {
                // Emitir hacia el puerto "Approved"
                item.Metadata["ProcessedBy"] = "SampleMultiPortNode";
                item.Metadata["InputSourcePort"] = inputPortName;
                item.AddLog($"SampleMultiPortNode clasificado como APROBADO (Ext={actualExt}, MB={item.FileSizeBytes / (1024.0 * 1024.0):F2})");

                await context.EmitAsync("Approved", item);
            }
            else
            {
                // Emitir hacia el puerto "Rejected"
                item.Metadata["RejectionReason"] = !extensionMatches ? "Extensión no coincide" : "Excede tamaño máximo";
                item.AddLog($"SampleMultiPortNode clasificado como RECHAZADO ({item.Metadata["RejectionReason"]})");

                await context.EmitAsync("Rejected", item);
            }
        }
        catch (OperationCanceledException)
        {
            context.Log($"[SampleMultiPortNode] Operación cancelada durante la ejecución.", LogLevel.Warning);
            throw;
        }
        catch (Exception ex)
        {
            context.Log($"[SampleMultiPortNode] Error en ejecución: {ex.Message}", LogLevel.Error);
            item.AddLog($"SampleMultiPortNode falló con error: {ex.Message}");

            // Emitir elemento al puerto de Error para permitir manejo controlado en la tubería
            await context.EmitAsync("Error", item);
        }
    }
}
