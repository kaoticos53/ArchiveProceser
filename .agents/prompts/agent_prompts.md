# Guías y Prompts Operativos para Agentes de IA (.NET 9 & C# 13)

Este documento contiene los playbooks y secuencias operativas activas para guiar tareas de auditoría, refactorización y extensión de **FileFlow Studio**.

> [!NOTE]
> Para consultar los prompts históricos de creación fundacional de la solución:
> 📄 [**`docs/history/2026-09-13_historical_prompts_archive.md`**](file:///docs/history/2026-09-13_historical_prompts_archive.md)

---

## 🛠️ Playbook 1: Auditoría y Refactorización Limpia (Clean Code)

```text
Actúa como Arquitecto de Software Senior y Especialista en Clean Code en .NET 9 y C# 13.
Realizaremos una auditoría y refactorización estructurada sin aplicar cambios masivos de golpe:

FASE 1: AUDITORÍA Y MAPA DE RIESGOS
1. Analiza la estructura del módulo e identifica:
   - Archivos que concentran múltiples responsabilidades (violación de SRP) o superan 300-400 líneas.
   - Posibles fugas de memoria (event handlers no desenganchados, disposables huérfanos).
   - Primitivas de sincronización obsoletas (usar System.Threading.Lock en lugar de object).
   - Acoplamiento indebido con capas externas (Zero-Touch en FileFlow.App).
2. Presenta un Plan de Modularización proponiendo la extracción a servicios o handlers especializados.
*Espera aprobación antes de modificar código.*

FASE 2: REFACTORIZACIÓN ITERATIVA
- Extrae lógica a componentes con responsabilidad única.
- Preserva 100% las firmas públicas de interfaces del SDK (`IFlowNode`, `IFlowExecutionContext`).
- Emplea I/O asíncrono con CancellationToken.

FASE 3: VERIFICACIÓN
- Ejecuta la suite de pruebas unitarias (`dotnet test` o `.\test.ps1`).
- Asegura compilación limpia con 0 errores y 0 advertencias (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`).
```

---

## 🧩 Playbook 2: Creación de un Nuevo Nodo o Plugin Autónomo

```text
Actúa como Desarrollador de Extensiones para FileFlow Studio.
Diseña e implementa un nuevo nodo siguiendo las reglas estrictas de co-ubicación y aislamiento:

1. CONTRATO Y METADATOS:
   - Hereda de `FlowNodeBase` o implementa `IFlowNode`.
   - Decora la clase con `[NodeDefinition(Name, Category, Description)]`.
   - Declara puertos de entrada/salida tipados en `Ports`.
   - Declara descriptores de parámetros en `ParameterDescriptors` si requiere configuración UI.

2. CO-UBICACIÓN Y AUTONOMÍA (Regla 6 Zero-Touch):
   - Todo el código, lógica de inferencia, herramientas modales (`UI/`), convertidores y configuraciones residen dentro de `FileFlow.Plugin.<Nombre>`.
   - Cadenas de texto localizadas añadidas exclusivamente a `Resources/Strings.resx` (EN) y `Resources/Strings.es.resx` (ES).

3. SEGURIDAD Y RENDIMIENTO:
   - Operaciones no destructivas por defecto.
   - Uso de `context.TempWorkspace` para temporales y `context.Log` para trazas de telemetría.
   - Declarar `MaxConcurrency` si interactúa con GPUs, servidores locales o recursos limitados.

4. PRUEBAS UNITARIAS:
   - Crear suite de tests en `FileFlow.Tests/Unit/Plugins/` validando éxito, cancelación y manejo de errores.
```