# Centro de Documentación - FileFlow Studio

Bienvenido al centro de documentación técnica y manuales de usuario de **FileFlow Studio**, la plataforma modular de automatización y procesamiento masivo de archivos de ultra-alto rendimiento desarrollada en **C# 13**, **.NET 9** y **WPF**.

---

## 📚 Índice General de Documentos

| Documento | Descripción | Audiencia |
|---|---|---|
| 🏗️ [**Arquitectura y Diseño Técnico**](architecture.md) | Visión general del sistema, diagramas Mermaid.js, flujo de datos por capas, motor de telemetría SQLite In-Memory y Registros de Decisiones Arquitectónicas (ADRs). | Arquitectos de Software & Desarrolladores Core |
| 🚀 [**Guía de Instalación y Despliegue**](setup_and_deployment.md) | Requisitos del sistema, configuración del entorno local, script `.\run.ps1`, publicación autónoma (*self-contained*) y pipeline CI/CD en GitHub Actions. | Desarrolladores & Ingenieros DevOps |
| 🔌 [**Referencia de API y Módulos**](api_reference.md) | Documentación exhaustiva de contratos del SDK (`IFlowNode`, `FileItemContext`, `IFlowExecutionContext`), motor de plantillas y ejemplo completo de nodo personalizado. | Desarrolladores de Plugins & Integradores |
| 📖 [**Manual de Usuario y Operación**](user_guide.md) | Guía paso a paso de la interfaz visual, catálogo de los 24 nodos de producción, controles de depuración (Breakpoints y Silenciado de Logs `≡`), consola de telemetría con trazabilidad por archivo y FAQ. | Usuarios Finales & Administradores |
| 🤝 [**Guía de Contribución y Estándares**](contributing.md) | Principios de ingeniería en C# 13 / .NET 9, convenciones de código, flujo de trabajo con Git, creación de nodos y validación con la suite de pruebas. | Contribuidores & QA Engineers |
| 📜 [**Historial de Cambios (Walkthrough)**](PROJECT_WALKTHROUGH.md) | Registro cronológico por fechas de todas las implementaciones, optimizaciones de rendimiento y refactorizaciones del proyecto. | Todos los usuarios |

---

## ⚡ Inicio Rápido

Para compilar y ejecutar la aplicación inmediatamente en un entorno Windows con .NET 9 SDK instalado:

```powershell
.\run.ps1
```

Para ejecutar la batería completa de 178 pruebas automatizadas:

```powershell
dotnet test FileFlow.slnx
```
