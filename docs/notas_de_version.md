# Notas de versión — FileFlow Studio

**Versión 1.0.0 · compilación 5129 · 23 de septiembre de 2026**

Estas notas recogen **dos tramos**:

- **El del rediseño visual** (compilación 4743 → 5018): el aspecto, los estados de los controles, el arranque y
  la infraestructura de pruebas que lo sostiene. Son los apartados **1 a 3**.
- **El de la ejecución de flujos** (apartado **4**): el que explica por qué **un flujo que se cortaba en silencio
  ahora llega al final**, y qué errores dejan de ser invisibles. Nació de un flujo real que no terminaba.

Están escritas en dos mitades a propósito —**lo que ves** al usar la aplicación y **lo que no se ve** pero es lo
que impide que lo primero se rompa sin que nadie se entere—. Todo lo que se afirma aquí está medido en el
registro técnico ([`PROJECT_WALKTHROUGH.md`](PROJECT_WALKTHROUGH.md), hitos 169 a 190): las cifras salen de ahí,
no de la memoria. El tramo que sigue (hitos 191 a 193) —cómo se resuelven los puertos de un flujo al abrirlo, y
la infraestructura de pruebas que vigila todo lo anterior— tendrá su apartado aquí cuando cierre.

---

## 1. Lo que ves

### El arranque

- **La versión ya no sale duplicada.** La primera pantalla mostraba «vv1.0.0…»: ahora muestra la versión como la
  muestra «Acerca de».
- **El barrido de la barra de progreso funciona.** Moría en su primer fotograma unos dos segundos después de
  arrancar, así que la barra se quedaba quieta durante el resto de la carga; además cada arranque dejaba una
  entrada en el registro de incidentes (711 bytes medidos por inicio). Ahora un arranque de 18 segundos deja
  **0 bytes** de crecimiento y la barra se anima hasta el final.
- **El barrido sigue al tema.** Cambia de tema y el barrido se reconstruye con el acento nuevo en lugar de
  conservar los colores del arranque.

### Los controles deshabilitados, legibles

Era el defecto más visible del tramo: en el tema claro un control deshabilitado tenía su texto casi blanco
sobre fondo claro, y en los campos el texto se leía con el gris del tema base.

| Pieza deshabilitada | Oscuro (antes → ahora) | Claro (antes → ahora) |
| :--- | ---: | ---: |
| Botón y conmutador | 2,13:1 → **4,88:1** | 1,20:1 → **4,82:1** |
| Campo de texto | 4,00:1 → **4,88:1** | 3,30:1 → **4,82:1** |
| Desplegable | 3,50:1 → **4,88:1** | 2,62:1 → **4,82:1** |

Además, el estado deshabilitado **conserva la identidad de la pieza**: los botones de acento se apagan con su
propio color en vez de con un gris común, y los que no tienen fondo (fantasma, enlace, icono) siguen sin tenerlo
en lugar de ganar una caja gris. La cara de una pieza deshabilitada ya no depende de lo que haya detrás: se ve
igual sobre la barra de control, sobre una tarjeta o dentro de una ventana.

### Estados que antes mentían

- **El desplegable ya no se oscurece al pasar el puntero** en el tema oscuro; ahora se ilumina, como en el claro.
- **Un conmutador de icono activado se enciende al pasar el puntero**: antes «activado con el puntero encima» se
  veía exactamente igual que «activado».

### El lienzo y el editor

- **Renombrar un nodo en el sitio ya se confirma.** `Enter` confirma y `Escape` descarta. Antes los dos abrían la
  caja y se quedaban ahí: había que hacer clic fuera y, aun así, el nombre podía no aplicarse.
- **El color que eliges en el selector de color ya no se pierde.** Al cambiar de tema —o al pasar por el Estudio
  de temas— el muestrario volvía al acento del tema en silencio.

### Los avisos de la interfaz

- El aviso de **«copiado»** dura lo que declara (1,5 s), un segundo clic reabre su ventana y, si el portapapeles
  falla, no se anuncia una copia que no ha ocurrido.
- El **pulso de energía** de un cable: al conectar en ráfaga, el vencimiento de un pulso viejo ya no apaga el
  pulso nuevo.

