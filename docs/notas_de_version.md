# Notas de versión — FileFlow Studio

**Versión 1.0.0 · compilación 7005 · 28 de septiembre de 2026**

Estas notas recogen **veinte tramos**:

- **El del rediseño visual** (compilación 4743 → 5018): el aspecto, los estados de los controles, el arranque y
  la infraestructura de pruebas que lo sostiene. Son los apartados **1 a 3**.
- **El de la ejecución de flujos** (apartado **4**): el que explica por qué **un flujo que se cortaba en silencio
  ahora llega al final**, y qué errores dejan de ser invisibles. Nació de un flujo real que no terminaba.
- **El de los flujos que se creían hechos** (apartado **5**): el que explica por qué **una ejecución podía
  terminar en verde sin hacer nada** —el segundo «Ejecutar», un flujo con archivos reales escribiendo en un
  almacén invisible, un plan que arrastraba el de la simulación anterior— y qué se hizo para que no vuelva a
  pasar. Las cifras del tramo, medidas: **1626 → 1671 → 1692 pruebas superadas** (1 omitida).
- **El de los ejemplos que no entregaban lo que prometían** (apartado **6**): el que explica por qué un ejemplo
  del catálogo podía estar bien escrito y **no entregar nada** —o llevarse la aplicación por delante— y qué se
  encontró al ejecutarlos todos de punta a punta. Cifras del tramo, medidas: **1692 → 1706 pruebas superadas**
  (1 omitida).
- **El de los flujos que agrupan en lotes** (apartado **7**): el que explica por qué un flujo que acumula en
  lotes entregaba **cero** archivos cuando la carpeta tenía menos archivos que el lote —el caso corriente— y qué
  apareció detrás al ir a comprobarlo, más lo que se añadió al comprobar los ejemplos y al comprimir. Cifras del
  tramo, medidas: **1706 → 1719 pruebas superadas** (1 omitida).
- **El de dónde acaba el comprimido** (apartado **8**): el que explica por qué el compresor escribe ahora, por
  omisión, **en la carpeta de salida del flujo** —y en la salida por defecto de los ajustes cuando el flujo no
  declara ninguna—, qué pasa con los flujos que ya tenías guardados y qué se encontró al ir a hacerlo, incluida la
  carpeta de salida que no era una carpeta en manos de media docena de nodos. Cifras del tramo, medidas:
  **1719 → 1733 pruebas superadas** (1 omitida).
- **El del núcleo portable** (apartado **9**): el que explica por qué **la aplicación se ve exactamente igual**
  —y por qué eso es lo que se quería—: la lógica del editor vive ahora sin framework, en un núcleo compartido
  por los dos hosts (escritorio y multiplataforma), con lo que se encontró al pasar (un lienzo que dejaba de
  pintar los nodos y fallaba en silencio). Cifras del tramo, medidas: **1733 → 1742 pruebas superadas**
  (1 omitida).
- **El de la defensa en profundidad** (apartado **10**): el que explica por qué **la aplicación sigue viéndose
  igual** y qué la sostiene ahora: cada comportamiento que importa tiene un defecto declarado que sus pruebas
  matan — **28 → 41 declaraciones, las 41 mordiendo en la re-certificación final del catálogo** (la número 41,
  el dry-run de red, completó la cobertura de todos los plugins) —, la geometría del editor vive en el núcleo
  compartido y sus enlaces ya no pueden fallar en silencio en ninguno de los dos hosts, y el suite dejó de
  mentir con fallos intermitentes de carga. Cifras del tramo, medidas: **1742 → 1800 pruebas superadas**
  (1 omitida).
- **El del lienzo Uno vivo** (apartado **11**): el que explica por qué el host multiplataforma pasó de
  lienzo estático a **editor completo** — selección, arrastre, teclado, spotlight, notas, grupos, cables
  vivos y re-tematización — con el rendimiento medido (un frame de arrastre del grafo entero ~1 ms con 40
  nodos y 28 cables). Cifras del tramo, medidas: **1800 → 1849 pruebas superadas** (1 omitida).
- **El de los paneles del editor Uno** (apartado **12**): el que explica por qué el host multiplataforma
  es ya un **editor de tres zonas como el escritorio** — cajón, lienzo e inspector — sin duplicar lógica:
  los view models son los compartidos del núcleo y el host sólo escribió vistas. Cifras del tramo,
  medidas: **1849 → 1863 pruebas superadas** (1 omitida) al cierre del tramo.
- **El de la observación UIA y el cierre de la rebanada 4** (apartado **13**): el que explica por qué el
  host multiplataforma puede ser **observado desde fuera por otro proceso** — y qué frontera de la
  plataforma quedó medida y declarada en el camino —, por qué el botón Ejecutar, las pestañas de
  snapshots y el conmutador compacto/detallado ya están, y por qué el plan de paneles se quedó **sin
  pendientes de código**. Cifras del tramo, medidas: **1849 → 1866 pruebas superadas** (1 omitida).

Los tramos siguientes —los **ajustes del host** (apartado **14**), su **menú principal** (**15**), sus **paneles
de nodo** (**16**), las **entradas y los atajos que faltaban** (**17**), las **ventanas que faltaban** (**18**), el
**diseñador de datasets, las dos pestañas de ajustes que quedaban y el editor de URLs por modelo** (**19**), el
**gestor de presets de medios** (**20**) con su **confirmación** (**21**), las **seis órdenes destructivas que
quedaban mudas** (**22**) y el **cable que ahora es el mismo en las dos aplicaciones** (**23**)— están en sus
apartados.

Están escritas en dos mitades a propósito —**lo que ves** al usar la aplicación y **lo que no se ve** pero es lo
que impide que lo primero se rompa sin que nadie se entere—. Todo lo que se afirma aquí está medido en el
registro técnico ([`PROJECT_WALKTHROUGH.md`](PROJECT_WALKTHROUGH.md), hitos 169 a 246): las cifras salen de ahí,
no de la memoria.

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

## 5. El tramo de los flujos que se creían hechos (compilación 5129 → 5248)

Este tramo tampoco cambia cómo se ve la aplicación: cambia **si el trabajo se hace**. Es la familia de defectos
más engañosa que tiene un motor de flujos —la ejecución termina **en verde**, sin un solo error en la consola, y
el resultado es que no se procesó nada—. Seis casos en el motor y cuatro en el trabajo de los propios nodos,
todos reproducidos y todos cerrados.

### Lo que ves

**Si vuelves a ejecutar sin cerrar la aplicación, el trabajo se vuelve a hacer.** El motor recordaba los archivos
completados de la ejecución anterior y los daba todos por hechos: pulsar **Ejecutar** una segunda vez terminaba en
milisegundos, en verde, con la carpeta de destino tan vacía como estaba. Ahora una ejecución nueva empieza su
cuenta desde cero; y si la anterior **se interrumpió de verdad**, el motor reanuda donde se quedó, que es para lo
que existe el punto de control.

**Un flujo con archivos de verdad ya no escribe en un almacén invisible.** Cuando un flujo trae un origen
simulado, el motor activa solo el modo virtual —así una simulación no toca el disco—, y ese modo se quedaba
activado para la siguiente: un flujo con archivos reales movía y copiaba dentro del almacén virtual, **el disco
quedaba vacío y la ejecución terminaba en verde**. Ahora cada ejecución decide su modo.

**Después de pausar y detener, el motor vuelve a arrancar solo.** Pausabas la ejecución y la detenías; el estado
de pausa se quedaba en el motor, así que la siguiente **esperaba indefinidamente** a que alguien la reanudara
—nadie la había pausado—. Ahora arranca.

**«Deshacer la última ejecución» deshace solo la última.** El diario de operaciones acumulaba las ejecuciones
anteriores: un solo clic revertía también lo que había hecho la anterior, aunque el mensaje dijera «la última».

**El plan de una simulación ya no arrastra el de la anterior.** Tras varios ensayos en seco, el contador de
«acciones planificadas» sumaba las de todos ellos.

**Las carpetas vacías se limpian de verdad en una ejecución simulada.** El nodo que limpia carpetas respondía «no
hay nada que limpiar» sobre carpetas que sí existían en el almacén —las enumeraba en el disco del sistema—, así
que el flujo terminaba en verde **sin haber borrado nada**. Ahora pregunta al mismo sitio donde borra.

**El modelo de texto que descargues se usa de verdad, y no hace falta reiniciar para que se note.** La detección
por descripción traduce cada descripción a un vector: con el modelo de lenguaje visual descargado, y con una
proyección interna si no lo está. El complemento buscaba el fichero con el nombre equivocado —el identificador del
catálogo en lugar del nombre del fichero—, así que el modelo descargado **no se encontraba nunca** y las
descripciones salían siempre de la proyección interna; y aunque se hubiera encontrado, el vector ya calculado se
guardaba para todo el proceso, de modo que descargar el modelo a mitad de sesión no cambiaba nada. Ahora, con el
modelo en disco, las descripciones lo usan; y si lo descargas con la aplicación abierta, la ejecución siguiente ya
lo aprovecha.

### Lo que no se ve (y sostiene lo anterior)

- **El punto de control se escribe por lotes, no archivo a archivo.** Cada archivo completado reescribía la lista
  entera bajo un candado; con miles de archivos eso es cuadrático y frena el reparto del trabajo. Medido con
  **2 000 archivos reales**: **2 000 escrituras y 3 855 ms** antes, **7 escrituras y 581 ms** ahora (**×6,6** de
  tiempo).
- **Catorce pruebas ejecutan dos veces** —una sola ejecución no destapa ninguno de estos defectos—: seis del
  motor (modo virtual que no se hereda, pausa que no se hereda, plan que no se acumula, diario que no se acumula,
  contadores que empiezan en cero y un flujo con archivos reales que tiene que escribir en el disco) y ocho del
trabajo de los propios nodos, incluido lo que un complemento guarda en memoria —el índice de hashes que decide qué
  archivo está repetido, el búfer de un lote, la tabla de datos que se cruza y el vector de una descripción—.
  **Las cinco del primer barrido se pusieron rojas al escribirlas**, cada una con su síntoma medido antes de
  arreglarla; la sexta —el segundo «Ejecutar» que no hacía nada— tiene su defecto declarado y comprobado que la
  suite lo caza.
- **Defectos deliberados que la suite tiene que cazar.** El repositorio declara **20** defectos pequeños (un
  puerto que se borra, un tema al que le falta un color, un permiso que no se comprueba…) y comprueba que la suite
  los detecta: si uno sobrevive, la suite lo cuenta como **fallo**. Nueve de este tramo son los de arriba: los
  tres del motor —el modo virtual heredado, la pausa heredada y el diario que acumula— y los seis del trabajo de
  los nodos —el índice de hashes heredado, el búfer de lotes heredado, la descripción que no mira si el modelo
  está, el modelo buscado por el nombre equivocado y las dos de la tabla de datos (identidad y tope)—, y los nueve
  muerden.
- **Los puertos que un nodo calcula en ejecución** (los que dependen de un parámetro) ya se materializan antes de
  validar el flujo, y las ramas que sólo fallan según el entorno (permisos denegados, disco lleno, un archivo
  bloqueado) tienen su fallo **inyectado y comprobado**, en vez de confiar en que algún día ocurra.
- **El censo de puertos**: las **154 salidas** del producto (69 nodos, contando el camino feliz y sus ramas de
  error y de omisión) están declaradas con la prueba que las ejecuta o con el motivo por el que no se puede. Es
  la guardia que impide que un puerto nuevo entre sin nadie que lo ejecute.

### Lo que sigue viéndose así

- **El optimizador de imágenes tarda lo que tarda su trabajo.** Medido: **~450 ms de CPU por imagen** de
  1600×1200 recomprimida a WebP con calidad 80; con la misma imagen redimensionada a 800 px, **~12,5 ms**. No es
  el reparto de hilos del motor —alcanza los 25-27 nodos simultáneos que se le piden, también la primera vez—:
  es un codificador que no reparte una imagen entre hilos. Si un flujo de imágenes grandes tarda, mira el tamaño
  de la salida antes que el número de hilos.
- **Tras una caída seca se reprocesan hasta 256 archivos** que ya estaban hechos: el punto de control se escribe
  por lotes, y ése es el trato (se pierde poco de trabajo repetido a cambio de no reescribir la lista entera por
  cada archivo). Al interrumpir o cancelar la ejecución desde la aplicación, el lote pendiente se vuelca antes de
  terminar, así que lo perdido es sólo lo de una caída del proceso.
- **El punto de control sigue siendo cuadrático dividido por 256** (unos 390 volcados con 100 000 archivos) y su
  marca de tiempo sigue siendo la del inicio de la ejecución: el volcado no la actualiza.

---

## 6. El tramo de los ejemplos que no entregaban lo que prometían (compilación 5248 → 5277)

