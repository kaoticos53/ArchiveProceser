# Guía Maestra para Agentes de IA - FileFlow Studio

Este documento (`AGENTS.md`) define el protocolo operativo, los estándares técnicos y el mapa de ficheros auxiliares para cualquier agente de IA (Antigravity, Cursor, Claude Code, Copilot, Roo Code, Windsurf, etc.) que trabaje en este repositorio.

---

## 🚀 Protocolo de Arranque Obligatorio (Inicio de Sesión)

Antes de escanear archivos de código fuente o proponer cambios, **TODO AGENTE DEBE CONSULTAR** los siguientes ficheros en orden de prioridad:

1. **Estado y Memoria Reciente:**
   - 📄 [`.antigravity/knowledge/session_summary.md`](file:///.antigravity/knowledge/session_summary.md): Estado actual de la última sesión, hitos completados, tareas pendientes y decisiones de diseño activas.
2. **Historial Cronológico de Cambios:**
   - 📄 [`docs/PROJECT_WALKTHROUGH.md`](file:///docs/PROJECT_WALKTHROUGH.md): Bitácora histórica completa con registro de cambios por fecha, resultados de tests unitarios y cobertura.
3. **Arquitectura y Topología del Repositorio:**
   - 📄 [`.antigravity/knowledge/repo_architecture.md`](file:///.antigravity/knowledge/repo_architecture.md): Mapeo completo de proyectos, contratos de interfaz, flujo DAG y dependencias.
4. **Reglas y Estándares de Codificación:**
   - 📄 [`.agents/rules/rules.md`](file:///.agents/rules/rules.md) y [`GEMINI.md`](file:///GEMINI.md): Principios de ingeniería, versiones de lenguaje y directrices de persistencia.

---

## 🗺️ Mapa de Ficheros Auxiliares del Proyecto

| Fichero / Directorio | Propósito | Cuándo consultarlo / actualizarlo |
| :--- | :--- | :--- |
| [`.antigravity/knowledge/session_summary.md`](file:///.antigravity/knowledge/session_summary.md) | Resumen ejecutivo de la última sesión de desarrollo y siguientes pasos. | **Lectura:** Al iniciar sesión.<br>**Escritura:** Al finalizar cada sesión o cambio significativo. |
| [`docs/PROJECT_WALKTHROUGH.md`](file:///docs/PROJECT_WALKTHROUGH.md) | Bitácora cronológica exhaustiva de avances, refactorizaciones y métricas de tests. | **Lectura:** Contexto histórico.<br>**Escritura:** Registro obligatorio con fecha tras cada modificación. |
| [`.antigravity/knowledge/repo_architecture.md`](file:///.antigravity/knowledge/repo_architecture.md) | Documento vivo de la arquitectura de la solución, puertos y modelos. | **Lectura:** Antes de modificar contratos o estructuras de módulos.<br>**Escritura:** Al alterar contratos o añadir componentes estructurales. |
| [`.agents/rules/rules.md`](file:///.agents/rules/rules.md) | Reglas técnicas de .NET 9, C# 13, threading, asincronía y desacoplamiento. | **Lectura:** Antes de escribir código en cualquier módulo. |
| [`.agents/architecture.md`](file:///.agents/architecture.md) | Síntesis arquitectónica rápida (Microkernel, DAG Engine, FileItemContext). | **Lectura:** Consulta rápida de patrones del motor. |
| [`.antigravity/mcp.json`](file:///.antigravity/mcp.json) | Configuración de servidores MCP (memoria, filesystem, ripgrep) para búsqueda rápida sin lectura completa de archivos. | **Lectura:** Antes de explorar el repositorio o localizar símbolos. |

---

## ⚡ Optimización de Tokens (Regla Estricta)

> No leas archivos de código fuente completos de forma preventiva. Utiliza primero las herramientas del servidor MCP (memoria/búsqueda rápida, `ripgrep`) para ubicar funciones, clases o líneas exactas antes de abrir un archivo.
| [`.agents/nodes_catalog.md`](file:///.agents/nodes_catalog.md) | Catálogo de todos los nodos disponibles, puertos de entrada/salida y categorías. | **Lectura:** Al crear o modificar nodos o plugins. |
| [`.agents/prompts/agent_prompts.md`](file:///.agents/prompts/agent_prompts.md) | Guías y secuencias de prompts especializadas para auditoría, refactorización y extensión. | **Lectura:** Para guiar auditorías por fases o tareas complejas. |
| [`docs/architecture.md`](file:///docs/architecture.md) y [`docs/ARCHITECTURE_DEEP_DIVE.md`](file:///docs/ARCHITECTURE_DEEP_DIVE.md) | Documentación técnica profunda del diseño del sistema y flujo de datos. | **Lectura:** En tareas que involucren rediseño o extensiones mayores. |
| [`docs/api_reference.md`](file:///docs/api_reference.md) | Referencia de interfaces públicas del SDK y Core. | **Lectura:** Al consultar contratos de interfaces (`IFlowNode`, `IFlowExecutionContext`, etc.). |

---

## ⚙️ Principios Técnicos y Estándares de Código

1. **Plataforma y Lenguaje:**
   - **Target Framework:** `net9.0` (o `net9.0-windows` exclusivamente en la capa de UI `FileFlow.App`).
   - **Lenguaje:** `C# 13` (`<LangVersion>13</LangVersion>`).
   - **Tipos de referencia nulos activados:** `<Nullable>enable</Nullable>` de forma estricta.
   - **Sincronización moderna:** Usar `System.Threading.Lock` de .NET 9 en lugar de `object` para bloqueos.

2. **Desacoplamiento Estricto por Capas:**
   - **`FileFlow.Sdk`**: Debe permanecer puro. Solo tipos base de C# 13 y contratos de interfaces. Sin dependencias de UI ni librerías pesadas.
   - **`FileFlow.Plugin.*`**: Solo pueden referenciar `FileFlow.Sdk` y sus respectivas librerías de dominio (ej. `SharpCompress`, `ImageSharp`, `MetadataExtractor`). Nunca referenciar `FileFlow.Core` ni `FileFlow.App`.
   - **`FileFlow.Core`**: Orquestador del motor DAG, carga dinámica de plugins (`AssemblyLoadContext`), ejecución en canales (`System.Threading.Channels` / `TPL Dataflow`) y serialización polimórfica.
   - **`FileFlow.App`**: Capa de presentación WPF con Nodify y `CommunityToolkit.Mvvm`.

3. **I/O Asíncrono y Rendimiento en .NET 9:**
   - Métodos I/O de disco 100% asíncronos (`ValueTask` / `Task`) con propagación obligatoria de `CancellationToken`.
   - Liberación determinista de recursos con `await using` y `using var`.
   - Inyección de dependencias nativa (`Microsoft.Extensions.DependencyInjection`).

4. **Inmutabilidad del Archivo de Origen por Defecto (Seguridad en Pipeline):**
   - Los pipelines son **no destructivos por defecto**: los archivos de entrada (`OriginalPath`) no se modifican ni se destruyen.
   - Los nodos de transformación crean archivos nuevos en carpetas destino (`OutputDirectory`, `DestinationFolder`) o transforman metadatos en memoria (`Virtual`).
   - La manipulación del archivo de origen (conservar, mover a cuarentena, enviar a papelera o borrar) está centralizada exclusivamente en `OriginalFileActionNode`.

5. **Localización e Internacionalización Obligatoria de la UI (i18n):**
   - Todos los textos visibles en la interfaz de usuario (`FileFlow.App`), incluyendo menús, botones, telemetría, tooltips, nombres de categorías, nombres de nodos y etiquetas de parámetros de configuración (`DisplayName`), **deben soportar localización dinámica** (actualmente **Español (`es-ES`)** e **Inglés (`en-US`)**).
   - Las claves y variables en el código se mantienen en inglés, mientras que la UI consume `LocalizationManager.Instance` y diccionarios de recursos (`Strings.resx` y `Strings.es.resx`).
   - El cambio de idioma debe reflejarse en caliente e instantáneamente en todas las vistas sin reiniciar la aplicación.

6. **Optimización de Tokens para Agentes:**
   - **No leer archivos completos preventivamente.** Utilizar herramientas de búsqueda (`grep_search`, `find_symbol`) para inspeccionar líneas o funciones específicas.

---

## 🛠️ Comandos y Scripts de Validación

Para validar cualquier cambio, el agente debe ejecutar las suites de prueba correspondientes:

```powershell
# Ejecutar todas las pruebas unitarias e integración
.\test.ps1

# Ejecutar pruebas y generar reporte de cobertura de código
.\coverage.ps1

# Compilar y ejecutar la aplicación WPF
.\run.ps1

# Limpiar todos los artefactos de compilación, binarios y temporales
.\clean.ps1
```

---

## 🔄 Protocolo de Cierre / Mantenimiento Continuo

Al terminar cualquier tarea o sesión de trabajo, el agente **DEBE**:
1. **Actualizar [`docs/PROJECT_WALKTHROUGH.md`](file:///docs/PROJECT_WALKTHROUGH.md):** Añadir una entrada cronológica con la fecha actual, resumen de cambios realizados y estado de pruebas.
2. **Actualizar [`.antigravity/knowledge/session_summary.md`](file:///.antigravity/knowledge/session_summary.md):** Reflejar el estado actual del repositorio, decisiones de diseño y próximos pasos para la siguiente sesión.
3. **Verificar que la suite de tests pasa al 100% (`dotnet test` o `.\test.ps1`).**
