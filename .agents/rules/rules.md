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

4. **Documentación, Memoria y Repositorio:**
   - Consultar **OBLIGATORIAMENTE** al inicio de cada sesión de chat `.antigravity/knowledge/session_summary.md`, `docs/PROJECT_WALKTHROUGH.md` y la arquitectura del repositorio para no empezar desde cero.
   - Mantener **SIEMPRE** al día los ficheros auxiliares de estado (`docs/PROJECT_WALKTHROUGH.md` por fechas, `session_summary.md`, artefactos de plan y la base de conocimiento `.antigravity/knowledge/`).
   - Mantener el repositorio Git limpio y sincronizado ante cualquier cambio importante en la arquitectura o lógica de nodos.