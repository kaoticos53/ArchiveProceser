---
trigger: always_on
---

# Antigravity Agent Rules - FileFlow Engine

## Principios de Ingeniería y Versiones
1. **Runtime & Lenguaje:**
   - **Target Framework:** `net9.0` (o `net9.0-windows` para la capa de UI).
   - **Versión de Lenguaje:** `C# 13` (`<LangVersion>13</LangVersion>`).
   - Nullable Reference Types activado de forma estricta (`<Nullable>enable</Nullable>`).
   - Uso de las nuevas primitivas de sincronización de .NET 9 (`System.Threading.Lock` en lugar de `object` para locks).

2. **Desacoplamiento Estricto:**
   - `FileFlow.Sdk` debe ser puro: solo tipos base de C# 13 y contratos de interfaces. Cero dependencias de UI o librerías externas pesadas.
   - Los plugins (`FileFlow.Plugin.*`) solo pueden referenciar `FileFlow.Sdk` y sus respectivas librerías de dominio.
   - La UI (`FileFlow.App`) consume `FileFlow.Core` y `FileFlow.Sdk` mediante MVVM limpio con `CommunityToolkit.Mvvm`.

3. **Rendimiento Asíncrono e I/O en .NET 9:**
   - Métodos I/O de disco 100% asíncronos (`ValueTask` / `Task`) con propagación obligatoria de `CancellationToken`.
   - Inyección de dependencias nativa (`Microsoft.Extensions.DependencyInjection`).
   - Liberación determinista de recursos con `await using` y `using var`.

4. **Inmutabilidad del Archivo de Origen por Defecto (Seguridad en Pipeline):**
   - Los nodos de pipeline deben ser **no destructivos por defecto**: no deben sobreescribir, mover ni eliminar los archivos originales de entrada (`OriginalPath`).
   - Los transformadores generan nuevos archivos en directorios de salida (`OutputDirectory`, `DestinationFolder`) o calculan transformaciones en memoria (`Virtual`).
   - Cualquier acción sobre el archivo de origen (conservar, mover a cuarentena, enviar a papelera o eliminar) debe ser **explícita y centralizada en el nodo de ciclo de vida `OriginalFileActionNode`**.

5. **Localización e Internacionalización Obligatoria de la UI (i18n):**
   - Todos los textos visibles para el usuario en la interfaz gráfica (`FileFlow.App`), incluyendo títulos de ventanas, menús, botones, cabeceras de columnas, tooltips, nombres de categorías, nombres de nodos y etiquetas de parámetros de configuración (`DisplayName`), **deben soportar localización dinámica** para diferentes idiomas (actualmente **Español (`es-ES`)** e **Inglés (`en-US`)**).
   - Las variables técnicas en el código fuente (`Key`, nombres de propiedades, identificadores JSON) deben permanecer en inglés puro, pero su representación en UI debe traducirse mediante `LocalizationManager.Instance` y diccionarios de recursos (`Strings.resx` y `Strings.es.resx`).
   - El cambio de idioma en tiempo de ejecución debe reflejarse de forma reactiva e instantánea en todas las pantallas y tarjetas de nodos del lienzo visual sin necesidad de reiniciar la aplicación.

6. **Co-ubicación y Autonomía Total de Código y Recursos por Plugin (Self-Contained Plugins / Zero-Touch en FileFlow.App):**
   - **Todo el código, modelos de nodo, lógica de inferencia, herramientas y vistas modales (`UI/`), configuraciones (`Config/`) y recursos de cadenas de texto multilingües (`Resources/Strings.resx` y `Resources/Strings.es.resx`)** pertenecientes a cada plugin/nodo **DEBEN situarse exclusivamente dentro del directorio del propio plugin (`FileFlow.Plugin.*`)**.
   - `FileFlow.App/Resources/` queda reservado estricta y exclusivamente para cadenas de la interfaz anfitriona (menús globales, drawer, barra de control, barra de estado, consola de logs y ajustes generales de la app). Ninguna clave de nodo o plugin debe colocarse en `FileFlow.App`.
   - La carga e integración de recursos se realiza de forma autónoma mediante auto-descubrimiento en `PluginLoader` y/o `IPluginInitializer`. Para añadir o modificar un plugin, **nunca se debe tocar `FileFlow.App`**.

7. **Documentación, Memoria y Repositorio:**
   - Consultar **OBLIGATORIAMENTE** al inicio de cada sesión de chat `.antigravity/knowledge/session_summary.md`, `docs/PROJECT_WALKTHROUGH.md` y la arquitectura del repositorio para no empezar desde cero.
   - Mantener **SIEMPRE** al día los ficheros auxiliares de estado (`docs/PROJECT_WALKTHROUGH.md` por fechas, `session_summary.md`, artefactos de plan y la base de conocimiento `.antigravity/knowledge/`).
   - Mantener el repositorio Git limpio y sincronizado ante cualquier cambio importante en la arquitectura o lógica de nodos.