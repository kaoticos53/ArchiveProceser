# ⚡ FileFlow Studio

<div align="center">

![Platform](https://img.shields.io/badge/.NET-9.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![Language](https://img.shields.io/badge/C%23-13.0-239120?style=for-the-badge&logo=csharp&logoColor=white)
![UI](https://img.shields.io/badge/WPF-Nodify%20MVVM-0078D4?style=for-the-badge&logo=windows&logoColor=white)
![Nodes](https://img.shields.io/badge/Nodes-57%20DAG%20Nodes-38BDF8?style=for-the-badge&logo=diagram-next)
![Tests](https://img.shields.io/badge/Tests-477%2F477%20Passing%20(100%25)-brightgreen?style=for-the-badge&logo=xunit)
![Telemetry](https://img.shields.io/badge/Telemetry->82.000%20logs%2Fsec-blueviolet?style=for-the-badge)
![License](https://img.shields.io/badge/License-GPLv3-blue?style=for-the-badge&logo=gnu)

**Motor de automatización, procesamiento masivo y transformación de archivos basado en Grafos Dirigidos Acíclicos (DAG) interactivos para Windows.**

[✨ Características](#-características-principales) •
[🏛️ Arquitectura](#️-arquitectura-del-sistema) •
[🧩 Módulos y Plugins](#-módulos-y-plugins-oficiales-57-nodos) •
[🚀 Inicio Rápido](#-inicio-rápido) •
[📊 Reportes Visuales](#-reporte-visual-de-operaciones) •
[📚 Documentación](#-documentación-adicional) •
[📄 Licencia](#-licencia)

</div>

---

## 🌟 Características Principales

- **🎨 Lienzo Visual de Diseño de Flujos (DAG)**:
  - Diseñe flujos de trabajo arrastrando y conectando nodos con **Nodify** y **CommunityToolkit.Mvvm**.
  - Validación topológica en tiempo real con detección de ciclos, puertos huérfanos y compatibilidad de tipos.
- **⚡ Motor Asíncrono de Alto Rendimiento**:
  - Procesamiento concurrente basado en `System.Threading.Channels` y `TPL Dataflow`.
  - Cancelación cooperativa instantánea (`CancellationToken`) y despacho paralelo multihilo sin bloqueos de interfaz.
- **🛡️ Pipelines No Destructivos por Defecto y Simulación Dry Run**:
  - Inmutabilidad del archivo de origen garantizada por defecto.
  - Pruebe flujos complejos sin tocar el disco mediante el diario de acciones planificadas (`PlannedAction` / `IExecutionJournal`) para previsualizar movimientos, renombrados o transformaciones antes de ejecutarlos.
- **🤖 Inferencia de Inteligencia Artificial Local (ONNX Runtime)**:
  - Clasificación de imágenes sin conexión, detección de rostros, segmentación y OCR local rápido.
- **🌐 Conectividad Universal Multi-Protocolo (Network & Cloud Hub)**:
  - Nodos unificados con soporte simétrico para **HTTP/HTTPS**, **FTP/FTPS**, **SFTP/SSH**, **WebDAV/Nextcloud** y **SMB/Red Local** con visibilidad condicional reactiva de parámetros.
- **📊 Reportes Interactivos y Trazabilidad Completa (`OperationReportNode`)**:
  - Generación de informes en **HTML Interactivo** (con acordeón colapsable por directorios, KPIs, timeline con badges y búsqueda reactiva en Vanilla JS), **Markdown**, **Texto Plano en Árbol ASCII**, **JSON** y **CSV**.
- **📈 Telemetría y Registro SQLite Ultrarrápido**:
  - Almacén de logs en memoria capaz de registrar más de **82.000 trazas/segundo** en 28 núcleos con paginación virtualizada en la UI.
- **🧩 Arquitectura Microkernel Extensible (ADR-006)**:
  - Sistema de plugins desacoplado basado en `AssemblyLoadContext` donde cada dominio (`FileSystem`, `Archives`, `Images`, `Documents`, `Network`, `AI`, `Data`, `Logic`, `Scripting`, `Integrations`, `Hashing`) contiene de forma autónoma su código, recursos y localización multilingüe.

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
│  FileSystem  │ │   Archives    │ │    Images     │ │   Network    │ │       AI        │
│  (14 Nodos)  │ │   (3 Nodos)   │ │   (4 Nodos)   │ │  (2 Nodos)   │ │   (8 Nodos)     │
└───┬──────────┘ └───┬───────────┘ └───┬───────────┘ └───┬──────────┘ └───┬─────────────┘
    │                │                 │                 │                │
┌───┴──────────┐ ┌───┴───────────┐ ┌───┴───────────┐ ┌───┴──────────┐ ┌───┴─────────────┐
│  Documents   │ │     Data      │ │     Logic     │ │  Scripting   │ │  Integrations   │
│  (4 Nodos)   │ │   (3 Nodos)   │ │   (6 Nodos)   │ │  (3 Nodos)   │ │   (5 Nodos)     │
└──────────────┘ └───────────────┘ └───────────────┘ └──────────────┘ └─────────────────┘
```

---

## 🧩 Módulos y Plugins Oficiales (57 Nodos)

FileFlow Studio organiza sus capacidades en 11 macro-categorías de plugins:

1. **📁 FileSystem (14 Nodos)**: Ingesta recursiva (`FolderSourceNode`), sumideros con resolución de colisiones (`DestinationSinkNode`), renombrado dinámico por plantillas (`AdvancedRenamerNode`), reubicación con hash (`FileRelocatorNode`), papelera segura (`SafeRecycleDeleteNode`), ciclo de vida de origen (`OriginalFileActionNode`), informe visual de operaciones (`OperationReportNode`), limpiador de carpetas vacías (`EmptyDirectoryCleanerNode`), entre otros.
2. **🗜️ Archives (3 Nodos)**: Descompresión inteligente con aplanado (`SmartUnpackNode`), empaquetado multicompresor ZIP/7z/TAR (`ArchiveCompressorNode`), filtrado de volúmenes divididos (`ArchiveFilterNode`).
3. **🖼️ Images (4 Nodos)**: Optimización y conversión WebP/JPEG/PNG (`ImageOptimizerNode`), extracción de metadatos EXIF (`ExifMetadataNode`), redimensionamiento inteligente y transformaciones.
4. **🌐 Network & Cloud (2 Nodos Unificados)**:
   - **`NetworkDownloadNode`**: Hub universal de descarga (`HTTP/HTTPS`, `FTP/FTPS`, `SFTP/SSH`, `WebDAV/Nextcloud`, `SMB/Red Local`).
   - **`NetworkUploadNode`**: Hub universal de subida y transferencia (`HTTP POST/PUT`, `FTP/FTPS`, `SFTP/SSH`, `WebDAV/Nextcloud`, `SMB/Red Local`).
5. **🤖 AI & Machine Learning (8 Nodos)**: Clasificación inteligente de imágenes (`SmartImageClassifierNode`), detección de objetos (`PromptObjectDetectorNode`), OCR local (`LocalOcrNode`), transcripción de audio Whisper (`WhisperAudioTranscriberNode`), detección facial (`FaceDetectorNode`), búsqueda semántica (`ZeroShotSemanticSearchNode`), anonimizador de PII (`PiiAnonymizerNode`), superresolución (`SuperResolutionUpscalerNode`).
6. **📄 Documents & PDF (4 Nodos)**: Fusión de PDFs (`PdfMergeNode`), división y extracción de páginas (`PdfSplitNode`), extracción de texto (`PdfTextExtractorNode`), conversión a imágenes (`PdfToImageNode`).
7. **📊 Data & Structured Files (3 Nodos)**: Lectura de Excel (`ExcelReaderNode`), conversión y filtrado de CSV (`CsvProcessorNode`), cruce de tablas de datos (`DataLookupNode`).
8. **⚙️ Logic & Control Flow (6 Nodos)**: Enrutador condicional múltiple (`SwitchCaseNode`), filtro de expresiones lógicas (`ExpressionFilterNode`), control de caudal (`ThrottleDelayNode`), acumulador de lotes (`BatchBufferNode`), barrera paralela (`ForkJoinBarrierNode`), inyector de variables (`VariableInjectorNode`).
9. **🔐 Hashing & Security (3 Nodos)**: Cálculo criptográfico multialgoritmo (`HashCalculatorNode`), desduplicación inteligente (`DeduplicationFilterNode`), verificación de firmas.
10. **📜 Scripting & Custom Logic (3 Nodos)**: Scripts C# Roslyn dinámicos (`CustomScriptNode`), ejecución de Python integrado, automatización PowerShell.
11. **🔌 Integrations & CLI (5 Nodos)**: Ejecución de utilidades CLI (`CliExecutionNode`), notificaciones Webhook (`WebhookNotificationNode`), transcodificación multimedia con FFmpeg (`MediaTranscoderNode`), exportación SQLite (`SqliteDatabaseSinkNode`), cola de mensajes.

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

# 3. Ejecutar la suite de pruebas automatizadas (477 tests)
dotnet test FileFlow.slnx

# 4. Lanzar la aplicación FileFlow Studio
.\run.ps1
```

---

## 🧪 Pruebas Automatizadas y Calidad

FileFlow Studio cuenta con una rigurosa suite de pruebas automatizadas con **100% de cobertura de éxito**:

- **477 Pruebas Unitarias, de Integración y Estrés** ejecutadas bajo xUnit y FluentAssertions.
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

Este proyecto está distribuido bajo la licencia **GNU General Public License v3.0 (GNU GPLv3)**.

Copyright (C) 2026 **RGLara**.

Consulte el archivo [`LICENSE`](LICENSE) para obtener los términos completos y condiciones de copia, distribución y modificación.
