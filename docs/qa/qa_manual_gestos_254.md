# El cable del lienzo Uno: pegado, y con forma de cable (hito 254)

**Veredicto: ARREGLADO Y CERTIFICADO.** Tres defectos, los tres medidos y los tres cerrados: (1) el cable no
tocaba sus sockets —quedaba a 45,0 px de cada uno y, con las anclas cerca, salía **invertido** (el rulo con
forma de «2»); (2) al ajustar el **zoom** el ancla medida se corría y el cable quedaba a 6,1 px de su socket;
(3) la **forma** no era de cable: dos bajíos rectos y una ese apretada en medio, que en pantalla se lee como
una **Z**. El operador certificó los tres con su ratón.

Cinco sesiones con puntero real (**258**, **259**, **260**) y dos sondeos en la app viva. La última respuesta
del operador, con el algoritmo nuevo: «*ya parece un cable: sin Z y sin bajío*».

---

## 1. El encargo

«Al mover o ajustar el zoom las líneas de conexión se desplazan quedando fuera de su sitio.» Y después, ya con
los cables tocando sus sockets: «*al mover un nodo la parte recta es demasiado grande y se ve mal… el algoritmo
de la forma tiende a dejar una forma como de Z que no cuadra con la forma que haría un cable real o un hilo.
Avalonia tampoco lo hace bien del todo. ¿Puedes mejorar el algoritmo?*»

## 2. Lo que midió la sonda (la causa, no la impresión)

`ProbeWireTracking`, en la app viva, con los números crudos en el detalle:

| estado | extremo dibujado vs socket | ancla medida (grafo) | tarjeta (grafo) |
| :--- | ---: | :--- | :--- |
| plano sin mover (T=(0,0) S=1,00) | **45,0 px** | 542,234 → 600,234 | 350,0 → 600,0 |
| tras pan (+140,+90) | **0,0 px** | 542,234 → 600,234 | 350,0 → 600,0 |
| tras zoom ×1,25 | **6,1 px** | **538,233 → 597,233** | 350,0 → 600,0 |
| redibujando con el plano movido | 0,0 px | 538,233 → 597,233 | 350,0 → 600,0 |

Dos cosas quedan dichas ahí, y ninguna se veía a ojo:

1. **La tarjeta no se movía** (`350,0 → 350,0`) mientras el ancla del socket **sí** (`542 → 538`): el defecto
   estaba en **cómo se medía el centro**, no en el dibujo. La aritmética lo cierra: `TransformToVisualCenter`
   transformaba el vértice `(0,0)` y le **sumaba** después la mitad del tamaño, olvidando la **escala** de la
   cadena; con el plano al 125 % el centro medido se quedaba corto `0,25 · (w/2)`, y con el socket de 39 px del
   árbol eso son ~5 px — justo lo medido. Con el plano al 100 % o con un pan el error es **cero**, y por eso el
   defecto sólo aparecía al tocar el zoom.
2. Los **45,0 px** del estado de reposo eran el `spacing` del control (45): la figura abría en el primer punto
   de control y descartaba los tramos que unen la curva con las anclas.

## 3. Los tres arreglos

### 3.1 El trazo (un solo camino)

La geometría compartida (`FileFlow.App.Core/Services/ConnectionGeometry.cs`) dibuja ahora **una Bézier que nace
y muere en las anclas**, con los dos cuellos horizontales como puntos de control. Su largura es
`min(100 + √(25 · ancho), ancho/2)`:

- el **techo** del control (`100 + √(25 · ancho)`) conserva el gusto por el cuello corto en huecos enormes;
- el **tope de la mitad del hueco** es lo que faltaba: sin él los controles se cruzaban y la curva salía
  invertida (el rulo);
- y el trazo **no lleva ningún tramo recto**: son los dos bajíos del algoritmo del control (que allí existen
  porque allí la Bézier **sí** sale retirada) los que en pantalla se leían como una **Z**.

### 3.2 La medida del ancla

`TransformToVisualCenter` transforma ahora el **centro local** del elemento. El centro mal medido no sólo movía
los cables: es la misma medida del hit-testing del lienzo (las cajas del `ProbeHitAreas`) y de «qué tarjeta hay
bajo el puntero», así que la corrección quita de en medio el descuadre de las tres.

### 3.3 La forma, con el hueco mandando

