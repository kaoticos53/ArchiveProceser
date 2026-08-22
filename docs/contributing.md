# Guía de Contribución y Estándares de Código - FileFlow Studio

¡Gracias por tu interés en contribuir a **FileFlow Studio**! Este documento establece las pautas, estándares de codificación y el flujo de trabajo con Git para colaborar en el desarrollo del repositorio de forma ordenada y profesional.

---

## 1. Principios de Ingeniería y Versiones

1. **Target Framework & Runtime:**
   - Todo el proyecto utiliza **.NET 9** (`net9.0` para bibliotecas de clases y `net9.0-windows` para WPF).
   - **Versión de Lenguaje:** **C# 13** (`<LangVersion>13</LangVersion>`).
2. **Nullability Estricta:**
   - La opción `<Nullable>enable</Nullable>` está activada en todos los proyectos `.csproj`. No se permiten advertencias de referencias nulas desatendidas.
3. **Primitivas Modernas de .NET 9:**
   - Se debe utilizar la nueva clase `System.Threading.Lock` en lugar de objetos genéricos `new object()` para bloques de sincronización (`lock (_lock)`).
4. **Desacoplamiento Estricto:**
   - `FileFlow.Sdk` debe permanecer puro: cero dependencias externas o de UI.
   - Los nodos en `FileFlow.Plugin.*` dependen únicamente de `FileFlow.Sdk` y sus respectivas librerías de dominio.
5. **E/S Asíncrona:**
   - Todas las operaciones de disco, red o subprocesos deben ser 100% asíncronas (`Task` / `ValueTask`) con propagación explícita del parámetro `CancellationToken cancellationToken`.

---

## 2. Convenciones de Código y Formateo

- **Indentación:** 4 espacios (no usar tabuladores).
- **Estilo de Nombres:**
  - `PascalCase` para nombres de clases, métodos, propiedades, eventos y espacios de nombres.
  - `camelCase` para variables locales y parámetros de métodos.
  - `_camelCase` para campos privados de instancia.
  - `UPPER_CASE` o `PascalCase` para constantes.
- **Formateo Automático:** Se recomienda ejecutar `dotnet format` antes de enviar cualquier Pull Request:
  ```powershell
  dotnet format FileFlow.slnx
  ```

---

## 3. Flujo de Trabajo con Git (Git Workflow)

### 3.1 Estructura de Ramas
- `main`: Rama de producción estable. Cero commits directos.
- `develop`: Rama de integración de características.
- `feature/nombre-caracteristica`: Ramas temporales para nuevas funcionalidades.
- `fix/descripcion-error`: Ramas temporales para correcciones de errores.

### 3.2 Mensajes de Commit Semánticos
Los mensajes de commit deben seguir el estándar [Conventional Commits](https://www.conventionalcommits.org/):

- `feat: añadir nuevo nodo de conversión PDF`
- `fix: corregir interbloqueo en WorkflowExecutor al pausar ejecuciones`
- `docs: actualizar manual de usuario con guía de presets`
- `refactor: extraer evaluadores de expresiones a la capa SDK`
- `test: añadir pruebas de estrés para BatchBufferNode`

---

## 4. Guía para Desarrollar un Nuevo Nodo Personalizado

1. **Ubicación:** Crea tu clase dentro del proyecto de plugin correspondiente (`FileFlow.Plugin.FileSystem`, `FileFlow.Plugin.Archives`, etc.).
2. **Atributo Obligatorio:** Decora la clase con `[NodeDefinition("ClaveNombre", "Categoria", "ClaveDescripcion")]`.
3. **Implementación:** Implementa la interfaz `IFlowNode`.
4. **Respetar Dry Run:** Si `context.IsDryRun == true`, registra la acción planeada (`context.RegisterPlannedAction`) y **no ejecutes modificaciones físicas en el disco**.
5. **Manejo de Excepciones:** Encapsula la lógica dentro de un bloque `try-catch` enviando el contexto al puerto `"Error"` o `"Failed"` en caso de fallo.

### Ejemplo Mínimo de Estructura de Nodo:
```csharp
using FileFlow.Sdk;

namespace FileFlow.Plugin.FileSystem;

[NodeDefinition("MyNode_Name", "FileSystem", "MyNode_Desc")]
public class MyCustomNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => "Mi Nodo";
    public string Category => "FileSystem";
    public string Description => "Descripción de mi nodo.";

    public IReadOnlyList<NodePort> Inputs { get; } = new[]
    {
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "Entrada")
    };

    public IReadOnlyList<NodePort> Outputs { get; } = new[]
    {
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Salida"),
        new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
    };

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MyParam"] = "ValorPorDefecto"
    };

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            // Lógica de procesamiento
            await context.EmitAsync("Out", item);
        }
        catch (Exception ex)
        {
            context.Log($"Error en MyCustomNode: {ex.Message}", LogLevel.Error);
            await context.EmitAsync("Error", item);
        }
    }
}
```

---

## 5. Batería de Pruebas Automatizadas

Antes de enviar un Pull Request, es **obligatorio** verificar que la totalidad de las pruebas unitarias e integración pasen al 100%:

```powershell
dotnet test FileFlow.slnx --configuration Release
```

### 5.1 Reglas para Nuevos Tests
- Todos los tests deben estar ubicados dentro del proyecto `FileFlow.Tests`.
- Estructura AAA (*Arrange, Act, Assert*).
- Uso de **xUnit**, **FluentAssertions** y **Moq**.
- Evitar operaciones bloqueantes en métodos de prueba (`.Result` o `.GetAwaiter().GetResult()`). Utilizar métodos `async Task`.
