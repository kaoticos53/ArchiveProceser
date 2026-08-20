# Secuencia de Prompts para Antigravity (.NET 9 & C# 13)

---
### Prompt 1: Creación del SDK y Contratos Base (.NET 9)
"Actúa como un arquitecto senior de .NET. Lee '.antigravity/rules.md' y '.antigravity/architecture.md'.
Genera el proyecto 'FileFlow.Sdk' configurado con TargetFramework 'net9.0' y C# 13:
1. `FileItemContext`: `record` con Id (Guid), CurrentPath, OriginalPath, IsDirectory, FileSizeBytes, Metadata (`Dictionary<string, object>`), Tags (`HashSet<string>`) y ExecutionLog.
2. `NodePort`: Definición de puertos tipados con dirección (Input/Output).
3. `IFlowNode` y `IFlowExecutionContext`: Contratos para ejecución asíncrona (`ValueTask`/`Task`), reporte de progreso con IProgress y emisión de datos por puertos.
4. Atributos de metadatos para descubrimiento de plugins (`[NodeDefinition]`, `[PortDefinition]`)."

---
### Prompt 2: Motor de Ejecución y Plugins Dinámicos (.NET 9)
"Lee '.antigravity/architecture.md' y el proyecto 'FileFlow.Sdk' generado.
Genera el proyecto 'FileFlow.Core' (net9.0) con:
1. `PluginLoader`: Cargador dinámico usando `AssemblyLoadContext` aislado para escanear y cargar plugins sin bloquear archivos DLL en Windows.
2. `WorkflowGraph`: Modelo de grafo (Nodos y Conexiones) con validación de ciclos (DAG) mediante ordenación topológica y validación de tipos de puertos.
3. `WorkflowExecutor`: Motor asíncrono con control de concurrencia usando `System.Threading.Lock` y `System.Threading.Channels` / `TPL Dataflow`, con soporte para pausar, cancelar (`CancellationToken`) y reporte de eventos en tiempo real."

---
### Prompt 3: Implementación de Módulos de Plugins (.NET 9)
"Basándote en '.antigravity/nodes_catalog.md' y referenciando exclusivamente 'FileFlow.Sdk' en net9.0:
1. Crea el proyecto 'FileFlow.Plugin.FileSystem' con los nodos FolderSource, DirectoryInspector, OriginalFileAction y DestinationSink.
2. Crea el proyecto 'FileFlow.Plugin.Archives' con el nodo SmartUnpack utilizando SharpCompress.
3. Crea el proyecto 'FileFlow.Plugin.Images' con ExifMetadataNode e ImageOptimizerNode utilizando MetadataExtractor y SixLabors.ImageSharp."

---
### Prompt 4: Interfaz de Usuario WPF con Nodify (.NET 9)
"Lee la especificación general e implementa el proyecto 'FileFlow.App' con TargetFramework 'net9.0-windows' y C# 13:
1. Integra la librería 'Nodify' y 'CommunityToolkit.Mvvm'.
2. Diseña la vista del editor de nodos con canvas interactivo (soporte para zoom, desplazamiento, conexión visual de puertos y selección múltiple).
3. Implementa el ViewModel principal que cargue los nodos dinámicamente desde el PluginLoader en una paleta lateral arrastrable (Drag & Drop).
4. Agrega barra de herramientas para Guardar/Cargar flujos en JSON (System.Text.Json con soporte para polimorfismo en .NET 9) y botones de Ejecutar/Pausar conectados a WorkflowExecutor."