El catálogo de ejemplos del producto son **40 flujos** que se pueden abrir y ejecutar tal cual. Hasta ahora la
suite comprobaba que fueran del formato correcto, que sus puertos existieran y que se abrieran enteros en el
editor: su **papel**. Este tramo los pone a **trabajar**: cada uno se ejecuta de verdad sobre un área de trabajo
sembrada con archivos reales y se mira **qué quedó en el disco**, no si terminó en verde. Ejecutarlos destapó
cinco defectos que iban desde uno que **cerraba la aplicación sin decir nada** hasta un archivo que se perdía por
el camino, más un ejemplo que filtraba por un dato que ningún nodo del producto produce.

### Lo que ves

**«Enviar a la papelera» ya no cierra la aplicación.** El nodo que manda los originales a la papelera de
reciclaje llamaba a la función del sistema con la estructura de datos mal alineada: el sistema leía media
dirección como si fuera un puntero y el **proceso moría en el acto** —sin error, sin consola, sin aviso—. Se
descubrió ejecutando el ejemplo de papelera segura. Ahora la llamada usa la alineación del sistema y el archivo
acaba en la papelera, que es lo que el nodo promete.

**Un flujo que guarda en carpetas por fecha ya no escribe junto a la aplicación.** Los ejemplos que arman el
destino con variables —el que organiza tus fotos por año y mes, por ejemplo— resolvían la carpeta relativa
**contra el directorio de trabajo del programa**: los archivos aparecían en una carpeta `sub/2026/09/` al lado de
la aplicación en vez de en el origen. Ahora la ruta relativa se ancla al origen que se le dio.

**El ejemplo de paralelismo (bifurcar y esperar) ya se ejecuta.** Su nodo barrera divide un archivo en dos ramas
independientes y espera a que las dos acaben. El nodo existía en el lienzo, se podía arrastrar y cablear, pero
**ningún flujo que lo usara llegaba a ejecutarse**: el motor veía la vuelta de las ramas como un ciclo y
rechazaba el grafo entero. Ahora la barrera funciona de verdad —el ejemplo de fork/join entrega sus seis
archivos— y el nodo está disponible para usarlo en tus propios flujos.

**Un archivo optimizado ya no se pierde por el camino.** En ese mismo ejemplo, la rama del optimizador de
imágenes cambiaba la identidad del archivo al producir el suyo, así que la barrera no reconocía su vuelta y el
archivo **nunca salía del nodo**: de seis entradas llegaban cinco, y la que faltaba era justo la única imagen del
lote. Ahora la identidad se conserva.

**El ejemplo de archivado en 7Z ya produce un 7Z.** Pedía formato 7Z dejando la compresión por defecto
(`Deflate`), que ese contenedor no admite: el nodo registraba el error y además dejaba un archivo de **cero
bytes** con la extensión del archivo prometido —ni era un 7Z ni eran tus datos—. Ahora el ejemplo declara la
compresión que el contenedor sí admite y entrega un archivo real; y cuando una compresión no se puede hacer, el
nodo **retira** el archivo vacío en vez de dejarlo ahí.

**El ejemplo de inspección de documentos ya filtra por algo que existe.** Filtraba por «número de palabras», un
dato que **ningún nodo del producto publica** (la documentación citaba incluso un parámetro que no existe): la
condición no se cumplía nunca y el flujo terminaba en verde sin archivar nada. Ahora filtra por el número de
líneas, que es lo que el nodo mide de verdad, y entrega el documento que cumple.

### Lo que no se ve (y sostiene lo anterior)

- **Los 40 ejemplos se ejecutan de punta a punta en la suite**, uno por uno, sobre un área de trabajo sembrada con
  archivos de verdad (texto, un CSV, una imagen, un comprimido con contenido, una carpeta anidada y una carpeta
  vacía). De cada ejemplo se exige **lo que promete** —que entregue sus archivos, que descomprima el comprimido,
  que ponga el duplicado en cuarentena, que vacíe la entrada, que no escriba nada— y hay tres exigencias que
  valen para **todos**: que el motor **acepte** el grafo, que la ejecución no reviente y que no deje archivos de
  cero bytes ni escriba fuera de su área de trabajo. **Veintiséis** ejemplos se juzgan así; los **catorce** que no
  se pueden juzgar sin depender de la máquina (media de vídeo real, un servicio externo, la papelera del usuario)
  se declaran **con su motivo escrito**, en vez de fingir una comprobación. Un ejemplo nuevo sin asiento rompe la
  guardia.
- **Los cinco defectos tienen su defecto deliberado declarado y comprobado**: el struct desalineado, la vuelta de
  rama contada como ciclo, los avisos de retroalimentación silenciados, la identidad perdida al optimizar y el
  archivo vacío dejado atrás. Los cinco muerden. El conjunto pasa a **25 defectos declarados**, y la suite caza
  los 25.
- **Catorce pruebas nuevas** sostienen el tramo: las tres del banco de ejemplos, las dos de la barrera (con las
  dos ramas de vuelta el archivo se libera **una sola vez**; con una rama que no vuelve, no se libera y el
  archivo no llega al destino), las cuatro reglas del validador (la vuelta de rama no es un ciclo; un ciclo
  normal se sigue rechazando; los dos avisos de un fork/join mal cableado), las dos del contrato de la estructura
  del sistema, las dos del compresor (no deja basura al fallar, y con la combinación buena entrega el archivo) y
  la del optimizador que conserva la identidad.
- **El censo de puertos del producto baja sus huecos de cinco a dos**: la barrera de sincronización era, junto al
  OCR local, el único nodo cuyos puertos no ejecutaba ninguna prueba —porque era inejecutable— y ya tiene las
  suyas.

### Lo que sigue viéndose así

- **Catorce ejemplos siguen declarados y no juzgados** por depender de algo que la máquina de pruebas no tiene:
  media de vídeo real, un servicio al que notificar, un programa externo o la papelera del usuario. Su límite
  está escrito en el asiento, y si alguno deja de entregar, el defecto aparece ahí.
- **Dos familias de defectos quedan abiertas, escritas y sin arreglar**: los flujos que agrupan en lotes (de 10 y
  de 50 archivos) **no entregan nada** cuando la entrada tiene menos archivos que el lote, porque el lote
  pendiente muere con la ejecución; y varios flujos que prometen vídeo, audio o GIF entregan, con entradas que no
  son media, **copias con la extensión del destino**.
- **La identidad del archivo se cambia en más sitios.** El optimizador no era el único nodo que construye su
  elemento desde cero: hay una veintena de sitios parecidos, y sólo se ha demostrado el daño en ése. La regla para
  quien escriba un nodo nuevo: si produces un archivo a partir del que entró, **clona el elemento** (conserva la
  identidad) en vez de crear uno nuevo.
- **Los ejemplos 10 y 33 mandan sus originales a tu papelera de reciclaje real** cuando los ejecuta la suite: son
  seis archivos de texto de unos pocos bytes por vuelta, y es el único efecto que la prueba deja fuera de su área
  de trabajo.

---

## 7. El tramo de los flujos que agrupan en lotes (compilación 5277 → 5305)

El nodo **«Agrupar por Lotes»** acumula archivos y los suelta cuando llega a un umbral («cada 10», «cada 50»).
Es un nodo de los que se usan para no lanzar cien operaciones caras a la vez. Ejecutados de punta a punta, los
dos ejemplos del catálogo que lo usan entregaban **cero archivos** —y terminaban en verde—, y detrás de eso
aparecieron otros tres defectos que sólo se ven corriendo el flujo con archivos de verdad.

### Lo que ves

**Un flujo que agrupa en lotes ya no termina sin entregar nada.** El nodo sólo soltaba el lote al alcanzar el
umbral, así que si la carpeta de entrada tenía menos archivos que el lote —seis archivos con un lote de diez, el
caso corriente— **no se soltaba nada nunca** y la ejecución acababa en verde con la carpeta de destino vacía. Ahora
al terminar la ejecución lo que quede dentro del búfer **se entrega igual**: los archivos y el aviso de cierre
del lote, por el mismo camino que un lote completo.

**Los dos ejemplos de lotes entregan de verdad.** El de «Procesamiento por Lotes» saca cada archivo del lote a
comprimir y deja los comprimidos en su destino; el de «Ingesta Documental Empresarial» recorre la cadena entera
—analizar, inyectar metadatos corporativos, renombrar y empaquetar— y entrega los comprimidos con el nombre
corporativo que declaraba.

**El renombrador aplica la plantilla que el flujo declara.** El segundo ejemplo declaraba renombrar a
`{Year}_DOC_{Guid}` y los archivos salían con **otro nombre** (`carpeta_20260924_archivo`), sin que nada lo
dijera: la plantilla se guarda como una lista de pasos y el nodo no sabía leer la forma en la que el propio
catálogo la escribe. Ahora la lee (y si algún día no puede, lo dice en el log en vez de renombrar en silencio).

**El compresor ya no puede destruir el archivo que comprime.** Con los valores de fábrica, comprimir un archivo
que ya tiene la extensión del archivo de salida apuntaba al **mismo archivo**: el nodo lo abría para escribir
—lo que lo vacía— antes de leerlo, y en su lugar quedaba un comprimido sin entradas. Medido: un archivo de
**148 bytes** salía del flujo convertido en uno de **22**. Ahora el nodo **se para** y lo dice.

### Lo que no se ve (y sostiene lo anterior)

- **El gancho del final de la ejecución ya se usaba y el nodo no lo aprovechaba.** El motor llama a cada nodo
  cuando todos los de aguas arriba han terminado —y mantiene una fase de drenado después, para lo que esos nodos
  emitan—; el búfer era el único acumulador del producto que no lo tenía escrito. Ahora entrega lo pendiente por
  el mismo método que un lote completo, así que **lo que se entrega no depende de por qué se entregó**, y el
  aviso de cierre distingue el lote cerrado por umbral del cerrado por fin de ejecución.
- **La identidad de la ejecución se sigue respetando.** El búfer guarda a qué ejecución pertenecen sus ítems y
  los reinicia cuando cambia; hay una prueba con la **misma instancia del nodo** en dos ejecuciones distintas
  —la única forma de que ese reinicio sea observable— y el caso de dos ejecuciones completas se quedó como
  regresión de los conteos.
- **Seis pruebas nuevas** sostienen el tramo: tres del búfer (el lote incompleto sale entero y lo entregado no
  vuelve a salir; sin pendientes no sale nada; dos ejecuciones en la misma instancia no se mezclan), dos del
  renombrador (los pasos con los nombres de las enumeraciones se aplican; unos pasos ilegibles se dicen) y una del
  compresor (el destino que es la propia entrada se rechaza y la entrada queda **byte a byte** como estaba).
- **Cuatro pruebas más al cerrar el compresor**: el respaldo del destino —sin carpeta declarada el comprimido sale
  junto al archivo que comprime y el registro lo dice—, la **ayuda del parámetro** leída del propio ensamblado del
  plugin en los dos idiomas, la **guardia del catálogo** que exige que todo compresor de un ejemplo declare su
  destino, y la resolución de esa ayuda en la ficha del parámetro (con su caída a la clave cuando no existe).
- **El banco de los 40 ejemplos** juzga ahora también estos dos (antes se declaraban sin comprobar), y como sus
  lotes son mayores que la entrada sembrada, lo que llega al destino **sólo puede** venir del cierre de la
  ejecución: el asiento es el testigo de punta a punta del arreglo.
- **Defectos deliberados declarados y comprobados**: cinco nuevos —descartar el lote pendiente, no leer los pasos
  con nombres de enumeración, comprimir sobre la propia entrada, escribir el comprimido **donde corre el proceso**
  y quitarle al ejemplo más básico la carpeta de destino— y uno reescrito para que siga mordiendo con el
  comportamiento nuevo. El conjunto pasa a **30 defectos declarados**, y la suite caza los 30.
- **La entrada de un flujo se vigila.** Los 40 ejemplos se ejecutan sobre una carpeta de entrada sembrada, y ahora
  se comprueba **qué le pasó a esa carpeta**: cada archivo tiene que seguir **byte a byte** como estaba, salvo en
  los **seis** flujos que de verdad se la llevan —la papelera de reciclaje, la cuarentena, la organización por
  fecha, el renombrado en disco—, y ésos lo declaran **con su motivo escrito y con una afirmación más fuerte**:
  lo que la entrada traía tiene que seguir estando en el área de trabajo (mover no es destruir). Es la prueba que
  habría cazado, sin abrir el flujo a mano, el `paquete.zip` que se quedaba en 22 bytes.