---

## 2. Lo que no se ve (y sostiene lo anterior)

### Un solo servicio de latidos

Las cuatro tareas periódicas del producto —vigilante de subflujos (1 s), volcado de la consola (40 ms), muestreo
de rendimiento (1 s) y fotograma visual de la ejecución (33 ms)— tenían cada una su copia del mismo ritual de
veinte líneas. Ahora se **declaran** en un único registro del que la aplicación puede enumerar qué late y con qué
periodo: añadir una nueva es declararla, un nombre duplicado falla en voz alta y su entrega está protegida, de
modo que una excepción dentro de un latido deja constancia en lugar de tumbar la aplicación.

### Un reloj que la prueba controla

Los cuatro latidos y las animaciones de la interfaz corren en las pruebas bajo un reloj que avanza cuando la
prueba lo dice. Antes, afirmar el valor final de una transición costaba esperar tiempo real —una transición de
120 ms quedaba al 30 %, al 80 % o al 100 % según la carga de la máquina—, así que una prueba que «pasaba» podía
no estar midiendo nada. Es lo que permite ahora **medir la cadencia**: ni un tick antes del periodo, uno por
periodo, ninguno después de parar el latido.

### La capa de interacción, por fin bajo prueba

Hasta este tramo el suite no simulaba **ni un clic, ni una tecla, ni un arrastre**: los estados de estilo, los
atajos de teclado, el arrastre de un nodo del cajón al lienzo y el foco del buscador rápido sólo se veían usando
la aplicación. Ahora se simulan con entrada real y se afirma el valor exacto de cada estado.

Las tres correcciones visibles de la sección anterior salieron de tres preguntas distintas, y las tres son de
esta mitad del trabajo: el **barrido de la splash** murió durante varios hitos porque su camino sólo corría en la
aplicación real (se descubrió midiendo el registro de incidentes de un arranque de verdad, y ahora hay una
prueba que ejecuta su tick); el **renombrado que no confirmaba** se encontró al pulsar `Enter` de verdad sobre el
control, cosa que antes no hacía ninguna prueba; y el **muestrario que perdía el color** apareció al buscar por
todo el código el patrón «escribir donde el tema también escribe», que es hoy una regla que se comprueba sola.

### El aspecto, congelado en imágenes

- **37 capturas de referencia** (antes 29), entre ellas el **tablero de estados** del sistema de diseño —cada
  pieza repetida una vez por estado— y las superficies del producto **en tema claro**, que no existían.
- El aspecto de un botón, un campo, una pestaña o un chip en reposo, con el puntero encima, pulsado, con foco,
  deshabilitado y seleccionado tiene hoy contrato de imagen y no sólo de token.
- La comparación de imágenes **no ve un detalle de 1 px** (un borde de foco es el 0,1 % de la imagen), así que
  además hay **sondas de píxel** que exigen el color exacto en un punto concreto y guardias de **contraste medidas
  sobre el píxel que de verdad se pinta**: son las que hoy exigen que ningún control deshabilitado vuelva a
  quedar ilegible, y las que hacen que un token correcto pintado mal no pase por bueno.

### Guardias que impiden que esto vuelva

- **Ningún temporizador ni espera sin decisión.** El inventario del trabajo aplazado se recalcula del código en
  cada ejecución: un sitio nuevo sin una prueba que lo ejercite o sin un motivo escrito rompe el suite. Son 17
  sitios (15 ejercitados y 2 declarados de tiempo real, que son los que por diseño esperan de verdad).
- **El código no puede escribir en una propiedad que el tema posee** y dar por hecho su valor. Es el patrón que
  mató el barrido de la splash y que había dejado al selector de color sin recordar el color elegido.
- **Ninguna regla puede atenuar dos veces el texto de un control deshabilitado**, y su contraste se exige en los
  **8 temas incluidos** —no sólo en los dos por defecto—, además de sobre el píxel que de verdad se pinta.

### El suite, determinista

- Se cerró una **carrera real** que hacía fallar de forma intermitente una prueba de la consola: una clave de
  idioma del host cambiaba de valor a mitad de la ejecución porque el diccionario se registraba de forma perezosa.
  El host registra ya sus cadenas al arrancar, que es lo que hace la aplicación.
