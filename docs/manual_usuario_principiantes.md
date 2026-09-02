# 📘 Manual de Usuario para Principiantes: FileFlow Studio
## *Guía paso a paso y sin tecnicismos para organizar, renombrar y automatizar tus archivos*

---

## 🌟 1. ¿Qué es FileFlow Studio? (Explicado de Forma Sencilla)

Imagina que tienes miles de fotos, canciones, facturas o películas repartidas por el ordenador, desordenadas, con nombres feos o en formatos que ocupan demasiado espacio.

Hacer esto a mano (copiar, pegar, cambiar nombres uno a uno, descomprimir...) te llevaría días. 

**FileFlow Studio** es como una **cinta transportadora inteligente de fábrica**:
1. En un extremo pones una carpeta llena de archivos.
2. En medio colocas unas *"estaciones de trabajo"* (las llamadas **Cajas** o **Nodos**) que hacen tareas: cambiar nombres, ordenar por fechas, comprimir, convertir vídeos, etc.
3. Los archivos viajan por las conexiones ("cables") de una caja a otra.
4. Al final, todo queda limpio, ordenado y en su sitio exacto en cuestión de segundos.

> [!TIP]
> **No necesitas saber nada de programación ni informática avanzada.** Todo se hace con el ratón: arrastrar, soltar y unir cables.

---

## 🛡️ 2. Tu Tranquilidad Primero: La Regla de la Máxima Seguridad

Mucha gente tiene miedo de usar programas automáticos por si borran o estropean sus fotos o documentos importantes. En **FileFlow Studio** estás 100% protegido:

1. **Tus archivos originales no se tocan por defecto:** Las transformaciones crean copias ordenadas en carpetas nuevas.
2. **El Botón Mágico "Simulación Virtual" (Dry Run):** Puedes probar cualquier flujo sin que se mueva ni un solo archivo real. El programa te mostrará una simulación exacta de qué haría antes de pulsar el botón real.
3. **El Botón "Deshacer" (Rollback):** Si ejecutas algo y no te convence el resultado, pulsas el botón **Deshacer (Ctrl+Z)** y todos los archivos vuelven exactamente a su nombre y sitio original.
4. **Papelera de Reciclaje:** Si alguna vez decides eliminar algo a propósito, el programa nunca lo destruye; lo envía a la Papelera de Reciclaje de Windows para que puedas recuperarlo en cualquier momento.

---

## 🖥️ 3. Conoce la Pantalla (Las 4 Zonas Clave)

Cuando abres FileFlow Studio, la pantalla se divide en 4 partes muy fáciles de identificar:

```
+-----------------------------------------------------------------------------------+
|  Barra Superior: [ ▶ Ejecutar ]  [ 🔍 Simular (Dry Run) ]  [ ↩ Deshacer ]  [ 🎨 Tema ] |
+-----------------------+-----------------------------------+-----------------------+
|  IZQUIERDA            |  CENTRO (Mesa de Trabajo)         |  DERECHA              |
|  Caja de Herramientas |                                   |  Inspector / Ajustes  |
|  (Los Nodos)          |  [Carpeta Origen] -> [Renombrar]  |                       |
|                       |                           \       |  Aquí configuras      |
|  • Leer Carpetas      |                            v      |  las opciones de la   |
|  • Renombrar          |                      [Mover a...] |  caja seleccionada.   |
|  • Comprimir          |                                   |                       |
|  • Convertir Vídeo    |                                   |                       |
+-----------------------+-----------------------------------+-----------------------+
|  ABAJO: Consola de Mensajes y Registro de lo que va ocurriendo en directo           |
+-----------------------------------------------------------------------------------+
```

1. **Barra Superior:** Botones grandes para **Ejecutar**, **Simular (Dry Run)**, **Pausar**, **Deshacer**, **Guardar** y cambiar el **Idioma o Tema**.
2. **Panel Izquierdo (Caja de Herramientas):** Es tu catálogo de piezas. Buscas lo que quieres hacer (ej: *"Fotos"*, *"Renombrar"*, *"Descomprimir"*) y lo arrastras con el ratón al centro.
3. **Centro (Lienzo / Mesa de Trabajo):** El espacio donde colocas tus cajas y las conectas entre sí mediante cables.
4. **Panel Derecho (Inspector de Ajustes):** Cuando haces clic en cualquier caja de la mesa, aquí aparecen sus opciones explicadas en español claro (por ejemplo: *"¿Dónde quieres guardar las fotos?"* o *"¿Qué formato prefieres?"*).
5. **Panel Inferior (Consola):** Muestra mensajes paso a paso con lo que se está procesando en cada milisegundo.

---

