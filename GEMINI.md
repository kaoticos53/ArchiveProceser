# Reglas de Proyecto - FileFlow Studio

## Reglas de Optimización de Contexto y Persistencia
- **Optimización de tokens:** No leas archivos de código fuente completos de forma preventiva. Utiliza primero las herramientas del servidor MCP (memoria/búsqueda rápida) para ubicar funciones, clases o líneas exactas antes de abrir un archivo.
- **Persistencia:** Al inicio de cada sesión, consulta `.antigravity/knowledge/repo_architecture.md` y la base de datos MCP local antes de escanear archivos. Actualiza la memoria MCP con cada cambio arquitectónico importante.
