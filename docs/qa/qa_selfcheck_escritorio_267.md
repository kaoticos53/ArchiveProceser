# La sonda del escritorio: el trazo, el pan y el zoom con puntero inyectado (hito 267)

**Medido el 28 de septiembre de 2026** sobre el host de escritorio (`FileFlow.App`) compilado con `dotnet build`
(compilación 7039; el contador `.build_number` sigue avanzando con cada compilación de verificación, así que la
app puede declarar uno posterior). La medida se ejecutó con el comando del producto —`.\run-fast.ps1 -SelfCheck`
y, en su forma directa, `FileFlow.App\bin\Debug\net10.0\FileFlow.App.exe --selfcheck`— y **dos veces seguidas**,
comparando los informes.

## La frontera que se cierra

El hito 266 cerró la mitad visible de la frontera del 254 —el escritorio dibuja el cable con el trazador
compartido— y **declaró** la otra mitad, con estas palabras:

> **No se mide en la app del escritorio con puntero**: el escritorio no tiene sonda propia (las `--selfcheck*`
> son del host Uno). Lo que se mide aquí es la **figura** que el control va a pintar y el **árbol visual real** del
> lienzo headless; el trazo **con un dedo** queda para una sesión del escritorio.

Este tramo cierra esa frontera por los dos lados: el escritorio tiene sonda propia y la sonda **usa un puntero**.

## Lo que se construyó

| Pieza | Qué es |
| :--- | :--- |
| `FileFlow.App/SelfCheck/DesktopSelfCheck.cs` (nuevo) | La sonda: arranca la aplicación real (servicios, plugins, vistas y estilos del producto) y mide el lienzo en cuatro fases, con informe `selfcheck-report.txt` y veredicto por código de salida. |
| `FileFlow.App/Program.cs` | La puerta del modo `--selfcheck`, **antes** del arranque normal. |
| `FileFlow.App/App.axaml.cs` | La medida corre **con la ventana ya en pantalla** y el arranque **no lanza la comprobación de actualizaciones** en este modo. |
| `FileFlow.App/FileFlow.App.csproj` | `Avalonia.Headless` (plataforma headless con **Skia real**): la única forma de que un puntero entre por el pipeline de entrada de Avalonia desde dentro del proceso. |
| `run.ps1` · `run-fast.ps1` | `-SelfCheck`: pasa el argumento, **espera el proceso y hereda su código de salida**. |

### Las cuatro fases de la medida

| Fase | Qué se mide | Cómo |
| :--- | :--- | :--- |
| **El trazo** | Que cada cable del grafo esté dibujado con el control del host, que su curva sea la del **trazador compartido** (`ConnectionGeometry.BuildWire`) y que su extremo caiga **sobre el punto dibujado de su socket**. | La figura del `DefiningGeometry` contra el núcleo, punto por punto; el aterrizaje, con `TranslatePoint` al **espacio de la ventana**, contra la figura del socket (`PortSocketTemplate`). |
| **El pan** | Que arrastrar el fondo con el **botón derecho** mueva el encuadre el gesto dividido por el zoom, que el contenido siga a la mano y que el trazo no se despegue. | Puntero inyectado: mover, pulsar, **ocho tramos** y soltar. |
| **El zoom** | Que la rueda acerque, aleje y se detenga en el techo (`MaxViewportZoom`), y que el trazo siga sobre sus sockets **con la escala cambiada**. | Rueda inyectada sobre el fondo libre, con el aterrizaje re-medido en cada estado. |
| **El reparto de botones** | Que el **izquierdo** sobre el fondo libre **no** mueva el encuadre. | Arrastre izquierdo y comparación del encuadre antes/después. |

## El informe, literal

El informe completo queda junto al ejecutable
(`FileFlow.App\bin\Debug\net10.0\selfcheck-report.txt`, **4863 bytes**) y **es byte a byte idéntico entre dos
corridas** (comprobado con `diff`). Su cabecera declara la frontera del instrumento:

```
Sonda del host de escritorio (FileFlow.App --selfcheck)
plataforma: headless de Avalonia con Skia real · puntero inyectado por el pipeline de entrada del framework
(el dedo del sistema operativo NO entra aquí: eso es una sesión manual)
```

Y sus cifras, por fase:

| Medida | Valor observado |
| :--- | :--- |
| Lienzo materializado | **(0,0) 1075×565** |
| Escena | `flow_01_organizador_imagenes.json`: **3 nodos · 2 cables** |
| Cables dibujados con el control del host | **2 de 2** |
| Curva del cable | **la del núcleo** (nace en el ancla, muere en el ancla, el cuello es el suyo), en los dos cables |
| **Aterrizaje (en reposo)** | hueco **0,00 px** en el origen y **0,00 px** en el destino |
| **Pan** (arrastre `(120, 80)` px con el derecho, zoom 1,000) | encuadre `(0,0 → -120,0, -80,0)`; trazo desplazado `(120,0, 80,0)` —sigue a la mano—; huecos **0,00 px** después |
| **Zoom** (rueda) | `1,000 → 2,000` (una muesca arriba); `2,000 → 0,200` (dos abajo: el suelo `MinViewportZoom`) |
| **Zoom al tope** | **20 muescas → 2,500** (el techo), y el trazo sigue con huecos **0,00 px** |
| **El izquierdo sobre el fondo** | encuadre `(-120,0, -64,0)` antes y después: **no se mueve** |
| Recuento final | **41 `[OK]` · 0 `[FALLO]` · VERIFICADO** (código de salida **0**) |

## Cuatro cosas que sólo aparecen midiendo

1. **El gesto iba a la pieza flotante.** La primera corrida eligió el punto del gesto fijándolo en la esquina
   libre del lienzo… y ahí está la **barra de zoom**: el puntero pulsó **encima** de ella y la sonda midió el
   silencio del lienzo (el encuadre no se movía y el trazo tampoco). El punto se elige ahora **por hit-testing**
   —el primero cuyo recorrido de respuesta llega al lienzo y no a una tarjeta, un decorador, un cable ni la
   barra—; es la misma lección que el host Uno se llevó al medir su hit-testing (hito 249).
2. **La escena que se medía a sí misma.** El bucle de reintentos —copiado del host Uno, donde la medida recorre
   el árbol y puede repetirse sin consecuencias— aquí **mutaba**: cada intento paneaba y hacía zoom sobre el
   anterior, así que la corrida siguiente medía un encuadre con el zoom ya al tope que **ningún usuario ve**, y la
   fase del pan salía en rojo por un estado que la propia sonda había dejado. Ahora la espera **no mide** (espera
   a que el lienzo tenga tamaño) y la medida es **una sola vez**, sobre la escena que el producto presenta.
3. **El ancla medida contra la etiqueta del puerto.** Medir el aterrizaje contra el **control del conector** daba
   un hueco de **14,5 px**: es el ancho de la **etiqueta del puerto**, que vive dentro del mismo control. El punto
   que el usuario ve es la **figura del socket** (clases `socket`/`socketTriangle`), y ahí es donde Nodify sitúa el
   ancla del puerto que el cable enlaza: con esa referencia el hueco es **0,00 px**. La medida estaba bien; la
   referencia estaba mal, y el rojo lo dijo con sus dos puntos crudos.
4. **El arrastre de una tarjeta no es un gesto del lienzo.** La fase se midió y se **retiró** de la sonda, con su
   razón escrita: al pulsar una tarjeta, el producto **la lleva al frente** (lo que reordena la colección del
   documento) y, si el puntero se va hacia el borde, el editor **auto-paneea** mientras arrastra. Son dos efectos
   del propio producto que no tienen que ver con el reparto de botones que la sonda defiende; el arrastre de la
   tarjeta se mide en el suite, con la escena bajo control (y con una mutación que lo muerde).

## Las pruebas y las mutaciones

