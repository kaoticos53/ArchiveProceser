# Manual de Usuario y Operación - FileFlow Studio

Bienvenido a la guía oficial de **FileFlow Studio**, la herramienta visual para diseñar, automatizar y ejecutar flujos de procesamiento de archivos por lotes sin necesidad de escribir código complejo.

---

## 1. Conceptos Básicos

Un flujo de trabajo en FileFlow Studio se compone de:
- **Nodos**: Bloques funcionales individuales que realizan una acción específica (leer carpetas, descomprimir, renombrar, optimizar imágenes, calcular hashes, filtrar o mover archivos).
- **Pines y Conexiones**: Los puertos de entrada (azules/verdes a la izquierda) y salida (a la derecha). Conectar un pin de salida con uno de entrada define por dónde viajarán los archivos.
- **Elementos en Tránsito (`FileItem`)**: Cada archivo que ingresa al flujo recibe un identificador único `#ShortItemId` que se conserva durante todo su ciclo de vida para garantizar trazabilidad total.

---

## 2. Descripción de la Interfaz Visual

```
+---------------------------------------------------------------------------------------------------------------+
|  FILEFLOW STUDIO  [▶ Iniciar Flujo] [⏸ Pausa] [⏹ Detener] [🔍 Simulación Dry-Run]   [💾 Guardar] [📂 Cargar] |
+---------------------------------------------------------------------------------------------------------------+
| PANEL DE NODOS (Izquierda) | LIENZO VISUAL DE GRAFOS (Centro)                    | INSPECTOR DE PROPIEDADES   |
| - Origen Carpeta           |                                                     | (Derecha)                  |
| - Descompresor Smart       |    [ 📂 Origen ] ---> [ 🗜️ SmartUnpack ]           | Parámetros del nodo        |
| - Optimizador Imagen       |      (Breakpoint: ●)     (Logs: ≡)                  | seleccionado...            |
| - Destino                  |                                                     |                            |
+---------------------------------------------------------------------------------------------------------------+
| 🖥️ CONSOLA DE TELEMETRÍA: [Todos] [🔴 Errores (0)] [🟠 Warn (0)] [🔵 Info (4)] [🟣 Debug (12)] | ⚡ En Vivo |
| [ 🔍 Filtrar por ID, nodo, archivo...       ✕ ] • 16 logs • Listo                       [💾 Exportar] [🗑 Limpiar]|
|  17:15:02.120 | [INF] | FolderSource   | #d3b07384 | photos.zip | 0.0 ms | [Origen] 1 archivo emitido         |
|  17:15:02.340 | [INF] | SmartUnpack    | #d3b07384 | photos.zip | 45.2 ms| [Descompresor] 12 fotos extraídas  |
+---------------------------------------------------------------------------------------------------------------+
```

### 2.1. Controles en la Cabecera de cada Nodo
Cada tarjeta de nodo en el lienzo cuenta con dos botones circulares rápidos:
1. **Punto de Interrupción (*Breakpoint*)** (Icono circular rojo `●`):
   - Al estar encendido, el motor de ejecución detendrá el flujo cuando un archivo llegue a este nodo, permitiendo inspeccionar sus variables y metadatos.
2. **Control de Emisión de Logs** (Icono `≡` cian brillante / gris atenuado):
   - **Encendido (Cian)**: El nodo emite todas sus trazas y métricas a la consola.
   - **Apagado (Gris)**: Silencia los logs de este nodo específico para no saturar la consola cuando procesas millones de archivos rutinarios.

---

## 3. Catálogo Completo de los 24 Nodos de Producción

### 📁 Sistema de Archivos (`FileSystem`)
1. **Origen Carpeta (`FolderSourceNode`)**: Escanea directorios locales/red con soporte recursivo y filtros de extensión.
2. **Destino de Archivos (`DestinationSinkNode`)**: Guarda o copia archivos al directorio final con gestión inteligente de colisiones (Sobrescribir, Renombrar, Omitir).
3. **Reubicador Seguro (`FileRelocatorNode`)**: Mueve o copia archivos validando integridad criptográfica post-transferencia.
4. **Renombrador Avanzado (`AdvancedRenamerNode`)**: Aplica plantillas dinámicas con fecha, tamaño, prefijos y reemplazo por expresiones regulares.
5. **Inyector de Variables (`VariableInjectorNode`)**: Añade variables dinámicas al archivo para ser utilizadas por nodos posteriores.
6. **Inspector de Carpetas (`DirectoryInspectorNode`)**: Analiza la estructura de directorios y emite métricas de conteo y tamaño.
7. **Limpiador de Carpetas Vacías (`EmptyDirectoryCleanerNode`)**: Elimina carpetas residuales huérfanas de forma segura.
8. **Papelera de Reciclaje Segura (`SafeRecycleDeleteNode`)**: Envía archivos obsoletos a la papelera del sistema en lugar de eliminarlos permanentemente.
9. **Acción sobre Archivo Original (`OriginalFileActionNode`)**: Permite archivar, poner en cuarentena o purgar el archivo de origen una vez procesado.
10. **Procesador de Documentos (`DocumentProcessorNode`)**: Clasifica y analiza metadatos de documentos PDF, Word, Excel y texto plano.

