# Referencia de API y Contratos de SDK - FileFlow Studio

Esta guía documenta los contratos principales de `FileFlow.Sdk`, tipos de datos, firmas de métodos y cómo extender el sistema creando nodos personalizados.

---

## 1. Contratos Fundamentales del SDK

### 1.1. `IFlowNode`
Contrato primordial que implementa cualquier nodo que procese datos dentro del grafo de ejecución. **Los nodos
del producto no lo implementan a mano**: derivan de [`FlowNodeBase`](../../FileFlow.Sdk/FlowNodeBase.cs) (o
de `AiFlowNodeBase` en el plugin de IA), que ya aporta `Id`, el diccionario `Parameters`, los puertos
(`Inputs`/`Outputs` con `protected set`), `MaxConcurrency` y los ayudantes `GetParameter<T>`, `SetParameter`,
`EmitAsync`, `Log` y `GetLocalizedString`. Implementarlo directamente sólo se justifica en dobles de prueba; la
guía de nodos explica el porqué en
[**Pasos para Crear un Nodo**](nodes/CREATING_NODES.md#-pasos-para-crear-un-nodo).

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

    /// <summary>Concurrencia máxima simultánea del nodo; ≤ 0 usa la del flujo.</summary>
    int MaxConcurrency => 0;

    Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken);

    /// <summary>Hook para nodos acumuladores, al terminar la ejecución aguas arriba.</summary>
    Task OnWorkflowCompletedAsync(IFlowExecutionContext context, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
```

#### Métodos:
- **`ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)`**: Ejecuta la lógica asíncrona del nodo cuando recibe un elemento por un puerto específico. Debe propagar obligatoriamente el `CancellationToken`.
- **`OnWorkflowCompletedAsync(IFlowExecutionContext context, CancellationToken cancellationToken)`**: Lo invoca el motor cuando todos los elementos aguas arriba terminaron, para que un nodo acumulador (un informe consolidado, un archivador por lotes) emita sus resultados finales.
- **`MaxConcurrency`**: Serializa el nodo cuando accede a un recurso exclusivo (GPU local, API con límite estricto) devolviendo `1`.

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

Ejemplo de un nodo que valida la integridad de archivos de texto, en la forma vigente: **hereda de
`FlowNodeBase`**, declara sus puertos en el constructor, lee sus parámetros con `GetParameter<T>` y registra
con `Log(...)` y `EmitAsync(...)`. Es el mismo patrón que siguen los nodos de los plugins, y el ciclo completo
—puertos dinámicos, estado de diseño, anuncio de topología, despliegue— está en
[**Creación de Nodos**](nodes/CREATING_NODES.md).

```csharp
using System.Diagnostics;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.Custom;

[NodeDefinition("TextFileValidatorNode_Name", "Validación", "TextFileValidatorNode_Desc", PipelineRole.Filter,
    "validador", "texto", "integridad")]
public sealed class TextFileValidatorNode : FlowNodeBase
{
    public override string Name =>
        LocalizationManager.Instance.GetString("TextFileValidatorNode_Name", "Validador de Texto");

    public override string Category => "Validación";

    public override string Description => LocalizationManager.Instance.GetString(
        "TextFileValidatorNode_Desc",
        "Comprueba que un archivo de texto no contenga caracteres nulos y cuenta sus líneas.");

    public TextFileValidatorNode()
    {
        Inputs = [new NodePort("In", typeof(FileItemContext), PortDirection.Input, "Entrada")];
        Outputs =
        [
            new NodePort("Valid", typeof(FileItemContext), PortDirection.Output, "Válido"),
            new NodePort("Invalid", typeof(FileItemContext), PortDirection.Output, "Inválido")
        ];

        Parameters["MaxAllowedLines"] = 10000;
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("MaxAllowedLines", ParameterEditorType.Number, DefaultValue: 10000, DisplayOrder: 1,
            Min: 1, Step: 1, HelpText: "Número máximo de líneas admitido.")
    ];

    public override async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        int maxAllowedLines = GetParameter("MaxAllowedLines", 10000);
        var sw = Stopwatch.StartNew();
        int lineCount = 0;
        bool hasNullChars = false;