| Pieza | Casos |
| :--- | :--- |
| `FileFlow.Tests/Unit/Views/DesktopCanvasGestureTests.cs` (nuevo) | **4**: el trazo toca sus sockets con el plano quieto · el pan mueve el encuadre el gesto/zoom, el trazo sigue a la mano y no se despega · la rueda acerca, aleja y se detiene en el techo sin despegar el trazo · el arrastre de una tarjeta mueve el nodo y no el plano |
| `FileFlow.Tests/Unit/App/DesktopSelfCheckGuardTests.cs` (nuevo) | **4**: el modo en la línea de comandos y el anfitrión que inyecta · el veredicto por código de salida y su informe · la medida en espacio de ventana y **sin escribir el encuadre** · el arranque sin trabajo de fondo ni red |
| `pan-que-responde-al-boton-izquierdo` (mutación nueva) | **MUERDE** (32,1 s). Testigo: `TheRightDragOnTheCanvas_…` (con el paneo en el izquierdo, el derecho deja de mover el encuadre). Control: `TheLeftDragOnANodeCard_…`, **verde** —y está medido que el mutante **no** lo rompe: el arrastre de la tarjeta se queda el gesto antes de que el editor vea el paneo—. |
| `sonda-de-escritorio-sin-el-anfitrion-del-puntero` (mutación nueva) | **MUERDE** (29,3 s). Testigo: `TheProbe_ShouldBeItsOwnCommandLineMode_OnTheHostThatCanInjectAPointer`. Control: `TheCable_ShouldTouchItsSockets_InWindowSpace` (verde). |

`mutations/COVERAGE.md` regenerado por su guardia: **98 declaradas · 15 de 17 subsistemas · 17 de 45 guardias**
con una mutación que las muerde (antes **96 · 16 de 44**).

## Validación

| Pieza | Resultado |
| :--- | :--- |
| Compilación de `FileFlow.App` (XAML de Avalonia incluido) | **0 errores** |
| Sonda del escritorio (`. \run.ps1 -SelfCheck`) | **EXIT 0 · 41 `[OK]` · 0 `[FALLO]` · VERIFICADO**, informe **idéntico entre dos corridas** |
| Suite completa | **1950 superadas + 1 omitida de 1951, 0 errores** (cuatro corridas: 2 m 40 s, 2 m 45 s, 2 m 40 s y 2 m 44 s; antes **1942 + 1**) |
| Rojo intermitente | **una corrida trajo 1 fallo que no quedó nombrado** —la salida se cortó con un `tail`— y las **tres** siguientes (una de ellas **con compilación**, como la que falló) quedaron verdes con los mismos 1950. Es el ruido de carga que ya declararon los hitos 255, 258 y 262, no una regresión de este tramo |
| Mutaciones | **2 nuevas, las 2 MUERDEN** · **98 declaradas** |
| Sonda del host Uno | **no se re-ejecutó**: lo tocado es del escritorio y el host Uno no referencia `FileFlow.App` (referencia `FileFlow.App.Core`, `Sdk`, `Core` y sus plugins) |

## Lo que la sonda NO mide (declarado, y también en su propio informe)

- **El dedo del sistema operativo**: el puntero entra por el pipeline de entrada del framework (hit-testing,
  gestos del editor, captura de puntero) sobre la aplicación montada; **no** es el ratón del sistema sobre la
  ventana nativa. Una sesión con la ventana abierta sigue siendo otra cosa, y el informe lo dice en su cabecera.
- **El arrastre de una tarjeta** (razón arriba: lo mide el suite).
- **El píxel**: lo medido es la **geometría dibujada** proyectada al espacio de la ventana; que el trazo del
  escritorio y el del host Uno coincidan **píxel a píxel** no está medido.
- **Lo que la sonda toca del usuario**: nada. La escena la carga ella (un ejemplo del catálogo, con
  `WorkflowGraph.FromJson` + `LoadFromGraphModel`, el mismo camino del host Uno) y vive en memoria hasta que el
  proceso termina: ni preferencias, ni contadores de uso, ni flujos guardados.

## Cómo reproducirlo

```powershell
# La sonda y su veredicto (el script espera y hereda el código de salida)
.\run.ps1 -SelfCheck          # compila y mide
.\run-fast.ps1 -SelfCheck     # sin compilar

# El informe completo, junto al ejecutable
Get-Content FileFlow.App\bin\Debug\net10.0\selfcheck-report.txt

# La misma medida, dentro de la suite
dotnet test FileFlow.Tests --filter "FullyQualifiedName~DesktopCanvasGestureTests"
dotnet test FileFlow.Tests --filter "FullyQualifiedName~DesktopSelfCheckGuardTests"

# Las dos mutaciones del tramo
.\mutate.ps1 -Name pan-que-responde-al-boton-izquierdo,sonda-de-escritorio-sin-el-anfitrion-del-puntero
```
