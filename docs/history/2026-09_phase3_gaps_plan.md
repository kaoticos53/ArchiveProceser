# FASE 3 — Cerrar los silencios que quedan

> [!NOTE]
> **Estado (2026-09-22): 3A, 3B, 3C, 3D, 3E, 3F, 3G y 3H ✅ HECHAS. La fase 3 queda cerrada.** Recoge los huecos que quedaron abiertos tras las fases
> 2E-P8 a 2E-P12 y los ordena para ejecutarlos de uno en uno. **La fase 0** —poner en orden con commits lo ya
> hecho— sigue **pendiente** aunque 3A se haya ejecutado encima: el árbol acumula los cambios de todas las fases. Cada sub-fase se cierra igual que las anteriores:
> con pruebas nuevas, **verificación por mutación** (cada afirmación de las pruebas tiene que fallar cuando se
> rompe el código que la sostiene), build sin errores ni advertencias, suite completa en verde, y la fase
> documentada en [`2026-08_phase1_audit_plan.md`](2026-08_phase1_audit_plan.md) con su evidencia.

## De dónde sale esto

Las fases anteriores quitaron silencios uno detrás de otro —puertos que no existían cuando se emparejaban las
aristas, memoria de puertos que no viajaba en el archivo, formato sin versión, cables que se descartaban sin
contarse, ejecuciones vacías que decían «éxito»—. Al hacer inventario quedaron **siete huecos**, y el orden de
abajo no es el orden en que aparecieron: es el orden en que conviene tocarlos, por lo que cuesta descubrir que
algo se rompió si se hace al revés.

| # | Hueco | Naturaleza | Riesgo de no hacerlo |
|---|---|---|---|
| 1 | Un flujo de un formato **posterior** se puede abrir y **sobrescribir** | Pérdida de datos | Se pierde trabajo que el usuario no puede recuperar | ✅ 3A |
| 2 | El camino de ejecución de la **interfaz** no tiene prueba de extremo a extremo | Verificación | Cada cambio en el camino más usado entra a ciegas |
| 3 | El **pegado** descarta cables sin avisar | Silencio | Último camino de reconstrucción mudo que queda | ✅ 3C |
| 4 | Nadie **diagnostica** un flujo antes de ejecutarlo | Capacidad | «No hay nodos» es sólo el caso más simple de «no hay nada que hacer» |
| 5 | El formato son **dos formatos** que se leen entre sí | Simplificación | Dos escritores son dos sitios donde equivocarse |
| 6 | El contenedor no se entera de que su **subflujo cambió en disco** | Estado obsoleto | Puertos que ya no son los de la definición |
| 7 | El informe de carga vive sólo en la **consola** | Usabilidad | El aviso se lee, pero no se puede actuar sobre él |

**Lo que NO está en este plan, y por qué.** Los límites de la migración de formato (los casos de un switch sin
`CasesJson` y una definición incrustada perdida **no** se recuperan; un puerto de contenedor sin cable no dejó
rastro; la huella de un archivo es fecha y tamaño) y la caché del resolutor por nodo **no son defectos**: son
límites declarados, con prueba y con su porqué escrito en el código y en las fases 2E-P5 a 2E-P8. Intentar
«arreglarlos» sería inventar datos que el archivo nunca tuvo.

---

## Fase 0 (preliminar, no es un hueco) — Poner en orden lo que ya está hecho

**Objetivo.** Que el trabajo de las fases 2E-P8 a 2E-P12 deje de estar sólo en el árbol de trabajo.

**Cambios.** Ninguno de código. Commits separados por fase, con los archivos de cada una (el árbol arrastra
además cambios que no son de este trabajo: `.build_number` y lo que ya estaba modificado antes, que **no** se
tocan).

**Por qué va primero.** Cualquier fase de abajo se apoya en un `git diff` legible para saber qué cambió y poder
revertir un experimento. Sin eso, cada mutación y cada reversión es a ciegas.

## Fase 3A — Un flujo más nuevo que la aplicación no se sobrescribe ✅ HECHA

> Ejecutada el 2026-09-22: la regla vive en el guardado y mira el destino; la versión se lee del JSON crudo para
> poder reconocer un archivo posterior cuyo cuerpo no se enlaza; y un archivo ilegible o sin versión no se
> protege, porque bloquearlo impediría el guardado que lo repara. 9 pruebas, 5 mutaciones, suite
> **1370 superadas / 0 fallos / 1 omitida**. Detalle y evidencia en
> [`2026-08_phase1_audit_plan.md`](2026-08_phase1_audit_plan.md).