- **La ayuda de un parámetro ya se ve.** Los plugins venían escribiendo la aclaración de cada parámetro en su
  diccionario de recursos (`Param_<clave>_Help`, en Archives, FileSystem, Network y AI), y **ninguna interfaz la
  leía**: la ficha del parámetro enseñaba en su *tooltip* la clave cruda. Ahora la enseña, y con el cambio de
  idioma en caliente. Lo que **no** se pinta es la ayuda escrita dentro del código (casi un centenar de textos
  sin traducir): mostrarla pondría castellano en la interfaz inglesa, así que la ayuda visible es la de los
  recursos, en los dos idiomas.
- **El banco de ejemplos ya no mira la carpeta de los binarios.** Antes comparaba el directorio de trabajo del
  proceso —el de los binarios— antes y después de cada flujo, así que cualquiera que escribiera ahí durante esa
  ventana salía como si lo hubiera escrito el ejemplo medido: un rojo que nadie puede reproducir, y encima
  ilegible, porque el mensaje volcaba el árbol entero. Medido antes de cambiarlo: una pasada del suite sin el
  banco **no deja ni un archivo nuevo** ahí, así que quien puede escribir no es una prueba. Ahora los cuarenta
  flujos corren en una **sala limpia** propia —vacía y sólo suya— y la exigencia es la misma con atribución
  exacta: un archivo ahí es un flujo que decidió su destino donde corre. Cambiar el directorio de trabajo es
  estado del proceso, así que la prueba corre sola. **Comprobado con el defecto dentro**: un compresor que cae en
  el directorio donde corre hace que el banco señale los **34** archivos que escribieron los flujos 08, 21 y 34.

### Lo que sigue viéndose así

- **El lote suelta los archivos en tandas; no los empaqueta juntos.** El producto no tiene ningún nodo que meta
  **varios** archivos en un comprimido en un solo paso: el compresor comprime el archivo que recibe y el
  agregador de archivos trabaja con las sesiones de descompresión. Los textos del catálogo ya no prometen «un ZIP
  por lote»: el búfer agrupa y el compresor empaqueta cada elemento. Un «un ZIP por lote» pide una pieza nueva.
- **Varios flujos que prometen vídeo, audio o GIF** entregan, con entradas que no son media, copias con la
  extensión del destino. Sigue abierto y escrito: pide decidir qué debe hacer un transcodificador con una entrada
  que no puede decodificar.
- *(Resuelto en el apartado 8.)* **El compresor escribía los comprimidos en la carpeta de sus entradas cuando el
  flujo no le declaraba una carpeta de destino** —y ya lo decía en voz alta—. En este tramo el valor por omisión no
  se tocó: la respuesta fue la otra mitad (la ficha del parámetro explicando en los dos idiomas que dejarlo vacío
  significaba «junto al archivo que comprime», el registro de la ejecución anunciándolo con la carpeta exacta, y
  los **cinco** ejemplos del catálogo declarando su destino). Quedó pendiente decidir el valor por omisión, y **es
  lo que decide el apartado 8**.
- **La identidad del archivo** se sigue cambiando en una veintena de sitios sin daño demostrado: la regla para
  quien escriba un nodo nuevo no cambia —si produces un archivo a partir del que entró, clona el elemento—.

---

## 8. El tramo de dónde acaba el comprimido (compilación 5305 → 5362)

El nodo **«Compresor de Archivos»** dejaba el comprimido en la carpeta que le declararas y, si no le declarabas
ninguna, **junto al archivo que comprimía** —lo que hace un compresor de línea de órdenes—. El tramo anterior lo
dejo escrito en el registro de la ejecución y en la ficha del parámetro. Este tramo cambia **qué pasa cuando no le
declaras nada**: el comprimido sale en la **carpeta de salida del flujo**.

### Lo que ves

**El compresor escribe en tu carpeta de salida.** Un compresor recién puesto en el lienzo trae ya escrito
`{GlobalOutputDir}` en su «Carpeta de Destino»: el comprimido acaba en la carpeta que el flujo declara como suya
y, si el flujo no declara ninguna, en **la salida por defecto de los ajustes** (la de «Ajustes del flujo ▸
Almacenamiento»). Es lo que ves en la ficha del parámetro antes de ejecutar, no lo que hay que deducir del
resultado.

**Y si quieres el sitio de antes, se dice.** Para dejar el comprimido **junto al archivo que comprime**, escribe
`{CurrentDir}` en «Carpeta de Destino». Es la carpeta del archivo que llega al nodo, que es exactamente lo que el
nodo hacía por su cuenta cuando no le declarabas nada, así que el comportamiento viejo sigue disponible —ahora
escrito donde se lee, en vez de ser lo que pasaba sin decirlo—.

**El registro de la ejecución dice dónde acabó.** Cuando el flujo no pone carpeta —ni la de fábrica, ni una suya—
el nodo lo anuncia al ejecutar, con la carpeta exacta y con el recordatorio de `{CurrentDir}` para pedir el sitio
viejo.

### Tus flujos guardados: qué les pasa

**No se reescribe ningún archivo tuyo.** La regla nueva se aplica al ejecutar, así que alcanza también a los
flujos que ya tenías guardados, sin reescribirlos ni avisos de conversión de formato:

- **Si el flujo declara su carpeta de destino, no cambia nada.** Sigue escribiendo exactamente donde decía.
- **Si el flujo lo declara con el nombre heredado del parámetro** (el que ya no aparece en la ficha), **tampoco
  cambia nada**: ese valor manda sobre el valor por omisión, así que un flujo que sí declaró dónde escribe
  conserva su carpeta.
- **Si el flujo no declara carpeta** —como quedaron todos los que guardaste sin tocar el parámetro, porque el
  valor de fábrica era vacío—, el comprimido pasa de salir **junto al archivo** a salir en **la carpeta de salida
del flujo**: la que el propio flujo declara o, si no declara ninguna, la salida por defecto de tus ajustes. El
  registro de la ejecución lo dice la primera vez que lo ejecutes, con la carpeta exacta. **Es el cambio de sitio
  que este apartado viene a contarte**: si lo querías donde estaba, escribe `{CurrentDir}` en esa carpeta de destino.

**Por qué se puede hacer sin pedir permiso por cada flujo**: declarar «si dejo la carpeta vacía, el comprimido va
junto al archivo» se escribió en el tramo anterior y **no se ha publicado hasta ahora**, así que nadie eligió ese
sitio porque se lo dijéramos. Y el comprimido **no es el archivo original**: se escribe uno nuevo (o se reemplaza
uno con ese nombre), y los archivos que comprimes no se tocan.

### Lo que no se ve (y sostiene lo anterior)

- **La carpeta de salida del flujo tenía que ser una carpeta, y no lo era.** Un flujo declara su salida con un
  valor que puede llevar variables —el catálogo de ejemplos entero declara `{RelativeDir}`, que significa «la
  estructura de la carpeta de entrada»—, y ese valor llegaba al nodo **sin expandir**: `{GlobalOutputDir}`
  devolvía el texto `{RelativeDir}`, el anclaje lo combinaba consigo mismo y la ruta resultante acababa colgada del
  **directorio donde corre la aplicación**. Medido antes de arreglarlo: `bin/Debug/net10.0/{RelativeDir}/{RelativeDir}`.
  Es la misma forma del defecto del tramo 6 —quince ejemplos escribían su salida entre los binarios—, estaba vivo
  en el árbol y estaba a punto de convertirse en el destino por omisión de todo compresor. Ahora la salida
  declarada **se expande y se ancla** (bajo la carpeta de origen del barrido, o bajo tu salida por defecto) y el
  patrón del nodo recibe el valor ya terminado.
- **La resolución vive en un solo sitio y con su contrato escrito** (`ParameterHelper.ResolveOutputPath`): la
  regla de dónde anclar una ruta relativa es la misma para todos los nodos, y este tramo no la duplica. En el
  compresor queda además un último escalón explícito: un destino que se quede relativo pese a todo **no se
  escribe donde corre el proceso**, se ancla en tu salida por defecto y se anuncia.
- **La carpeta de salida del flujo vale una carpeta en cualquier parámetro, no sólo en los que son rutas.** La
  variable `{GlobalOutputDir}` se escribe también en mensajes de registro, asuntos de notificación o expresiones, y
  ahí no pasa por la resolución de rutas: media docena de sitios del producto la leían **tal cual** —la variable, la
  regla de cuatro nodos de IA, la sustitución del token de siete nodos de datos, dos escritores que resuelven al
  terminar la ejecución— y con una salida declarada como plantilla el usuario se encontraba `{RelativeDir}` dentro
  de su texto o de su ruta, además de archivos que aparecían **dentro de la carpeta de la aplicación** cuando el
  flujo no declaraba ninguna. Ahora todos leen la **misma regla**, escrita en un solo sitio del SDK, así que la
  variable, el anclaje de las rutas, los nodos de IA y los escritores de datos contestan lo mismo; la interfaz
  deja además de abrir una ruta de Windows escrita a mano cuando el editor no tiene carpeta. Y vale lo mismo en
  **cualquier nodo**: los siete nodos de datos sustituían **un solo nombre** a mano, así que escribir
  `{DefaultOutputDir}` —nombre que el producto acepta— en su destino dejaba el alias **escrito dentro de la ruta**,
  en una carpeta llamada así; ahora entregan el patrón entero a la misma regla y los cuatro alias valen lo que la
  clave vigente.
- **La regla admite que un flujo se declare en términos de sí mismo** (`{GlobalOutputDir}/sub`): la expansión
  termina en tu salida por defecto en lugar de entrar en un bucle, y los alias históricos de la variable
  (`DefaultOutputDir`, `GlobalOutputPath`…) se leen en el mismo sitio que la clave vigente —antes podían anclar en
  un lugar distinto del que decía la variable—.
- **Un defecto del propio arreglo, cazado por el arnés**: el último escalón caía en cadena sobre
  `Path.GetDirectoryName`, que devuelve cadena **vacía** —y no vacío de valor— para un nombre de archivo suelto, así
  que no llegaba a dispararse y el nodo habría escrito en una ruta vacía. La mutación declarada **sobrevivió** a la
  primera medición y obligó a arreglar la regla antes de darla por cubierta; ahora muerde, y la declaración de la
  mutación cuenta también esa medición.- **Doce pruebas nuevas** sostienen el tramo, contadas contra el total del suite (**1725 → 1737**): el compresor
  (la salida declarada como plantilla, el nombre heredado del parámetro frente al valor de fábrica y el valor de
  fábrica que la ficha promete se **reescriben** —medían el respaldo viejo—), el SDK (la expansión y el anclaje, su
  caída a tu salida por defecto cuando no hay carpeta de origen, la variable en un parámetro que no es una ruta y
  una salida declarada en términos de sí misma), la regla compartida de los nodos de IA (la plantilla anclada en el
  origen, la carpeta del propio archivo, el último escalón y una carpeta declarada en redondo o relativa), los dos
  escritores de datos (el CSV por su token y el reporte de Excel, que la resuelve al vuelo), **los cuatro alias** de
  la carpeta del flujo en un nodo de datos, uno por uno, y **una que ejecuta con el motor de verdad** un flujo
  guardado con la carpeta vacía, para medir la migración de punta a punta en vez de suponerla. Cada nodo se mide
  **por su propia costura** —el método que el nodo llama de verdad—, no por una copia paralela en el suite.
- **Defectos deliberados declarados y comprobados**: uno **reescrito** —el compresor que decide su destino en la
  carpeta donde corre el proceso, que antes le quitaba el respaldo viejo («junto al archivo») y ahora el valor por
  omisión—, uno **redirigido** —la salida global que se queda sin expandir, ahora atado a la regla única y con las
  cinco lecturas del producto como testigo, alias incluidos— y dos **nuevos**: los nodos de IA escribiendo donde
  corre el proceso, y la sustitución de **un solo nombre** que devuelve los alias a una ruta de datos. El conjunto
  pasa a **33 defectos declarados**, y la suite caza los 33.
- **Una guardia que ya existía se queda, con su motivo actualizado**: todo compresor del catálogo de ejemplos
  **declara** su destino, porque el catálogo es documentación y «dónde acaba mi archivo» no se deduce de un
  diagrama.

### Lo que sigue viéndose así

- **Qué sitio le corresponde a un camino *relativo* en un nodo de lectura** sigue por decidir: al pasar los
  nodos de datos por la misma resolución de rutas que todo lo demás, un `export.csv` sin carpeta ya no cae donde
  corre la aplicación —que era el defecto—, pero cae en tu **carpeta de salida**, y en un lector lo natural sería la
  carpeta **de origen** del barrido. No es un sitio del que el usuario se queje hoy; es una decisión escrita para
  que se tome a conciencia y no por omisión.
- **El compresor sigue comprimiendo un archivo por vez.** El producto no tiene ningún nodo que meta **varios**
  archivos en un comprimido en un solo paso: el búfer agrupa y el compresor empaqueta cada elemento. Un «un ZIP por
  lote» pide una pieza nueva.
