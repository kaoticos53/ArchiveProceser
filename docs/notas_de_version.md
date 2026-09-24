# Notas de versión — FileFlow Studio

**Versión 1.0.0 · compilación 5355 · 24 de septiembre de 2026**

Estas notas recogen **seis tramos**:

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

Están escritas en dos mitades a propósito —**lo que ves** al usar la aplicación y **lo que no se ve** pero es lo
que impide que lo primero se rompa sin que nadie se entere—. Todo lo que se afirma aquí está medido en el
registro técnico ([`PROJECT_WALKTHROUGH.md`](PROJECT_WALKTHROUGH.md), hitos 169 a 205): las cifras salen de ahí,
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

## 9. Cómo verificarlo

```powershell
# La suite completa (pruebas unitarias, de integración y de aspecto)
.\test.ps1

# Compilar y ejecutar la aplicación
.\run.ps1
```

En Linux/macOS la ejecución y la limpieza están en `./run.sh` y `./clean.sh`; la suite se lanza con el mismo
`dotnet test` que usa el script de Windows.