**Objetivo.** Que abrir un archivo escrito por una versión posterior del formato no acabe perdiendo los campos
que esa versión escribió.

**Por qué importa.** Hoy `WorkflowFormat.Plan` reconoce una versión posterior y, con buen criterio, **no
repara** —lo que dice el archivo manda—. Pero nada impide guardar encima: la app escribe con su formato, así que
los campos que no conoce desaparecen sin que nadie lo diga. Es la única pérdida de datos de la lista que el
usuario no puede deshacer.

**Cambios.** (1) **La regla vive en el guardado y mira el destino**, no el documento abierto: antes de
sobrescribir, si el archivo existe y declara una versión posterior a `WorkflowFormat.CurrentVersion`, el guardado
falla y la interfaz lo convierte en aviso y diálogo —con la salida honesta, elegir otra ruta—. Se descartó la
alternativa de recordar la versión con la que se abrió: el guardado de la app **siempre pregunta la ruta**, así
que lo que hay que proteger es el archivo concreto al que se va a escribir, y esa comprobación en el destino
cubre además el caso de un archivo que cambió mientras la aplicación estaba abierta.

(2) Al **abrir** también se dice, una vez: un flujo de un formato posterior se abre igual —el lector tolerante ya
lo hace—, pero el usuario se entera ahí, no cuando se lo rechacen al guardar.

**Cómo se verifica.** Guardar sobre un archivo que declara `FileFlow.Workflow.v3` falla y **no cambia sus bytes**
(se lee el archivo, no el DTO); guardar en otra ruta sí escribe, y la copia declara la versión actual; guardar
sobre un archivo del formato actual —o de uno anterior— sigue funcionando, que es la mitad que evita que la
protección se convierta en un estorbo; y al abrir un archivo posterior queda su aviso. Mutaciones: quitar la
comprobación del destino deja en rojo la del «no sobrescribe»; invertirla —rechazar cualquier versión distinta—
deja en rojo las de los formatos anteriores.

**Riesgo.** Bajo: no cambia el formato ni la lectura, añade una lectura del destino en el guardado (el archivo
se va a escribir de todas formas) y una negativa a sobrescribir.

## Fase 3B — Que la ejecución desde la interfaz se pueda probar de punta a punta

**Objetivo.** Poder ejecutar un flujo completo **desde la app** en una prueba, sin un bucle de mensajes de
interfaz.

**Por qué va antes que el resto.** Es un hueco de verificación, y de este camino dependen las fases 3D y, en
parte, 3F. Además ya obligó a renunciar a una prueba: la frontera de la fase 2E-P12 —«un flujo **con** nodos y
sin trabajo sigue siendo un éxito»— no se pudo fijar a ese nivel porque el test **se colgaba** en vez de fallar.

**Cambios.** (1) Aislar detrás de una abstracción el trabajo que el coordinador hace **al terminar** y que exige
el hilo de la interfaz —liberar modelos de IA y reclamar memoria—: una implementación real que despacha al hilo
de la interfaz y una que lo hace en línea, que es la que usan las pruebas. (2) Con eso, la prueba de extremo a
extremo: un flujo real (origen con archivos en un directorio temporal, hasta un destino) ejecutado por el
comando de la barra, comprobando el registro, el resumen y el estado de los nodos. (3) Recuperar la prueba de
frontera que se perdió, y **quitar los topes de diez segundos** que se pusieron para que un fallo no colgara el
suite: con el camino drivable, el tope deja de ser una muleta.

> [!IMPORTANT]
> **✅ HECHA (2026-09-22), y la premisa de arriba era falsa en un punto que cambió el trabajo.** Medido con una
> sonda antes de tocar nada: de las dependencias del hilo de la interfaz, el cronómetro del lienzo, las
> publicaciones **sin esperar** y el bucle de descarga funcionan desde cualquier hilo —no hacen nada hasta que
> el bucle los atienda—; **sólo** bloquea el despacho **esperado** del cierre. Así que no hubo que aislar
> trabajo ninguno: se inyectó el despachador —`IUiDispatcher`, que ya existía en el SDK con `NullUiDispatcher`
> para justo esto, sin inventar abstracción nueva— y, de paso, las **preferencias** del usuario, que el
> coordinador leía del proceso (directorio temporal, limpieza de intermedios y descarga de modelos al
> terminar) y que hacían que una prueba tocara la configuración real. El cronómetro se quedó como estaba: era
> un problema imaginario. **Resultado:** la frontera recuperada (un origen vacío no es un fallo), el camino del
> botón recorrido de punta a punta, los dos topes de diez segundos retirados, y **una mutación que cuelga en vez
> de fallar** al desoír el despachador —la razón exacta por la que el seam existe—. Lo que **no** queda
> verificado es el bucle de descarga de modelos: exige un nodo con modelo, y un doble aquí sería descubrible
> por el propio producto. Detalle y evidencia en [`2026-08_phase1_audit_plan.md`](2026-08_phase1_audit_plan.md).