        using (var reader = new StreamReader(item.CurrentPath))
        {
            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
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
        bool isValid = !hasNullChars && lineCount <= maxAllowedLines;

        // El registro estructurado lo arma la base: nodo, duración, elemento y el delta del flujo. Un nodo
        // sólo aporta el mensaje, la severidad y, si quiere detalle, un objeto serializable.
        Log(context,
            $"[Validador] Archivo {(isValid ? "aprobado" : "rechazado")}: {lineCount} líneas",
            isValid ? LogLevel.Information : LogLevel.Warning,
            item,
            new { lineCount, maxAllowed = maxAllowedLines, hasNullChars, elapsedMs = sw.Elapsed.TotalMilliseconds });

        // Los errores esperables salen por un puerto, no como excepción: el flujo decide qué hacer con ellos.
        await EmitAsync(context, item, isValid ? "Valid" : "Invalid");
    }
}
```

> Los parámetros se leen con `GetParameter<T>`, que entiende el valor tal como llegue —de la interfaz, de un
> perfil guardado (`JsonElement`) o embebido en texto (`"50%"` → 50)— y devuelve el valor por defecto si el
> valor está fuera de rango.

---

## 4. El Archivo de Flujo: Versión, Reparación y Convergencia

Un flujo se guarda como JSON y el archivo declara la versión del formato con la que está escrito
(`"schema": "FileFlow.Workflow.v2"`). La aplicación y el CLI comparten **el mismo** lector y **el mismo**
escritor, así que el mismo grafo produce el mismo texto desde cualquier puerta.

| Miembro | Qué hace |
|---|---|
| `WorkflowGraph.Schema` | Versión con la que está escrito el grafo, o con la que se leyó. `null` sólo lo tiene un grafo construido en memoria y todavía sin escribir. |
| `WorkflowGraph.SerializationOptions` | La **definición única** del formato: camelCase, lector tolerante (acepta los nombres en cualquier caja), tipos inferidos y sin escapar acentos. |
| `WorkflowGraph.ToJson()` / `FromJson(json)` | Escribir y leer un flujo; `ToJson` declara la versión que escribe. |
| `WorkflowFormat.CurrentVersion` / `CurrentSchema` | Versión vigente (`2`) y su nombre. |
| `WorkflowFormat.UndeclaredVersion` / `UndeclaredSchema` | El formato anterior al versionado (`1`, `FileFlow.Workflow.v1`), que es lo que se deduce de un archivo que no declara versión. |
| `WorkflowFormat.VersionOf(graph \| schema)` | Qué versión declara un grafo o un valor de `schema`. Un valor ausente o ilegible se lee como no declarado. |
| `WorkflowFormat.Plan(graph)` | Lo que hay que reparar de un archivo anterior, deducido **sólo del propio archivo**. |
| `WorkflowFormat.DeclareRepaired(graph)` | La reparación ya se aplicó: el grafo pasa a declarar la versión actual. No baja versiones. |
| `WorkflowFormat.IsFromNewerFormat(graph)` | Lo escribió una versión posterior: se puede leer y **no** se puede sobrescribir. |

Tres reglas que conviene tener presentes:

1. **Ausencia de versión = versión 1**, la que se repara. A un archivo que no dice su versión no se le puede
   inventar otra: el formato sin versionar es el único que pudo escribirlo.
2. **El que escribe no declara; el que repara declara.** `WorkflowFormat.DeclareCurrent` no pisa una versión ya
   declarada, así que un archivo anterior que nadie reparó se guarda como lo que era —y su reparación sigue
   pendiente para el que lo abra—; `DeclareRepaired` es lo que hace que un archivo reparado **converja** y deje
   de repararse en cada apertura.
3. **Un archivo de una versión posterior no se sobrescribe**: `WorkflowStorageService.SaveWorkflowAsync` lo
   rechaza con `InvalidDataException` y la salida honesta es guardar en otra ruta.

El ciclo completo, con el porqué de cada decisión y los límites declarados, está en
[**El archivo de flujo**](architecture.md#el-archivo-de-flujo-formato-versión-y-reparación), en
[**Creación de Nodos**](nodes/CREATING_NODES.md) y en el [registro de fases por fecha](history/2026-08_phase1_audit_plan.md).
