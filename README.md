# ⚡ FileFlow Studio

<div align="center">

![Platform](https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![Language](https://img.shields.io/badge/C%23-14.0-239120?style=for-the-badge&logo=csharp&logoColor=white)
![UI](https://img.shields.io/badge/Uno%20Platform-WinUI%203%20%7C%20Skia%20%7C%20WASM-512BD4?style=for-the-badge&logo=windows&logoColor=white)
![Platforms](https://img.shields.io/badge/Windows%20%7C%20Linux%20%7C%20macOS%20%7C%20Web%20%7C%20iPadOS-38BDF8?style=for-the-badge&logo=diagram-next)
![Tests](https://img.shields.io/badge/Tests-1755%2F1756%20Passing-brightgreen?style=for-the-badge&logo=xunit)
![License](https://img.shields.io/badge/License-GPLv3-blue?style=for-the-badge&logo=gnu)

**File automation, large-scale processing, and transformation engine powered by interactive Directed Acyclic Graphs (DAG).**

**Motor de automatización, procesamiento masivo y transformación de archivos basado en Grafos Dirigidos Acíclicos (DAG) interactivos.**

[🇬🇧 English](#-english) • [🇪🇸 Español](#-español)

</div>

---

## 🇬🇧 English

### 🌟 Key Features

- **Visual DAG Workflow Designer** on Uno Platform (WinUI 3 on Windows, Skia Desktop on Linux/macOS, WebAssembly in the browser, iOS/iPadOS).
- **Cross-platform by construction**: one host, one portable core, no UI-framework lock-in.
- **High-performance async engine** using Channels and TPL Dataflow.
- **Safe-by-default pipelines** with non-destructive behavior and Dry Run simulation.
- **Local AI inference (ONNX Runtime)** for classification, OCR, detection, and semantic search.
- **Unified Network & Cloud connectivity**: HTTP/HTTPS, FTP/FTPS, SFTP/SSH, WebDAV, SMB.
- **Interactive operation reports** in HTML, Markdown, JSON, CSV, and plain text.
- **Extensible microkernel plugin architecture** with isolated domain plugins.

### 🏛️ Architecture

FileFlow Studio is organized in three main layers:

- **FileFlow.App.Uno**: the single UI host (Uno Platform over WinUI 3 / Skia / WASM / iOS).
- **FileFlow.App.Core**: the portable presentation core (ViewModels and UI-agnostic services) shared by the host.
- **FileFlow.Core**: DAG orchestration engine, validation, telemetry, plugin loading.
- **FileFlow.Sdk**: core contracts (`IFlowNode`, `FileItemContext`, `IFlowExecutionContext`).

Official plugins include: **FileSystem, Archives, Images, Network, AI, Documents, Data, Logic, Scripting, Integrations, Hashing, Subflows**.

### 🚀 Quick Start

```powershell
git clone https://github.com/kaoticos53/ArchiveProceser.git
cd ArchiveProceser
dotnet build FileFlow.slnx
dotnet test FileFlow.slnx
.\run.ps1
```

### 📚 Documentation

- [System Architecture](docs/architecture.md)
- [User Manual](docs/manual_de_usuario.md)
- [Testing Guide](docs/guia_de_pruebas.md)
- [Project Walkthrough](docs/PROJECT_WALKTHROUGH.md)

### 📄 License

This project is licensed under **GNU General Public License v3.0 (GPLv3)**. See [LICENSE](LICENSE).

---

## 🇪🇸 Español

### 🌟 Características Principales

- **🎨 Lienzo Visual de Diseño de Flujos (DAG)**:
  - Diseñe flujos de trabajo arrastrando y conectando nodos con el **lienzo propio del host Uno Platform** y `CommunityToolkit.Mvvm`.
  - Validación topológica en tiempo real con detección de ciclos, puertos huérfanos y compatibilidad de tipos.
- **🧭 Multiplataforma de verdad**: un solo host que compila para **Windows, Linux, macOS, Web (WASM) e iOS/iPadOS**, sobre un núcleo portable sin dependencias de framework de UI.
- **⚡ Motor Asíncrono de Alto Rendimiento**:
  - Procesamiento concurrente basado en `System.Threading.Channels` y `TPL Dataflow`.
  - Cancelación cooperativa instantánea (`CancellationToken`) y despacho paralelo multihilo sin bloqueos de interfaz.
- **🛡️ Pipelines No Destructivos por Defecto y Simulación Dry Run**:
  - Inmutabilidad del archivo de origen garantizada por defecto.
  - Pruebe flujos complejos sin tocar el disco mediante el diario de acciones planificadas (`PlannedAction` / `IExecutionJournal`).
- **🤖 Inferencia de Inteligencia Artificial Local (ONNX Runtime)**:
  - Clasificación de imágenes sin conexión, detección de rostros, segmentación y OCR local rápido.
- **🌐 Conectividad Universal Multi-Protocolo (Network & Cloud Hub)**:
  - Nodos unificados con soporte para **HTTP/HTTPS**, **FTP/FTPS**, **SFTP/SSH**, **WebDAV/Nextcloud** y **SMB/Red Local**.
- **📊 Reportes Interactivos y Trazabilidad Completa (`OperationReportNode`)**:
  - Generación de informes en **HTML Interactivo**, **Markdown**, **Texto Plano en Árbol ASCII**, **JSON** y **CSV**.
- **📈 Telemetría y Registro SQLite Ultrarrápido**:
  - Almacén de logs en memoria con paginación virtualizada en la UI.
- **🧩 Arquitectura Microkernel Extensible (ADR-006)**:
  - Sistema de plugins desacoplado basado en `AssemblyLoadContext` donde cada dominio contiene de forma autónoma su código, recursos y localización multilingüe.

---

## 🏛️ Arquitectura del Sistema

```
                      ┌─────────────────────────────────┐
                      │ FileFlow.App.Uno (host único)   │
                      │  WinUI 3 · Skia · WASM · iOS    │
                      │  Lienzo DAG · MVVM Toolkit      │
                      └────────────────┬────────────────┘
                                       │ Referencia
                                       ▼
                      ┌─────────────────────────────────┐
                      │  FileFlow.App.Core (portable)   │
                      │  ViewModels y servicios UI-free │
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
                      │    FileFlow.Sdk (Puro .NET 10)  │
                      │  • IFlowNode • FileItemContext  │
                      │  • IFlowExecutionContext        │
                      │  • VariableTemplateResolver     │
                      └────────────────▲────────────────┘
                                       │ Implementan
    ┌────────────────┬─────────────────┼─────────────────┬────────────────┐
    │                │                 │                 │                │
┌───┴──────────┐ ┌───┴───────────┐ ┌───┴───────────┐ ┌───┴──────────┐ ┌───┴─────────────┐
│  FileSystem  │ │   Archives    │ │    Images     │ │   Network    │ │       AI        │
└───┬──────────┘ └───┬───────────┘ └───┬───────────┘ └───┬──────────┘ └───┬─────────────┘
    │                │                 │                 │                │
┌───┴──────────┐ ┌───┴───────────┐ ┌───┴───────────┐ ┌───┴──────────┐ ┌───┴─────────────┐
│  Documents   │ │     Data      │ │     Logic     │ │  Scripting   │ │  Integrations   │
└──────────────┘ └───────────────┘ └───────────────┘ └──────────────┘ └─────────────────┘
```

---

## 🚀 Inicio Rápido

### Requisitos Previos
- **SDK**: [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Windows**: Windows 10 (1809+) / Windows 11 con el SDK de Windows App.
- **Linux/macOS**: dependencias de Skia Desktop (X11/Wayland o macOS 12+).
- **Web/iOS**: workloads `wasm-tools` (y `ios` + Xcode para iPadOS).

### Compilación y Ejecución

```powershell
# 1. Clonar el repositorio
git clone https://github.com/kaoticos53/ArchiveProceser.git
cd ArchiveProceser

# 2. Compilar toda la solución
dotnet build FileFlow.slnx

# 3. Ejecutar la suite de pruebas automatizadas
dotnet test FileFlow.slnx

# 4. Lanzar la aplicación
.\run.ps1

# 5. Compilar la matriz multiplataforma (desktop + web; iOS con -IncludeIos en macOS)
.\build-matrix.ps1
```

---

## 🧪 Pruebas Automatizadas y Calidad

FileFlow Studio cuenta con una suite de pruebas automatizadas con **100% de éxito**:

- **1.755 pruebas unitarias, de integración y estrés** ejecutadas bajo xUnit y FluentAssertions.
- **Aislamiento Total**: entornos temporales con GUID para operaciones de disco y pruebas deterministas.
- **Guardias de arquitectura**: ven el árbol real y fallan si un nodo, un diálogo o un tema se sale del contrato.

```powershell
# Ejecutar pruebas y generar informe de cobertura
.\test.ps1
.\coverage.ps1
```

---

## 📚 Documentación Adicional

- 🏛️ [**Arquitectura y Diseño Técnico**](docs/architecture.md)
- 📄 [**Especificaciones Formales del Sistema (SRS v2.0 - Histórico)**](docs/history/2026-08_srs_especificaciones.md)
- 📖 [**Manual de Usuario Completo**](docs/manual_de_usuario.md)
- 🧪 [**Guía y Catálogo Exhaustivo de Pruebas**](docs/guia_de_pruebas.md)
- 📋 [**Historial Cronológico de Cambios (Walkthrough)**](docs/PROJECT_WALKTHROUGH.md)

---

## 📄 Licencia

Este proyecto está distribuido bajo la licencia **GNU General Public License v3.0 (GNU GPLv3)**.

Copyright (C) 2026 **RGLara**.

Consulte el archivo [`LICENSE`](LICENSE) para obtener los términos completos y condiciones de copia, distribución y modificación.