**Cómo se verifica.** La prueba de extremo a extremo en verde y, en mutación, romper el resumen de telemetría o
el registro la pone en rojo (hoy ninguna prueba lo cubre). El tope retirado sólo se puede retirar si la prueba
pasa sin él —y se comprueba quitándolo—.

**Riesgo.** Medio: se toca la clase que orquesta toda ejecución de la interfaz. Mitigación: el cambio es de
*dependencia* (a quién le pide despachar), no de secuencia, y las pruebas actuales de la barra siguen corriendo.

## Fase 3C — El pegado cuenta lo que no puede reconstruir ✅ HECHA

**Objetivo.** Cerrar el último camino de reconstrucción que descarta cables en silencio: pegar o duplicar.

**Cambios.** (1) La regla se extrae a `ConnectionReconstructor.TryRebuild` y **la comparten** `Import` y
`Paste` —el informe `WorkflowGraphImportResult` pasa a llamarse `ConnectionRebuildReport`, porque producirlo el
pegado era lo que hacía mentir al nombre, y el texto vive en un solo sitio, `DroppedConnectionText`—.
`Paste`/`Duplicate` devuelven `ClipboardPasteResult`: nodos **e** informe en el mismo objeto, para que quien
pega no pueda olvidarse de mirarlo. (2) El aviso va en el **lienzo** (banner con su botón para descartarlo)
además del registro, y no se esconde por reloj: se retira al descartarlo, al deshacer el pegado o cuando el
siguiente pegado es sano.

**Hallazgo que cambió el alcance.** Al fijar «deshacer no deja rastro del aviso» resultó que **deshacer no
deshacía el pegado**: los cables se registraban solos y los nodos no, así que un Ctrl+Z deshacía la acción
anterior y dejaba los nodos pegados. Se arregló aquí (el pegado inscribe sus nodos como una sola acción),
porque el aviso daba por supuesto un deshacer que no existía.

**Cómo se verifica.** Pegar nodos cuyos puertos no emparejan informa con identidad y motivo; pegar nodos sanos
no informa de nada; deshacer el pegado no deja rastro del aviso; y la superficie —que una prueba de modelo no
ve— comprueba que el cartel del lienzo existe, aparece y ocupa sitio. Mutaciones: volver a descartar en silencio
deja en rojo las que informan; informar también de lo reconstruido deja en rojo las que callan —en los dos
caminos a la vez, que es la prueba de que la regla es una sola—; y desatar la visibilidad del cartel del XAML
deja en rojo la prueba de la superficie.

**Riesgo.** Medio-bajo por la superficie nueva; el informe y su lógica ya existían y estaban probados. El
deshacer del pegado sí fue un cambio de comportamiento, y va cubierto por su propia prueba.

**Lo que queda fuera.** La rama «el nodo no está» no se reproduce por el camino del pegado —el cargador
resuelve cualquier tipo presente en el proceso—; su rama es la que ya fija la apertura, y compartir la regla es
lo que hace que esa prueba valga por los dos caminos.

## Fase 3D — Diagnóstico antes de ejecutar ✅ HECHA

> Ejecutada el 2026-09-22: `WorkflowDiagnosis.Analyze` en Core es la regla única que los dos puntos de entrada
> consumen, y las dos guardias ad hoc de 2E-P11/P12 se retiraron con su clave de texto. Dos supuestos del plan
> cambiaron al medirlos: «sin nodo terminal» no puede buscarse en los puertos —**ningún** nodo del catálogo se
> queda sin salidas, así que habría avisado en todos los flujos— y «sin origen, sin terminal, inalcanzables»
> definidos por aristas son imposibles en un DAG, de modo que la forma se cuenta con el `PipelineRole` que cada
> nodo declara y lo que sí ocurre de verdad: qué nodos arranca el motor con un elemento vacío. 9 pruebas de
> tabla + una por punto de entrada, 6 mutaciones, suite **1401 superadas / 0 fallos / 1 omitida**. Detalle en
> [`2026-08_phase1_audit_plan.md`](2026-08_phase1_audit_plan.md).

