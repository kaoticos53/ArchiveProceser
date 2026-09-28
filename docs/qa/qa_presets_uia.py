# -*- coding: utf-8 -*-
"""Sesion del GESTOR DE PRESETS DE MEDIOS en la app abierta (hito 263).

El reparto es el de siempre (255/257/258/259/260/261/262): quien ACTUA es este driver —otro proceso pulsa
los controles REALES por su AutomationId o por su rectangulo— y quien MIDE es el propio driver: las anclas
de automatizacion del arbol, el TEXTO que expone el canal externo, el pixel dominante de una banda de la
captura y, sobre todo, el FICHERO del almacen leido por fuera de la aplicacion.

Que se ejercita, y por que cada paso mide lo que mide:

  - el nodo de transcodificacion se AÑADE por el camino del usuario (buscar en el cajon de herramientas +
    doble clic en el item): sin nodo no hay fila de preset que pulsar;
  - las DOS puertas de la MISMA superficie, una detras de otra: el boton «🎬 Presets...» de la TARJETA del
    nodo (su accion personalizada) y el boton «🎬» de la FILA del preset (la puerta que tiene el escritorio
    en la fila del parametro). Las dos tienen que abrir el gestor: con una sola se mediria media entrada;
  - el cuerpo se reconoce por sus anclas y por la LISTA: su primera fila tiene que ser el primer preset del
    ALMACEN (leido del fichero por este driver, no del arbol);
  - se EDITA la descripcion del preset elegido en la CAJA real, se pulsa GUARDAR, se cierra y se lee el
    FICHERO: la descripcion nueva esta escrita donde el nodo que transcodifica la lee. Ese es el veredicto
    —un gestor que se cerrara sin escribir dejaria el fichero como estaba—;
  - se RESTABLECE la descripcion original por el mismo camino y se compara el md5 del fichero: la sesion
    mide, no configura;
  - las ORDENES DESTRUCTIVAS se ejercen por las DOS puertas con la pregunta del host delante: «Nuevo», luego
    «Eliminar» (que tiene que PREGUNTAR), un «no» (que no puede borrar) y un «si» (que tiene que borrar). El
    veredicto es el FICHERO del almacen en cada paso, no que la capa se dibuje: antes de este tramo, la
    puerta de la tarjeta no borraba NI avisaba, y la de la fila borraba sin preguntar;
  - «Restablecer» (la otra orden destructiva) se cancela: el catalogo del usuario queda intacto;
  - el nodo añadido se retira con Deshacer y la escena vuelve a sus 3 tarjetas.

Uso (con la app ya lanzada por el instrumento, que maximiza y escribe session_state.json):
  FILEFLOW_QA_WORK=qa-manual-263 python qa_manual_session.py --launch
  FILEFLOW_QA_WORK=qa-manual-263 python qa_presets_uia.py --session

Salida: una linea por paso (`[presets] ...`) con lo medido antes y despues, capturas rotuladas en la carpeta
de la sesion, y exit 0 solo si todos los pasos salieron como dicen.
"""
import hashlib
import json
import os
import sys
import time

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import qa_ajustes_uia as drv  # noqa: E402
import qa_manual_session as sess  # noqa: E402
import qa_menu_uia as menu  # noqa: E402
import qa_dialogs_uia as dlg  # noqa: E402

STEP = 1.6
CENTER = menu.CENTER
CANVAS = dlg.CANVAS
# Las rutas REALES del producto (AppPaths): el modo instalado vive en %AppData%\FileFlow (config/ y presets/),
# no en la carpeta vieja %AppData%\FileFlowStudio —que guarda una copia de hace semanas y que la sesion
# leyo al principio creyendo que era el almacen—.
PREFS = os.path.join(os.environ.get("APPDATA", ""), "FileFlow", "config", "user_preferences.json")

# El nodo que la sesion AÑADE: el de transcodificacion, que es el dueño de la superficie.
# OJO con los dos nombres, que NO coinciden: el CAJON de herramientas busca por el nombre del item, y en
# este host ese nombre es la CLAVE cruda del recurso del plugin (`MediaTranscoderNode_Name`), porque el
# diccionario del plugin no se carga en el cajon; la TARJETA del lienzo, en cambio, si muestra el texto
# resuelto («Transcodificar Media»). Medido con la sonda antes de escribir la sesion.
NODE_SEARCH = "Transcoder"
NODE_CARD = "Transcodificar Media"

