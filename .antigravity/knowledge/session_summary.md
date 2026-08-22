# Resumen Consolidado de Sesiones y Memoria de Proyecto - FileFlow Studio

Este documento se actualiza al finalizar cada sesión de trabajo para consolidar los puntos clave, decisiones arquitectónicas, capacidades del sistema y el estado de la solución, evitando empezar desde cero en futuras conversaciones.

---

## 1. Estado Actual del Repositorio y Calidad
- **Target Framework**: `.NET 9` (`net9.0` / `net9.0-windows` para WPF UI).
- **Lenguaje**: `C# 13` (`<LangVersion>13</LangVersion>`), Nullable activado de forma estricta.
- **Estado de Compilación**: `dotnet build FileFlow.slnx` $\rightarrow$ **0 Advertencias, 0 Errores**.
- **Suite de Pruebas**: `.\test.ps1` $\rightarrow$ **85 / 85 Pruebas Pasadas con Éxito** (Unit & Integration tests en xUnit).
- **Git**: Repositorio sincronizado con suite de pruebas al 100%.

---

## 2. Nodos y Funcionalidades Recientemente Desarrolladas

### A. Migración Completa a Rutas Relativas por Defecto en Todos los Nodos
- **Rutas Relativas en Nodos**: Se eliminaron las rutas absolutas residuales (`C:\...`) de `FolderSourceNode`, `DestinationSinkNode`, `OriginalFileActionNode`, `SmartUnpackNode` e `ImageOptimizerNode`.
- **Anclaje Dinámico Global**: Todos los nodos utilizan ahora plantillas relativas (`{RelativeDir}\Input`, `{RelativeDir}\Output`, `{RelativeDir}\Unpacked`, etc.), las cuales son resueltas dinámicamente por `ParameterHelper.ResolveOutputPath` combinándolas con la **Ruta de Salida Global** persistida en Ajustes de la Aplicación.

### B. Actualización Dinámica de Parámetros de Preset y Eliminación de FFmpegPath Redundante
- **Actualización Automática de Argumentos (`NodeViewModel.cs`)**: Al seleccionar un preset en la tarjeta del nodo `Transcodificar Media`, el campo `CustomArguments` se actualiza de forma instantánea con la configuración FFmpeg del preset elegido.
- **Uso Estricto de Herramienta Global (`MediaTranscoderNode.cs`)**: Se eliminó el parámetro `FFmpegPath` de las tarjetas del nodo y se configuró para usar directamente la ruta de `ExternalToolsService` definida en Ajustes de la Aplicación.

### C. Solución Definitiva a la Selección de Presets en ComboBox (`IsEditable=False`)
- **Ajuste WPF (`NodeParameterTemplates.xaml`)**: Se estableció `IsEditable="False"` en el `ComboBox` de opciones de parámetros para garantizar una selección limpia y libre de fallos de foco.

---

## 3. Capa SDK y Motor de Plantillas (`FileFlow.Sdk`)
- **`NodePort`**: Record extendido con parámetro opcional `Description`.
- **`ParameterHelper`**: Extracción inteligente global de unidades de tiempo, porcentajes y dimensiones.

---

## 4. Reglas de Mantenimiento Memorizadas
1. **Consulta al Inicio de Sesión**: Consultar siempre `.antigravity/knowledge/session_summary.md`, `docs/PROJECT_WALKTHROUGH.md` y `.antigravity/knowledge/repo_architecture.md` antes de escanear archivos.
2. **Actualización Continua**: Mantener actualizados `docs/PROJECT_WALKTHROUGH.md` (por fechas), `.antigravity/knowledge/session_summary.md` y los artefactos de plan ante cualquier modificación de código.
3. **Repositorio Git**: Garantizar que el repositorio Git permanezca limpio, probado y sincronizado.