- **Varios flujos que prometen vídeo, audio o GIF** entregan, con entradas que no son media, copias con la
  extensión del destino. Sigue abierto y escrito: pide decidir qué debe hacer un transcodificador con una entrada
  que no puede decodificar.
- **La identidad del archivo** se sigue cambiando en una veintena de sitios sin daño demostrado: la regla para
  quien escriba un nodo nuevo no cambia —si produces un archivo a partir del que entró, clona el elemento—.

---

## 9. El tramo del núcleo portable (compilación 5362 → 5517)

La aplicación **se ve exactamente igual** — y ese es el punto. Este tramo no cambia nada de lo que ves: separa
**lo que la aplicación hace** de **con qué la pinta**, para que la misma aplicación pueda pintarse con otro
framework (el trabajo multiplataforma hacia Uno Platform que empezó en la rebanada 1).

### Lo que ves

**Nada cambió a propósito.** El editor, los temas, el personalizador, el lienzo y sus cables se pintan píxel a
píxel como antes — las capturas de referencia del producto **no se regeneraron**: siguieron siendo idénticas y
las pruebas lo comprobaron. Si notas alguna diferencia, es un defecto y no una decisión.

### Lo que no se ve (y sostiene lo anterior)

- **Un proyecto nuevo, `FileFlow.App.Core`, lleva ahora los ViewModels y los servicios sin framework.** Es la
  lógica del editor —el grafo, los temas, el portapapeles de nodos, la coordinación de ejecución— viviendo sin
  Avalonia, para que un segundo host (Uno) pueda montarla tal cual. Los hosts Avalonia y Uno comparten ese
  núcleo, y una guardia impide que el núcleo vuelva a depender de un framework de ventanas.
- **Donde la lógica necesita tocar la pantalla, declara la necesidad y el host responde**: un puente de temas
  (la variante y los colores los publica quien pinta), un puente de interfaz (dispatcher, portapapeles,
  selector de color, ventanas), un puente de diálogos. La aplicación de escritorio instala sus respuestas en el
  arranque; el host Uno instala las suyas.
- **Un defecto real cazado al pasar**: al separar la capa, el lienzo dejó de pintar los nodos — las tarjetas
  quedaban amontonadas en una esquina y los cables desaparecían. La causa: los enlaces entre la lógica y el
  lienzo no sabían traducir el punto del núcleo al punto del framework, y fallaban **en silencio**. Ahora la
  traducción es explícita en los ocho enlaces, y una guardia de contrato la fija. Si usaste la aplicación en
  este tramo y viste un lienzo vacío, era esto.

### Lo que sigue viéndose así

- **Tu flujos guardados, tus temas y tus ajustes no cambian**: el tramo no toca formato de archivo ni
  comportamiento, solo dónde vive el código.
- Quedan abiertos, escritos en el apartado anterior: los flujos que prometen vídeo, audio o GIF con entradas
  que no son media, y el sitio que corresponde a un camino relativo en un nodo de lectura.

---

## 10. El tramo de la defensa en profundidad (compilación 5517 → 5779)

La aplicación **sigue viéndose igual** — y eso es la mitad de la historia. Este tramo no añade una sola función
visible: construye las pruebas de que lo construido **no se puede romper en silencio**.

### Lo que ves

- **En el escritorio, nada cambió a propósito** — si notas alguna diferencia, es un defecto y no una decisión.
- **El host multiplataforma pasó de ventana de sondeo a lienzo real**: tarjetas de nodo en su posición, cables
  con la misma curva que pinta el escritorio (misma matemática, extraída al núcleo compartido), arrastre para
  moverse por el lienzo, zoom y encuadre con el mismo calculador. Todavía no llega a quien usa el producto: es
  la fase en curso.

### Lo que no se ve (y sostiene lo anterior)

- **Cada comportamiento que importa tiene un defecto declarado que sus pruebas matan.** El catálogo pasó de 28 a
  **41 declaraciones**, y las 41 **muerden**: re-certificadas en una pasada completa, cada una con su testigo en
  rojo y su control en verde, restaurando el árbol por bytes y recompilando antes de pasar a la siguiente. La
  declaración número 41 — el dry-run de red que dejaba de simular y entregaba el disparador — fue la que
  completó la cobertura de **todos los plugins**; quedan 3 de 17 subsistemas sin su primera declaración,
  escritos como lista de trabajo, no como reproche.
- **Los enlaces de geometría ya no pueden morir en silencio en ninguno de los dos hosts.** El defecto del tramo
  anterior (el lienzo amontonado en una esquina) no puede repetirse: las pruebas ejecutan los enlaces contra el
  editor real en las dos direcciones, y el censo del host multiplataforma nació *antes* de su primer enlace —
  con cero enlaces en el árbol, la única manera de que la regla no tenga excepciones históricas. Donde ese
  framework no puede enlazar (sus estilos no evalúan enlaces: no fallan, no hacen nada), la regla sigue al
  código y delata el cruce hecho a mano.
- **La geometría del cable y del encuadre es del núcleo, pura y probada**: la misma curva para los dos hosts,
  con valores esperados calculados a mano — un espejo del código solo probaría que es igual a sí mismo — y una
  mutación que muerde.
- **El suite dejó de mentir con fallos intermitentes de carga.** El último flake sin dueño se cazó corriendo la
  suite tres veces seguidas hasta ponerle nombre: un test de ritmo que afirma con margen cero que los huecos
  entre emisiones duran al menos el retardo prometido, midió 4,911 ms contra el umbral de 5 ms porque el resto
  de la suite le competía por la CPU. Su colección es ya la séptima exclusiva del suite — existía desde hacía
  tramos, pero sin definición, así que se paralelizaba como cualquier otra —, y el contrato de paralelismo lo
  fija: cualquier test futuro que mida tiempos así nace obligado a ella.

### Lo que sigue viéndose así

- **Tus flujos, tus temas y tus ajustes no cambian**: el tramo no toca formato de archivo ni comportamiento
  visible; solo dónde vive el código y qué garantías lo rodean.
- Quedan abiertos, escritos en el apartado anterior: los flujos que prometen vídeo, audio o GIF con entradas
  que no son media, y el sitio que corresponde a un camino relativo en un nodo de lectura.

---

## 11. El tramo del lienzo Uno vivo (compilación 5779 → 6084)

### Lo que ves

- **El host multiplataforma pasó de lienzo estático a editor completo**: los nodos se seleccionan con un clic
  (con su borde de selección), se arrastran solos o en grupo (con deshacer y rehacer), se borran con Delete, se
  copian y pegan con los atajos de siempre, se renombran con F2 y se agrupan; el rubber band selecciona por
  rectángulo; el spotlight (Shift+A o doble clic en el fondo) crea el nodo que escribas en el punto del cursor;
  las notas y los grupos se crean, mueven, recolorean y borran; las migas navegan entre subflujos.
- **Los puertos y los cables están vivos**: arrastrar desde un socket inicia el cable, que sigue al cursor y
  salta al socket compatible cercano; soltar conecta; un clic derecho en el socket desconecta; y si un flujo
  cargado trae cables que no pudieron anclarse, un aviso en el lienzo ofrece ir al nodo o reconectarlos.
- **El lienzo sigue el tema**: cambiar de tema (incluido el claro) re-pinta fondo, tarjetas, cables y acentos
  sin reiniciar, y los textos del marco siguen el idioma sin reiniciar.
- **El host arrastra y se mueve sin tirones con grafos del tamaño del banco de ejemplos**: medido con 40 nodos
  y 28 cables, un frame de arrastre del grafo entero cuesta ~1 ms — el margen contra 30 fps es de unas 30 veces.

### Lo que no se ve (y sostiene lo anterior)

- **Una sola fuente de claves**: los atajos de teclado viven en una tabla del núcleo compartida por los dos
  hosts — lo que se arregla en uno, se arregla en ambos, y una guardia compara que ninguno use teclas fuera de
  la tabla.
- **Anclas reales, no estimaciones**: los cables nacen del socket (calculado del árbol visual y proyectado por
  la regla compartida), no del centro estimado de la tarjeta — la única divergencia que la comparación con el
  escritorio midió, corregida y defendida por su mutación.
- **45 mutaciones declaradas** cubren los defectos del tramo (cables que llegan tarde, tarjeta que no habla por
  su color, anclas estimadas, decoradores que no llegan al árbol) con testigo que muerde y control en verde.
- **La tabla de paridad**: 24 interacciones del lienzo, cada una con su prueba citada por nombre y verificada
  contra el índice real de pruebas — la tabla no puede mentir sobre cobertura que no existe.
- **El sondeo en runtime** (`--selfcheck`) inventaria el árbol real de la app viva por fases (53 comprobaciones)
  y mide el rendimiento con el grafo de referencia.

### Lo que sigue viéndose así (declarado)

- El guion de los gestos puros (arrastre de cable a mano, rubber band, pan/zoom con el puntero) espera una
  sesión de QA con puntero real: en el entorno automatizado, WinAppSDK descarta el puntero inyectado sin
  UIAccess (el teclado sí llega); los métodos que ejecutan esos gestos están probados por las sondas.
- Los pinceles del escritorio y del host provienen del mismo tema del núcleo; la comparación de píxel fino
  entre hosts no se ha repetido desde el hito 226 (las divergencias que midió quedaron curadas y defendidas).

---

## 12. El tramo de los paneles del editor Uno (compilación 6084 → 6131)

### Lo que ves

- **El host multiplataforma ya es un editor de tres zonas como el escritorio**: a la izquierda la caja
  de herramientas (buscador, chips de categoría, grupos acordeón, insignia de rol); en el centro el lienzo
  de la rebanada anterior; a la derecha el inspector del nodo seleccionado, que se abre con la selección
  y se cierra con su botón.
- **Encontrar y añadir un nodo**: escribe en el buscador y el catálogo se queda con lo que coincide;
  un doble clic en el nodo lo crea en el centro del lienzo — con su deshacer de siempre, su conteo de uso
  y su selección. El filtro de búsqueda y las categorías son las mismas que en el escritorio, traducidas
  a los dos idiomas y sensibles al cambio sin reiniciar.
- **Editar un nodo**: cada parámetro muestra el editor que le toca (interruptor, deslizador, desplegable,
  ruta con botón de explorar que abre los pickers de Windows, texto de varias líneas, texto corto), y lo
  que escribes llega al nodo — no sólo a la ficha: es lo que el flujo ejecuta y guarda. Bajo el campo,
  el valor evaluado con su botón de copiar cuando el parámetro lleva expresiones.
- **La telemetría del nodo**: estado, procesados, latencia media, tiempo total y pico de memoria, con el
  botón de vaciar métricas.

### Lo que no se ve (y sostiene lo anterior)

- **Cero lógica duplicada en el host**: el catálogo, el filtro, el acordeón y la ficha son los view models
  compartidos del núcleo (los mismos del escritorio); el host Uno sólo escribió vistas. Dos guardias de
  árbol exigen ese consumo y una tabla de paridad por panel cuyas citas se verifican contra el índice
  real de pruebas — la tabla no puede mentir.
- **El write-through defendido por su mutación**: si la cadena que lleva la edición del parámetro al nodo
  se corta, un testigo rojo cae (el valor observable seguiría pintándose con el nodo usando valores
  viejos — el defecto más caro de la ficha). Y el filtro del buscador tiene el suyo: un mutante que
  pinta el catálogo entero siempre muere en la prueba de reducción.
- **48 mutaciones declaradas** cubren ya los defectos de las dos rebanadas del host Uno, con testigo que
  muerde y control en verde.
- **El sondeo en runtime** (`--selfcheck`) verifica la rebanada en la app viva (63 comprobaciones):
  catálogo poblado, filtro que reduce y restaura, añadir con deshacer, favorito conmutado y restaurado,
  inspector con sus editores, la edición llegando al nodo y el cierre del panel.

### Lo que sigue viéndose así (declarado)

- El conmutador de vista compacta/detallada del cajón y el botón «Probar» aislado del inspector esperan
  su variante (el segundo, un diálogo de fichero asíncrono); los pickers de variables y los diálogos de
  nodo caen a su no-op seguro. El gesto de arrastre fino desde el cajón espera la sesión con puntero real
  del tramo anterior; el doble clic ya cubre el añadido.

---

## 13. El tramo de la observación UIA y el cierre de la rebanada 4 (compilación 6131 → 6309)

El host multiplataforma ganó su infraestructura de observación externa y cerró el último pendiente de
código de sus paneles. Cifras del tramo, medidas: **1849 → 1875 pruebas superadas** (1 omitida);
**48 → 56 mutaciones declaradas, todas mordiendo**; el sondeo en runtime pasó de **63 a 80
comprobaciones**; el sondeo externo llegó a **8 sondeos en verde**.

### Lo que ves

