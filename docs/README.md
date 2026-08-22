# Centro de Documentación - FileFlow Studio

Bienvenido al centro de documentación técnica y manuales de usuario de **FileFlow Studio**, el sistema de automatización y procesamiento masivo de archivos de ultra-alta flexibilidad desarrollado en **C# 13**, **.NET 9** y **WPF**.

---

## 📚 Índice General de Documentos

| Documento | Descripción | Público Objetivo |
|---|---|---|
| 🏗️ [**Arquitectura y Diseño Técnico**](architecture.md) | Visión general del sistema, diagrama Mermaid.js, flujo de datos, diseño por capas y Registros de Decisiones Arquitectónicas (ADRs). | Arquitectos de Software & Desarrolladores Core |
| 🚀 [**Guía de Instalación y Despliegue**](setup_and_deployment.md) | Requisitos previos, entorno de desarrollo local, compilación, script `.\run.ps1`, empaquetado para distribución y CI/CD. | Desarrolladores & Ingenieros DevOps |
| 🔌 [**Referencia de API y Módulos**](api_reference.md) | Documentación de contratos del SDK (`IFlowNode`, `FileItemContext`), motor de plantillas de variables, firmas de métodos y ejemplos. | Desarrolladores de Plugins & Integradores |
| 📖 [**Manual de Usuario y Operación**](user_guide.md) | Guía paso a paso de la interfaz visual, catálogo de 22 nodos, gestor de presets multimedia, contraseñas, simulación *Dry Run* y FAQ. | Usuarios Finales & Administradores |
| 🤝 [**Guía de Contribución y Estándares**](contributing.md) | Convenciones de código en C# 13, workflow de Git, guía para crear nuevos nodos y ejecución de la suite de pruebas. | Contribuidores & QA Engineers |
| 💡 [**Biblioteca de 40 Ejemplos Ejecutables**](examples/README.md) | Catálogo de 40 plantillas de flujos de trabajo organizadas en 4 niveles de complejidad con archivos `.json` listos para importar. | Todos los usuarios |
| 📜 [**Historial de Cambios (Walkthrough)**](PROJECT_WALKTHROUGH.md) | Registro cronológico por fechas de todas las implementaciones, auditorías de seguridad y refactorizaciones del proyecto. | Todos los usuarios |

---

## ⚡ Inicio Rápido

Para compilar y ejecutar la aplicación inmediatamente en un entorno Windows con .NET 9 SDK instalado, abre una consola de PowerShell en la raíz del repositorio y ejecuta:

```powershell
.\run.ps1
```

Para ejecutar la batería completa de 115 pruebas unitarias e integración:

```powershell
dotnet test FileFlow.slnx
```