**Objetivo.** Que los dos puntos de entrada digan **qué va a hacer** un flujo antes de lanzarlo, y no sólo que
está vacío.

**Cambios.** (1) Un diagnóstico en Core sobre grafo + cargador de plugins, con hallazgos por severidad: sin
nodos (error —lo que ya rechazan 2E-P11 y 2E-P12), sin ningún nodo origen, sin ningún nodo terminal, nodos
alcanzables desde ningún origen, nodos cuyo tipo no está registrado y aristas que nombran puertos inexistentes.
Se apoya en lo que ya existe: `GraphValidator` cubre tipos y ciclos; esto añade la **forma** del flujo. (2) Los
dos puntos de entrada consumen el mismo diagnóstico: el CLI falla con los errores e imprime los avisos; la
interfaz los muestra antes de ejecutar. (3) Las dos guardias ad hoc de 2E-P11/P12 se **sustituyen** por esta
regla única; sus pruebas actuales son la red de regresión de que el caso «sin nodos» sigue fallando igual.

**Cómo se verifica.** Una tabla de grafos → hallazgos esperados (uno por caso, sin excepciones), y el
comportamiento de cada punto de entrada por severidad. Mutaciones: quitar la detección de «sin origen» deja en
rojo sólo su caso; degradar un error a aviso deja en rojo la mitad del CLI y la de la interfaz.

**Riesgo.** Bajo en el motor, medio en el ruido: un diagnóstico que avisa de más es un diagnóstico que se
ignora. Por eso los avisos no bloquean la ejecución y cada uno tiene que poder justificarse.

## Fase 3E — Un solo formato, dos lectores ✅ HECHA

> Ejecutada el 2026-09-22: la definición de serialización del formato vive ahora en Core
> (`WorkflowGraph.SerializationOptions`) y la comparten los dos escritores —el de la app y el de Core—, así que el
> mismo grafo da **el mismo texto** por los dos caminos; la regla de declarar la versión también es una sola
> (`WorkflowFormat.DeclareCurrent`), porque escribir el campo es parte del formato y no un detalle de quien
> escribe. Se midió al hacerlo: de una **propiedad** nula no se escribe nada, pero la condición es de propiedades
> y no toca los valores de un diccionario, así que ningún parámetro se pierde por omitir nulos. Un archivo del
> dialecto anterior —los nombres tal cual, que es como guardaba la app— se sigue leyendo entero, y esa es la
> prueba que hace seguro cambiar lo que se escribe: la herramienta de pruebas que fabrica un archivo anterior
> buscaba el campo `Schema` por su nombre exacto, así que dejó de fabricarlo en silencio al unificar; ahora lo
> busca en cualquier caja, como los lectores. 6 pruebas (4 nuevas), 6 mutaciones, suite **1431 superadas / 0 fallos
> / 1 omitida**. Con esto la fase 3 queda cerrada, y el formato queda con guardia propia (fase 3E-G1 en el
> ledger): una fuente que (des)serialice un flujo con opciones propias es un fallo con fichero y línea —encontró
> una de verdad en el árbol y una rama sin prueba en el propio analizador—. Y la otra mitad del formato —su
> **forma**— queda atada a su versión (fase 3E-G2 en el ledger): un campo que aparece, desaparece o cambia de tipo
> sin subir el `schema` pone la suite en rojo y dice cuál fue, con la fila que hay que registrar. Cada versión tiene
> además su **archivo testigo** —un flujo de verdad guardado por el escritor de esa versión y comprometido en
> `FileFlow.Tests/FormatBaselines`—, y la fila tiene que decir lo que ese archivo dice: es lo que hace que
> reescribir la fila a mano falle en vez de pasar en silencio (fase 3E-G3 en el ledger). Detalle en
> [`2026-08_phase1_audit_plan.md`](2026-08_phase1_audit_plan.md).

**Objetivo.** Que los dos escritores escriban lo mismo. Hoy la app guarda con los nombres tal cual y Core con
camelCase; desde 2E-P10 se leen entre sí, así que **no hay prisa** — y esa es justamente la razón de que sea una
fase de simplificación y no una urgencia.

