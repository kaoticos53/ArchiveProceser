# FileFlow Studio - Arquitectura y Funcionamiento Interno (De Principiante a Experto)

Este documento describe de forma exhaustiva el funcionamiento interno de **FileFlow Studio**, desde los conceptos de alto nivel hasta la mecánica profunda de su motor asíncrono, aislamiento de memoria y subsistema de interfaz gráfica.

---

## 🎯 Nivel 1: Conceptos Básicos y Visión General (Principiante)

### ¿Qué es FileFlow Studio?
FileFlow Studio es una plataforma visual basada en **nodos interconectados** para automatizar el procesamiento, conversión, organización y análisis de archivos y carpetas en Windows.

```
[Carpeta Origen] ───(Salida)───► (Entrada)─── [Optimizador de Imágenes] ───(Salida)───► (Entrada)─── [Carpeta Destino]
```

### Componentes Visuales del Flujo:
1. **Nodo**: Una unidad independiente de procesamiento (ej. *Descompresión Inteligente*, *Optimizador de Imágenes*).
2. **Puertos de Entrada (Inputs)**: Puntos por los que el nodo recibe los elementos a procesar.
3. **Puertos de Salida (Outputs)**: Puntos por los que el nodo emite los elementos procesados o filtrados (ej. *Salida*, *Aprobados*, *Rechazados*, *Error*).
4. **Conexiones (Wires)**: Tuberías virtuales que unen un puerto de salida con un puerto de entrada.
5. **Parámetros (Settings)**: Variables configurables dentro de cada nodo (rutas, resoluciones, estrategias de conflicto).
6. **Modo Prueba (Dry-Run)**: Simulación de ejecución en la que el motor recorre todo el gráfico emitiendo los eventos y clasificando los archivos sin modificar el disco real.

---

## 🏛 Nivel 2: Arquitectura del Sistema y Componentes (Intermedio)

FileFlow Studio se estructura en 4 capas estrictamente desacopladas:

```
┌────────────────────────────────────────────────────────┐
│                   FileFlow.App (WPF)                   │
│      (MVVM, Nodify Canvas, Estilos, Localización)      │
└──────────────────────────┬─────────────────────────────┘
                           │
┌──────────────────────────▼─────────────────────────────┐
│                 FileFlow.Core (Motor)                  │
│  (Grafo, Validaciones, WorkflowExecutor, PluginLoader) │
└──────────────────────────┬─────────────────────────────┘
                           │
┌──────────────────────────▼─────────────────────────────┐
│               FileFlow.Plugin.* (Plugins)              │
│    (FileSystem, Archives, Images, Plugins de Terceros) │
└──────────────────────────┬─────────────────────────────┘
                           │
┌──────────────────────────▼─────────────────────────────┐
│                   FileFlow.Sdk (Puro)                  │
│       (IFlowNode, FileItemContext, Localization)       │
└────────────────────────────────────────────────────────┘
```

### Contrato del Contexto de Elementos (`FileItemContext`)
Toda la información que fluye entre nodos viaja empaquetada en una instancia de `FileItemContext`:
- `CurrentPath`: Ruta actual del archivo o carpeta.
- `OriginalPath`: Ruta origen inmutable.
- `Metadata`: Diccionario dinámico de claves/valores (`DateTaken`, `CameraModel`, `UnpackedFrom`, etc.).
- `ExecutionLog`: Historial cronológico de modificaciones y pasos sufridos por el archivo.

---

## ⚡ Nivel 3: Mecanismos Internos y Motor de Ejecución (Avanzado)