- El tramo cierra con **1577 pruebas superadas** (1 omitida) frente a las 1475 del inicio, y con las capturas de
  referencia verificadas en la misma corrida.

---

## 3. Lo que sigue viéndose así (conocido)

- **El indicador de la pestaña activa pinta el azul del tema base** en lugar del acento del sistema de diseño: el
  color viene fijado en la plantilla del control, así que un estilo no puede ganarle; necesita una plantilla
  propia.
- **`F2` abre el renombrado pero no lleva el foco a la caja**: hay que hacer clic en ella para escribir.
- El **tema claro** ya tiene referencia visual en la barra de control y en el Estudio de temas, pero no en el
  resto de paneles ni en las ventanas del producto.
- Las piezas deshabilitadas de dos temas incluidos (`nord_slate` y `dracula_purple`) quedan en el mínimo que
  exige un control inactivo (3,36 y 3,58:1) porque su acento es claro sobre superficie oscura; subirlo borraría
  el color de la variante, que es justo lo que el estado conserva.

---

## 4. El tramo de la ejecución de flujos (compilación 5018 → 5129)

Este tramo no cambia cómo se ve la aplicación: cambia **si tus flujos terminan** y si un fallo se queda en
silencio. Nació de un parte concreto —un flujo de recomprimir cómics que se cortaba en la mitad y terminaba «en
verde»— y su resultado se mide así: los nodos que antes llegaban a `Completed` eran **dos**; ahora llegan **los
cuatro**.

### Lo que ves

**El flujo que se cortaba a la mitad, ahora llega al final.** El caso reportado era carpeta origen → desempaquetar
→ optimizador de imágenes → empaquetar. La consola sólo mostraba los registros de los dos primeros nodos y la
ejecución terminaba **sin error y sin una línea que explicara nada**; los dos últimos nodos no se ejecutaban
nunca. La causa era un nombre de puerto que no existía: el nodo que desempaqueta **dibujaba** su salida con el
nombre `Out` y **emitía** los archivos extraídos por otro; el motor busca el cable por el nombre
exacto y, al no encontrarlo, daba cada archivo por terminado —indistinguible de «este nodo ya no tiene nada más
que hacer»—. Ahora emite por el puerto que la interfaz dibuja: el flujo recorre los cuatro nodos y el archivo
recomprimido aparece en la carpeta de destino con su nombre original y, dentro, la página ya optimizada.

**El mismo defecto estaba en dos nodos más, y sus salidas ya son puertos de verdad** —visibles en la tarjeta y
conectables—:

| Nodo | Salida que existía sin ser visible | Qué pasaba antes |
| :--- | :--- | :--- |
| Desempaquetar (Fan-Out) | su error, y el camino feliz por el nombre equivocado | el flujo entero se cortaba en silencio y terminaba en verde |
| Insertar en base de datos (SQLite) | su error | un fallo al escribir en la base **se perdía sin dejar rastro** |
| Renombrar (avanzado) | su error y sus omisiones | los archivos fallidos y los omitidos **desaparecían** |

Además, el **catálogo de nodos** se regeneró con esos tres nodos: la interfaz ofrece hoy las salidas que el nodo
usa de verdad.

**La consola avisa cuando un nodo emite por un puerto que no declara.** El aviso lleva el nodo y el nombre exacto
del puerto, y aparece **una vez por nodo, puerto y ejecución** —no una por archivo—: un lote de tres archivos con
el mismo defecto deja **una** línea, no tres. Es lo que hace visible este defecto cuando venga de un nodo de un
plugin que no está en este repositorio, donde ninguna auditoría del código llega.

**El renombrado por lotes ya no renombra con un nombre que nadie configuró.** Con varios archivos a la vez, el
segundo podía caer en la plantilla por omisión del nodo —un nombre del tipo `FF_…_20260923_….txt`— sin avisar,
porque la migración de los parámetros antiguos ocurría mientras el lote corría en paralelo. Ahora esa migración se
resuelve **una vez por nodo**, antes de tocar ningún archivo: el nombre que sale es el que configuraste.

