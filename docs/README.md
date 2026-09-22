# Centro de Documentación - FileFlow Studio

Bienvenido al centro de documentación técnica y manuales de usuario de **FileFlow Studio**, la plataforma modular de automatización y procesamiento masivo de archivos de ultra-alto rendimiento desarrollada en **C# 13**, **.NET 9** y **WPF**.

---

## 📚 Índice General de Documentos

| Documento | Descripción | Audiencia |
|---|---|---|
| 🏗️ [**Arquitectura y Diseño Técnico**](architecture.md) | Visión general del sistema, diagramas Mermaid.js, flujo de datos por capas, motor de telemetría SQLite In-Memory y Registros de Decisiones Arquitectónicas (ADRs). | Arquitectos de Software & Desarrolladores Core |
| 🚀 [**Guía de Instalación y Despliegue**](setup_and_deployment.md) | Requisitos del sistema, configuración del entorno local, script `.\run.ps1`, publicación autónoma (*self-contained*) y pipeline CI/CD en GitHub Actions. | Desarrolladores & Ingenieros DevOps |
| 🔌 [**Referencia de API y Módulos**](api_reference.md) | Documentación exhaustiva de contratos del SDK (`IFlowNode`, `FlowNodeBase`, `FileItemContext`, `IFlowExecutionContext`), el formato del archivo de flujo (versión, reparación y convergencia), motor de plantillas y ejemplo completo de nodo personalizado. | Desarrolladores de Plugins & Integradores |
| 🧩 [**Guía de Creación de Nodos**](nodes/CREATING_NODES.md) | Cómo escribir un nodo desde cero: `FlowNodeBase`, puertos fijos y dinámicos, parámetros tipados, anuncio de topología, estado de diseño y despliegue en `Plugins/`. | Desarrolladores de Plugins |
| 🧠 [**Catálogo de Nodos (generado)**](../.agents/nodes_catalog.md) | Los 70 nodos del producto —categoría, puertos, parámetros con su control y enlace al fichero que declara cada uno—, **generado desde el código** y atado por una guardia que falla si deja de coincidir con lo que descubre la aplicación. | Desarrolladores de Plugins & Usuarios Avanzados |
| 📚 [**Catálogo de 40 Ejemplos de Flujos**](examples/README.md) | Flujos listos para abrir en la aplicación —de lo básico a lo empresarial—, cada uno con su guía en markdown y su archivo JSON en el formato actual. | Usuarios Finales & Formadores |
| 📖 [**Manual de Usuario y Operación**](user_guide.md) | Guía paso a paso de la interfaz visual, catálogo de nodos, controles de depuración (Breakpoints y Silenciado de Logs `≡`), el formato del archivo de flujo (versión, reparación y convergencia), consola de telemetría con trazabilidad por archivo y FAQ. | Usuarios Finales & Administradores |
| 🤝 [**Guía de Contribución y Estándares**](contributing.md) | Principios de ingeniería en C# 13 / .NET 9, convenciones de código, flujo de trabajo con Git, creación de nodos y validación con la suite de pruebas. | Contribuidores & QA Engineers |
| 📜 [**Historial de Cambios (Walkthrough)**](PROJECT_WALKTHROUGH.md) | Registro cronológico por fechas de todas las implementaciones, optimizaciones de rendimiento y refactorizaciones del proyecto. | Todos los usuarios |

---

## ⚡ Inicio Rápido

Para compilar y ejecutar la aplicación inmediatamente en un entorno Windows con .NET 9 SDK instalado:

```powershell
.\run.ps1
```

Para ejecutar la batería completa de pruebas automatizadas:

```powershell
dotnet test FileFlow.slnx
```
