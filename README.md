# ⚡ FileFlow Studio

<div align="center">

![Platform](https://img.shields.io/badge/.NET-9.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![Language](https://img.shields.io/badge/C%23-13.0-239120?style=for-the-badge&logo=csharp&logoColor=white)
![UI](https://img.shields.io/badge/WPF-Nodify%20MVVM-0078D4?style=for-the-badge&logo=windows&logoColor=white)
![Tests](https://img.shields.io/badge/Tests-190%2F190%20Passing%20(100%25)-brightgreen?style=for-the-badge&logo=xunit)
![Telemetry](https://img.shields.io/badge/Telemetry->82.000%20logs%2Fsec-blueviolet?style=for-the-badge)
![License](https://img.shields.io/badge/License-MIT-blue?style=for-the-badge)

**Motor de automatización, procesamiento masivo y transformación de archivos basado en Grafos Dirigidos Acíclicos (DAG) interactivos para Windows.**

[✨ Características](#-características-principales) •
[🏛️ Arquitectura](#️-arquitectura-del-sistema) •
[🧩 Catálogo de Nodos](#-catálogo-de-nodos-27-nodos) •
[🚀 Inicio Rápido](#-inicio-rápido) •
[📊 Reportes Visuales](#-reporte-visual-de-operaciones) •
[📚 Documentación](#-documentación-adicional)

</div>

---

## 🌟 Características Principales

- **🎨 Lienzo Visual de Diseño de Flujos (DAG)**:
  - Diseñe flujos de trabajo arrastrando y conectando nodos con **Nodify** y **CommunityToolkit.Mvvm**.
  - Validación topológica en tiempo real con detección de ciclos, puertos huérfanos y compatibilidad de tipos.
- **⚡ Motor Asíncrono de Alto Rendimiento**:
  - Procesamiento concurrente basado en `System.Threading.Channels` y `TPL Dataflow`.
  - Cancelación cooperativa instantánea (`CancellationToken`) y despacho paralelo multihilo sin bloqueos de interfaz.
- **🛡️ Modo Simulación Determinista (Dry Run)**:
  - Pruebe flujos complejos sin tocar el disco. Registra un diario de acciones planificadas (`PlannedAction` / `IExecutionJournal`) para previsualizar movimientos, renombrados o borrados antes de ejecutarlos.
- **📊 Reportes Interactivos y Trazabilidad Completa (`OperationReportNode`)**:
  - Generación de informes en **HTML Interactivo** (con acordeón colapsable por directorios, KPIs, timeline con badges y búsqueda reactiva en Vanilla JS), **Markdown**, **Texto Plano en Árbol ASCII**, **JSON** y **CSV**.
  - Ámbitos configurables: consolidado en un único archivo acumulativo o reportes individuales adjuntos.
- **📈 Telemetría y Registro SQLite Ultrarrápido**:
  - Almacén de logs en memoria capaz de registrar más de **82.000 trazas/segundo** en 28 núcleos con paginación virtualizada en la UI.
- **🧩 Arquitectura Microkernel Extensible**:
  - Sistema de plugins desacoplado basado en `AssemblyLoadContext` donde cada dominio (`FileSystem`, `Archives`, `Images`, `Hashing`, `Logic`, `Integrations`) solo referencia el SDK puro.

---

## 🏛️ Arquitectura del Sistema

```
                      ┌─────────────────────────────────┐
                      │    FileFlow.App (WPF UI)        │
                      │  • Nodify Canvas • MVVM Toolkit │
                      │  • Virtualized Telemetry View   │
                      └────────────────┬────────────────┘
                                       │ Referencia
                                       ▼
                      ┌─────────────────────────────────┐
                      │    FileFlow.Core (Motor DAG)    │
                      │  • WorkflowExecutor (Channels)  │
                      │  • GraphValidator • PluginLoader│
                      │  • SqliteLogStore In-Memory     │
                      └────────────────┬────────────────┘
                                       │ Consume Contratos
                                       ▼
                      ┌─────────────────────────────────┐
                      │    FileFlow.Sdk (Puro .NET 9)   │
                      │  • IFlowNode • FileItemContext  │
                      │  • IFlowExecutionContext        │
                      │  • VariableTemplateResolver     │
                      └────────────────▲────────────────┘
                                       │ Implementan
    ┌────────────────┬─────────────────┼─────────────────┬────────────────┐
    │                │                 │                 │                │
┌───┴──────────┐ ┌───┴───────────┐ ┌───┴───────────┐ ┌───┴──────────┐ ┌───┴─────────────┐
│  FileSystem  │ │   Archives    │ │    Images     │ │   Hashing    │ │  Integrations   │
│  (12 Nodos)  │ │   (3 Nodos)   │ │   (2 Nodos)   │ │  (2 Nodos)   │ │   (3 Nodos)     │
└──────────────┘ └───────────────┘ └───────────────┘ └──────────────┘ └─────────────────┘
```

---

## 🧩 Catálogo de Nodos (27 Nodos)

### 📁 1. FileSystem (Operaciones de Archivos y Carpetas)
| Nodo | Icono | Descripción |
| :--- | :---: | :--- |
| **`FolderSourceNode`** | 📂 | Disparador de escaneo recursivo de directorios con filtros por extensión y globbing. |
| **`DestinationSinkNode`** | 📥 | Receptor final con estrategias de colisión (`Overwrite`, `Skip`, `AutoIncrement`). |
| **`AdvancedRenamerNode`** | ✏️ | Renombrado masivo con tokens dinámicos (`{Date}`, `{Counter:000}`, metadatos EXIF/Hash). |
| **`FileRelocatorNode`** | 🚚 | Mueve o copia archivos a rutas calculadas con verificación opcional SHA-256. |
| **`SafeRecycleDeleteNode`** | 🗑️ | Eliminación segura enviando los archivos a la Papelera de reciclaje de Windows (`SHFileOperationW`). |
| **`OriginalFileActionNode`**| 📦 | Aplica la política de ciclo de vida al archivo inicial (Preservar, Cuarentena, Papelera). |
| **`OperationReportNode`** | 📋 | Genera informes visuales interactivos con acordeones por carpeta y trazabilidad completa. |
| **`DirectoryInspectorNode`**| 🔍 | Clasifica carpetas según contengan únicamente un comprimido o contenido mixto. |
| **`EmptyDirectoryCleanerNode`**| 🧹 | Limpieza determinista de directorios vacíos tras operaciones de extracción o movimiento. |
| **`DocumentProcessorNode`** | 📄 | Extracción de metadatos de documentos (`.pdf`, `.txt`, `.docx`, `.csv`, `.json`). |
| **`VariableInjectorNode`** | 💉 | Inyecta variables calculadas personalizadas en el contexto del archivo. |
| **`LogOutputNode`** | 📝 | Emite trazas diagnósticas intermedias durante el flujo. |

### 🗜️ 2. Archives (Compresión y Descompresión)
| Nodo | Icono | Descripción |
| :--- | :---: | :--- |
| **`SmartUnpackNode`** | 📦 | Descompresión inteligente de ZIP, RAR, 7Z, TAR, GZ con aplanado de carpetas redundantes. |
| **`ArchivePackerNode`** | 🗜️ | Empaquetado y compresión en formatos ZIP, TAR, GZ con algoritmo configurable. |
| **`ArchiveFilterNode`** | 🧩 | Filtra y procesa únicamente la primera parte de archivos multivolumen (`.part1.rar`, `.z01`). |

### 🖼️ 3. Images (Procesamiento Gráfico)
| Nodo | Icono | Descripción |
| :--- | :---: | :--- |
| **`ImageOptimizerNode`** | 🖼️ | Comprime y redimensiona imágenes a WebP, JPEG o PNG calculando el % de ahorro de espacio. |
| **`ExifMetadataNode`** | 📷 | Extrae metadatos de cámara, fecha de captura, geolocalización y orientación EXIF. |

### 🔐 4. Hashing (Integridad Criptográfica)
| Nodo | Icono | Descripción |
| :--- | :---: | :--- |
| **`HashCalculatorNode`** | 🔒 | Calcula checksums criptográficos (SHA-256, MD5, SHA-512, SHA-1, xxHash). |
| **`DeduplicationFilterNode`** | 👥 | Compara firmas en tiempo real y desvía archivos duplicados a ramas alternativas. |

### ⚙️ 5. Logic (Control de Flujo)
| Nodo | Icono | Descripción |
| :--- | :---: | :--- |
| **`SwitchCaseNode`** | 🔀 | Enrutador múltiple condicional basado en extensiones, tamaños o variables. |
| **`ExpressionFilterNode`**| ⚖️ | Filtro booleano basado en condiciones (`Size > 10MB`, `Tags.Contains('raw')`). |
| **`ThrottleDelayNode`** | ⏱️ | Control de caudal y limitación de tasa para evitar saturación de I/O. |
| **`BatchBufferNode`** | 📦 | Acumulador de archivos en memoria por número de elementos o tamaño en MB. |
| **`ForkJoinBarrierNode`** | 🚧 | Barrera de sincronización que espera la culminación de ramas paralelas. |

### 🌐 6. Integrations (Multimedia y Conectividad Externa)
| Nodo | Icono | Descripción |
| :--- | :---: | :--- |
| **`CliExecutionNode`** | 💻 | Ejecución segura de scripts externos (PowerShell, Python, CLI) con captura de stdout/stderr. |
| **`WebhookNotificationNode`** | 🌐 | Notificaciones HTTP POST/PUT con payloads JSON dinámicos a APIs o servicios webhook. |
| **`MediaTranscoderNode`** | 🎬 | Transcodificación de audio y video mediante FFmpeg con presets optimizados. |

---

## 📊 Reporte Visual de Operaciones

El nodo **`OperationReportNode`** permite obtener una visión ejecutiva y técnica de todas las transformaciones realizadas en un lote:

- **Agrupación Jerárquica (`GroupBy = Directory`)**: Visualice sus archivos organizados por su carpeta de origen en un acordeón interactivo colapsable con un solo clic.
- **Búsqueda Reactiva**: Filtre instantáneamente por nombre de archivo, directorio o metadato; las carpetas coincidentes se desplegarán de forma automática.
- **Historial Completo (Timeline)**: Cada tarjeta de archivo incluye el paso a paso detallado desde que fue descubierto hasta su destino final.
- **Multi-Formato**: Exportación nativa a `HTML`, `Markdown`, `Text` (Árbol ASCII), `JSON` y `CSV`.

---

## 🚀 Inicio Rápido

### Requisitos Previos
- **Sistema Operativo**: Windows 10 / Windows 11 (x64)
- **SDK**: [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Compilación y Ejecución

```powershell
# 1. Clonar el repositorio
git clone https://github.com/kaoticos53/ArchiveProceser.git
cd ArchiveProceser

# 2. Compilar toda la solución
dotnet build FileFlow.slnx

# 3. Ejecutar la suite de pruebas (190 tests)
dotnet test FileFlow.slnx

# 4. Lanzar la aplicación FileFlow Studio
.\run.ps1
```

---

## 🧪 Pruebas Automatizadas y Calidad

FileFlow Studio cuenta con una rigurosa suite de pruebas automatizadas con **100% de cobertura de éxito**:

- **190 Pruebas Unitarias, de Integración y Estrés** ejecutadas bajo xUnit y FluentAssertions.
- **Aislamiento Total**: Entornos temporales con GUID para operaciones de disco y pruebas deterministas.
- **Benchmarking Multihilo**: Pruebas de estrés que validan >82.000 logs/segundo en telemetría concurrente.

```powershell
# Ejecutar pruebas y generar informe de cobertura
.\test.ps1
.\coverage.ps1
```

---

## 📚 Documentación Adicional

- 📄 [**Especificaciones Formales del Sistema (SRS v2.0)**](docs/ESPECIFICACIONES.md)
- 📖 [**Manual de Usuario Completo**](docs/manual_de_usuario.md)
- 🧪 [**Guía y Catálogo Exhaustivo de Pruebas**](docs/guia_de_pruebas.md)
- 🏛️ [**Arquitectura y Diseño Técnico**](docs/architecture.md)
- 📋 [**Historial Cronológico de Cambios (Walkthrough)**](docs/PROJECT_WALKTHROUGH.md)

---

## 📄 Licencia

Este proyecto está bajo la Licencia **MIT**. Consulte el archivo `LICENSE` para más detalles.