**Un detalle práctico si vuelves a probar un flujo con puntos de interrupción:** en **Depurar** el flujo se
detendrá en ellos esperando «Continuar» —es el comportamiento esperado, no un corte—; con **Ejecutar** llega al
final.

### Lo que no se ve (y sostiene lo anterior)

- **Las salidas que no son el camino feliz tienen contrato de ejecución.** Veinticuatro pruebas ejecutan el
  **motor real** con el cableado real —un origen de prueba → el nodo del caso → un espía que declara dos
  entradas—, de modo que **el puerto por el que llega el archivo es, en sí mismo, la afirmación**. Se cubre así:
  el error de extracción de un archivo corrupto y el de un comprimido **sin archivos dentro** (dos ramas de la
  misma salida, distintas por su carga), el rechazo de un **nombre de tabla inseguro** (y que no quede ninguna base
  de datos en el disco), los archivos **omitidos** porque su destino ya existía, un **origen que no existe**, un
  **protocolo sin estrategia** de red (sin abrir conexión), un **ejecutable de CLI que no existe** y una **URL de
  webhook sin esquema**.
- **Una guardia del código vigila los nombres de puerto sin ejecutar nada**: compara, clase por clase, los puertos
  **declarados** con los **emitidos** —resolviendo las constantes que usa el producto— sobre los trece proyectos
  de plugins. Juzga **69 clases de nodo** y **aplaza 3** con su motivo escrito (las que calculan sus puertos en
  ejecución). Es la que encontró los tres nombres que no existían; su primera redacción pasaba en verde con toda
  la familia de nodos de inteligencia artificial invisible, porque miraba sólo la clase base directa.
- **Un inventario de las salidas de error y de omisión del producto**: **23 nodos y 24 pares nodo·puerto**, de los
  que **23 los ejecuta una prueba** y **2 se declaran imposibles de forzar con una entrada**, con el motivo escrito
  en vez de dejar la casilla vacía. El inventario encontró **tres ramas que una búsqueda por texto no ve** (emiten
  a través de una constante, no de un texto literal) y hoy **una salida de rama nueva rompe la suite hasta que se
  declare**: es el trato que impide que la lista caduque al añadir el próximo nodo.
- **El flujo del parte, como prueba de extremo a extremo**: se ejecuta con el motor real y se afirma que **los
  cuatro nodos llegan a `Completed`** y que dentro del archivo reempaquetado está la página optimizada.
- **Las cifras del tramo**, medidas en cada hito: **1580 → 1598 → 1626 pruebas superadas** (1 omitida), frente a
  las **1577** del tramo anterior. El tramo cierra con **1626**.

### Lo que sigue viéndose así

- **Una salida de rama que no conectas termina el recorrido del archivo ahí.** Es lo que es una rama: un desvío
  para lo que se salió del camino. Lo que cambia es que ahora **se ve en la tarjeta del nodo y se puede conectar**:
  un error de extracción, un nombre de tabla inseguro o un archivo omitido se pueden llevar a un informe, a un
  registro o a una carpeta de cuarentena; si no los conectas, esos archivos acaban su recorrido en el nodo.
- **El aviso de puerto no declarado es una línea en la consola, no un fallo:** la ejecución sigue terminando en
  verde. El motor no puede saber si un puerto que no existe en el grafo es un nodo que legítimamente terminó ahí o
  un nombre mal escrito, así que lo dice en voz alta en lugar de detenerte medio proceso por algo que puede ser
  correcto. Es un aviso para encontrarlo, no una parada.
- **La auditoría por código es heurística:** mira los nombres literales de los puertos. Una emisión calculada en
  tiempo de ejecución —un nombre de puerto que venga de un parámetro— sólo la ve el aviso del motor.

---

## 5. Cómo verificarlo

```powershell
# La suite completa (pruebas unitarias, de integración y de aspecto)
.\test.ps1

# Compilar y ejecutar la aplicación
.\run.ps1
```

En Linux/macOS la ejecución y la limpieza están en `./run.sh` y `./clean.sh`; la suite se lanza con el mismo
`dotnet test` que usa el script de Windows.