- **El botón Ejecutar en el host multiplataforma**: el mismo comando del escritorio (con coordinador,
  simulación por defecto y punto de control), y una línea de estado que cuenta el ciclo — renglón de
  longitud fija, sin parpadeos ni textos partídos, con su fichero espejo legible desde fuera.
- **El inspector completo**: la ficha del nodo se abrió en el tramo anterior; ahora tiene la vista
  combinada de snapshots, **las pestañas separadas de Entradas y Salidas** (cada una con exactamente
  los datos de su colección, paridad con el escritorio), y la pestaña de **diferencias de metadatos**
  (añadidos, eliminados y modificados, con sus colores) que se recalcula al inspeccionar y al
  seleccionar un snapshot. Cada tarjeta y cada fila lleva su nombre de automatización.
- **El botón «Probar» del inspector** funciona: abre los selectores de Windows y ejecuta la prueba
  aislada del nodo con el fichero que elijas (la variante asíncrona del diálogo, con la vista que
  nunca se bloquea).
- **El conmutador compacto/detallado del cajón de herramientas**: el botón de la cabecera alterna entre
  la lista ligera (sólo nombre) y la detallada (insignia de rol + descripción), y **la elección
  persiste entre sesiones** en tus preferencias.
- **Los atajos del lienzo del host multiplataforma funcionan con el puntero**: clic en una tarjeta para
  seleccionarla y, a continuación, `Supr` la borra, `Ctrl+Z` la devuelve, `Ctrl+Y` la vuelve a quitar y
  `F2` abre el renombrado. Antes el teclado se quedaba sin destinatario al clic (medido: borrar y
  deshacer no hacían nada con la ventana en primer plano). Y **el área de clic de las tarjetas coincide
  con lo que se dibuja**: el clic cae en la tarjeta que clicas, no en la de al lado (el reparto se
  desplazaba la columna del cajón y la barra superior).
- **La observación externa como producto** (`--selfcheck-uia`): la app puede ser observada desde otro
  proceso — anclas del lienzo y del zoom, foco del lienzo, zoom que cambia y se restaura, el atajo del
  spotlight, el buscador que recibe teclado inyectado, las pestañas del inspector y las filas de
  diferencias — con veredicto propio por código de salida y reporte en disco. El guion del ciclo
  completo (superficie viva, botón expuesto, **el motor ejecutando un flujo real verificado por el
  CLI del producto**, canal legible) corre 4/4.

### Lo que no se ve (y sostiene lo anterior)

- **La escena para la observación la monta la app, no el observador**: fixture real (una entrada y tres
  salidas por la misma vía que usa el motor) montado con reintentos, asentado sin cliente y señal de
  listo/fracaso — el orden lo impuso la medición: con un observador conectado durante la
  materialización, el proceso moría sin rastro.
- **La frontera medida y declarada**: el contenido de snapshots materializado y en pie tumba al
  proveedor de automatización del proceso (con retardo, sin registro de errores ni excepción). Quedó
  declarado en el veredicto del sondeo: el contenido queda LATENTE para el canal externo y sus
  tarjetas las verifica el sondeo interno — honestidad medida, no cobertura fingida.
- **Cero lógica duplicada, sigue**: el conmutador y el botón Ejecutar atan al MISMO comando del núcleo
  que el escritorio; la reacción de la vista vive en código porque el binding de una plantilla de
  datos de WinUI no alcanza la página (la lección que dejó el pendiente).
- **El teclado del lienzo tiene dos protecciones nuevas**: (1) los atajos **no dependen del foco** —la
  raíz de la ventana enruta al lienzo las teclas que nadie consumió, con el mismo burbujeo del
  escritorio— y (2) el lienzo **recupera** el teclado si alguien se lo lleva justo después de un clic.
  Hacían falta las dos porque, medido con puntero real, el clic entrega el foco y ~0,1 s después un
  envoltorio de la plantilla de ventana del *framework* se lo lleva (no es código del producto: es un
  contenedor sin nombre, sin datos y del tamaño de la ventana). Certificado con puntero real: borrar,
  deshacer, rehacer y renombrar, y el buscador del cajón conservando sus letras mientras se escribe.
- **Los cables ya parecen cables**: antes no tocaban sus conectores —quedaban separados 45 px de cada uno y,
  con dos tarjetas cerca, se doblaban hacia atrás en un rulo con forma de «2»— y además se iban de sitio al
  ajustar el zoom. Ahora nacen y mueren **en el conector**, con una sola curva suave y sin el tramo recto
  que se leía como una **Z**, y su forma se adapta al hueco que haya entre las dos tarjetas: si están juntas,
  la curva se cierra dentro; si están lejos, el cable queda tenso y fino. **Certificado con el ratón de
  verdad** en tres rondas (mover tarjetas, pan y zoom) y medido en pantalla: en el hueco corto el cable es
  **una sola curva**, sin dobles.
- **59 mutaciones declaradas**, todas con testigo que muerde y control en verde — las nuevas: el toggle
  que no persiste (el modo elegido que se olvida al reiniciar), el área de clic desplazada (el clic que
  cae en otra tarjeta), el atajo que no llega sin foco, la reclamación que se lo quita al cuadro de
  texto (el lienzo robándole el teclado a quien escribe), el cable despegado de su conector, el ancla que
  se olvida de la escala del zoom y el cuello que no cabe en el hueco (la Z).

### Lo que sigue viéndose así (declarado)

- **El clic y el teclado del lienzo ya están certificados con puntero real** (las sesiones de gestos
  nuevas: seleccionar, deseleccionar, arrastrar, borrar, deshacer, rehacer y renombrar, con la ventana
  en primer plano). El puntero inyectado sin UIAccess sigue descartado por WinAppSDK, así que lo que
  falte se mide con dedos de verdad.
- **Lo que aún no está medido y se declara**: la puntería del clic sobre los conectores de un cable
  (~12 px de diana: el operador cae 100–160 px por debajo, y ese clic perdido, con el botón derecho,
  arranca un desplazamiento del lienzo) y el rectángulo de selección cuando el lienzo está desplazado
  (con el lienzo centrado acierta). El contenedor que se lleva el foco tras el clic ya está
  identificado —es del *framework*, no del producto— y el lienzo se defiende de él.
- **La forma nueva del cable está en el host multiplataforma, no en el escritorio**: el escritorio dibuja
  con el control de siempre (la puntita retirada y los tramos rectos), así que allí los cables siguen
  viéndose como antes; su clic sobre el cable, en cambio, ya se calcula con la misma curva compartida.
  Llevarle también la forma exige que el escritorio dibuje con la geometría del núcleo.
- Los selectores de variables y los diálogos de nodo caen a su no-op seguro (sus servicios del host
  siguen pendientes de cablear); el diálogo de ficheros ya es real desde el tramo anterior.

---

## 14. El tramo de los ajustes, el tema y el idioma del host (compilación 6309 → 6550)

El host multiplataforma estrenó su **superficie de ajustes** —hasta entonces tenía el lienzo, los paneles y la
barra de zoom, pero ningún sitio donde cambiar nada— y con ella **sus propios textos en dos idiomas**. Cifras
del tramo, medidas: **1875 → 1894 pruebas superadas** (1 omitida); **56 → 62 mutaciones declaradas, todas
mordiendo**; el sondeo en runtime pasó de **80 a 83 comprobaciones** del lienzo **más 9 propias de los ajustes**
(en su modo, porque cambiar de tema e idioma a mitad de las sondas del lienzo las tumbaba).

### Lo que ves

- **El botón «Ajustes» en la cabecera del host**: abre una superficie de **cuatro secciones** —Almacenamiento y
  rutas, Apariencia e idioma, Rendimiento y ejecución, Herramientas externas— con su pie de Cancelar/Guardar.
  Es el mismo cuadro de ajustes del escritorio, con las mismas opciones y los mismos hábitos.
- **Cambiar el tema y el idioma desde ahí, y que aguante**: eliges el tema en su desplegable, pulsas Guardar y el
  lienzo se repinta —fondo, tarjetas, cables y acentos—; eliges el idioma y **los textos del marco cambian de
  idioma sin reiniciar**. Al cerrar la aplicación y volver a abrirla, **siguen puestos los dos**.
- **La elección, cuando se guarda**: el tema y el idioma se aplican **al guardar**, no al pasar por el
desplegable, así que **Cancelar deja la aplicación exactamente como estaba**. Es la misma regla de la ventana de
  ajustes del escritorio; el cajón de control es el que aplica en vivo.
- **El guardado es el del producto**: los ajustes escriben el fichero de preferencias de siempre, con las
  mismas claves y los mismos valores por defecto que el escritorio (lo que ya hubiera se conserva).

### Lo que no se ve (y sostiene lo anterior)

- **El diccionario del host, en dos idiomas**: sin él, elegir «English» re-culturaba el proceso y todo seguía
  en español por el texto incrustado en el código. Ahora cada renglón del marco sale de su tabla (inglés y
  español), y una guardia exige que **ninguna clave citada falte en ninguna de las dos**.
- **La superficie se prueba sola**: un sondeo en modo propio (`-SelfCheckSettings`) abre la superficie, cambia
  tema e idioma, comprueba que llegan al producto y **devuelve las preferencias del usuario como estaban**;
  corre aparte porque cambiar tema e idioma a mitad de las pruebas del lienzo deja esas pruebas ciegas.
- **La coartada del verde, retirada**: la sonda del lienzo probaba con un tema fijo y «restauraba» a otro fijo —
  era verde *porque* el arranque ignoraba el tema guardado—. Ahora elige el tema contrario al activo y devuelve
  el de la entrada, en las dos direcciones.
- **Los ajustes del host, ejercidos con la aplicación abierta**: no por dentro, sino por sus controles reales
  (el botón, las pestañas, los desplegables, las casillas, Guardar) desde otro proceso, con el vigilante de
  píxeles midiendo. Cambiar el tema y el idioma, guardar, **cerrar y reabrir** y comprobar en píxeles que el
  lienzo arranca con el tema guardado y el marco en el idioma guardado.

### Lo que sigue viéndose así (declarado)

- Faltan dos pestañas que el escritorio sí tiene: **Actualizaciones** y **Modelos de IA** (el cuadro portable
  las trae; la vista del host todavía no).
- El **menú principal / barra de control** completa del escritorio (tema, idioma, ejecutar, ajustes y atajos en
  una sola barra) **no está portado**: el host tiene el botón de ajustes, la barra de zoom y los atajos del
  lienzo.
- Los **selectores de variables** y los **diálogos de nodo** siguen cayendo a su no-op seguro (sus servicios
  del host están pendientes de cablear); el diálogo de ficheros ya es real.
- Los controles de esta pantalla que **no exponen su valor** al canal de automatización externo (los
  desplegables y los campos numéricos) se comprueban por la **preferencia guardada** y por el **píxel**, no por
  una lectura de su valor; se declara en vez de inventar un veredicto.

---

## 15. El tramo del menú principal del host (compilación 6550 → 6584)

El host multiplataforma, que ya tenía lienzo, paneles, atajos y ajustes, estrenó **su barra de control**: la
misma barra del escritorio, con sus tres islas y su cajón de menú. Cifras del tramo, medidas: **1894 → 1902
pruebas superadas** (1 omitida); **62 → 66 mutaciones declaradas, todas mordiendo**; el sondeo en runtime pasó
de 83 comprobaciones del lienzo y 9 de los ajustes a **83 + 9 + 14 propias del menú** (en su modo, por la misma
razón que los ajustes: su ciclo mueve el documento y dejaría ciegas las pruebas del lienzo).

### Lo que ves

- **La barra, arriba**: el botón de menú, el nombre del producto, el **Modo Prueba** y el **Vigilante**, más las
  órdenes del ciclo —**Ejecutar** y **Depurar** cuando no hay nada corriendo, **Pausar** y **Detener** mientras
  corre, **Siguiente Paso** y **Continuar** en una depuración— y las herramientas: Deshacer y Rehacer (que se
  **apagan** cuando no hay nada que deshacer), Revertir Archivos e Inspector.
- **El cajón, al pulsar «Menú»**: un velo sobre la escena y un panel de 320 px con el **tema**, el **idioma**,
  la entrada a los **ajustes**, el **Inspector** y la versión del producto al pie.
- **Lo que no se dibuja**: lo que este host no puede cumplir. Nuevo, Cargar y Guardar Flujo, el estudio de
  temas, las métricas, el explorador de ficheros virtuales, el diseñador de dataset, el manual, los ejemplos,
  «Acerca de» y el aviso de actualización **no están**; y las teclas del escritorio que ligan el ciclo (F5, F10,
  Shift+F5) y el flujo (Ctrl+N, Ctrl+O, Ctrl+S) **tampoco hacen nada aquí**. Ni un botón ni una tecla prometen
  lo que no hay.

