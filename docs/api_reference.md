# Referencia de API y Módulos - FileFlow Studio

Este documento detalla los contratos base, firmas de métodos públicas, modelos de datos y motores principales expuestos por la capa SDK (`FileFlow.Sdk`) y el motor de ejecución (`FileFlow.Core`).

---

## 1. Contratos Base de la Capa SDK (`FileFlow.Sdk`)

### 1.1 `FileItemContext`
Representa la unidad de datos inmutable/transmutable que fluye a través del grafo durante la ejecución de los nodos.

```csharp
public record FileItemContext
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string CurrentPath { get; set; }
    public string OriginalPath { get; init; }
    public bool IsDirectory { get; set; }
    public long FileSizeBytes { get; set; }
    public Dictionary<string, object?> Metadata { get; }
    public HashSet<string> Tags { get; }
    public List<string> ExecutionLog { get; }

    public FileItemContext(string path, bool isDirectory = false);
    public FileItemContext DeepClone();
    public void AddLog(string message);
}
```
- **Parámetros Clave:**
  - `CurrentPath`: Ruta física actual del archivo o carpeta en el disco.
  - `OriginalPath`: Ruta original del archivo al ser capturado en el puerto de origen.
  - `Metadata`: Diccionario insensible a mayúsculas/minúsculas para almacenar metadatos inyectados (`{Hash:SHA256}`, `{ImageWidth}`, `{UnpackedFileCount}`, etc.).

---

### 1.2 `IFlowNode`
Contrato fundamental que deben implementar todas las clases de nodos de procesamiento.

```csharp
public interface IFlowNode
{
    string Id { get; set; }
    string Name { get; }
    string Category { get; }
    string Description { get; }
    IReadOnlyList<NodePort> Inputs { get; }
    IReadOnlyList<NodePort> Outputs { get; }
    Dictionary<string, object?> Parameters { get; }

    Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken);
}
```

#### Atributo `[NodeDefinition]`
Decorador obligatorio para que el `PluginLoader` descubra dinámicamente el nodo:
```csharp
[NodeDefinition(string nameResourceKey, string category, string descriptionResourceKey)]
```

---

### 1.3 `IFlowExecutionContext`
Interfaz del contexto de ejecución inyectado al nodo para interactuar con el motor orquestador.

```csharp
public interface IFlowExecutionContext
{
    bool IsDryRun { get; }
    Task EmitAsync(string outputPortName, FileItemContext item);
    void ReportProgress(double percentage, string statusMessage);
    void Log(string message, LogLevel level);
    void RegisterPlannedAction(PlannedAction action);
    void RecordJournalEntry(JournalEntry entry);
}
```
- **`EmitAsync(port, item)`:** Emite el elemento procesado hacia el puerto de salida especificado, activando los nodos conectados aguas abajo.
- **`RegisterPlannedAction`:** Registra acciones virtuales cuando `IsDryRun == true`.
- **`RecordJournalEntry`:** Registra una transacción atómica con delegado inverso para rollback.

---

### 1.4 `NodePort`
Representa un punto de conexión (puerto de entrada o salida) en la tarjeta del nodo.

```csharp
public record NodePort(
    string Name,
    Type DataType,
    PortDirection Direction,
    string DisplayName,
    string? Description = null);

public enum PortDirection
{
    Input,
    Output
}
```

---

## 2. Motor de Plantillas de Variables (`VariableTemplateResolver`)

El motor de plantillas evalúa expresiones encerradas en llaves `{...}` sobre cualquier parámetro de texto o ruta de nodo.

```csharp
public static class VariableTemplateResolver
{
    public static string Resolve(string template, FileItemContext item, string? sourceRootPath = null);
    public static string GetVariableValue(string varName, FileItemContext item, string? sourceRootPath = null);
}
```