| caso | antes | ahora |
| :--- | :--- | :--- |
| anclas a la misma altura, hueco amplio | recta (los dos cuellos en el medio) | igual: un cable tenso |
| diagonal con hueco de sobra (200 × 150) | ese suave | igual |
| **hueco 30 px, caída 80 px** | cuello 45 px y controles a 125 px: **curva invertida** | cuello 15 px, controles a 15: ese **dentro** del hueco |
| **hueco 70 px, caída 120 px** (el caso reportado) | rulo con forma de «2» | `cuello 35,0 y 35,0 — nace y muere en las anclas, sin salirse` |
| anclas apiladas (sin hueco horizontal) | asomaba 145 px a cada lado | recta vertical |

## 4. Certificación con puntero real

Tres sesiones. Las dos primeras (258, 259) fueron instrumento: la ronda del operador movió tarjetas, paneó y
zoomó, y el vigilante registró 14 cambios de escena con sus capturas; la medición de píxeles en la sesión 260
sobre el hueco de cierre da **un único tramo por columna** (el cable es una curva, no una Z: la Z mostraría dos
o tres tramos por columna, que es exactamente lo que se midió en la ronda anterior).

Lo que el operador certificó, y con qué medición:

| t | gesto | medición de la escena |
| :--- | :--- | :--- |
| 0,6 s | estado de partida (3 tarjetas, barras en y=281) | `gspans [[488,731,281],[800,1044,281],[1113,1356,281]]` |
| 49,7 → 65,4 s | **arrastra tarjetas** hasta dejarlas cerca y a distinta altura | `gspans` de las tres con `y` 187, 237, 256, 262, 337 y huecos de 56–66 px: **el caso del rulo, provocado a mano** |
| 83,7 s | encuadre final | `[[870,1114,262],[1170,1414,262],[488,731,281]]` |
| cierre | medición del cable en el hueco | un **único** tramo por columna (534–539 → 531–535 px), sin dobles |

## 5. Sonda, guardia y mutaciones

- **Sonda** `ProbeWireTracking` (ampliada en este hito): mide el extremo dibujado **contra el socket real** en
  la raíz —el espacio que ve el usuario— antes del gesto, tras el pan, tras el zoom **y** la forma en el hueco
  estrecho. Autochequeo **EXIT 0 con 83 OK**.
- **Guardia** `UnoCanvasWireGuardTests` (3 casos) + **9 casos de comportamiento** en `ConnectionGeometryTests`
  (el trazado empieza y termina en las anclas, sin tramos rectos; el cuello no se pasa del hueco; el caso
  apilado cierra en recta; la ancla no depende del zoom).
- **Mutaciones** (las tres **muerden**, con testigo y control y el árbol restaurado por bytes):
  `cable-que-no-toca-su-socket` (figura despegada de las anclas), `ancla-que-ignora-la-escala` (sumar la mitad
  después de transformar) y `cuello-que-no-cabe-en-el-hueco` (quitar el tope). La mutación del 216
  `cable-con-la-curva-al-reves` se actualizó a la línea nueva del algoritmo y sigue mordiendo.

## 6. Frontera declarada

- El **escritorio (Avalonia)** dibuja con el `Connection` de Nodify, así que **su forma sigue siendo la del
  control** (puntita retirada y tramos rectos): lo que se arregló aquí es la geometría compartida, que él ya
  usa para el hit-testing del cable. Llevarle la misma forma exige que el host Avalonia dibuje con
  `ConnectionGeometry` en vez de con el control — el mismo movimiento que ya hizo el Uno—, y queda como trabajo
  del siguiente tramo.
- El **caso apilado** (anclas sin hueco horizontal) dibuja una recta vertical: es lo que sale de acotar el
  cuello por el hueco, y no se ha medido con puntero.
- La **caída tipo hilo** (una curva con gravedad, no simétrica) no está implementada: la forma actual es la
  simétrica de siempre, ahora sin Z.

## 7. Verificación

| qué | resultado |
| :--- | :--- |
| Suite completa | **1882 superadas + 1 omitida de 1883, 0 errores** |
| Autochequeo interno del host Uno | **EXIT 0** (83 OK, inventario completo) |
| Sondeo externo UIA | **VERIFICADO** (8/8, exit 0): el canal de observación no se tocó |
| Mutaciones | 3 nuevas + 1 actualizada: **muerden** |
| Cobertura de mutaciones | 59 declaraciones, 15 de 17 subsistemas, guardias con mutación que las muerda 12 de 41 |
