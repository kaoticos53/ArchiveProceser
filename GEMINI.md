# Reglas de Proyecto - FileFlow Studio

## Reglas de Optimización de Contexto, Memoria y Persistencia
- **Optimización de tokens:** No leas archivos de código fuente completos de forma preventiva. Utiliza primero las herramientas del servidor MCP (memoria/búsqueda rápida) para ubicar funciones, clases o líneas exactas antes de abrir un archivo.
- **Consulta Obligatoria al Inicio de Sesión:** Al inicio de CADA nueva sesión de chat, consulta OBLIGATORIAMENTE `.antigravity/knowledge/session_summary.md`, `docs/PROJECT_WALKTHROUGH.md` y `.antigravity/knowledge/repo_architecture.md` antes de escanear archivos, para cargar todo el contexto conversacional previo sin empezar de cero.
- **Mantenimiento Continuo de Ficheros Auxiliares:** Mantener **SIEMPRE** actualizados `docs/PROJECT_WALKTHROUGH.md` (registro cronológico por fechas), `.antigravity/knowledge/session_summary.md`, artefactos de plan y notas de arquitectura tras realizar cambios en el código.
- **Sincronización de Repositorio:** Garantizar que los cambios importantes del proyecto se mantengan limpios, probados y listos en el repositorio Git.
- **Inmutabilidad del Archivo de Origen:** Todo flujo debe ser no destructivo por defecto. Los archivos de origen (`OriginalPath`) permanecen intactos. La eliminación, traslado a cuarentena o envío a papelera del original solo debe ejecutarse mediante el nodo `OriginalFileActionNode`.
- **Localización e Internacionalización de la UI (i18n):** Todos los textos de la interfaz gráfica (`FileFlow.App`), incluyendo títulos, botones, menús, telemetría y parámetros de nodos (`DisplayName`), deben soportar localización dinámica (Español e Inglés). El código interno preserva claves en inglés, mientras que la UI consume `LocalizationManager.Instance` y diccionarios de recursos (`Strings.resx` y `Strings.es.resx`).
- **Co-ubicación y Autonomía Total de Plugins / Nodos:** Todo el código, modelos de nodo, lógica de inferencia, herramientas/vistas modales (`UI/`), configuraciones (`Config/`) y recursos de cadenas de texto multilingües (`Resources/Strings.resx` y `Resources/Strings.es.resx`) de cada plugin deben residir exclusivamente dentro de la carpeta del propio plugin (`FileFlow.Plugin.*`). `FileFlow.App/Resources/` queda reservado exclusivamente para cadenas de la interfaz anfitriona. Nunca colocar recursos ni claves de nodos en `FileFlow.App`.
- **Arquitectura de Adaptadores para Modelos de IA Intercambiables:** Los motores de inferencia con modelos intercambiables (`FileFlow.Plugin.AI`) deben emplear el patrón de adaptadores por familia de modelo (`I[Task]Adapter` + `[Task]AdapterFactory`). El nodo proporciona entradas canónicas (imágenes puras, umbrales, prompts) y cada adaptador gestiona su preprocesado geométrico exacto (Letterbox y des-padding, normalizaciones, tensores de embeddings CLIP ViT-B/32) y decodificación NMS.




