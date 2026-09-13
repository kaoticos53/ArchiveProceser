# Secuencia de Prompts para Antigravity (.NET 9 & C# 13)

---
### Prompt 1: Creación del SDK y Contratos Base (.NET 9)
"Actúa como un arquitecto senior de .NET. Lee '.antigravity/rules.md' y '.antigravity/architecture.md'.
Genera el proyecto 'FileFlow.Sdk' configurado con TargetFramework 'net9.0' y C# 13:
1. `FileItemContext`: `record` con Id (Guid), CurrentPath, OriginalPath, IsDirectory, FileSizeBytes, Metadata (`Dictionary<string, object>`), Tags (`HashSet<string>`) y ExecutionLog.
2. `NodePort`: Definición de puertos tipados con dirección (Input/Output).
3. `IFlowNode` y `IFlowExecutionContext`: Contratos para ejecución asíncrona (`ValueTask`/`Task`), reporte de progreso con IProgress y emisión de datos por puertos.
4. Atributos de metadatos para descubrimiento de plugins (`[NodeDefinition]`, `[PortDefinition]`)."

---
### Prompt 2: Motor de Ejecución y Plugins Dinámicos (.NET 9)
"Lee '.antigravity/architecture.md' y el proyecto 'FileFlow.Sdk' generado.
Genera el proyecto 'FileFlow.Core' (net9.0) con:
1. `PluginLoader`: Cargador dinámico usando `AssemblyLoadContext` aislado para escanear y cargar plugins sin bloquear archivos DLL en Windows.
2. `WorkflowGraph`: Modelo de grafo (Nodos y Conexiones) con validación de ciclos (DAG) mediante ordenación topológica y validación de tipos de puertos.
3. `WorkflowExecutor`: Motor asíncrono con control de concurrencia usando `System.Threading.Lock` y `System.Threading.Channels` / `TPL Dataflow`, con soporte para pausar, cancelar (`CancellationToken`) y reporte de eventos en tiempo real."

---
### Prompt 3: Implementación de Módulos de Plugins (.NET 9)
"Basándote en '.antigravity/nodes_catalog.md' y referenciando exclusivamente 'FileFlow.Sdk' en net9.0:
1. Crea el proyecto 'FileFlow.Plugin.FileSystem' con los nodos FolderSource, DirectoryInspector, OriginalFileAction y DestinationSink.
2. Crea el proyecto 'FileFlow.Plugin.Archives' con el nodo SmartUnpack utilizando SharpCompress.
3. Crea el proyecto 'FileFlow.Plugin.Images' con ExifMetadataNode e ImageOptimizerNode utilizando MetadataExtractor y SixLabors.ImageSharp."

---
### Prompt 4: Interfaz de Usuario WPF con Nodify (.NET 9)
"Lee la especificación general e implementa el proyecto 'FileFlow.App' con TargetFramework 'net9.0-windows' y C# 13:
1. Integra la librería 'Nodify' y 'CommunityToolkit.Mvvm'.
2. Diseña la vista del editor de nodos con canvas interactivo (soporte para zoom, desplazamiento, conexión visual de puertos y selección múltiple).
3. Implementa el ViewModel principal que cargue los nodos dinámicamente desde el PluginLoader en una paleta lateral arrastrable (Drag & Drop).
4. Agrega barra de herramientas para Guardar/Cargar flujos en JSON (System.Text.Json con soporte para polimorfismo en .NET 9) y botones de Ejecutar/Pausar conectados a WorkflowExecutor."

---
### Prompt 5: Auditoría y Refactorización
Actúa como Arquitecto de Software Senior y Especialista en Clean Code. 

Quiero realizar una auditoría y refactorización completa del proyecto para mejorar su mantenibilidad, corregir errores y modularizar archivos excesivamente largos. No apliques cambios masivos de golpe; sigue estrictamente este flujo de trabajo por fases:

--- FASE 1: AUDITORÍA Y MAPA DE RIESGOS ---
1. Analiza la estructura del proyecto y genera un informe breve en Markdown que liste:
   - Archivos críticos o monolíticos (más de 300-500 líneas).
   - Posibles code smells, bugs latentes, fugas de memoria o cuellos de botella.
   - Dependencias circulares o alto acoplamiento.
