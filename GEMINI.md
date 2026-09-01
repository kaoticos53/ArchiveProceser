# Reglas de Proyecto - FileFlow Studio

## Reglas de Optimización de Contexto, Memoria y Persistencia
- **Optimización de tokens:** No leas archivos de código fuente completos de forma preventiva. Utiliza primero las herramientas del servidor MCP (memoria/búsqueda rápida) para ubicar funciones, clases o líneas exactas antes de abrir un archivo.
- **Consulta Obligatoria al Inicio de Sesión:** Al inicio de CADA nueva sesión de chat, consulta OBLIGATORIAMENTE `.antigravity/knowledge/session_summary.md`, `docs/PROJECT_WALKTHROUGH.md` y `.antigravity/knowledge/repo_architecture.md` antes de escanear archivos, para cargar todo el contexto conversacional previo sin empezar de cero.
- **Mantenimiento Continuo de Ficheros Auxiliares:** Mantener **SIEMPRE** actualizados `docs/PROJECT_WALKTHROUGH.md` (registro cronológico por fechas), `.antigravity/knowledge/session_summary.md`, artefactos de plan y notas de arquitectura tras realizar cambios en el código.
- **Sincronización de Repositorio:** Garantizar que los cambios importantes del proyecto se mantengan limpios, probados y listos en el repositorio Git.
- **Inmutabilidad del Archivo de Origen:** Todo flujo debe ser no destructivo por defecto. Los archivos de origen (`OriginalPath`) permanecen intactos. La eliminación, traslado a cuarentena o envío a papelera del original solo debe ejecutarse mediante el nodo `OriginalFileActionNode`.
- **Localización e Internacionalización de la UI (i18n):** Todos los textos de la interfaz gráfica (`FileFlow.App`), incluyendo títulos, botones, menús, telemetría y parámetros de nodos (`DisplayName`), deben soportar localización dinámica (Español e Inglés). El código interno preserva claves en inglés, mientras que la UI consume `LocalizationManager.Instance` y diccionarios de recursos (`Strings.resx` y `Strings.es.resx`).



