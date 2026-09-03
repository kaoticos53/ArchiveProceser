# Referencia de API y Contratos de SDK - FileFlow Studio

Esta guía documenta los contratos principales de `FileFlow.Sdk`, tipos de datos, firmas de métodos y cómo extender el sistema creando nodos personalizados.

---

## 1. Contratos Fundamentales del SDK

### 1.1. `IFlowNode`
Contrato primordial que debe implementar cualquier nodo que procese datos dentro del grafo de ejecución.

```csharp
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
    IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => Array.Empty<NodeParameterDescriptor>();
    IReadOnlyList<NodeActionDescriptor> CustomActions => Array.Empty<NodeActionDescriptor>();

    Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken);

    ValidationResult ValidateConfiguration() => ValidationResult.Success();
}
```

#### Métodos:
- **`ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)`**: Ejecuta la lógica asíncrona del nodo cuando recibe un elemento por un puerto específico. Debe propagar obligatoriamente el `CancellationToken`.
- **`ValidateConfiguration()`**: Valida que los parámetros requeridos (rutas, expresiones, patrones) estén correctamente formateados antes de iniciar el grafo.

---

### 1.2. `FileItemContext`
Representa el estado y metadatos de un archivo en tránsito a lo largo de los nodos del flujo de trabajo.

```csharp
namespace FileFlow.Sdk;

public class FileItemContext
{
    public Guid Id { get; }
    public string OriginalPath { get; set; }
    public string CurrentPath { get; set; }
    public long SizeBytes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Dictionary<string, object?> Variables { get; }
    public Dictionary<string, object?> Metadata { get; }

    // Accesores Memoizados de Ultra-Bajo Costo (Zero-Alloc Hot Paths)
    public string IdString { get; }
    public string ShortIdString { get; }
    public string FileName { get; }
    public string FileExtension { get; }

    public FileItemContext(string filePath);
    public FileItemContext Clone();
}
```

---

### 1.3. `IFlowExecutionContext`
Proporciona al nodo acceso al entorno de ejecución global, emisión de elementos y telemetría estructurada.

```csharp
namespace FileFlow.Sdk;

public interface IFlowExecutionContext
{
    CancellationToken CancellationToken { get; }
    bool IsDryRun { get; }
    string ExecutionId { get; }
    
    ValueTask EmitAsync(FileItemContext item, string outputPinName = "Output");
    void SetGlobalVariable(string key, object? value);
    bool TryGetGlobalVariable<T>(string key, out T? value);
    
    // Telemetría y Logging Estructurado
    void Log(
        LogLevel level, 
        string message, 
        string? nodeId = null, 
        string? nodeName = null, 
        string? filePath = null, 
        double durationMs = 0.0, 
        string? itemId = null, 
        long fileSizeBytes = 0, 
        string? detailsJson = null
    );
}
```

---

### 1.4. `StructuredLogRecord`
Registro inmutable de telemetría optimizado para serialización y visualización reactiva en UI.

```csharp
namespace FileFlow.Sdk.Telemetry;

public record StructuredLogRecord(
    long Id,
    string ExecutionId,
    DateTime Timestamp,
    LogLevel Level,
    string? NodeId,
    string? NodeName,
    string? ItemId,
    string? FilePath,
    string? FileName,
    long FileSizeBytes,
    double DurationMs,
    string Message,
    string? DetailsJson = null
)
{
    public bool HasDetails => !string.IsNullOrWhiteSpace(DetailsJson);
    public string ShortItemId => !string.IsNullOrWhiteSpace(ItemId) ? (ItemId.Length > 8 ? ItemId[..8] : ItemId) : string.Empty;
    public string FormattedTimestamp => $"[{Timestamp:HH:mm:ss.fff}]";
    public string FormattedFileSize => ...;
    public string BadgeText => ...;
}
```

---

## 2. Motor de Interpolación de Variables (`VariableTemplateResolver`)

