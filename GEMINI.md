# Reglas de Proyecto - FileFlow Studio

## Reglas de Optimización de Contexto, Memoria y Persistencia
- **Optimización de tokens:** No leas archivos de código fuente completos de forma preventiva. Utiliza primero las herramientas del servidor MCP (memoria/búsqueda rápida) para ubicar funciones, clases o líneas exactas antes de abrir un archivo.
- **Consulta Obligatoria al Inicio de Sesión:** Al inicio de CADA nueva sesión de chat, consulta OBLIGATORIAMENTE `.antigravity/knowledge/session_summary.md`, `docs/PROJECT_WALKTHROUGH.md` y `.antigravity/knowledge/repo_architecture.md` antes de escanear archivos, para cargar todo el contexto conversacional previo sin empezar de cero.
- **Mantenimiento Continuo de Ficheros Auxiliares:** Mantener **SIEMPRE** actualizados `docs/PROJECT_WALKTHROUGH.md` (registro cronológico por fechas), `.antigravity/knowledge/session_summary.md`, artefactos de plan y notas de arquitectura tras realizar cambios en el código.
- **Sincronización de Repositorio:** Garantizar que los cambios importantes del proyecto se mantengan limpios, probados y listos en el repositorio Git.