# La accion personalizada de la TARJETA del nodo y el boton de la FILA del preset (dos puertas, misma puerta).
# La accion vive en el panel de acciones rapidas de la tarjeta, y ese panel cuelga de IsExpanded: antes de
# pulsarla hay que DESPLEGAR la tarjeta con su conmutador (el chevron de la cabecera), como el usuario.
CARD_TOGGLE = "NodeCardExpandToggle"
CARD_ACTION = "Presets"
ROW_ACTION = "ParamPreset_Preset"
# El cierre del gestor: su ancla del cuerpo. NO el texto «Cerrar»: el inspector tiene otro control con ese
# mismo texto y otra consecuencia (medido: pulsarlo no cierra el gestor).
CLOSE_ACTION = "PresetManagerCloseButton"

# La PREGUNTA del host (hito 263): la capa que el host monta DENTRO del modal abierto y sus dos botones.
# Son las anclas que el driver pulsa: contestar por el TEXTO (Aceptar/Cancelar) seria contestar el control
# del diccionario equivocado —el inspector tiene un «Cerrar» y los avisos llevan su propio idioma—.
CONFIRM_LAYER = "HostConfirmationDialog"
CONFIRM_ACCEPT = "HostConfirmationAccept"
CONFIRM_CANCEL = "HostConfirmationCancel"

# Las anclas del CUERPO del gestor que el canal externo puede ver (los controles, no el contenedor).
BODY_ANCHORS = ("PresetManagerList", "PresetManagerNameBox", "PresetManagerDescriptionBox",
                "PresetManagerExtensionBox", "PresetManagerCategoryBox", "PresetManagerNewButton",
                "PresetManagerDeleteButton", "PresetManagerResetButton", "PresetManagerSaveButton",
                "PresetManagerCloseButton")

# El almacen: el fichero que lee el nodo que transcodifica (el mismo del escritorio).
STORE = os.path.join(os.environ.get("APPDATA", ""), "FileFlow", "presets", "media_presets.json")

PROBE_DESCRIPTION = "Descripcion escrita por la sesion 263 (gestor de presets)"

RESULT = os.path.join(sess.SHOTS, "presets-session.json")


def md5(path):
    if not os.path.exists(path):
        return None
    with open(path, "rb") as fh:
        return hashlib.md5(fh.read()).hexdigest()


def read_store():
    """El almacen de presets, leido por FUERA de la aplicacion (la evidencia del valor escrito)."""
    if not os.path.exists(STORE):
        return []
    with open(STORE, "r", encoding="utf-8-sig") as fh:
        return json.load(fh)


# Lo que la sesion NO puede tocar de las preferencias del usuario: sus ajustes. La huella byte a byte no
# vale como veredicto aqui porque el producto ANOTA el uso de los nodos (`NodeUsageCounts`), y la sesion
# añade y retira uno: pedirle identidad byte a byte seria pedirle a la app que no haga su trabajo.
PREFS_ESSENTIALS = ("ActiveTheme", "Language", "DefaultGlobalOutputDir", "FavoriteNodeTypes")


def prefs_essentials():
    """Tema, idioma, carpeta de salida y favoritos: los ajustes que la sesion no viene a tocar."""
    if not os.path.exists(PREFS):
        return None
    with open(PREFS, "r", encoding="utf-8-sig") as fh:
        data = json.load(fh)
    return {key: data.get(key) for key in PREFS_ESSENTIALS}


def aid_set(win):
    found = set()
    for _, el, _ in dlg.elements(win):
        try:
            aid = el.element_info.automation_id or ""
        except Exception:
            continue
        if aid:
            found.add(aid)
    return found


def text_of(win, aid):
    el = dlg.by_aid(win, aid)
    if el is None:
        return ""
    parts = []

    def collect(node, depth=0):
        if depth > 3:
            return
        try:
            own = node.window_text()
        except Exception:
            own = ""
        if own and own not in parts:
            parts.append(own)
        try:
            children = node.children()
        except Exception:
            return
        for child in children:
            collect(child, depth + 1)

    collect(el)
    return " | ".join(parts)


