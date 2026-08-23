# Guía de Contribución y Estándares de Ingeniería - FileFlow Studio

¡Gracias por tu interés en contribuir a **FileFlow Studio**! Este documento detalla las directrices de arquitectura, estilo de código en C# 13, flujo de trabajo con Git y cómo validar tus cambios antes de enviar un Pull Request.

---

## 1. Principios de Ingeniería y Versiones

1. **Runtime & Lenguaje**:
   - **Target Framework**: `net9.0` (y `net9.0-windows` para la capa de UI `FileFlow.App`).
   - **Versión de Lenguaje**: `C# 13` (`<LangVersion>13</LangVersion>`).
   - **Nullable Reference Types**: Activado estrictamente (`<Nullable>enable</Nullable>`). No se permiten advertencias de posibles desreferencias nulas sin mitigar.
   - **Primitivas de Sincronización**: Uso exclusivo de `System.Threading.Lock` en lugar de `object` para bloqueos de exclusión mutua.

2. **Desacoplamiento Estricto por Capas**:
   - `FileFlow.Sdk` debe ser puro: solo contratos e interfaces base. Cero dependencias de UI o librerías externas pesadas.
   - Los plugins (`FileFlow.Plugin.*`) solo pueden referenciar `FileFlow.Sdk` y sus respectivas librerías de dominio específicas.
   - La interfaz (`FileFlow.App`) consume `FileFlow.Core` y `FileFlow.Sdk` aplicando el patrón **MVVM** estricto con `CommunityToolkit.Mvvm`.

3. **Rendimiento Asíncrono e I/O**:
   - Todas las operaciones de disco o red deben ser 100% asíncronas (`ValueTask` / `Task`) con propagación obligatoria de `CancellationToken`.
   - Liberación determinista de recursos con `await using` y `using var`.
   - Cero asignaciones innecesarias en *hot paths* (uso de `ReadOnlySpan<T>`, memoización inmutable en `FileItemContext`).

---

## 2. Flujo de Trabajo con Git

### 2.1. Estructura de Ramas
- `main`: Rama de producción estable. Todo commit en `main` debe compilar y pasar el 100% de los tests.
- `feature/<nombre-feature>`: Nuevas funcionalidades o nuevos nodos.
- `fix/<nombre-bug>`: Corrección de incidentes o defectos.
- `refactor/<nombre-mejora>`: Mejoras de rendimiento o deuda técnica sin alterar contratos públicos.

### 2.2. Commits Semánticos
Utilizamos el estándar *Conventional Commits*:
- `feat: añade nodo SmartUnpack con auto-aplanado de directorios`
- `fix: corrige desincronización de radio buttons en filtros de telemetría`
- `perf: optimiza canalización transaccional en SqliteLogStore a >82k logs/s`
- `test: añade tests de integración para deduplicación criptográfica`
- `docs: actualiza manual de usuario con catálogo de 24 nodos`

---

## 3. Pasos para Crear un Nuevo Nodo de Procesamiento

1. **Ubicación**: Crea la clase en el proyecto de plugin correspondiente dentro de `FileFlow.Plugin.<Dominio>`.
2. **Implementación de Interfaz**: Implementa `IFlowNode`.
3. **Manejo de Telemetría**:
   - Mide el tiempo de ejecución con `Stopwatch`.
   - Emite logs estructurados mediante `context.Log(...)` indicando `nodeId`, `nodeName`, `durationMs`, `itemId` y un JSON estructurado con `detailsJson`.
4. **Pruebas Unitarias**: Añade una suite de pruebas xUnit en `FileFlow.Tests/Unit/Nodes/` que valide:
   - Procesamiento exitoso de archivos válidos.
   - Manejo de excepciones y errores controlados.
   - Respeto al token de cancelación `CancellationToken`.

---

## 4. Verificación Local y Batería de Pruebas

Antes de crear un commit o solicitar una revisión de código, ejecuta localmente:

```powershell
# 1. Compilación limpia
dotnet build FileFlow.slnx -c Release

# 2. Ejecución completa de pruebas
dotnet test FileFlow.slnx -c Release

# 3. Verificación de formato y linters
dotnet format --verify-no-changes
```

Asegúrate de que todas las pruebas (146+ tests) pasen con éxito al 100%.
