# ⚡ FileFlow Studio

<div align="center">

![Platform](https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![Language](https://img.shields.io/badge/C%23-14.0-239120?style=for-the-badge&logo=csharp&logoColor=white)
![UI](https://img.shields.io/badge/UI-Avalonia%2012%20(Cross--Platform)-9A4993?style=for-the-badge&logo=avalonia&logoColor=white)
![Nodes](https://img.shields.io/badge/Nodes-57%20DAG%20Nodes-38BDF8?style=for-the-badge&logo=diagram-next)
![Tests](https://img.shields.io/badge/Tests-1742%2F1742%20Passing%20(100%25)-brightgreen?style=for-the-badge&logo=xunit)
![Telemetry](https://img.shields.io/badge/Telemetry->82.000%20logs%2Fsec-blueviolet?style=for-the-badge)
![License](https://img.shields.io/badge/License-GPLv3-blue?style=for-the-badge&logo=gnu)

**File automation, large-scale processing, and transformation engine powered by interactive Directed Acyclic Graphs (DAG) for Windows, Linux, macOS, and Web (WebAssembly).**

**Motor de automatización, procesamiento masivo y transformación de archivos basado en Grafos Dirigidos Acíclicos (DAG) interactivos para Windows, Linux, macOS y Web (WebAssembly).**

[🇬🇧 English](#-english) • [🇪🇸 Español](#-español)

</div>

---

## 🇬🇧 English

### 🌟 Key Features

- **Cross-Platform Visual DAG Workflow Designer** powered by Avalonia 12 + Nodify + MVVM.
- **High-performance async engine** using Channels and TPL Dataflow.
- **Safe-by-default pipelines** with non-destructive behavior and Dry Run simulation.
- **Local AI inference (ONNX Runtime)** for classification, OCR, detection, and semantic search.
- **Unified Network & Cloud connectivity**: HTTP/HTTPS, FTP/FTPS, SFTP/SSH, WebDAV, SMB.
- **Interactive operation reports** in HTML, Markdown, JSON, CSV, and plain text.
- **Extensible microkernel plugin architecture** with isolated domain plugins.

### 🏛️ Architecture

FileFlow Studio is organized in three main layers:

- **FileFlow.App**: Cross-platform presentation layer (Avalonia 12 + Nodify canvas + MVVM).
- **FileFlow.App.Browser**: WebAssembly host for running the visual pipeline builder in modern web browsers.
- **FileFlow.Core**: DAG orchestration engine, validation, telemetry, plugin loading.
- **FileFlow.Sdk**: Pure contracts (`IFlowNode`, `FileItemContext`, `IFlowExecutionContext`).

Official plugins include: **FileSystem, Archives, Images, Network, AI, Documents, Data, Logic, Scripting, Integrations, Hashing**.

### 🛠️ Building & Running by Operating System

#### 🪟 Windows (Desktop GUI & CLI)

**Prerequisites**: [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

```powershell
# 1. Clone repository
git clone https://github.com/kaoticos53/ArchiveProceser.git
cd ArchiveProceser

# 2. Build desktop solution
dotnet build FileFlow.slnx

# 3. Launch the desktop GUI application
.\run.ps1
# Or quick launch without rebuilding:
.\run.ps1 -Fast

# 4. Run in Headless / CLI mode (command line execution of a pipeline)
dotnet run --project FileFlow.App/FileFlow.App.csproj -- --run "path\to\workflow.json"
```

#### 🐧 Linux (Desktop GUI & Headless CLI)

**Prerequisites**:
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- X11/Wayland runtime dependencies: `sudo apt install libx11-6 libx11-xcb1 libice6 libsm6 libfontconfig1` (Debian/Ubuntu) or `sudo dnf install libX11 libX11-xcb libICE libSM fontconfig` (Fedora).
- 7-Zip CLI: `sudo apt install p7zip-full` or `sudo dnf install p7zip`.

```bash
# 1. Build desktop solution
dotnet build FileFlow.slnx

# 2. Launch the desktop GUI application
./run.sh
# Or quick launch without rebuilding:
./run.sh --fast

# 3. Headless / CLI execution (ideal for servers and Docker containers)
dotnet run --project FileFlow.App/FileFlow.App.csproj -- --run "/path/to/workflow.json"

# 4. Generate self-contained native Linux binary
dotnet publish FileFlow.App/FileFlow.App.csproj -c Release -r linux-x64 --self-contained
```

#### 🍎 macOS (Desktop GUI & CLI)

**Prerequisites**:
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- 7-Zip CLI: `brew install sevenzip` or `brew install p7zip`

```bash
# 1. Build desktop solution
dotnet build FileFlow.slnx

# 2. Launch GUI via launcher script or dotnet
./run.sh
# Or directly via dotnet:
dotnet run --project FileFlow.App/FileFlow.App.csproj

# 3. Headless / CLI execution
dotnet run --project FileFlow.App/FileFlow.App.csproj -- --run "/path/to/workflow.json"

# 4. Publish self-contained macOS binary (Apple Silicon / Intel)
dotnet publish FileFlow.App/FileFlow.App.csproj -c Release -r osx-arm64 --self-contained
# For Intel Macs:
dotnet publish FileFlow.App/FileFlow.App.csproj -c Release -r osx-x64 --self-contained
```

#### 🌐 Web (WebAssembly / Browser)

**Prerequisites**: [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

```bash
# 1. Build & Publish the WebAssembly project
dotnet publish FileFlow.Browser.slnx -c Release

# 2. Run local web server with hot-reload / dev server
dotnet run --project FileFlow.App.Browser/FileFlow.App.Browser.csproj
# Access the browser app at http://localhost:5000

# 3. Static Web Hosting
# The optimized static files (dotnet.wasm, HTML, JS) are generated in:
# FileFlow.App.Browser/bin/Release/net10.0/browser-wasm/publish/wwwroot
```

### 🧪 Automated Testing

```powershell
# Windows
.\test.ps1

# Linux / macOS
dotnet test FileFlow.Tests/FileFlow.Tests.csproj
```

## 🇪🇸 Español

### 🌟 Características Principales

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

## 🛠️ Compilación y Ejecución por Sistema Operativo

### 🪟 Windows (GUI de Escritorio y CLI / Headless)

**Requisitos**: [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

```powershell
# 1. Clonar el repositorio
git clone https://github.com/kaoticos53/ArchiveProceser.git
cd ArchiveProceser

# 2. Compilar la solución de escritorio
dotnet build FileFlow.slnx

# 3. Lanzar la interfaz gráfica de usuario (GUI)
.\run.ps1
# O modo de arranque rápido (sin recompilar):
.\run.ps1 -Fast

# 4. Modo Headless / CLI (ejecutar un pipeline desde la consola de comandos sin UI)
dotnet run --project FileFlow.App/FileFlow.App.csproj -- --run "ruta\a\tu_flujo.json"
```

### 🐧 Linux (GUI de Escritorio y CLI para Servidores / Docker)

**Requisitos**:
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Librerías gráficas X11/Wayland:
  - Ubuntu / Debian: `sudo apt install libx11-6 libx11-xcb1 libice6 libsm6 libfontconfig1`
  - Fedora / RHEL: `sudo dnf install libX11 libX11-xcb libICE libSM fontconfig`
  - Arch Linux: `sudo pacman -S libx11 libice libsm fontconfig`
- Utilidad 7-Zip CLI: `sudo apt install p7zip-full` o `sudo dnf install p7zip`.

```bash
# 1. Compilar la solución
dotnet build FileFlow.slnx

# 2. Lanzar la interfaz gráfica de escritorio
./run.sh
# O arranque rápido sin recompilar:
./run.sh --fast

# 3. Modo Servidor / CLI (procesamiento por lotes desatendido sin entorno gráfico)
dotnet run --project FileFlow.App/FileFlow.App.csproj -- --run "/ruta/a/tu_flujo.json"

# 4. Generar ejecutable nativo autocontenido para Linux
dotnet publish FileFlow.App/FileFlow.App.csproj -c Release -r linux-x64 --self-contained
```

### 🍎 macOS (GUI de Escritorio y CLI)

**Requisitos**:
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Utilidad 7-Zip: `brew install sevenzip` o `brew install p7zip`

```bash
# 1. Compilar la solución
dotnet build FileFlow.slnx

# 2. Iniciar la aplicación de escritorio
./run.sh
# O directamente vía dotnet:
dotnet run --project FileFlow.App/FileFlow.App.csproj

# 3. Ejecución por línea de comandos (Headless)
dotnet run --project FileFlow.App/FileFlow.App.csproj -- --run "/ruta/a/tu_flujo.json"

# 4. Publicar binario autocontenido para macOS (Apple Silicon M1/M2/M3/M4 o Intel)
dotnet publish FileFlow.App/FileFlow.App.csproj -c Release -r osx-arm64 --self-contained
# Para Macs con Intel:
dotnet publish FileFlow.App/FileFlow.App.csproj -c Release -r osx-x64 --self-contained
```

### 🌐 Web (Navegador / WebAssembly)

**Requisitos**: [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

```bash
# 1. Compilar y publicar los artefactos WebAssembly
dotnet publish FileFlow.Browser.slnx -c Release

# 2. Ejecutar el servidor de desarrollo local
dotnet run --project FileFlow.App.Browser/FileFlow.App.Browser.csproj
# Abra su navegador en http://localhost:5000

# 3. Despliegue en Servidor Web Estático
# Los archivos estáticos optimizados y precomprimidos con Brotli (dotnet.wasm, HTML, JS) se ubican en:
# FileFlow.App.Browser/bin/Release/net10.0/browser-wasm/publish/wwwroot
```

---

## 📦 Generación de Instaladores y Publicación Multiplataforma

FileFlow Studio dispone de scripts de empaquetado para generar instaladores y distribuciones autocontenidas en cada sistema operativo:

### 🪟 Windows (Instalador Setup y Portable)
- **Instalador ejecutable Inno Setup** (`.exe` con desinstalador, iconos y accesos directos):
  ```powershell
  .\installer\build-installer.ps1 -Version "1.0.0"
  ```
- **Versión Portable** (`.zip` sin instalación con manuales PDF y ejemplos):
  ```powershell
  .\installer\build-portable.ps1 -Version "1.0.0"
  ```

### 🐧 Linux (Debian `.deb`, AppImage y Tarball Universal)
- **Generador de paquetes Linux** (produce `fileflow_1.0.0_amd64.deb`, `fileflow-linux-x64-v1.0.0.AppImage` y `.tar.gz` con script lanzador e integración `.desktop`):
  ```powershell
  # Desde Windows / CI con PowerShell:
  .\installer\build-linux-installer.ps1 -Version "1.0.0" -Distro deb,appimage,tarball

  # O desde Linux nativo:
  ./package-linux.sh 1.0.0
  ```

### 🍎 macOS (`.app` Bundle, ZIP Portable y `.dmg`)
- **Generador de paquete macOS** (construye `FileFlow Studio.app` con `Info.plist`, PkgInfo, iconos y soporte Apple Silicon / Intel):
  ```powershell
  # Con PowerShell (multiplataforma):
  .\installer\build-macos-installer.ps1 -Version "1.0.0" -Runtime "both"   # o osx-arm64 / osx-x64

  # O desde macOS nativo (genera también .dmg con hdiutil):
  ./package-macos.sh 1.0.0 arm64
  ```

### 🌐 Web (WebAssembly Distribution)
- **Generador de paquete Web estático** (compila `FileFlow.App.Browser`, optimiza recursos y comprime con Brotli en `FileFlowStudio-Web-v1.0.0.zip` listo para Nginx, Apache o GitHub Pages):
  ```powershell
  .\installer\build-web.ps1 -Version "1.0.0"
  ```

### 🚀 Orquestador Universal de Publicación
Para generar todos los artefactos de publicación en un único paso:
```powershell
# Publicar todos los binarios nativos (Windows, Linux, macOS y Web)
.\publish-all.ps1 -Configuration Release -IncludeMac -IncludeWeb

# Compilar todos los instaladores y empaquetados finales en installer/output/
.\installer\build-all.ps1 -Version "1.0.0" -IncludeMac -IncludeWeb
```

---

## 🧪 Pruebas Automatizadas y Calidad

FileFlow Studio cuenta con una rigurosa suite de pruebas automatizadas con **100% de cobertura de éxito**:

- **1.740+ pruebas unitarias, de integración y estrés** ejecutadas bajo xUnit y FluentAssertions.
- **Aislamiento Total**: Entornos temporales con GUID para operaciones de disco y pruebas deterministas.
- **Benchmarking Multihilo**: Pruebas de estrés que validan >82.000 logs/segundo en telemetría concurrente.

```powershell
# En Windows (suite completa de pruebas)
.\test.ps1

# En Linux / macOS
dotnet test FileFlow.Tests/FileFlow.Tests.csproj

# Análisis de cobertura de código
.\coverage.ps1
```

---

## 📚 Documentación Adicional

- 🏛️ [**Arquitectura y Diseño Técnico**](docs/architecture.md)
- 📄 [**Especificaciones Formales del Sistema (SRS v2.0 - Histórico)**](docs/history/2026-08_srs_especificaciones.md)
- 📖 [**Manual de Usuario Completo**](docs/manual_de_usuario.md)
- 🧪 [**Guía y Catálogo Exhaustivo de Pruebas**](docs/guia_de_pruebas.md)
- 🏛️ [**Arquitectura y Diseño Técnico**](docs/architecture.md)
- 📋 [**Historial Cronológico de Cambios (Walkthrough)**](docs/PROJECT_WALKTHROUGH.md)

---

## 📄 Licencia

Este proyecto está distribuido bajo la licencia **GNU General Public License v3.0 (GNU GPLv3)**.

Copyright (C) 2026 **RGLara**.

Consulte el archivo [`LICENSE`](LICENSE) para obtener los términos completos y condiciones de copia, distribución y modificación.
