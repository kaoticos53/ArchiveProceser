# Guía para la Creación de Nodos Personalizados

Esta guía explica en detalle cómo desarrollar nuevos nodos de procesamiento para **FileFlow Studio** utilizando **C# 13** y **.NET 9**.

---

## 📋 Requisitos Previos

Para crear un nodo de procesamiento, tu proyecto solo necesita referenciar el paquete/proyecto **`FileFlow.Sdk`**. No requiere dependencias de UI (WPF) ni de librerías de interfaz gráfica.

---

## 🛠 Pasos para Crear un Nodo

### 1. Definir la Clase e Implementar `IFlowNode`

Crea una clase pública que implemente la interfaz `IFlowNode` y decórala con el atributo `[NodeDefinition]`:

```csharp
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace MiCompañia.FileFlow.Plugin.Custom;

[NodeDefinition("SampleMultiPortNode_Name", "General", "SampleMultiPortNode_Desc")]
public class SampleMultiPortNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    // Nombre dinámico localizado o por defecto
    public string Name => LocalizationManager.Instance.GetString("SampleMultiPortNode_Name", "Filtro Multi-Puerto");

    // Categoría del nodo (ej. FileSystem, Images, Archives, General, Utility)
    public string Category => "General";

    // Descripción explicativa para la tarjeta del nodo
    public string Description => LocalizationManager.Instance.GetString("SampleMultiPortNode_Desc", "Filtra elementos por tamaño y extensión emitiéndolos por distintos puertos.");
    
    // ...
}
```

---

### 2. Definir los Puertos de Entrada (`Inputs`) y Salida (`Outputs`)

Los puertos permiten conectar nodos entre sí. Cada puerto especifica su nombre, el tipo de datos transportado (`typeof(FileItemContext)`), la dirección y su etiqueta legible:

```csharp
public IReadOnlyList<NodePort> Inputs { get; } = new[]
{
    new NodePort("MainInput", typeof(FileItemContext), PortDirection.Input, "Entrada Principal"),
    new NodePort("SecondaryInput", typeof(FileItemContext), PortDirection.Input, "Entrada Secundaria")
};

public IReadOnlyList<NodePort> Outputs { get; } = new[]
{
    new NodePort("Approved", typeof(FileItemContext), PortDirection.Output, "Aprobados"),
    new NodePort("Rejected", typeof(FileItemContext), PortDirection.Output, "Rechazados"),
    new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
};
```

---

### 3. Definir los Parámetros Configurables (`Parameters`)

Los parámetros se exponen automáticamente en la tarjeta gráfica del nodo en WPF. El tipo de valor determina la interfaz generada (casilla para `bool`, selector de archivos para rutas, o campo numérico/texto):

```csharp
public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
{
    ["MaxFileSizeMB"] = 50,
    ["AllowedExtension"] = ".png",
    ["EnableStrictValidation"] = true,
    ["TargetFolderPath"] = @"C:\Procesados"
};
```

---

### 4. Implementar el Método de Ejecución `ExecuteAsync`

El método `ExecuteAsync` contiene la lógica de procesamiento. Se invoca cada vez que un elemento llega a uno de los puertos de entrada del nodo:

```csharp
public async Task ExecuteAsync(
    string inputPortName,
    FileItemContext item,
    IFlowExecutionContext context,
    CancellationToken cancellationToken)
{
    // 1. Obtener parámetros
    int maxMb = Parameters.TryGetValue("MaxFileSizeMB", out var mb) ? Convert.ToInt32(mb) : 50;
    string ext = Parameters.TryGetValue("AllowedExtension", out var ex) ? ex?.ToString() ?? ".png" : ".png";

    // 2. Registrar progreso o logs en la consola de la app
    context.Log($"Procesando '{item.CurrentPath}' desde puerto '{inputPortName}'...", LogLevel.Information);

    // 3. Evaluar lógica y emitir por los puertos de salida apropiados
    if (item.FileSizeBytes > maxMb * 1024 * 1024)
    {
        item.AddLog($"Rechazado por exceder el tamaño máximo de {maxMb} MB");
        await context.EmitAsync("Rejected", item);
    }
    else
    {
        item.AddLog("Aprobado exitosamente");
        await context.EmitAsync("Approved", item);
    }
}
```

---

## 📦 Despliegue del Nodo

Para que FileFlow Studio cargue tu nuevo nodo:

1. Compila tu proyecto como librería de clases (`.dll`).
2. Copia el archivo `.dll` resultante a la carpeta `Plugins/` del directorio ejecutable de la aplicación.
3. Al iniciar FileFlow Studio, el catálogo detectará e incorporará el nodo automáticamente.

Ver un código completo de ejemplo en: **[`docs/nodes/examples/SampleMultiPortNode.cs`](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/docs/nodes/examples/SampleMultiPortNode.cs)**.