**Cambios.** Una única definición de opciones de serialización en Core, compartida por el servicio de guardado
de la app y por `WorkflowGraph.FromJson`/`ToJson`, con el lector tolerante y la inferencia de tipos que ya
tienen los dos. Los archivos escritos antes de esta fase (en cualquiera de las dos cajas) siguen cargando: es
justo lo que el lector tolerante garantiza desde 2E-P10.

**Cómo se verifica.** Las pruebas de interop de 2E-P10 se estrechan: el mismo grafo escrito por los dos caminos
produce **el mismo texto** —hoy se comprueba que los dos lectores coinciden, que es más débil—; y un archivo con
el formato anterior se sigue leyendo entero.

**Riesgo.** Bajo para leer, medio para lo que ya está en disco: cambia la forma de los archivos nuevos. Por eso
va después de 3A, que es la que protege a un archivo de un formato distinto.

**Lo que queda fuera, declarado.** Los otros dos artefactos que el producto escribe no son el archivo de flujo y
no se tocan: el paquete del portapapeles lleva su propio nombre (`FileFlow.NodeClipboard.v1`) y los puntos de
control de la ejecución guardan progreso, no un grafo. Y el dialecto anterior sigue **escribiéndose** en ningún
sitio, pero sí leyéndose: no se convierte lo que ya está en disco, se guarda encima con el formato único cuando
el usuario lo pida.

## Fase 3F — El contenedor se entera de que su subflujo cambió en disco ✅ HECHA

> Ejecutada el 2026-09-22: se pregunta en vez de vigilar —la huella del origen responde «¿cambió?» sin leer el
> archivo, así que no hay ningún vigilante del sistema de archivos—, y el refresco pasa por la misma puerta que
> el inspector para que una sola regla decida los puertos del contenedor. Lo que se pierde se **mide** contra el
> lienzo anterior, no se predice, y sólo se levanta cartel si el cambio se llevó algún cable: el registro de la
> consola va siempre. El punto (3) del plan —«un solo vigilante por archivo»— quedó sin objeto, y el punto (4)
> cambió al descubrir que un origen que no se puede resolver responde «cambió» en cada latido y habría inundado
> la consola: lo que se cuenta es lo que cambió para quien mira, no lo que cambió en el disco. 8 pruebas, 6
> mutaciones, suite **1390 superadas / 0 fallos / 1 omitida**. Detalle en
> [`2026-08_phase1_audit_plan.md`](2026-08_phase1_audit_plan.md).

**Objetivo.** Que editar la definición de un subflujo **fuera de la aplicación** actualice los puertos del
contenedor que la usa, en vez de dejarlos como estaban hasta que algo pregunte.

**Cambios.** (1) El resolutor ya sabe identificar el origen por su huella (fecha y tamaño) sin leer el archivo:
comprobarla en un tic de baja frecuencia es barato. (2) Cuando la huella cambia, se vuelve a materializar la
topología del contenedor y se anuncia con el mecanismo que ya existe (`PortsChanged`), de modo que el lienzo
revalide las conexiones colgantes —eso ya está hecho y probado desde 2E-P2—. (3) Un solo vigilante por archivo
resuelto, no uno por contenedor. (4) La política se declara: sólo se re-materializa por cambio de huella, y el
trabajo del usuario no se descarta en mitad de una edición.

**Cómo se verifica.** Cambiar el archivo cambia los puertos del contenedor y revalida sus cables; tocar **otro**
archivo no lo toca; y volver a escribir el mismo contenido (misma huella) no dispara nada. Mutaciones: no
comparar la huella deja en rojo la de «otro archivo»; vigilar por contenedor deja en rojo la de un solo
vigilante.

**Riesgo.** Medio: introduce trabajo periódico y un ciclo de actualización que hoy no existe en el editor.

## Fase 3G — El informe de carga, accionable ✅ HECHA

> Ejecutada el 2026-09-22: cada cable perdido es una fila del cartel con el nodo cuyo puerto falta (y un botón
> que lo centra), el puerto vigente más parecido —con umbral, y sin contar los que ya tienen cable— y la
> reconexión a un clic, deshacible. Abrir un archivo se suma al aviso, porque ya hay acción del lienzo a la
> que apuntar: era la razón por la que 3C lo había dejado sólo en la consola. Dos ajustes hicieron falta por el
> camino: el informe nombraba el nodo con el identificador del **origen**, que al pegar es el del gemelo —y no
> el de los nodos pegados—, y la cabecera del cartel dejó de enumerar los cables, que quedaba mintiendo en
> cuanto se arreglaba uno. 15 pruebas, 6 mutaciones, suite **1416 superadas / 0 fallos / 1 omitida**. Su punto
> (2) —el resumen en la barra de estado— quedó pendiente y lo cerró la fase 3H. Detalle en
> [`2026-08_phase1_audit_plan.md`](2026-08_phase1_audit_plan.md).