### Lo que no se ve (y sostiene lo anterior)

- **Todo el menú es una vista del núcleo**: las entradas llaman a los mismos comandos del cuadro de mando
  portable que el escritorio, así que el estado que enseña la barra no puede contradecir a la ejecución.
- **Los textos son los del escritorio, copiados**: clave por clave y en los dos idiomas. Una traducción propia
  del host sería otra interfaz.
- **Se prueba solo**: un sondeo propio de **14 comprobaciones** pulsa las entradas por el mismo canal que un
  lector de pantalla y lee el estado del cuadro de mando; corre aparte porque su ciclo mueve el documento.
- **Lo que falta está escrito**: **once entradas pendientes**, una cumplida por el host y **seis atajos**, en
  tres tablas del propio control. La guardia recorre las órdenes del escritorio y exige destino para cada una
  —dibujada, pendiente o cumplida—, así que portar una entrada a medias no puede pasar por portada.
- **El menú, con la aplicación abierta**: pulsar «Menú» **se ve en el píxel** (el velo oscurece la escena y la
  escena vuelve al recogerse), el Inspector recoge la columna derecha hasta dejarla en lienzo, y **Deshacer
  deshabilitado rechaza la orden** en vez de tragársela. Al terminar, tus preferencias quedan **intactas**.

### Lo que sigue viéndose así (declarado)

- **Los paneles de los nodos** (diálogos de parámetros y selectores de variables) siguen sin interfaz en este
  host: caen a su no-op seguro.
- Las **once entradas** y los **seis atajos** del menú del escritorio, arriba: el host no tiene todavía las
  ventanas ni los diálogos que las cumplen.
- Los desplegables de **tema e idioma del cajón** se han ejercido por su presencia, su enlace y su catálogo; su
  selección en vivo **no se tocó** en la sesión del menú para no escribir tu fichero de preferencias (esa misma
  ruta ya está medida en la sesión de los ajustes).

---

## 16. El tramo de los paneles de nodo del host (compilación 6584 → 6638)

El host multiplataforma estrenó **los paneles que cada nodo tiene dentro**: el **editor de texto y prompts** del
parámetro largo y el **selector de variables**. Hasta ahora los dos botones existían y **no hacían nada** —sin
error en pantalla, el usuario pulsaba y no pasaba nada—. Cifras del tramo, medidas: **1902 → 1911 pruebas
superadas** (1 omitida); las dos corridas de cierre dejaron **un rojo distinto cada una** —la de hilos y la de
los ejemplos de punta a punta— y **las dos pasan en aislamiento**: carga de la máquina, no regresión; **66 → 70
mutaciones declaradas**, las cuatro nuevas mordiendo; el
sondeo en runtime pasó de 83 del lienzo + 9 de los ajustes + 14 del menú a **83 + 9 + 14 + 24 propias de los
paneles de nodo** (en su modo, por la misma razón que las anteriores).

### Lo que ves

- **«✎» en un parámetro de texto largo**: abre el editor, **con el valor que el nodo ya tenía** dentro (no vacío);
tiene su botón para insertar una variable en el punto del cursor, otro para vaciar la caja, y al pulsar
**«Guardar y Aplicar»** el texto queda **en el parámetro del nodo**.
- **«{x}» en la fila de un parámetro**: abre el **catálogo de variables** del flujo, **poblado** (47 variables en
el flujo de ejemplo), con buscador que filtra al escribir, el detalle de cada variable y **«Insertar Variable»**
que escribe el token elegido en el parámetro. El campo del nodo **se actualiza a la vista** en el acto.
- **Los dos diálogos son los del escritorio**: mismos textos y mismo comportamiento, porque son la **misma
lógica** —los cuadros de mando del núcleo— con una vista distinta encima.

### Lo que no se ve (y sostiene lo anterior)

- **El servicio de diálogos del host, con su censo**: de las **9** puertas de diálogo del producto, este host
**sirve 2** (el editor y el catálogo) y **declara las 7** que no, cada una con su razón. Lo que se pide y no se
sirve **no se cancela en silencio**: queda en una traza y en la consola de la aplicación, porque un «cancelado»
mudo se lee como un error tuyo.
- **El defecto que se encontró usándolo**: las cajas de las filas del inspector sólo escribían **en un sentido**,
así que el valor que insertaba el diálogo no volvía al campo (habrías visto desaparecer tu inserción). Se arregló
el atado en las dos direcciones, en las tres clases de caja.
- **Cuatro defectos declarados que las pruebas matan**: quitar el anclaje del servicio del arranque (el catálogo
existe y las filas siguen sin alcanzarlo), abrir desde la fila un menú que este host no tiene, un editor que no
devuelve el texto confirmado y un campo que no muestra lo que el diálogo escribió. Cada uno tiene su testigo y su
control: **los cuatro muerden**.
- **Se prueba solo**: un sondeo propio de **24 comprobaciones** recorre el camino entero —abrir el catálogo,
filtrarlo, insertar el token **y leerlo del parámetro del nodo**, abrir el editor sembrado con el valor y guardarlo—
por dentro de la aplicación, no en una maqueta.
- **Los paneles de nodo, con la aplicación abierta**: 25 de 25 pasos; el modal **se ve en el pixel** (el catálogo
oscurece la escena y al cerrarse vuelve al color de base), el campo del nodo pasa de vacío a `{FileName}` al
insertar y el texto escrito en el editor queda en el parámetro. Al terminar, tus preferencias quedan **intactas**.

### Lo que sigue viéndose así (declarado)

- **Siete puertas de diálogo sin interfaz en este host** (aviso de actualización, explorador de ficheros virtuales,
Acerca de, métricas del flujo, estudio de temas, modelos de IA y la apertura de los ajustes por esta vía): se
declaran, no se fingen.
- El **«{x}» de las filas abre el catálogo completo**, no el menú emergente del escritorio: el host no tiene menú
emergente, y el catálogo **es** la primera entrada de aquel menú.
- Las **entregas anteriores** siguen como quedaron: las **once entradas** y los **seis atajos** del menú del
escritorio, y las pestañas **Actualizaciones** y **Modelos de IA** de los ajustes.

---

## 17. El tramo de las entradas y los atajos que faltaban del menú (compilación 6638 → 6666)

El host multiplataforma completó **su menú**: lo que quedaba declarado «sin interfaz» del cajón —Nuevo,
Cargar y Guardar Flujo, Manual, Ejemplos y Acerca de— **ya está**, y las seis teclas que el escritorio liga
al ciclo y al flujo (F5, F10, Shift+F5, Ctrl+N, Ctrl+O, Ctrl+S) **hacen aquí lo mismo que allí**. Cifras del
tramo, medidas: **1902 → 1912 pruebas superadas** (1 omitida); **66 → 71 mutaciones declaradas**, la nueva
mordiendo (y dos anteriores actualizadas al producto nuevo, también mordiendo); el sondeo en runtime pasó de
14 comprobaciones del menú a **25**.

### Lo que ves

- **En el cajón, dos secciones nuevas**: **GESTIÓN DE FLUJOS** —**Nuevo**, **Cargar…** y **Guardar…**— y
**AYUDA Y RECURSOS** —**Manual de Usuario**, **Ejemplos de Flujos** y **Acerca de**—. Contigo delante: Nuevo
pregunta antes de vaciar el lienzo, Cargar y Guardar abren el selector de archivos del sistema, el Manual abre
el manual, Ejemplos abre la carpeta de ejemplos y **Acerca de** enseña la versión y la información del producto.
- **Las teclas del escritorio, por fin**: F5 continúa, F10 avanza un paso, Shift+F5 detiene, y Ctrl+N / Ctrl+O /
Ctrl+S hacen lo mismo que las tres entradas de flujo del cajón.

### Lo que no se ve (y sostiene lo anterior)

- **La frontera que las bloqueaba, cruzada sin tocar el contrato**: esas órdenes piden en el núcleo un diálogo
**síncrono**, y desde el hilo de interfaz este host devuelve «nada» (el selector exige el hilo de UI y la
confirmación no puede bloquearlo). En vez de copiar la lógica en la ventana, **el diálogo se separó de la
operación** en el cuadro de mando portable —quien ya tiene la ruta (o la respuesta) no necesita el diálogo— y
el host aporta lo que sí sabe hacer: sus diálogos **asíncronos**.
- **Ninguna tecla muda**: la tabla que enruta los seis atajos es la **misma** que lee la guardia, así que
quitar una fila deja la tecla sin ruta y sin declaración — y eso cae en las pruebas.
- **Cuatro defectos declarados que las pruebas matan**: una entrada de flujo que se cumpliese por el comando
síncrono (el botón se pulsa y no pasa nada), una orden del escritorio sin destino en el host, un atajo del
escritorio sin ruta y sin declaración, y una entrada declarada que además estuviese dibujada. Todas muerden.
- **Se prueba solo**: el sondeo del menú subió a **25 comprobaciones** y mide el **efecto**, no el gesto: abre
«Acerca de» y lee la versión del producto, pide «Nuevo Flujo» y comprueba que cancelar deja el grafo intacto,
confirma y comprueba que el lienzo queda vacío, **guarda y vuelve a cargar** por el canal asíncrono, y
comprueba que F5 llega al comando del ciclo (reanudar deja su línea en la consola).
- **El menú, con la aplicación abierta**: 27 de 27 pasos. El velo del cajón y el modal **se ven en el píxel**
(el fondo pasa de `#FCF8F8` a `#585454` al desplegar y a `#B0ACAC` con «Acerca de» abierto, y vuelve al
cerrarlo), «Acerca de» enseña la versión al canal de observación, Ctrl+N por **teclado físico** abre la misma
confirmación y F5/F10 dejan su rastro. Al terminar, tus preferencias quedan **intactas**.

### Lo que sigue viéndose así (declarado)

- **Cinco entradas del cajón siguen sin interfaz en este host**, cada una con su razón: el **Estudio de temas**
(edita temas por secciones), las **métricas**, el **explorador de archivos virtuales**, el **diseñador de
dataset** y el **aviso de actualización** (este host todavía no comprueba si hay una versión nueva). No se
dibuja un botón que no pueda hacer nada.
- **«Acerca de» aquí es una ventana modal** dentro de la única ventana del host; en el escritorio es una
ventana aparte. La misma información, contada igual.
- **Ctrl+O y Ctrl+S** abren el selector del sistema: en la sesión de medida no se pulsaron con teclado físico
(se llevarían por delante la observación), así que su camino lo atan las pruebas y su mitad sin diálogo se
ejerció guardando y cargando sobre un fichero temporal.

---

## 18. El tramo de las ventanas que faltaban del host (compilación 6666 → 6713)

De las cinco entradas que el tramo anterior dejó «sin interfaz», **cuatro ya la tienen**: el **Estudio de
personalización de temas**, las **métricas** del flujo, el **explorador de archivos virtuales** y el **aviso de
actualización**. La quinta —el **diseñador de dataset**— sigue declarada con su razón. Cifras del tramo,
medidas: **1912 → 1915 pruebas superadas** (1 omitida); **71 → 75 mutaciones declaradas**, las cuatro nuevas
mordiendo; el sondeo del menú pasó de **25 a 37 comprobaciones**, y el catálogo de diálogos del host pasó de
**3 servidas + 6 declaradas** a **7 + 2**.

### Lo que ves

- **Estudio de Temas**: se abre desde el cajón y trae el **catálogo de temas** con su nombre, su estado y sus
  botones de **Aplicar**, **Guardar** y **Cerrar**; debajo, los **ajustes del tema por secciones** (nueve
  secciones, treinta y cuatro ajustes editables: colores con su muestra, números con su rango y sus flechas,
  elecciones). Cada fila es del **mismo editor del núcleo** que el escritorio: aquí no hay una segunda versión.
- **Métricas**: la duración total, el desglose **por nodo** con sus columnas, y el pie que resume —en la sesión
  de medida, «**3 nodos analizados. 0 cuello(s) de botella.**» para el flujo de ejemplo.
- **Explorador de archivos virtuales**: buscador, lista, selección y detalle de lo que un flujo deja en su
  almacén virtual. Su **chip en la barra** se enciende con el recuento en cuanto hay archivos virtuales.
- **Aviso de actualización**: si hay una versión nueva, el **distintivo de la barra** la enseña con su número, y
  la ventana cuenta las versiones, el formato del paquete, las novedades y el **progreso de la descarga**; se
  cierra desde su propio botón y recuerda la versión que le hayas pedido ignorar.

### Lo que no se ve (y sostiene lo anterior)

- **El menú, entero**: el cajón pasa de once a **catorce entradas** ancladas y **ninguna abre ya un botón que no
  haga nada**. Las cuatro ventanas son **superficies modales de la ventana del host** (mismo criterio que
  «Acerca de»), servidas por el **catálogo de diálogos** del servicio de ventanas, y cada una lleva la **clave
  del catálogo como ancla**, de modo que se puede comprobar desde fuera que es la que se pidió.