Permite sustituir patrones dinámicos en rutas y nombres de archivos:

```csharp
namespace FileFlow.Sdk;

public static class VariableTemplateResolver
{
    public static string Resolve(string template, FileItemContext item, IFlowExecutionContext? context = null);
}
```

### Tabla de Variables Predefinidas:

| Variable | Descripción | Ejemplo de Salida |
|---|---|---|
| `{FileName}` | Nombre del archivo con extensión | `informe_anual.pdf` |
| `{FileNameWithoutExt}` | Nombre del archivo sin extensión | `informe_anual` |
| `{Ext}` | Extensión sin punto | `pdf` |
| `{Date:yyyy-MM-dd}` | Fecha actual formateada | `2026-08-23` |
| `{Time:HH-mm-ss}` | Hora actual formateada | `17-30-00` |
| `{SizeMB}` | Tamaño del archivo en megabytes | `14.50` |
| `{Hash:sha256}` | Hash SHA-256 (si fue calculado) | `e3b0c44298fc1c149afbf4c8996fb924...` |
| `{Var:MiVariable}` | Variable personalizada inyectada | `Contabilidad` |

---

## 3. Ejemplo Práctico: Creación de un Nodo Personalizado

Ejemplo completo de implementación de un nodo que valida la integridad de archivos de texto:

```csharp
using System.Diagnostics;
using System.Text.Json;
using FileFlow.Sdk;
using FileFlow.Sdk.Telemetry;

namespace FileFlow.Plugin.Custom;

public class TextFileValidatorNode : IFlowNode
{
    public string Id => "custom.text_validator";
    public string Name => "Validador de Texto";
    public string Category => "Validación";
    public string Description => "Comprueba que un archivo de texto no contenga caracteres nulos y cuenta líneas.";

    public int MaxAllowedLines { get; set; } = 10000;

    public IReadOnlyList<NodePinDefinition> Inputs { get; } = [
        new("Input", "Entrada", PinDataType.File)
    ];

    public IReadOnlyList<NodePinDefinition> Outputs { get; } = [
        new("Valid", "Válido", PinDataType.File),
        new("Invalid", "Inválido", PinDataType.File)
    ];

    public async ValueTask ExecuteAsync(FileItemContext item, IFlowExecutionContext context)
    {
        var sw = Stopwatch.StartNew();
        int lineCount = 0;
        bool hasNullChars = false;

        using (var reader = new StreamReader(item.CurrentPath))
        {
            string? line;
            while ((line = await reader.ReadLineAsync(context.CancellationToken).ConfigureAwait(false)) != null)
            {
                lineCount++;
                if (line.Contains('\0'))
                {
                    hasNullChars = true;
                    break;
                }
            }
        }

        sw.Stop();
        bool isValid = !hasNullChars && lineCount <= MaxAllowedLines;
        string targetPin = isValid ? "Valid" : "Invalid";

        var details = new
        {
            lineCount,
            maxAllowed = MaxAllowedLines,
            hasNullChars,
            status = isValid ? "Approved" : "Rejected"
        };

        context.Log(
            isValid ? LogLevel.Information : LogLevel.Warning,
            $"[Validador] Archivo {(isValid ? "aprobado" : "rechazado")}: {lineCount} líneas",
            nodeId: Id,
            nodeName: Name,
            filePath: item.CurrentPath,
            durationMs: sw.Elapsed.TotalMilliseconds,
            itemId: item.IdString,
            fileSizeBytes: item.SizeBytes,
            detailsJson: JsonSerializer.Serialize(details)
        );

        await context.EmitAsync(item, targetPin).ConfigureAwait(false);
    }

    public ValidationResult ValidateConfiguration()
    {
        if (MaxAllowedLines <= 0)
            return ValidationResult.Failure("MaxAllowedLines debe ser mayor a 0.");

        return ValidationResult.Success();
    }
}
```
