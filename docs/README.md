# Centro de Documentación - FileFlow Studio

Bienvenido al centro de documentación técnica y manuales de usuario de **FileFlow Studio**, la plataforma modular de automatización y procesamiento masivo de archivos desarrollada en **C# 14** y **.NET 10**, con un único host UI de **Uno Platform** que compila para **Windows, Linux, macOS, Web (WASM) e iOS/iPadOS**.

---

## 📚 Índice General de Documentos

| Documento | Descripción | Audiencia |
|---|---|---|
| 🏗️ [**Arquitectura y Diseño Técnico**](architecture.md) | Visión general del sistema, diagramas Mermaid.js, flujo de datos por capas, motor de telemetría SQLite In-Memory y Registros de Decisiones Arquitectónicas (ADRs). | Arquitectos de Software & Desarrolladores Core |
| 🚀 [**Guía de Instalación y Despliegue**](setup_and_deployment.md) | Requisitos por plataforma, configuración del entorno local, scripts de ejecución, publicación autocontenida y pipeline CI/CD en GitHub Actions. | Desarrolladores & Ingenieros DevOps |
| 🔌 [**Referencia de API y Módulos**](api_reference.md) | Documentación exhaustiva de contratos del SDK (`IFlowNode`, `FlowNodeBase`, `FileItemContext`, `IFlowExecutionContext`), el formato del archivo de flujo (versión, reparación y convergencia), motor de plantillas y ejemplo completo de nodo personalizado. | Desarrolladores de Plugins & Integradores |
| 🧩 [**Guía de Creación de Nodos**](nodes/CREATING_NODES.md) | Cómo escribir un nodo desde cero: `FlowNodeBase`, puertos fijos y dinámicos, parámetros tipados, anuncio de topología, estado de diseño y despliegue en `Plugins/`. | Desarrolladores de Plugins |
| 🧠 [**Catálogo de Nodos (generado)**](../.agents/nodes_catalog.md) | Los nodos del producto —categoría, puertos, parámetros con su control y enlace al fichero que declara cada uno—, **generado desde el código** y atado por una guardia que falla si deja de coincidir con lo que descubre la aplicación. | Desarrolladores de Plugins & Usuarios Avanzados |
| 📚 [**Catálogo de Ejemplos de Flujos**](examples/README.md) | Flujos listos para abrir en la aplicación —de lo básico a lo empresarial—, cada uno con su guía en markdown y su archivo JSON en el formato actual. | Usuarios Finales & Formadores |
| 📖 [**Manual de Usuario y Operación**](manual_de_usuario.md) | Guía paso a paso de la interfaz visual, catálogo de nodos, controles de depuración (Breakpoints y Silenciado de Logs `≡`), el formato del archivo de flujo y consola de telemetría con trazabilidad por archivo. | Usuarios Finales & Administradores |
| 🤝 [**Guía de Contribución y Estándares**](contributing.md) | Principios de ingeniería en C# 14 / .NET 10, convenciones de código, flujo de trabajo con Git, creación de nodos y validación con la suite de pruebas. | Contribuidores & QA Engineers |
| 📜 [**Historial de Cambios (Walkthrough)**](PROJECT_WALKTHROUGH.md) | Registro cronológico por fechas de todas las implementaciones, optimizaciones de rendimiento y refactorizaciones del proyecto. Es la **ventana viva** del tramo en curso: el índice de su cabecera dice qué hito vive en cada archivo frío de [`docs/history/`](history/). | Todos los usuarios |

---

## ⚡ Inicio Rápido

Para compilar y ejecutar la aplicación con el SDK de **.NET 10** instalado:

```powershell
.\run.ps1
```

Para ejecutar la batería completa de pruebas automatizadas:

```powershell
dotnet test FileFlow.slnx
```

Para comprobar que el host compila en las familias soportadas:

```powershell
.\build-matrix.ps1            # desktop + web
.\build-matrix.ps1 -IncludeIos  # añade iOS/iPadOS (requiere macOS + Xcode)
```
