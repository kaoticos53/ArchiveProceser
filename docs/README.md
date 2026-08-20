# FileFlow Studio - Documentación Técnica y Arquitectura

Bienvenido a la documentación oficial de **FileFlow Studio**, un motor de procesamiento de archivos modular y entorno gráfico basado en **.NET 9** y **WPF**.

---

## 📁 Estructura de la Documentación

- **[Arquitectura y Funcionamiento de Principiante a Experto (`docs/ARCHITECTURE_DEEP_DIVE.md`)](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/docs/ARCHITECTURE_DEEP_DIVE.md)**: Explicación completa del funcionamiento interno en 4 niveles (visión general, componentes, motor asíncrono y subsistema UI).
- **[Guía de Creación de Nodos (`docs/nodes/CREATING_NODES.md`)](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/docs/nodes/CREATING_NODES.md)**: Manual paso a paso para desarrolladores sobre cómo crear, compilar y distribuir nuevos nodos de procesamiento.
- **[Ejemplo Completo de Nodo (`docs/nodes/examples/SampleMultiPortNode.cs`)](file:///e:/Users/kaoti/Documentos/GitHub/ArchiveProceser/docs/nodes/examples/SampleMultiPortNode.cs)**: Código fuente de ejemplo de un nodo con múltiples entradas, múltiples salidas, parámetros personalizables y manejo de errores.

---

## 🏗 Arquitectura del Proyecto

FileFlow Studio está diseñado siguiendo el principio de **desacoplamiento estricto**:

```
FileFlow.slnx
 ├── FileFlow.Sdk              -> Librería pura de contratos, interfaces (IFlowNode) y tipos base (.NET 9)
 ├── FileFlow.Core             -> Motor de ejecución asíncrono en paralelo y cargador dinámico de plugins
 ├── FileFlow.Plugin.FileSystem -> Plugin modular de operaciones de sistema de archivos
 ├── FileFlow.Plugin.Archives   -> Plugin modular de compresión y descompresión de archivos (SharpCompress)
 ├── FileFlow.Plugin.Images     -> Plugin modular de procesamiento de imágenes y EXIF (ImageSharp)
 └── FileFlow.App              -> Aplicación principal WPF (.NET 9-windows, MVVM, Nodify)
```

---

## 🚀 Características Clave para Desarrolladores

1. **Descubrimiento de Plugins:** El cargador `PluginLoader` descubre automáticamente cualquier ensamblado `.dll` ubicado en la carpeta `/Plugins` que implemente la interfaz `IFlowNode`.
2. **Localización Multilingüe:** Todos los nombres y descripciones de los nodos admiten traducción dinámica en tiempo real mediante `LocalizationManager.Instance.GetString(...)`.
3. **Control de Flujo Asíncrono:** La ejecución de nodos se realiza de manera 100% asíncrona (`Task` / `ValueTask`) con soporte para `CancellationToken` y ejecución en paralelo por tuberías.