- **La comprobación de actualizaciones del arranque** —la misma del escritorio, en segundo plano y respetando la
  versión ignorada— entra de verdad: sin ella, el aviso que ya existía no lo pediría nadie. Queda **fuera de los
  sondeos**, para que medir no dependa de la red.
- **Los textos se copian, no se re-traducen**: **192 claves** del diccionario del escritorio en los dos idiomas,
  exigidas al carácter por las pruebas.
- **El Estudio de temas declara lo que no dibuja** (su vista previa en vivo y Eliminar / Importar / Exportar,
  que piden el diálogo **síncrono** que este host no puede dar): está en una tabla con su razón, y las pruebas
  impiden que vuelva a dibujarse sin servir.
- **Cuatro defectos declarados que las pruebas matan**: una ventana servida que no esté en el catálogo, una
  entrada de ventana que no ejecute su orden, un aviso de actualización que nadie encienda y un estudio de temas
  que esconda lo que no sirve sin declararlo. Las cuatro muerden.
- **Se prueba solo**: el sondeo del menú sube a **37 comprobaciones** y abre cada ventana nueva, lee su contenido
  y la cierra dejando el lienzo como estaba; una de las pruebas nuevas de la guardia **nació sin morder** y hubo
  que endurecerla (buscaba las órdenes dentro de la tabla que las declaraba).
- **Las ventanas, con la aplicación abierta**: **25 de 25 pasos**. El Estudio de Temas y las Métricas se abren,
  se leen por el canal de observación (nueve temas en la lista, tres nodos y el pie de métricas) y **se ven en el
  píxel** (el fondo pasa de `#FCF8F8` a `#B0ACAC` con el modal y vuelve al cerrarlo). Al terminar, tus
  preferencias quedan **intactas**.

### Lo que sigue viéndose así (declarado)

- **El diseñador de dataset sigue sin interfaz en este host**, con su razón escrita: su ventana la monta el
  propio plugin con su juego de herramientas, y abrirla aquí pide una vista del host sobre una lógica que vive
  dentro de su ensamblado. No se dibuja un botón que no pueda hacer nada.
- **En el Estudio de temas**: **Eliminar**, **Importar** y **Exportar** no están dibujados (necesitan el diálogo
  síncrono) y la **vista previa en vivo** tampoco (necesitaría su propia copia de los colores del lienzo).
  Declarado en vez de fingido.
- **Las cuatro ventanas son modales dentro de la única ventana del host**; en el escritorio cada una es una
  ventana aparte. La misma información, con los mismos textos.
- **El aviso de actualización se ejercitó con una novedad sintética**; en el uso normal sólo aparece cuando hay
  una versión nueva de verdad.
- **Sigue pendiente**: las pestañas **Actualizaciones** y **Modelos de IA** de la propia ventana de ajustes, el
  **gestor de presets de medios** y el de **contraseñas**, y el **empaquetado y la entrega** del host (paquete,
  firma y publicación).

---

## 19. El tramo del diseñador de datasets, las pestañas de ajustes y el editor de URLs (compilación 6713 → 6818)

El tramo anterior dejó tres cosas «sin interfaz»: una entrada de menú —el **diseñador de datasets**—, dos
pestañas de la ventana de ajustes y la **edición de URLs por modelo**, que ni siquiera tenía punto de entrada.
Las tres funcionan ya. Cifras del tramo, medidas: **1915 → 1917 pruebas superadas** (1 omitida); **75 → 80
mutaciones declaradas**, todas las nuevas mordiendo y una reapuntada; el sondeo del menú pasó de **37 a 42
comprobaciones** y el de ajustes de **9 a 18**, y el catálogo de diálogos del host pasó de **7 servidas + 2
declaradas** a **10 servidas + 1 declarada**.

### Lo que ves

- **Diseñador de Datasets**: se abre desde el cajón y trae el **buscador**, el **catálogo de datasets** con sus
  conjuntos (en la sesión de medida, siete, con «Cómics y Manga (Oficial)» y sus 40 elementos) y las **tres
  pestañas** —árbol, DSL y JSON—, más las órdenes de **añadir archivo** y **quitar nodo**. Es el mismo editor
  del plugin: aquí no hay una segunda versión del diseño de datasets.
- **Ajustes, dos pestañas más**: **Modelos de IA** (el catálogo del gestor del núcleo, con su carpeta, su estado
  y las acciones de descargar y borrar por fila —en la sesión de medida, veinticuatro modelos—) y
  **Actualizaciones** (tu versión, el formato del paquete, los canales y la comprobación automática). La ventana
  de ajustes pasa a **seis secciones**.
- **Editor de URLs de un modelo**: cada fila del catálogo de modelos tiene ahora su acción de **URLs**, y abre un
  editor con las **URLs de descarga** de ese modelo (una por línea, en orden de prioridad), el **recuento** que se
  actualiza mientras tecleas, su **distintivo** (oficial o personalizado), y las órdenes de **probar**,
  **restablecer** y **guardar**. Guardar escribe la configuración del modelo **donde la escribe el escritorio**, de
  modo que el motor de descargas la usa igual.

### Lo que no se ve (y sostiene lo anterior)

- **La frontera que había bloqueado el diseñador, cruzada sin reimplementarlo**: su ventana la monta el propio
  plugin con su juego de herramientas, y un host como este no puede montar una ventana ajena. Ahora el **nodo
  declara al SDK qué diálogo quiere y qué contiene** (una clave del catálogo compartido y su modelo de vista),
  y el host pinta esa clave **con su propia vista sobre el mismo modelo**. Si la vista se hiciera su propio
  modelo, habría **dos verdades sobre los mismos datasets**: hay una prueba que lo impide.
- **Ninguna orden de menú sin destino**: la tabla de entradas pendientes del host queda **vacía** por primera
  vez. Ya no hay ningún botón del menú del escritorio que no esté dibujado, declarado o cumplido por el host; la
  tabla se conserva (con su prueba) para que lo que vuelva a quedarse sin destino tenga dónde declararse.
- **El editor de URLs escribe donde escribe el escritorio, y no en un segundo sitio**: el guardado lo hace el
  **modelo de vista del editor** (el mismo que envuelve su ventana del escritorio), que escribe en el almacén del
  gestor de modelos; la vista del host **no escribe nada por su cuenta** y una prueba lo exige. Dos sitios
  escribiendo lo mismo serían dos verdades sobre las URLs de un modelo.
- **El botón hace lo que dice, y eso también se vigila**: la acción de URLs tiene su **rama propia**, y un defecto
  declarado la quita para comprobar que la prueba lo caza —sin esa rama, el botón **descarga el modelo** en vez de
  abrir su editor, que es peor que un botón que no hace nada—.
- **Seis defectos declarados que las pruebas matan**: un nodo que declare su superficie con una clave que no es
  la del catálogo, un diseñador que falte del censo de ventanas, una vista del diseñador que se construya su
  propio modelo, una sección de ajustes que pierda el panel que conmutaba entre ellas, una entrada declarada que
  además esté dibujada, y una acción de fila **que descargue el modelo en vez de abrir su editor**. Las seis
  muerden. (Un séptimo defecto declarado **se retiró** al cerrarse su frontera: vigilaba que la fila del catálogo
  **no** dibujara la acción de URLs, y ahora la dibuja.)
- **Se prueba solo**: el sondeo del menú sube a **42 comprobaciones** —abre la superficie del diseñador sobre el
  modelo del plugin y lee el catálogo— y el de ajustes a **18**: las seis secciones con su rótulo, el catálogo de
  modelos, la sección de actualizaciones y **seis pasos que pulsan la acción de URLs de la fila, escriben una URL en
  la caja del editor y comprueban que queda guardada** en el almacén del gestor (y luego devuelven la configuración
  del usuario a como estaba).
- **Ejercido con la aplicación abierta**: **39 de 39 pasos** en una sesión —además de las dos superficies nuevas,
  las **dos ventanas que el tramo anterior no había ejercido**: el **explorador de archivos virtuales** (cinco
  anclas, cuatro filas con nombre real, visible en el píxel) y el **aviso de actualización** (seis anclas, tu
  versión contra la nueva, y al cerrarlo **el distintivo de la barra sigue puesto**)— y **24 de 24 pasos** en otra
  —el **editor de URLs**: se pulsa la acción de la fila, el editor abre con las URLs del modelo, escribir dos URLs
  pone su recuento en «2», guardar cierra el modal y **al reabrir el distintivo dice «Personalizado»**, que es la
  prueba de que el valor quedó escrito en el almacén del gestor—. Al terminar las dos, tus preferencias quedan
  **intactas** (byte a byte).
- **Y dos defectos del propio instrumento los encontró la medición**, escritos para el guion futuro: el nombre que
  el canal externo lee de una **fila enlazada** no es el de la fila sino el del **tipo** de su modelo de vista, y el
  píxel tras cerrar el modal vuelve al de la **superficie que sigue abierta detrás**, no al del lienzo.

### Lo que sigue viéndose así (declarado)

- **Si dejas el editor de URLs sin ninguna URL válida**, el aviso lo pide el modelo de vista por el servicio de
  diálogos —igual que en el escritorio—, pero Windows sólo admite **un diálogo a la vez** y el editor ya está
  abierto: la petición queda escrita en la consola de la aplicación y **el host repite el aviso dentro del propio
  editor**, sin cerrarlo sobre algo que no aceptó. Es la única frontera de esta superficie.
- **El hallazgo del escritorio, anotado y sin tocar**: su ventana de URLs de modelos enlaza un «Guardar» que no
  existe en su modelo de vista, así que ese botón no guarda. Este host **no hereda el defecto** (llama al mismo
  guardado del modelo de vista), pero el escritorio no se toca: es una decisión aparte.
- **Los ajustes del flujo siguen sin abrirse por el catálogo de diálogos**: su superficie ya tiene puerta en la
  barra y el cajón, y una segunda puerta sería una segunda copia de lo mismo.
- **Diseño de datos**: el diseño de datasets se edita con el mismo editor del plugin, pero la información extra
  que el escritorio añade por encima (el archivo y el tamaño mínimo del modelo) aquí no se dibuja porque este
  host no tiene esa fuente; se dibujan los campos que sí existen.
- **Sigue pendiente**: el gestor de **contraseñas** (la **única** superficie de usuario que queda sin portar) y
  el **empaquetado y la entrega** del host (paquete, firma y publicación). *(El gestor de presets de medios se
  cerró en el tramo 20.)*

---

## 20. El tramo del gestor de presets de medios del host (compilación 6818 → 6893)

El **Gestor de Presets de Medios** —la superficie donde se crean y editan los presets con los que el nodo de
transcodificación convierte audio y vídeo— ya se puede usar en el host multiplataforma, y se usa **igual que en
el escritorio**.

### Lo que ahora puedes hacer

- **Abrirlo desde donde lo abrías**: por el botón **«🎬 Presets...» de la tarjeta del nodo** y por el botón
  **«🎬» de la fila del preset** en el inspector. Los dos abren **el mismo gestor**.
- **Ver tu catálogo**: la lista trae tus presets tal y como están guardados, con el que tengas elegido en el
  formulario y **su** descripción (no una vacía).
- **Crear, editar y retirar presets**: «Nuevo» da de alta uno personalizado, «Guardar» deja escrito lo que hayas
  tecleado —también en el fichero del almacén, que es el que lee el nodo al transcodificar— y «Eliminar» lo
  retira. Los presets del sistema quedan protegidos.
- **Desplegar la tarjeta**: la tarjeta de un nodo tiene por fin su **conmutador de parámetros** (el chevron de la
  cabecera). Antes no había forma de desplegar una tarjeta en este host, así que el botón «🎬 Presets...» estaba
  **dibujado y sin puerta**.

### Lo que sigue viéndose así (y por qué)

- **El gestor de contraseñas sigue sin portar**: es la **única** superficie de usuario del escritorio que queda,
  y su fila lo dice con ese motivo en vez de fingir un botón que no abre nada.
- **Eliminar y Restablecer sí piden confirmación** (desde la compilación 6923, ver §21): antes no la pedían y
  además se comportaban distinto según por dónde entraras —por la **fila** el borrado se ejecutaba **sin
  preguntar** y por la **tarjeta** **no se ejecutaba** y tampoco avisaba—. Queda como frontera el **mismo defecto
  en otras seis órdenes destructivas** del host (cerrar un flujo con cambios sin guardar, restablecer un tema,
  limpiar el almacén virtual, borrar un modelo descargado y quitar un dataset): ahí la orden **no hace nada y no
  avisa**, y su arreglo está acotado y localizado.
