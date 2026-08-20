# Arquitectura del Sistema - FileFlow

## 1. Patrón Microkernel & Plugins
- **Carga de Ensamblados:** Uso de `AssemblyLoadContext` aislado (`PluginAssemblyLoadContext`) para cargar DLLs desde la carpeta `/Plugins` sin bloquear los archivos binarios en el disco de Windows.
- **Descubrimiento:** Reflexión sobre tipos que implementen `IFlowNode` y estén decorados con `[NodeDefinition(Name, Category, Description)]`.

## 2. Motor de Ejecución (DAG Engine)
- **Topología:** Validación de Grafo Dirigido Acíclico (detección de ciclos mediante algoritmo de Tarjan o Kahn).
- **Procesamiento Asíncrono:** Basado en `System.Threading.Channels` o `TPL Dataflow` (`TransformBlock`, `ActionBlock`, `BufferBlock`) con control de contrapresión (BoundedCapacity) para evitar saturación de memoria RAM.
- **Concurrencia:** Límite configurable de hilos simultáneos (`MaxDegreeOfParallelism`) para operaciones I/O bound vs CPU bound.

## 3. Modelo de Dominio
- **`FileItemContext`:** Entidad que viaja por el grafo. Transporta la ruta actual, ruta original, bandera de directorio, metadatos dinámicos (`Dictionary<string, object>`), tags y log de operaciones.
- **`NodePort`:** Define nombre, tipo de dato aceptado, dirección (Input/Output) y capacidad de conexión (Single / Multi-connection).