**Objetivo.** Que el aviso de un cable perdido al abrir un flujo sirva para arreglarlo.

**Cambios.** (1) El registro de carga lleva el identificador del nodo —ya lo tiene en el informe— y un comando
que lo selecciona y lo centra en el lienzo (si el lienzo no sabe centrar todavía, eso es parte de la fase).
(2) El resumen de lo que no se pudo reconstruir aparece también en la barra de estado, no sólo en la consola.

**Cómo se verifica.** Tras una carga con un cable perdido, el comando selecciona el nodo del informe; con una
carga sana no hay nada que seleccionar ni nada que mostrar. Mutaciones: quitar el identificador del registro
deja en rojo la selección.

**Riesgo.** Bajo: es usabilidad sobre datos que ya existen.

---

## Fase 3H — El informe de pérdidas, cerrado ✅ HECHA

> Ejecutada el 2026-09-22: los dos puntos (1) y (2) que 3G dejó declarados como pendientes. El registro de la
> consola lleva el nodo de cada línea —el identificador con el que el nodo existe en el lienzo, que es lo que
> hace que seleccionar la fila lo abra en el inspector—, y el recuento de lo perdido aparece en la barra de
> estado. Dos cosas se midieron al hacerlas: un cable que falla por sus dos extremos son **dos** nodos que
> arreglar —así que se cuenta una línea por motivo, porque una sola no puede llevar dos identificadores—, y un
> nodo que no está en el lienzo **no** se señala: su identificador no lleva a ninguna parte y su nombre podría
> abrir el nodo homónimo que no es. 11 pruebas, 9 mutaciones, suite **1427 superadas / 0 fallos / 1 omitida**.
> Detalle en [`2026-08_phase1_audit_plan.md`](2026-08_phase1_audit_plan.md).

**Objetivo.** Que el informe de un cable perdido no deje cabos sueltos en ninguna de sus superficies.

**Cambios.** (1) Cada línea de la consola lleva el nodo al que hay que ir, para que la fila abra ese nodo en el
inspector en vez de ser un texto que se lee y ya. (2) El recuento de lo que sigue perdido, en la barra de
estado, que es la superficie que sigue a la vista cuando el lienzo no lo está.

**Cómo se verificó.** El identificador de un registro es el del nodo **reconstruido** —al pegar, el pegado, no
su original—, y abrirlo lleva a ese nodo; un nodo que no llegó a crearse se cuenta sin identificador; un cable
que falla por sus dos extremos deja dos líneas, una por nodo; el recuento baja al reconectar y desaparece con
el último; y una pérdida que el lienzo no puede arreglar sigue contada —y su aviso no se retira al arreglar la
última que sí—.

**Riesgo.** Bajo: es usabilidad sobre datos que ya existían.

---

## Orden, y por qué

1. **3A** — pérdida de datos, pequeña y aislada: primero lo que puede destruir trabajo.
2. **3B** — habilita la verificación del camino más usado, y es requisito para cerrar bien 3D y 3F.
3. **3C** — cierra el último silencio de reconstrucción; su informe ya existía.
4. **3D** — capacidad nueva, y con ella las dos guardias ad hoc pasan a ser una regla.
5. **3E** — simplificación, deliberadamente tarde: sólo es segura con el lector tolerante (ya en pie) y con 3A
   protegiendo los archivos de otra versión.
6. **3F** — estado vivo en el editor: lo último que añade complejidad al bucle de edición.
7. **3G** — usabilidad sobre lo que 2E-P9 ya produce.

## Regla de la casa para cada fase

- Ninguna afirmación de las pruebas se da por buena sin **mutar** el código que la sostiene y verla en rojo.
- Un fallo se cuenta **donde el usuario lo lee**, y una ejecución que no hizo nada no puede terminar en verde.
- Cada límite que se descubra queda **escrito** en el código, en las pruebas y en la fase, con su porqué.
- Cierre de fase: `dotnet build FileFlow.slnx` sin errores ni advertencias, suite completa en verde, `grep` sin
  residuos de mutaciones, y la fase añadida al ledger con su evidencia.