### 1. Carga Dinámica de Plugins e Aislamiento de Memoria (`PluginAssemblyLoadContext`)
Para permitir añadir o compilar nuevos nodos sin reiniciar ni bloquear archivos en disco:
- Cada ensamblado de plugin `.dll` se carga mediante un `PluginAssemblyLoadContext` personalizado derivado de `AssemblyLoadContext` de .NET.
- **Lectura sin Bloqueo de Disco:** El archivo `.dll` se lee en un búfer de memoria (`byte[]`) y se pasa a `LoadFromStream(...)`. De este modo, Windows no mantiene bloqueados los archivos en disco.
- **Ensamblados Compartidos:** Las referencias a `FileFlow.Sdk` y `FileFlow.Core` se redirigen al contexto de carga por defecto (`AssemblyLoadContext.Default`), garantizando que la interfaz `IFlowNode` sea exactamente el mismo tipo en memoria.

### 2. Validación de Grafos y Topología (`GraphValidator`)
Antes de iniciar la ejecución, el motor valida el grafo del flujo:
- **Detección de Bucles Infinitos:** Implementa el algoritmo de ordenación topológica de Kahn para comprobar que el grafo es un **Grafo Acíclico Dirigido (DAG)**.
- **Validación de Puertos:** Verifica que los puertos conectados sean compatibles y que no existan conexiones salérrimas o huérfanas.

### 3. Motor de Ejecución Asíncrona (`WorkflowExecutor`)
El motor de ejecución procesa los elementos mediante tuberías en paralelo:
- **Paralelismo Controlado:** Configura el grado máximo de paralelismo (`MaxDegreeOfParallelism`) según el número de núcleos de la CPU.
- **Pausa y Reanudación:** Utiliza primitivas de sincronización asíncronas (`SemaphoreSlim` / `TaskCompletionSource`) para detener o continuar la tubería sin bloquear los hilos principales.
- **Propagación de Cancelación:** Todos los nodos reciben y propagan estrictamente `CancellationToken` para detener inmediatamente la ejecución si el usuario pulsa **Detener**.

---

## 🔬 Nivel 4: Subsistema de UI, Renderizado y Extensibilidad (Experto)

### 1. Renderizado Dinámico y Estiramiento de Nodos en WPF
- **Estiramiento Horizontal 100%:** Las plantillas de estilo `BlenderNodeStyle` aplican `HorizontalAlignment="Stretch"` y `HorizontalContentAlignment="Stretch"` al contenedor `nodify:Node` y a los contenedores internos, garantizando que el borde visual ocupe el 100% del cuadro de selección sin espacios vacíos.
- **Límite de Crecimiento Máximo (`MaxWidth`):** La propiedad `MaxWidth` de `ItemContainerStyle` se enlaza directamente a `NodeViewModel.MaxWidth` (`600px`), impidiendo que el tirador de redimensionamiento crezca indefinidamente más allá de la tarjeta gráfica.
- **Memorización Dual de Tamaño (`CollapsedWidth` vs `ExpandedWidth`):** Cada nodo recuerda su anchura preferida tanto en estado replegado como desplegado. Al alternar el botón de parámetros (`⚙`), conmuta automáticamente entre ambas dimensiones.

### 2. Diálogo de Color Nativo Win32 (Sin WinForms)
Para evitar conflictos de espacio de nombres entre `System.Windows.Media.Color` y `System.Drawing.Color`:
- La selección de colores personalizados invoca directamente a la API nativa de Windows `comdlg32.dll` mediante P/Invoke (`ChooseColor`).
- El color elegido genera automáticamente un tono oscuro calculado (multiplicación por 0.25 en componentes RGB) para mantener la coherencia estética del encabezado.

### 3. Localización Dinámica Multilingüe Sin Reinicio (`LocalizationManager`)
- La clase `LocalizationManager` expone un indexador C# (`this[string key]`) y es `INotifyPropertyChanged`.
- En XAML, los controles se enlazan dinámicamente:
  `{Binding Source={x:Static loc:LocalizationManager.Instance}, Path=[NombreClave]}`
- Al alternar el selector desplegable de la barra superior (🌐 **Español** / 🇬🇧 **English**), `LocalizationManager` notifica el cambio de cultura (`LanguageChanged`), provocando que **la barra de herramientas, el catálogo lateral, los logs y todos los nodos en pantalla actualicen sus textos al instante**.