2. Presenta un Plan de Modularización proponiendo cómo dividir los archivos grandes en módulos/componentes más pequeños bajo el Principio de Responsabilidad Única (SRP).
*Detente aquí y espera mi aprobación del plan antes de modificar código.*

--- FASE 2: REFACTORIZACIÓN MODULAR (Iterativa) ---
Una vez aprobado el plan, refactorizaremos módulo por módulo:
- Extrae lógica reutilizable a utilidades, servicios o subcomponentes dedicados.
- Tipa estrictamente variables y funciones; elimina código muerto y comentarios obsoletos.
- Mantén intactas las interfaces públicas y contratos de API para evitar romper dependencias externas.

--- FASE 3: VERIFICACIÓN Y TESTS ---
- Tras modificar cada módulo, ejecuta linters, formateadores o suites de tests existentes.
- Si no hay tests, genera tests unitarios básicos para las nuevas funciones extraídas.
- Confirma que la compilación/ejecución termine con 0 errores antes de pasar al siguiente archivo.

Comienza ejecutando únicamente la FASE 1 y muestra el informe inicial.

---
### Prompt 6: Optimizacion del codigo
Actúa como Ingeniero de Rendimiento de Software (Performance Engineering Specialist).

Quiero optimizar este código al límite técnico para maximizar el rendimiento por segundo, paralelizar cargas sobre todos los núcleos de CPU disponibles y minimizar el consumo y fragmentación de memoria (RAM).

Sigue este procedimiento técnico paso a paso:

--- 1. ANÁLISIS DE CUELLOS DE BOTELLA Y MEMORIA ---
- Identifica bucles críticos (hot paths), algoritmos de complejidad temporal/espacial ineficiente y operaciones bloqueantes.
- Detecta asignaciones de memoria redundantes, clonaciones innecesarias de objetos/estructuras, retenciones en memoria (memory leaks) y patrones que saturen el Garbage Collector / asignador de memoria.

--- 2. ESTRATEGIA DE PARALELISMO MULTINÚCLEO ---
- Propón la arquitectura de paralelización adecuada para este entorno (ej. thread pools, multiprocessing, web workers, rayon/OpenMP, tareas asíncronas no bloqueantes o batches paralelos).
- Diseña el reparto de carga para evitar contención por locks (usar estructuras lock-free, channels o atomics donde aplique) y maximizar el uso de todos los hilos lógicos de la CPU.

--- 3. OPTIMIZACIÓN DE MEMORIA Y BAJO NIVEL ---
- Implementa reutilización de buffers, object pooling o estructuras de datos contiguas en memoria para favorecer el acierto en caché L1/L2/L3 (cache locality).
- Aplica vectorización (SIMD), procesamiento por lotes (batching) o técnicas de streaming para evitar cargar colecciones completas en RAM.

--- 4. BENCHMARKING Y VALIDACIÓN ---
- Genera un script de benchmarking antes/después para medir:
  * Throughput (operaciones/segundo) y latencia.
  * Porcentaje de ocupación de núcleos de CPU.
  * Consumo pico de RAM (Resident Set Size).
- Asegúrate de que las optimizaciones mantengan la determinismo y exactitud en los resultados.

Comienza analizando el código actual, señalando los puntos críticos y presentando el plan de optimización antes de sobreescribir los archivos.

---
### Prompt 7: Auditoría de Errores y Seguridad
Actúa como QA Lead y Especialista en Seguridad y Depuración de Software.

Realiza un escaneo y análisis exhaustivo de errores en todo el proyecto. Examina el código buscando fallos de cualquier índole y genera un informe estructurado.