- **El empaquetado y la entrega del host siguen pendientes**: el programa no se reparte instalado; lo ejecuta
  quien lo compila.

### Cómo se comprobó

Con la aplicación abierta: se añadió el nodo de transcodificación, se abrió el gestor **por las dos puertas**, se
escribió una descripción en la caja real y se comprobó **en el fichero del almacén** que quedaba escrita, que al
reabrir seguía ahí y que al restaurarla el fichero volvía **byte a byte** a como estaba; y se dio de alta un preset
(10 → 11) y se retiró (11 → 10) dejando el catálogo **idéntico** y los ajustes del usuario (tema, idioma, carpeta
de salida y favoritos) **intactos**. **34 de 34 pasos.**

---

## 21. El tramo de la confirmación de las órdenes destructivas del gestor (compilación 6893 → 6923)

Las órdenes que destruyen algo en el **Gestor de Presets** —**«Eliminar»** y **«Restablecer»**— **preguntan antes**,
y lo hacen **igual por las dos puertas** (la tarjeta del nodo y la fila del preset). Antes no era así: por la fila
el borrado se ejecutaba **sin preguntar** y por la tarjeta **no se ejecutaba y tampoco avisaba**, así que el mismo
botón hacía dos cosas distintas según por dónde entraras.

### Lo que ahora puedes hacer

- **Borrar con red**: «Eliminar» abre la pregunta **sobre el propio gestor** —con su velo y sus botones— y **no se
  borra nada hasta que contestas**. Un «no» deja el preset donde estaba; un «sí» lo retira de verdad del almacén
  que lee el nodo que transcodifica.
- **Restablecer con red**: «Restablecer» avisa igual de que va a devolver el catálogo a los presets del sistema,
  y puedes cancelarlo sin perder nada.
- **Cancelar con el teclado o con el botón de cerrar** no deja nada a medias: la pregunta se retira y se cuenta
  como un «no» (antes podía quedarse tomada y dejar el botón muerto para el resto de la sesión).
- **Los avisos del gestor se ven**: el «Preset guardado con éxito» se muestra **dentro** del gestor, y si llega una
  pregunta de verdad, la pregunta manda.

### Lo que sigue viéndose así (declarado)

- **Seis órdenes destructivas más siguen sin preguntar** en este host (cerrar o empezar un flujo con cambios sin
  guardar, restablecer un tema, limpiar el almacén virtual, borrar un modelo descargado y quitar un dataset
  sintético): en ellas el botón **no hace nada y no avisa**. Están localizadas, cada una con su arreglo acotado.
- **El empaquetado y la entrega del host siguen pendientes**: el programa no se reparte instalado; lo ejecuta
  quien lo compila.
- **El escritorio no cambia de comportamiento**: sigue confirmando como confirmaba.

### Cómo se comprobó

Con la aplicación abierta y **por cada una de las dos puertas**: se dio de alta un preset (10 → 11), se pulsó
«Eliminar» y se comprobó **en el fichero del almacén** que **seguía habiendo 11** mientras la pregunta estaba en
pantalla, que **cancelar dejaba 11** y que **confirmar dejaba 10**; y se canceló un «Restablecer» comprobando que
el catálogo quedaba **intacto**. Al terminar, el almacén quedó **byte a byte** como estaba y los ajustes del
usuario (tema, idioma, carpeta de salida y favoritos) **intactos**. **42 de 42 pasos.**

---

## 22. El tramo de las seis órdenes que destruían sin preguntar (compilación 6923 → 6961)

Seis botones del host que **destruyen** algo —**«Nuevo Flujo»**, **«Revertir Archivos»**, **«Eliminar»** del Estudio
de Temas, **«Limpiar»** del Explorador Virtual, **«Eliminar»** de un modelo de IA y **«Eliminar»** de un dataset
sintético— **no preguntaban nada** aunque el producto sí tuviera la pregunta escrita: usaban la vía **síncrona**
del contrato de diálogos, que en este host **no puede contestar de verdad** y devolvía «no» sin enseñar nada. El
síntoma del usuario era el peor de los posibles: **el botón no hacía nada y tampoco avisaba**.

### Lo que ahora puedes hacer

- **Los seis preguntan antes de destruir**, y la pregunta **se ve**: aparece con sus dos botones («Aceptar» y
  «Cancelar») y **no se borra nada hasta que contestas**.
- **Un «no» no destruye**: cancelar «Nuevo Flujo» deja el lienzo tal y como estaba; cancelar «Eliminar tema» deja
  el tema propio en el catálogo; cancelar «Limpiar» deja el almacén virtual entero.
- **Un «sí» sí destruye**, de verdad y de una vez: el lienzo queda vacío, el tema desaparece del almacén, el
  registro virtual se vacía.
- **«Eliminar tema» tiene puerta**: el Estudio de Temas tiene ya su botón «Eliminar» —habilitado sólo cuando el
  tema elegido es **tuyo**; los de fábrica son inmutables—. Antes la orden existía pero **no había ningún botón
  que la ofreciera**.
- **«Nuevo Flujo» por atajo y por menú se comportan igual**: `Ctrl+N` y el botón del cajón hacen lo mismo (el
  atajo ya no se saltaba la actualización de la línea de estado del ciclo).
- **«Eliminar modelo» pregunta en tu idioma**: era la única de las seis preguntas que estaba escrita dentro del
  programa —en español, cambiaras de idioma o no—; ahora se pide al diccionario como el resto de la interfaz, así
  que en inglés se lee en inglés.

### Lo que sigue viéndose así (declarado)

- **«Limpiar» del Explorador Virtual sigue sin botón**: ninguna vista —ni la del escritorio ni la de este host—
  lo dibuja. La orden pregunta bien cuando alguien la invoque, pero **hoy no hay quién**: poner el botón es
  diseño de interfaz nuevo, fuera de este tramo. Se declara en vez de fingirse.
- **«Eliminar modelo» no se prueba con el ratón** en la comprobación de abajo: retira ficheros **reales** del
disco (varios GB de modelos). Su comportamiento se mide en las pruebas automáticas.
- **El escritorio no cambia de comportamiento**: sigue confirmando como confirmaba, con su propio diálogo, y
  las seis órdenes se comportan allí igual que antes.
- **El empaquetado y la entrega del host siguen pendientes**: el programa no se reparte instalado; lo ejecuta
  quien lo compila.

### Cómo se comprobó

Con la aplicación abierta y **dos** de las seis órdenes, las dos que se pueden deshacer sin tocar tus datos.
**«Eliminar tema» del Estudio**: se creó un tema propio (el fichero del almacén pasó de **1 a 2** temas), se
pulsó «Eliminar» y se comprobó que la pregunta **estaba en pantalla**, que **seguía habiendo 2** mientras se
preguntaba, que **cancelar dejaba 2** y que **confirmar dejaba 1** —con tu catálogo **idéntico** al de partida y
el fichero **byte a byte**—. **«Nuevo Flujo»**: con el flujo de ejemplo cargado, se pulsó la orden y se comprobó
que **pregunta en su propio diálogo**, que **con la pregunta siguen las tres tarjetas**, que **cancelar las deja**
y que **confirmar vacía el lienzo** —y que al reiniciar la aplicación el ejemplo vuelve entero, porque el grafo no
se guarda en ningún fichero—. Al terminar, tu catálogo de temas y tu almacén de presets quedaron **byte a byte**
como estaban y tus ajustes (tema, idioma, carpeta de salida y favoritos) **intactos**. **33 de 33 pasos.**

**Cifras del tramo, medidas**: la suite pasa de **1930** a **1935** pruebas superadas (1 omitida, 0 errores); las
**cinco** mutaciones nuevas del tramo **muerden** (con su testigo en rojo y su control en verde) y el catálogo
publicado sube a **95 declaradas**; y las cuatro sondas del host quedan verdes —**88 · 42 · 46 · 18**
comprobaciones, ninguna fallo—. Una corrida intermedia de la suite trajo **1 fallo** cuyo nombre no quedó
registrado; las **dos corridas completas siguientes** quedaron verdes, igual que el conjunto de fin de flujo en
aislamiento.

---

## 23. El tramo del cable que ahora es el mismo en las dos aplicaciones (compilación 6961 → 7005)

FileFlow Studio tiene **dos aplicaciones** —la de escritorio y la multiplataforma— y las dos dibujan el mismo
lienzo. El **cable** entre dos puertos, sin embargo, no se veía igual en las dos: la aplicación de escritorio lo
dibujaba con la pieza gráfica que traía su lienzo, cuya curva **sale retirada del socket** y se une a él por dos
tramos rectos —en pantalla, una ese apretada con dos bajíos: una **Z**—, mientras que la multiplataforma ya
dibujaba la curva del producto, un cable que **nace y muere en sus dos sockets** y sale curvando desde el
primero.

### Lo que ahora puedes ver

- **El cable se ve igual en las dos aplicaciones.** La curva la decide una **sola pieza del núcleo**, así que ya
  no hay dos formas para la misma corriente de datos: sale del socket curvándose y llega al otro socket sin ningún
  tramo recto.
- **El cable que arrastras es ese mismo cable.** Mientras dibujas una conexión, el trazo no cambia de forma al
  soltar el botón: es la misma curva desde el primer píxel.
- **Todo lo demás sigue igual**: los colores por familia de tipo (archivo, texto, número, binario, colección,
  universal), el grosor, el cursor de mano al pasar por encima y el menú para **borrar un cable**.
- **Nada se movió de sitio**: los nodos, sus anclas y el gesto de conectar funcionan como antes; lo que cambió
  es quién dibuja el trazo.

### Lo que sigue viéndose así (declarado)

- **La comprobación de este tramo es automática**, no una sesión con el ratón: la aplicación de escritorio no
  tiene sonda propia (las de la multiplataforma sí). Lo que está medido es **la figura que el control va a
  pintar** —contra la pieza compartida, punto por punto— y el **árbol real del lienzo** (control materializado,
  ancla enlazada y trazo pintado con el color de su familia de tipo).
- **Dos sockets apilados** (sin hueco horizontal entre ellos) siguen dibujando una **recta vertical**, y la
  **caída tipo hilo** (los cables que no son simétricos) sigue sin implementarse. Las dos son fronteras
  heredadas del tramo que rediseñó el cable.
- **El empaquetado y la entrega siguen pendientes**: el programa no se reparte instalado; lo ejecuta quien lo
  compila.

### Cómo se comprobó

La suite completa pasa de **1935** a **1942 pruebas superadas** (1 omitida, 0 errores): cinco casos nuevos leen
la figura que el control va a dibujar y la comparan **punto por punto** con la pieza compartida (que nace y muere
en las anclas, que su cuello es el del núcleo —200 unidades en un hueco de 400, no las 45 del control que se
quitó—, que se da la vuelta cuando se arrastra desde una entrada, que sigue a sus anclas al mover un nodo y que
la traducción de la dirección habla los dos vocabularios), y una guardia barre el lienzo para que **ningún** cable
vuelva a la forma vieja; y el **cable que arrastras** se monta en una ventana real, con los estilos de la
aplicación, para comprobar que es el mismo control y que su curva también es la compartida. Una **mutación** que devuelve el cable a la pieza gráfica anterior **muerde** (el
catálogo publicado sube a **96 declaraciones**), y la aplicación multiplataforma —que ya dibujaba con esta
geometría— sigue verde en su sondeo: **88 comprobaciones, ninguna falla**, con sus medidas de cable intactas.

---

## 24. Cómo verificarlo

```powershell
# La suite completa (pruebas unitarias, de integración y de aspecto)
.\test.ps1

# Compilar y ejecutar la aplicación
.\run.ps1
```

El host multiplataforma (Uno Platform) tiene sus propios comandos:

```powershell
# Compilar (MSBuild de Visual Studio: los targets de WinAppSDK no corren con dotnet build) y lanzar
.\run-uno.ps1

# Lanzar sin compilar
.\run-uno-fast.ps1

# Los sondeos del host multiplataforma (el script espera y hereda el exit code: 0 = verificado)
..\run-uno.ps1 -SelfCheck            # sondeo interno en runtime (lienzo, paneles, atajos)
.\run-uno.ps1 -SelfCheckControlBar # sondeo de la BARRA DE CONTROL, su cajón, sus entradas y sus ATAJOS
.\run-uno.ps1 -SelfCheckDialogs    # sondeo de los PANELES DE NODO (editor de texto y catálogo de variables)
.\run-uno.ps1 -SelfCheckSettings   # sondeo de la superficie de AJUSTES (tema e idioma; restaura lo tuyo)
.\run-uno.ps1 -SelfCheckUia        # sondeo UIA externo (hijo python; exige python + pywinauto)
```

En Linux/macOS la ejecución y la limpieza están en `./run.sh` y `./clean.sh`; la suite se lanza con el mismo
`dotnet test` que usa el script de Windows.