### 2.1 Variables del Sistema Estandarizadas
| Token | Descripción | Ejemplo de Valor |
|---|---|---|
| `{FileName}` | Nombre de archivo con extensión | `documento.pdf` |
| `{FileNameNoExt}` | Nombre de archivo sin extensión | `documento` |
| `{Extension}` | Extensión con punto | `.pdf` |
| `{CurrentPath}` | Ruta absoluta actual | `C:\Input\documento.pdf` |
| `{CurrentDir}` | Directorio contenedor | `C:\Input` |
| `{RelativePath}` | Ruta de subcarpetas relativa | `facturas/2026` |
| `{DateNow}` | Fecha actual (`yyyy-MM-dd`) | `2026-08-22` |
| `{TimeNow}` | Hora actual (`HH-mm-ss`) | `21:40:00` |
| `{DateTimeNow}` | Fecha y hora combinadas | `2026-08-22_21-40-00` |
| `{Counter}` / `{Index}` | Contador numérico de lote | `1`, `2`, `3` |
| `{SizeMB}` / `{SizeKB}` | Peso formateado | `14.5 MB` |
| `{UserName}` | Usuario de Windows activo | `Ricardo` |

### 2.2 Funciones de Expresión Soportadas
- **Transformación de Texto:**
  - `{Upper(text)}`: Convierte a mayúsculas.
  - `{Lower(text)}`: Convierte a minúsculas.
  - `{Trim(text)}`: Elimina espacios de los extremos.
  - `{Replace(text, "buscado", "reemplazo")}`: Reemplazo consciente de comillas.
  - `{Sanitize(text)}`: Reemplaza caracteres ilegales en Windows por `-`.
  - `{PadLeft(text, longitud, "carácter")}`: Relleno numérico (ej. `{PadLeft(Counter, 4, "0")}` $\rightarrow$ `0005`).
  - `{Coalesce(val1, val2, ...)}`: Retorna el primer valor no vacío.
- **Expresiones Regulares (con protección ReDoS de 1 segundo):**
  - `{RegexMatch(text, "patrón")}`
  - `{RegexReplace(text, "patrón", "reemplazo")}`
- **Fechas y Tiempos:**
  - `{Year(fecha)}`, `{Month(fecha)}`, `{Day(fecha)}`
  - `{FormatDate(fecha, "yyyy-MM")}`
  - `{FileAgeDays(fecha)}`: Antigüedad calculada en días UTC.

---

## 3. Capa de Ejecución (`FileFlow.Core`)

### 3.1 `WorkflowExecutor`
Orquestador principal que ejecuta la topología del grafo.

```csharp
public class WorkflowExecutor
{
    public int MaxDegreeOfParallelism { get; set; }
    public string GlobalOutputDir { get; set; }
    public bool IsDryRun { get; set; }
    public bool IsPaused { get; }

    public event Action<double, string>? ProgressChanged;
    public event Action<string, LogLevel>? LogEmitted;
    public event Action<string, NodeExecutionStatus>? NodeStatusChanged;

    public void Pause();
    public void Resume();
    public Task ExecuteAsync(WorkflowGraph graph, PluginLoader loader, CancellationToken cancellationToken);
}
```

---

## 4. Ejemplo Práctico: Creación de un Nodo Personalizado

A continuación se muestra un ejemplo completo de cómo crear un nodo personalizado implementando `IFlowNode`:

```csharp
using FileFlow.Sdk;

namespace MyCustomPlugin;

[NodeDefinition("MiNodoFiltro_Name", "FileSystem", "MiNodoFiltro_Desc")]
public class CustomFilterNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => "Filtro Personalizado";
    public string Category => "FileSystem";
    public string Description => "Filtra archivos que superen un tamaño en Megabytes.";

    public IReadOnlyList<NodePort> Inputs { get; } = new[]
    {
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "Entrada")
    };

    public IReadOnlyList<NodePort> Outputs { get; } = new[]
    {
        new NodePort("Passed", typeof(FileItemContext), PortDirection.Output, "Aprobado"),
        new NodePort("Rejected", typeof(FileItemContext), PortDirection.Output, "Rechazado")
    };

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MaxMB"] = 50.0
    };

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        double maxMb = Parameters.TryGetValue("MaxMB", out var val) ? Convert.ToDouble(val) : 50.0;
        double fileSizeMb = item.FileSizeBytes / (1024.0 * 1024.0);

        if (fileSizeMb <= maxMb)
        {
            context.Log($"[CustomFilterNode] Archivo '{item.CurrentPath}' aprobado ({fileSizeMb:F2} MB).", LogLevel.Information);
            await context.EmitAsync("Passed", item);
        }
        else
        {
            context.Log($"[CustomFilterNode] Archivo '{item.CurrentPath}' rechazado por superar {maxMb} MB.", LogLevel.Warning);
            await context.EmitAsync("Rejected", item);
        }
    }
}
```