Categorías a auditar:
1. Errores Lógicos y Runtime: Excepciones no controladas, valores null/undefined/nil, desbordamientos, bucles infinitos, condiciones de carrera (race conditions) o estados inconsistentes.
2. Tipado y Compilación: Errores de tipos estrictos, coerción implícita indeseada, firmas de métodos desalineadas o parámetros faltantes.
3. Fugas y Recursos: Descriptores de archivos no cerrados, conexiones de base de datos o sockets huérfanos, event listeners no desvinculados y memory leaks.
4. Seguridad y Validación: Inyecciones (SQL, comandos, XSS), datos de entrada sin sanitizar, manejo inseguro de secretos/claves o dependencias vulnerables conocidas.
5. Concurrencia y Sincronización: Deadlocks, bloqueos excesivos, problemas de sincronización de hilos o mal uso de promesas/async/await.

Formato de salida requerido:
Genera una tabla o lista priorizada organizada por:
- Severidad: [Crítica | Alta | Media | Baja]
- Ubicación: Archivo y línea(s) aproximada(s).
- Descripción del fallo: Qué ocurre y bajo qué condición falla.
- Propuesta de solución: Código corregido sugerido o parche recomendado.

No apliques cambios en los archivos todavía; muestra primero el informe completo para revisar los hallazgos.

---
### Prompt 8: Generación de Tests
Actúa como Lead QA Automation Engineer y Especialista en Testing de Software.

Quiero generar una batería de tests exhaustiva y robusta que cubra todos los aspectos de la aplicación, garantizando una alta cobertura y resistencia a regresiones. 

Sigue esta metodología paso a paso:

--- 1. MAPEO DE ESCENARIOS Y CASOS DE PRUEBA ---
Identifica y documenta todos los casos de prueba antes de codificar, cubriendo:
- Happy Path: Flujos normales y comportamiento esperado con datos válidos.
- Edge Cases (Casos límite): Entradas vacías, nulas, caracteres especiales, colecciones gigantes, valores máximos/mínimos y desbordamientos.
- Error Handling: Excepciones forzadas, respuestas de error de red, timeouts, fallos de autenticación y datos corruptos.
- Concurrencia y Estado: Llamadas simultáneas, mutaciones de estado inesperadas y condiciones de carrera.

--- 2. ESTRUCTURA DE LA BATERÍA (Pirámide de Testing) ---
Organiza los tests en tres niveles usando el framework de pruebas del proyecto:
- Tests Unitarios: Aislando funciones, servicios y modelos puros mediante mocks/stubs de dependencias externas.
- Tests de Integración: Validando la interacción real entre módulos, contratos de API, bases de datos o middleware.
- Tests de Estrés / Rendimiento (si aplica): Verificando tiempos de respuesta y estabilidad bajo carga de datos pesada.

--- 3. ESTÁNDARES DE CÓDIGO DE TESTS ---
- Aplica el patrón AAA (Arrange, Act, Assert) o Given-When-Then de forma clara en cada test.
- Nombres descriptivos y semánticos (ej. `debe_lanzar_error_cuando_el_token_ha_expirado`).
- Tests deterministas: Sin dependencias del orden de ejecución ni efectos secundarios compartidos entre tests.

--- 4. EJECUCIÓN Y COBERTURA ---
- Genera y ubica los archivos de test en las carpetas correspondientes según las convenciones del repositorio.
- Proporciona el comando para ejecutar la suite completa y medir la cobertura de código (coverage report).

Empieza presentando un resumen de los módulos y casos de prueba que vas a cubrir; tras mi confirmación, genera el código de los tests módulo a módulo.

---
### Prompt 9: Modernización UI/UX
Actúa como Diseñador UI/UX Senior y Desarrollador Especialista en Aplicaciones Desktop de Windows.

Quiero modernizar y mejorar a fondo la interfaz gráfica (GUI) de este proyecto de Windows, optimizando la experiencia de usuario (UX), la consistencia estética y el rendimiento visual.

Sigue este flujo de trabajo estructurado:

--- 1. AUDITORÍA VISUAL Y ERGONOMÍA (UX) ---
Analiza las vistas, ventanas y controles actuales y genera un diagnóstico breve:
- Cumplimiento de principios Fluent Design (espaciado, tipografía, jerarquía visual, bordes redondeados y microinteracciones).
- Soporte para Modo Oscuro / Claro dinámico según la configuración del sistema operativo.
- Escalado y Per-Monitor High-DPI: Detección de elementos borrosos, fuentes desalineadas o ventanas que no escalan bien en pantallas 2K/4K.
- Accesibilidad: Contraste de colores, orden de tabulación con teclado y atajos rápidos (hotkeys).