### 🗜️ Archivos Comprimidos (`Archives`)
11. **Descompresor Inteligente (`SmartUnpackNode`)**: Extrae archivos Zip, 7z, Rar, Tar, Gz y auto-aplana carpetas contenedor únicas redundantes.
12. **Compresor de Archivos (`ArchiveCompressorNode`)**: Comprime elementos individuales o lotes en Zip, Tar, Gz o 7z con nivel de compresión configurable.
13. **Filtro de Partes de Archivo (`ArchiveFilterNode`)**: Detecta y procesa únicamente la primera parte de archivos multivolumen (`.part1.rar`, `.z01`).

### 🖼️ Procesamiento de Imágenes (`Images`)
14. **Optimizador de Imágenes (`ImageOptimizerNode`)**: Comprime y redimensiona imágenes a WebP, JPEG o PNG con cálculo automático del % de ahorro de espacio.
15. **Extractor EXIF (`ExifMetadataNode`)**: Lee metadatos de cámara, modelo, fecha de captura y resolución, inyectándolos como variables del archivo.

### 🔐 Integridad y Criptografía (`Hashing`)
16. **Calculador de Hash (`HashCalculatorNode`)**: Genera firmas SHA-256, MD5, SHA-1, SHA-512 o xxHash para verificación de integridad.
17. **Filtro de Duplicados (`DeduplicationFilterNode`)**: Compara firmas criptográficas en tiempo real y desvía archivos duplicados a una rama secundaria.

### ⚙️ Lógica y Control de Flujo (`Logic`)
18. **Bifurcador Switch-Case (`SwitchCaseNode`)**: Enruta archivos a diferentes ramas según patrones de extensión, tamaño o variables.
19. **Filtro de Expresiones (`ExpressionFilterNode`)**: Evalúa condiciones booleanas (`Size > 10MB`, `Ext == 'pdf'`).
20. **Retardo y Control de Caudal (`ThrottleDelayNode`)**: Limita la tasa de procesamiento para no saturar APIs o discos.
21. **Acumulador por Lotes (`BatchBufferNode`)**: Agrupa archivos en lotes por cantidad o tamaño total en MB antes de continuar.
22. **Barrera de Sincronización (`ForkJoinBarrierNode`)**: Espera a que todas las ramas paralelas de un archivo se completen antes de proseguir.

### 🌐 Integraciones y Multimedia (`Integrations`)
23. **Ejecutor de Comandos CLI (`CliExecutionNode`)**: Lanza scripts de PowerShell, Python o ejecutables externos pasando rutas como argumentos.
24. **Transcodificador Multimedia (`MediaTranscoderNode`)**: Convierte videos y audios mediante FFmpeg con presets de alta compatibilidad (MP4 H.264, MP3, WebM).

---

## 4. Uso de la Consola de Logs y Trazabilidad

1. **Filtros Rápidos por Severidad**: Haz clic en los botones `Todos`, `🔴 Errores`, `🟠 Warn`, `🔵 Info` o `🟣 Debug` para aislar rápidamente incidentes sin mezclar información.
2. **Búsqueda Instantánea**: Escribe cualquier texto en la caja de búsqueda para buscar por nombre de archivo, nodo o contenido del JSON. Usa el botón `✕` para limpiar el filtro al instante.
3. **Trazabilidad por Archivo**:
   - Cada archivo muestra un badge como `#d3b07384`. Al hacer clic sobre él o pulsar el botón **`🔍 Trazabilidad`** en los detalles, la consola se filtrará para mostrar exclusivamente todos los nodos por los que ha viajado ese archivo en orden cronológico.
4. **Visor de Detalles JSON**: Al seleccionar cualquier fila que tenga el badge `{ } JSON`, se desplegará un acordeón con los metadatos técnicos y un botón para **`📋 Copiar JSON`** al portapapeles.

---

## 5. Preguntas Frecuentes y Solución de Problemas (FAQ)

### ¿Cómo pruebo mi flujo sin alterar archivos reales?
Activa la casilla **Simulación (*Dry Run*)** en la barra superior antes de presionar Iniciar. El flujo simulará todas las operaciones e imprimirá los logs previstos sin mover, renombrar ni eliminar ningún archivo en disco.

### El nodo Transcodificador Multimedia muestra advertencia de FFmpeg no encontrado
Asegúrate de descargar `ffmpeg.exe` y colocarlo en la misma carpeta que `FileFlow.App.exe` o añadir su directorio a la variable de entorno `PATH` de Windows.

### ¿Dónde se guardan los archivos de log exportados?
Al presionar el botón **`💾 Exportar`** en la consola, se abrirá un diálogo para guardar el informe en formato `.log` o `.txt` con todas las marcas de tiempo e identificadores.