def row_names(win, aid):
    """Los nombres de las FILAS de una lista tal y como los ve el canal externo."""
    el = dlg.by_aid(win, aid)
    if el is None:
        return []
    names = []
    try:
        for child in el.children():
            name = (child.element_info.name or "").strip()
            if name:
                names.append(name)
    except Exception:
        pass
    return names


def press(win, aid, wait=STEP):
    el = dlg.by_aid(win, aid)
    if el is None:
        return False
    drv.invoke(el)
    time.sleep(wait)
    return True


def press_close_button(win, wait=STEP):
    """El boton de CIERRE del modal: su ancla del cuerpo (PresetManagerCloseButton)."""
    if press(win, CLOSE_ACTION, wait):
        return CLOSE_ACTION
    return ""


def confirm_anchors(win):
    """Las anclas de la pregunta que hay en pantalla ahora mismo (vacio = no hay pregunta)."""
    opened = aid_set(win)
    return [a for a in (CONFIRM_LAYER, CONFIRM_ACCEPT, CONFIRM_CANCEL) if a in opened]


def destructive_cycle(win, hwnd, label, check, baseline):
    """El ciclo destructivo del gestor por UNA puerta, con el almacen como unico veredicto.

    Alta, «Eliminar» (tiene que PREGUNTAR), un «no» (no puede borrar) y un «si» (tiene que borrar). Lo que se
    mide no es que la capa se dibuje sino que el FICHERO del almacen —el que lee el nodo que transcodifica—
    cambie exactamente cuando el usuario dice que si: la orden destructiva depende de SU respuesta.
    """
    creado = None
    if press(win, "PresetManagerNewButton", STEP + 1.2):
        after_new = read_store()
        creado = after_new[-1].get("Name") if after_new else None
        check(len(after_new) == baseline + 1,
              "[%s] «Nuevo» da de alta el preset EN EL ALMACEN: el fichero pasa de %d a %d presets"
              % (label, baseline, len(after_new)), "el ultimo es %r" % creado)

    press(win, "PresetManagerDeleteButton", STEP + 1.2)
    pedidas = confirm_anchors(win)
    drv.log("[presets] [%s] pregunta en pantalla: %s" % (label, pedidas))
    check(CONFIRM_ACCEPT in pedidas and CONFIRM_CANCEL in pedidas,
          "[%s] «Eliminar» PREGUNTA antes de destruir: la pregunta tiene sus dos botones" % label,
          "anclas: %s" % pedidas)
    con_pregunta = len(read_store())
    check(con_pregunta == baseline + 1,
          "[%s] y con la pregunta en pantalla NO se ha borrado nada todavia" % label,
          "%d presets" % con_pregunta)

    img_pregunta = sess.grab(hwnd, "85_%s_pregunta" % label)
    c_pregunta = menu.dominant(img_pregunta, CENTER)
    drv.log("[presets] [%s] pixel del centro con la pregunta: %s" % (label, menu.hexs(c_pregunta[0])))

    press(win, CONFIRM_CANCEL, STEP + 0.5)
    tras_cancelar = len(read_store())
    check(tras_cancelar == baseline + 1 and not confirm_anchors(win),
          "[%s] un «no» cierra la pregunta y NO borra: el preset sigue en el almacen" % label,
          "%d presets" % tras_cancelar)

    press(win, "PresetManagerDeleteButton", STEP + 1.2)
    press(win, CONFIRM_ACCEPT, STEP + 1.0)
    after = read_store()
    nombres = [p.get("Name") for p in after]
    check(len(after) == baseline and creado not in nombres,
          "[%s] y un «si» SI borra: el almacen vuelve a sus %d presets" % (label, baseline),
          "%d presets" % len(after))
    return creado, tras_cancelar, len(after)