--- 2. PLAN DE MODERNIZACIÓN DE COMPONENTES ---
Presenta una propuesta de rediseño detallando:
- Paleta de colores consistente, sistema de espaciado estándar (rejilla de 4px/8px) y tipografía nativa recomendada (ej. Segoe UI Variable).
- Reemplazo de controles obsoletos por equivalentes modernos (botones, inputs, barras de desplazamiento, diálogos modales y menús contextuales).
- Estados de retroalimentación: Spinners de carga asíncronos, animaciones sutiles de transición y placeholders para evitar ventanas congeladas o parpadeos (flicker).

--- 3. IMPLEMENTACIÓN Y ARQUITECTURA DE VISTAS ---
- Desacopla la lógica de negocio de la capa de presentación si están mezcladas (patrón MVVM, MVC o separación de eventos).
- Optimiza el renderizado gráfico (evitar redibujados innecesarios en el hilo principal de la UI).
- Organiza los estilos, recursos y temas en archivos centralizados y reutilizables.

Detén tu respuesta tras completar los puntos 1 y 2 para mostrarme el plan de diseño; una vez aprobado, comenzaremos a aplicar los cambios vista por vista.

---
### Prompt 10: Documentación Técnica
Actúa como Technical Writer Senior y Arquitecto de Software.

Quiero generar una suite completa de documentación técnica y manuales de usuario para este proyecto. Crea una carpeta `docs/` en la raíz del repositorio y organiza los documentos en archivos Markdown claros, estructurados y listos para producción.

Sigue esta estructura y contenido por archivo:

--- 1. ARQUITECTURA Y DISEÑO TÉCNICO (`docs/architecture.md`) ---
- Visión general del sistema, diagrama de arquitectura en formato Mermaid.js y flujo de datos principal.
- Descripción de capas/módulos, responsabilidades de cada componente y patrones de diseño utilizados.
- Decisiones técnicas clave (ADRs / Architecture Decision Records) justificando tecnologías y dependencias elegidas.

--- 2. GUÍA DE INSTALACIÓN Y DESPLIEGUE (`docs/setup_and_deployment.md`) ---
- Requisitos previos del sistema (versiones de runtime, dependencias de SO, herramientas CLI).
- Paso a paso para la configuración del entorno de desarrollo local y variables de entorno (`.env.example`).
- Instrucciones de compilación, empaquetado, ejecución y despliegue (incluyendo Docker / scripts CI/CD si aplica).

--- 3. REFERENCIA DE API Y MÓDULOS (`docs/api_reference.md`) ---
- Documentación de endpoints, contratos de interfaz, firmas de funciones públicas, parámetros, tipos y códigos de retorno/error.
- Ejemplos prácticos de peticiones y respuestas o fragmentos de código de uso común.

--- 4. MANUAL DE USUARIO (`docs/user_guide.md`) ---
- Guía orientada a usuarios finales o administradores sin jerga técnica innecesaria.
- Explicación paso a paso de las funcionalidades clave, flujos de trabajo habituales y capturas/esquemas de uso.
- Sección de resolución de problemas comunes (Troubleshooting / FAQ).

--- 5. GUÍA DE CONTRIBUCIÓN Y ESTÁNDARES (`docs/contributing.md`) ---
- Convenciones de código, nombrado, linters y formateo.
- Flujo de trabajo con Git (ramas, commits semánticos, pull requests).
- Cómo ejecutar la batería de tests y verificar cobertura localmente antes de enviar cambios.

--- 6. ÍNDICE PRINCIPAL (`docs/README.md`) ---
- Tabla de contenidos con enlaces relativos a todos los documentos anteriores.

Comienza analizando el código del proyecto para extraer la información real y genera los archivos dentro de `docs/` uno por uno.