## 🔌 4. Cómo Conectar Cajas (El ABC del Editor)

* **Añadir una caja:** Haz clic en la lista de la izquierda, mantén pulsado y arrástrala hacia la mesa central.
* **Mover una caja:** Pincha sobre el título de la caja y muévela donde quieras.
* **Unir dos cajas:** En el lateral derecho de cada caja hay un circulito (**Salida**). Pincha en él, arrastra el cable y suéltalo en el circulito izquierdo (**Entrada**) de la siguiente caja.
* **Borrar un cable o caja:** Haz clic sobre él y pulsa la tecla `Supr` o `Delete` de tu teclado.

---

## 📚 5. Cuatro Recetas Prácticas Paso a Paso

---

### 📷 Receta 1: Renombrar todas las fotos con la fecha en que se tomaron

**El Problema:** Tu cámara o móvil llama a las fotos `IMG_0042.JPG`, `DSC_9843.JPG` y no sabes de qué fecha son.

**La Solución en 3 pasos:**
1. Arrastra a la mesa la caja **"Lector de Carpetas"** (`FolderSource`).
   - En el panel derecho, pulsa el botón **Examinar...** y elige tu carpeta de fotos.
2. Arrastra la caja **"Estudio de Renombrado Avanzado"** (`AdvancedRenamer`).
   - Conecta la salida del lector a la entrada de la caja de renombrado.
   - Haz clic en la caja de renombrado y pulsa el botón **"Abrir Estudio de Renombrado..."**.
   - En la lista desplegable de Presets, elige: **"📷 Fotografía Digital (Fecha EXIF + Modelo + Contador)"**.
   - ¡Verás abajo una lista previa en directo con el nuevo nombre antes y después!
   - Pulsa **Guardar y Aplicar**.
3. Pulsa el botón verde superior **"▶ Ejecutar Flujo"**.

¡Listo! Todas tus fotos se llamarán ahora `20260815_NikonD850_001.jpg`, perfectamente ordenadas por año, mes y día.

---

### 📂 Receta 2: Organizar la carpeta de Descargas (Separar Vídeos, Música y Documentos)

**El Problema:** Tienes 500 archivos mezclados en la carpeta "Descargas".

**La Solución:**
1. Pon una caja **"Lector de Carpetas"** apuntando a tu carpeta `Descargas`.
2. Añade la caja **"Filtro por Extensión"** (`ExtensionFilter`):
   - Configura las extensiones: `.mp4, .mkv, .avi` para Vídeo, `.jpg, .png` para Fotos y `.pdf, .docx, .xlsx` para Documentos.