def nearest_toggle(win, rect):
    """El conmutador de ESA tarjeta: el arbol trae uno por tarjeta y todos con la misma ancla."""
    best, best_d = None, None
    cx = (rect.left + rect.right) / 2.0
    cy = (rect.top + rect.bottom) / 2.0
    for _, el, r in dlg.elements(win):
        try:
            if (el.element_info.automation_id or "") != CARD_TOGGLE:
                continue
        except Exception:
            continue
        d = abs(((r.left + r.right) / 2.0) - cx) + abs(((r.top + r.bottom) / 2.0) - cy)
        if best_d is None or d < best_d:
            best, best_d = el, d
    return best


def added_card(win, tries=8, wait=0.8):
    """La tarjeta del nodo añadido, esperando a que el lienzo la materialice."""
    for _ in range(tries):
        rect = dlg.card_rect(win, NODE_CARD)
        if rect is not None:
            return rect
        time.sleep(wait)
    return None


def main():
    if "--session" not in sys.argv:
        drv.log("[presets] nada que hacer: usa --session")
        return 2

    os.makedirs(sess.SHOTS, exist_ok=True)
    win = drv.connect()
    if win is None:
        drv.log("[presets] FALLO: la app no aparece por UIA")
        return 3

    hwnd = int(json.load(open(sess.STATE))["hwnd"])
    results = []
    prefs_before = prefs_essentials()
    store_before = md5(STORE)

    def check(condition, what, detail=""):
        results.append(bool(condition))
        drv.log("[presets] %s %s%s" % ("[OK]   " if condition else "[FALLO]", what,
                                       ("  <- " + detail) if detail else ""))
        return bool(condition)

    def cards():
        titles = ("Folder Source", "Optimizador de Imágenes", "Destination Sink", NODE_CARD)
        return len(dlg.elements(win, lambda n: n in titles, CANVAS))

    catalog_before = read_store()
    first_preset = catalog_before[0] if catalog_before else {}
    drv.log("[presets] almacen: %d preset(s), el primero es %r | md5=%s"
            % (len(catalog_before), first_preset.get("Name"), store_before))
    check(bool(catalog_before), "el almacen de presets expone su catalogo al driver",
          "primer preset=%r" % first_preset.get("Name"))

    # ── 0. La linea base ──
    img0 = sess.grab(hwnd, "80_base")
    c0 = menu.dominant(img0, CENTER)
    base_cards = cards()
    drv.log("[presets] base: tarjetas=%d pixel centro=%s (%.1f%%)" % (base_cards, menu.hexs(c0[0]), 100 * c0[1]))
    check(base_cards == 3, "el lienzo arranca con sus 3 tarjetas", "tarjetas=%d" % base_cards)

    # ── 1. El nodo de transcodificacion, por el camino del usuario ──
    sess.foreground(hwnd)
    dlg.set_value(dlg.by_aid(win, "SearchBox"), NODE_SEARCH)
    time.sleep(1.5)

    # El item del cajon esta hecho de varios TextBlock con el mismo prefijo (`..._Name`, `..._Role`,
    # `..._Desc`), y el canal externo los expone TODOS; el doble clic va al NOMBRE —el bloque que el
    # usuario ve como titulo del item— y, si ese no bastara, se prueban los demas de arriba abajo.
    hits = dlg.elements(win, lambda n: NODE_SEARCH in n, (0, 400))
    hits.sort(key=lambda h: (0 if h[0].strip().endswith("_Name") else 1, h[2].top))
    drv.log("[presets] item del cajon: %s" % [(n, (r.left, r.top, r.right, r.bottom)) for n, _, r in hits])
    item = hits[0][2] if hits else None
    check(item is not None, "el cajon de herramientas encuentra el nodo de transcodificacion al buscar %r"
          % NODE_SEARCH)
    if item is not None:
        for name, _el, rect in hits:
            dlg.click((rect.left + rect.right) // 2, (rect.top + rect.bottom) // 2, double=True)
            if added_card(win, tries=3, wait=0.7) is not None:
                drv.log("[presets] el doble clic sobre %r añadio el nodo" % name)
                break
            drv.log("[presets] el doble clic sobre %r no añadio el nodo" % name)

    rect = added_card(win)
    drv.log("[presets] tarjeta del nodo añadido: %s" % rect)
    check(rect is not None, "el doble clic del cajon añade el nodo al lienzo (su tarjeta esta en el arbol)")
    if rect is None:
        drv.log("[presets] lienzo sin la tarjeta: %s"
                % [n for n, _, _ in dlg.elements(win, lambda n: n, CANVAS)])
        drv.log("[presets] === RESULTADO: FALLOS (sin tarjeta no hay superficie que ejercer) ===")
        return 1

    dlg.click((rect.left + rect.right) // 2, (rect.top + rect.bottom) // 2)
    drv._TREE = None
    params = sorted(k for k in drv.tree(win) if k.startswith("Param"))
    drv.log("[presets] filas del nodo añadido: %s" % params)
    check(ROW_ACTION in params,
          "el nodo seleccionado expone su FILA de preset con su boton «🎬» anclado", ROW_ACTION)

    # ── 2. Puerta A: la ACCION de la TARJETA del nodo ──
    # El boton vive en el panel de acciones rapidas de la tarjeta, y ese panel cuelga de IsExpanded:
    # primero se DESPLIEGA la tarjeta con su conmutador (clic real sobre el chevron), como el usuario.
    toggle = nearest_toggle(win, rect)
    check(toggle is not None,
          "la tarjeta del nodo trae su conmutador de parametros («%s»)" % CARD_TOGGLE)
    if toggle is not None:
        trect = toggle.rectangle()
        drv.log("[presets] conmutador en %s" % ((trect.left, trect.top, trect.right, trect.bottom),))
        dlg.click((trect.left + trect.right) // 2, (trect.top + trect.bottom) // 2)
        drv._TREE = None
        time.sleep(STEP)

    card_action = None
    for name, el, rect_action in dlg.elements(win, lambda n: CARD_ACTION in n, CANVAS):
        card_action = el
        drv.log("[presets] accion de la tarjeta: %r en %s"
                % (name, (rect_action.left, rect_action.top, rect_action.right, rect_action.bottom)))
        break
    check(card_action is not None,
          "la tarjeta desplegada expone su accion personalizada («%s»)" % CARD_ACTION)
    if card_action is not None:
        drv.invoke(card_action)
        time.sleep(STEP + 1.2)

    img1 = sess.grab(hwnd, "81_gestor_por_la_tarjeta")
    c1 = menu.dominant(img1, CENTER)
    opened = aid_set(win)
    present = [a for a in BODY_ANCHORS if a in opened]
    rows = row_names(win, "PresetManagerList")
    drv.log("[presets] por la tarjeta: anclas=%d/%d filas=%d pixel centro=%s (%.1f%%)"
            % (len(present), len(BODY_ANCHORS), len(rows), menu.hexs(c1[0]), 100 * c1[1]))
    check(len(present) == len(BODY_ANCHORS),
          "el boton de la TARJETA abre el gestor: su lista, su formulario, sus tres acciones y su cierre",
          "presentes: %s" % ", ".join(present))
    check(c1[0] != c0[0], "y la superficie se ve en el pixel central",
          "%s -> %s" % (menu.hexs(c0[0]), menu.hexs(c1[0])))
    check(rows and rows[0] == first_preset.get("Name"),
          "la lista pinta el catalogo del ALMACEN: su primera fila es %r" % first_preset.get("Name"),
          "primera fila=%r" % (rows[0] if rows else None))

    # ── 2b. La ORDEN DESTRUCTIVA por la puerta de la TARJETA, con la pregunta del host ──
    # Alta, «Eliminar» preguntando, un «no» que no borra y un «si» que borra. Antes de este tramo esta puerta
    # no borraba NI avisaba: pulsar «Eliminar» no hacia nada y el usuario no sabia por que.
    card_cycle = destructive_cycle(win, hwnd, "tarjeta", check, len(catalog_before))

    closed = press_close_button(win, STEP + 0.6)
    img1b = sess.grab(hwnd, "82_tarjeta_cerrado")
    c1b = menu.dominant(img1b, CENTER)
    drv.log("[presets] cerrado con %r: pixel centro=%s" % (closed, menu.hexs(c1b[0])))
    check(closed != "", "el gestor tiene su boton de cierre en el arbol", repr(closed))
    check(not any(a in aid_set(win) for a in BODY_ANCHORS), "y cerrarlo retira la superficie")

    # ── 3. Puerta B: el boton «🎬» de la FILA del preset ──
    check(press(win, ROW_ACTION, STEP + 1.4),
          "el boton «🎬» de la FILA del preset se pulsa (la puerta del escritorio en la fila del parametro)")
    img2 = sess.grab(hwnd, "83_gestor_por_la_fila")
    c2 = menu.dominant(img2, CENTER)
    opened2 = aid_set(win)
    present2 = [a for a in BODY_ANCHORS if a in opened2]
    drv.log("[presets] por la fila: anclas=%d/%d pixel centro=%s (%.1f%%)"
            % (len(present2), len(BODY_ANCHORS), menu.hexs(c2[0]), 100 * c2[1]))
    check(len(present2) == len(BODY_ANCHORS),
          "las DOS puertas abren la MISMA superficie (la lista, el formulario y sus acciones)",
          "presentes: %s" % ", ".join(present2))
    check(c2[0] == c1[0], "y la superficie es la misma que sirvio la tarjeta",
          "%s / %s" % (menu.hexs(c1[0]), menu.hexs(c2[0])))

    name_shown = text_of(win, "PresetManagerNameBox")
    description_before = dlg.value_of(dlg.by_aid(win, "PresetManagerDescriptionBox")) or ""
    drv.log("[presets] formulario: nombre=%r descripcion=%r" % (name_shown, description_before))
    check(first_preset.get("Name", "") in name_shown,
          "el formulario se abre con el preset ELEGIDO (el primero del catalogo)", repr(name_shown))
    check(description_before.strip() == (first_preset.get("Description") or "").strip(),
          "y su descripcion es la del almacen, no una vacia", repr(description_before[:60]))

    # ── 4. Editar y guardar: la evidencia es el FICHERO ──
    dlg.set_value(dlg.by_aid(win, "PresetManagerDescriptionBox"), PROBE_DESCRIPTION)
    time.sleep(STEP)
    check(press(win, "PresetManagerSaveButton", STEP + 1.2) is not False,
          "«Guardar» del formulario se pulsa (el boton dibujado, no la orden por su cuenta)")

    img3 = sess.grab(hwnd, "84_guardado")
    c3 = menu.dominant(img3, CENTER)
    after = read_store()
    written = after[0].get("Description") if after else None
    drv.log("[presets] almacen tras guardar: %r" % written)
    check(written == PROBE_DESCRIPTION,
          "LA EDICION QUEDA ESCRITA EN EL ALMACEN: el fichero que lee el nodo trae la descripcion nueva")
    check(c3[0] == c2[0], "y guardar no cierra la superficie: el usuario sigue donde estaba",
          "%s -> %s" % (menu.hexs(c2[0]), menu.hexs(c3[0])))

    # Segunda vuelta: se cierra y se reabre por la fila; la caja tiene que traer lo guardado.
    press_close_button(win, STEP + 0.6)
    check(press(win, ROW_ACTION, STEP + 1.4), "se reabre el gestor para comprobar donde quedo lo escrito")
    reopened = dlg.value_of(dlg.by_aid(win, "PresetManagerDescriptionBox")) or ""
    drv.log("[presets] reabierto: descripcion=%r" % reopened[:70])
    check(reopened.strip() == PROBE_DESCRIPTION,
          "el gestor reabre con la descripcion guardada: el cambio persistio")

    # ── 5. Restaurar: la sesion mide, no configura ──
    dlg.set_value(dlg.by_aid(win, "PresetManagerDescriptionBox"), first_preset.get("Description") or "")
    time.sleep(STEP)
    press(win, "PresetManagerSaveButton", STEP + 1.2)
    restored = read_store()
    drv.log("[presets] almacen restaurado: %r" % (restored[0].get("Description") if restored else None))
    check(bool(restored) and restored[0].get("Description") == first_preset.get("Description"),
          "la descripcion del usuario vuelve a su valor por el mismo camino")

    press_close_button(win, STEP + 0.6)
    check(not any(a in aid_set(win) for a in BODY_ANCHORS), "el gestor queda cerrado")

    # ── 6. La MISMA orden destructiva por la puerta de la FILA, y el restablecimiento ──
    check(press(win, ROW_ACTION, STEP + 1.4), "se vuelve a abrir el gestor para ejercer el ciclo destructivo")
    row_cycle = destructive_cycle(win, hwnd, "fila", check, len(catalog_before))

    # «Restablecer» es la OTRA orden destructiva (vacia el catalogo entero): pregunta igual, y un «no» deja el
    # catalogo del usuario intacto. La sesion lo CANCELA: mide, no configura.
    press(win, "PresetManagerResetButton", STEP + 1.2)
    reset_anchors = confirm_anchors(win)
    check(CONFIRM_CANCEL in reset_anchors,
          "«Restablecer» tambien PREGUNTA (y ofrece cancelar)", "anclas: %s" % reset_anchors)
    press(win, CONFIRM_CANCEL, STEP + 0.5)
    tras_reset = read_store()
    check(len(tras_reset) == len(catalog_before) and not confirm_anchors(win),
          "y un «no» al restablecimiento deja el catalogo del usuario intacto",
          "%d presets" % len(tras_reset))

    press_close_button(win, STEP + 0.6)

    # ── 7. Limpieza: el nodo añadido se retira con Deshacer ──
    check(press(win, "ControlBarUndoButton", STEP + 1.2), "se deshace la adicion del nodo")
    img4 = sess.grab(hwnd, "99_final")
    c4 = menu.dominant(img4, CENTER)
    store_after = md5(STORE)
    prefs_after = prefs_essentials()
    drv.log("[presets] final: tarjetas=%d pixel centro=%s (%.1f%%) almacen md5=%s ajustes=%s"
            % (cards(), menu.hexs(c4[0]), 100 * c4[1], store_after, prefs_after))
    check(cards() == base_cards, "el lienzo queda con sus %d tarjetas" % base_cards, "%d" % cards())
    check(c4[0] == c0[0], "y la ventana vuelve a su pixel de linea base",
          "%s -> %s" % (menu.hexs(c0[0]), menu.hexs(c4[0])))
    check(store_after == store_before, "el almacen de presets del usuario queda byte-identico",
          "%s / %s" % (store_before, store_after))
    check(prefs_after == prefs_before,
          "y los ajustes del usuario (tema, idioma, carpeta de salida y favoritos) quedan como estaban",
          "%s -> %s" % (prefs_before, prefs_after))

    ok = all(results)
    drv.log("[presets] === RESULTADO: %s (%d de %d pasos) ==="
            % ("VERIFICADO" if ok else "FALLOS", sum(results), len(results)))
    with open(RESULT, "w", encoding="utf-8") as fh:
        json.dump({"pasos": len(results), "verificados": sum(results),
                   "tarjetas": base_cards,
                   "presets_en_el_almacen": len(catalog_before),
                   "ciclo_destructivo": {"tarjeta": {"creado": card_cycle[0], "tras_cancelar": card_cycle[1],
                                                            "tras_confirmar": card_cycle[2]},
                                          "fila": {"creado": row_cycle[0], "tras_cancelar": row_cycle[1],
                                                   "tras_confirmar": row_cycle[2]}},
                   "primer_preset": first_preset.get("Name"),
                   "anclas": {"cuerpo": present},
                   "filas": {"presets": len(rows), "primera": rows[0] if rows else None},
                   "textos": {"nombre_en_el_formulario": name_shown,
                              "descripcion_antes": description_before,
                              "descripcion_escrita": PROBE_DESCRIPTION,
                              "descripcion_en_el_almacen": written,
                              "descripcion_reabierta": reopened,
                              "boton_cerrar": closed},
                   "pixel_centro": {"base": menu.hexs(c0[0]), "tarjeta": menu.hexs(c1[0]),
                                    "cerrado": menu.hexs(c1b[0]), "fila": menu.hexs(c2[0]),
                                    "tras_guardar": menu.hexs(c3[0]), "final": menu.hexs(c4[0])},
                   "almacen_md5": {"antes": store_before, "despues": store_after},
                   "ajustes_del_usuario": {"antes": prefs_before, "despues": prefs_after}},
                  fh, ensure_ascii=False, indent=2)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