3. Conecta cada salida a una caja **"Mover Archivo"** (`MoveFile`):
   - Salida Vídeos $\rightarrow$ Destino: `D:\Mis Vídeos\`
   - Salida Fotos $\rightarrow$ Destino: `D:\Mis Fotos\`
   - Salida Documentos $\rightarrow$ Destino: `D:\Mis Documentos\`
4. Pulsa **"🔍 Simular (Dry Run)"** para ver el informe en la consola y verificar que todo irá a la carpeta correcta.
5. Pulsa **"▶ Ejecutar Flujo"**. En 2 segundos tu carpeta de descargas estará completamente limpia.

---

### 🗜️ Receta 3: Descomprimir 20 archivos ZIP o RAR a la vez

**El Problema:** Te has descargado varios archivos comprimidos (algunos con contraseña) y tener que abrirlos uno a uno es aburrido.

**La Solución:**
1. Pon un **"Lector de Carpetas"** seleccionando la carpeta donde están los ZIP.
2. Añade la caja **"Descompresor Inteligente"** (`SmartUnpackNode`).
   - Conecta ambas cajas.
   - Si los archivos tienen contraseña, pulsa en **"Gestionar Contraseñas..."** e introduce las posibles claves (una por línea). ¡El programa probará automáticamente cada clave hasta encontrar la correcta sin que tú hagas nada!
   - Elige la carpeta donde quieres el contenido extraído.
3. Pulsa **"▶ Ejecutar Flujo"**.

---

### 🎬 Receta 4: Reducir el tamaño de vídeos pesados para WhatsApp o móvil

**El Problema:** Tienes vídeos de 1 GB grabados con la cámara y quieres que ocupen 50 MB para enviarlos por mensaje o guardarlos en un pendrive pequeño.

**La Solución:**
1. Pon un **"Lector de Carpetas"** con tus vídeos.
2. Añade la caja **"Conversor Multimedia FFmpeg"** (`MediaTranscoderNode`).
   - Conéctalas.
   - En el panel derecho, en la lista de Ajustes Rápidos, elige: **"Móvil Ultra-Comprimido H.264"** o **"Convertir 720p H.264 (MP4 Rápido)"**.
   - Elige la carpeta donde guardar los vídeos convertidos.
3. Pulsa **"▶ Ejecutar Flujo"**.

---

### 📊 Receta 5: Modo Vigilante Automático (Procesar lo que entre solo)

**El Problema:** Quieres que cada vez que descargues un PDF o una foto a tu carpeta, se ordene sola sin tener que abrir el programa y darle a Ejecutar cada vez.

**La Solución:**
1. Crea tu flujo habitual (ej. `Lector de Carpetas` $\rightarrow$ `Filtro` $\rightarrow$ `Mover`).
2. En lugar de pulsar Ejecutar, pulsa el botón **"👁️ Vigilante"** en la barra superior.
3. El botón se iluminará en verde esmeralda. Ahora puedes minimizar el programa: cada archivo nuevo que caiga en la carpeta se procesará al instante de forma automática.

---

## 🎨 6. Consejos de Organización en la Pantalla

* **Notas Adhesivas:** Haz clic derecho en una zona vacía de la mesa y elige **"Crear Nota Adhesiva"**. Puedes escribir explicaciones o recordatorios con colores llamativos (amarillo, azul, verde, rosa).
* **Marcos de Grupo (Ctrl+G):** Si tienes varias cajas que hacen una tarea junta (por ejemplo, 3 cajas para procesar fotos), selecciónalas todas arrastrando un cuadro con el ratón y pulsa `Ctrl+G`. Quedarán agrupadas en una tarjeta con título que puedes mover en bloque.

---

## ❓ 7. Preguntas Frecuentes y Dudas Típicas (FAQ)

### 1. ¿Qué pasa si se me va la luz o el ordenador se apaga mientras trabaja?
FileFlow Studio procesa los archivos uno por uno de forma atómica y segura con puntos de control automáticos. No se corrompe nada. Al volver a abrir el programa, reanuda el trabajo omitiendo los archivos que ya habían terminado.

### 2. ¿Cómo sé si el programa ha terminado?
En la barra superior verás que la barra de progreso se llena en verde y en la consola inferior aparecerá un mensaje final diciendo: *"¡Flujo completado con éxito! X archivos procesados en Y segundos"*.

### 3. ¿Puedo guardar mis flujos para usarlos todos los días?
¡Sí! Pulsa en el menú lateral **"Guardar Flujo Como..."** (o `Ctrl+S`). Puedes guardar todos los flujos que quieras (por ejemplo: *"LimpiarDescargas.flow"* o *"OrganizarFotos.flow"*). Cuando quieras volver a usarlo, solo tienes que cargarlo y pulsar Ejecutar o Vigilante.

### 4. ¿Qué significa el circulito de colores en cada caja?
* ⚪ **Gris (Inactivo):** La caja está esperando a que le lleguen archivos.
* 🟡 **Amarillo / Azul (Trabajando):** La caja está procesando un archivo en este momento.
* 🟢 **Verde (Completado):** Ha terminado todo su trabajo con éxito.
* 🔴 **Rojo (Aviso/Error):** Ha ocurrido un problema con algún archivo (por ejemplo, que el disco estaba lleno o el archivo estaba abierto en Word). Puedes leer el motivo exacto en la consola de abajo.

---

## 📖 8. Glosario de Términos (Para Entender Todo)

| Término | Qué significa en cristiano |
| :--- | :--- |
| **Nodo / Caja** | Un bloque visual que hace una tarea específica (renombrar, copiar, filtrar, etc.). |
| **Flujo / Pipeline** | La cadena completa de cajas unidas por cables desde el principio hasta el final. |
| **Modo Vigilante (Watchdog)** | Modo en segundo plano que escucha una carpeta y procesa archivos según van llegando. |
| **Dry Run / Simulación** | Un ensayo general: hace todos los cálculos pero no toca tus archivos reales. |
| **Rollback / Deshacer** | Rebobinar la película: deshace todos los cambios y deja tus archivos como estaban. |
| **Punto de Control (Checkpoint)** | Memoria de guardado que permite continuar un trabajo largo donde se quedó si se corta la luz. |
| **Metadatos (EXIF / ID3 / Columnas)** | Información oculta dentro de un archivo (fecha de la foto, modelo de cámara, autor, columnas de un Excel). |
| **Versión Portable** | Una versión de FileFlow Studio que puedes llevar en un pendrive USB y usar en cualquier ordenador sin necesidad de instalar nada. |

---

¡Disfruta organizando tus archivos de forma rápida, segura y sin esfuerzo con **FileFlow Studio**! 🎉
