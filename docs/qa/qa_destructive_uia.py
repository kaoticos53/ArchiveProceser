# -*- coding: utf-8 -*-
"""Sesion de las ORDENES DESTRUCTIVAS del host Uno en la app abierta (hito 265).

El reparto es el de siempre (263/264): quien ACTUA es este driver —otro proceso pulsa los controles REALES
por su AutomationId, el canal que usa un lector de pantalla— y quien MIDE es el propio driver: las anclas
del arbol, el FICHERO del almacen leido por fuera de la aplicacion, el recuento de tarjetas del lienzo y el
pixel dominante de la banda central.

Que se mide, y por que asi. El tramo cerro SEIS ordenes que preguntaban por la via SINCRONA del contrato
(la que en este host contesta «no» desde el hilo de UI, o «si» sin preguntar si el servicio es el nulo).
Una orden arreglada tiene que hacer tres cosas, y cada una se mide con una prueba distinta:

  1. PREGUNTAR  -> la pregunta tiene que estar EN PANTALLA antes de tocar nada (sus anclas en el arbol);
  2. un «no» NO destruye -> el veredicto es el DATO (el fichero del almacen de temas / las tarjetas del
     lienzo), no que la capa se dibuje: una capa que se cierra y borra igual pasaria un test de pixeles;
  3. un «si» SI destruye -> el mismo dato tiene que cambiar.

Las dos ordenes que se ejercen, y por que estas dos:

  - «ELIMINAR TEMA» del Estudio (la orden del tramo que estrena puerta en este host). Se abre el estudio
    desde el cajon, se CREA un tema propio (la unica orden de la sesion que escribe: el almacen pasa de N a
    N+1), se pulsa «Eliminar»: la pregunta se monta DENTRO del modal abierto (WinUI no admite un segundo
    ContentDialog). Un «no» deja el tema en el almacen y un «si» lo quita, de modo que el catalogo del
    usuario vuelve a su contenido de partida —leido del fichero, no del arbol—.
  - «NUEVO FLUJO» (la orden que el usuario tiene en el menu y en Ctrl+N). Con nodos en el lienzo la
    pregunta sale como modal PROPIO del host (no hay modal abierto dentro del cual hacerla). Un «no» deja
    las tres tarjetas y un «si» vacia el lienzo. El grafo no se persiste en ningun fichero (vive en memoria
    y se recarga al arrancar), asi que al final se REINICIA la app y se comprueba que la escena vuelve a sus
    tres tarjetas: la destruccion se queda en el lienzo y los datos del usuario no se tocan.

Lo que la sesion NO usa: borrar modelos de IA y vaciar el VFS (el primero borra ficheros reales del disco y
el segundo no tiene puerta dibujada en ninguna vista: se declaran como fronteras, no se fingen).

Uso:
  FILEFLOW_QA_WORK=qa-manual-265 python qa_manual_session.py --launch
  FILEFLOW_QA_WORK=qa-manual-265 python qa_destructive_uia.py --session

Salida: una linea por paso (`[destructivas] ...`) con lo medido antes y despues, capturas rotuladas en la
carpeta de la sesion, y exit 0 solo si todos los pasos salieron como dicen.
"""
import hashlib
import json
import os
import sys
import time

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import qa_ajustes_uia as drv  # noqa: E402
import qa_dialogs_uia as dlg  # noqa: E402
import qa_manual_session as sess  # noqa: E402
import qa_menu_uia as menu  # noqa: E402

STEP = 1.5
CENTER = menu.CENTER
CANVAS = dlg.CANVAS

# Las rutas REALES del producto (`AppPaths`, modo instalado): %AppData%\FileFlow, no la carpeta vieja
# %AppData%\FileFlowStudio —que guarda una copia de hace semanas y que una sesion que leyera de ahi no
# mediria—.
APP = os.path.join(os.environ.get("APPDATA", ""), "FileFlow")
THEMES = os.path.join(APP, "themes", "custom_themes.json")
PREFS = os.path.join(APP, "config", "user_preferences.json")
PRESETS = os.path.join(APP, "presets", "media_presets.json")
PREFS_ESSENTIALS = ("ActiveTheme", "Language", "DefaultGlobalOutputDir", "FavoriteNodeTypes")

# La PREGUNTA del host. Las dos formas comparten la ancla de la capa (medido en el 263 y en el 265): dentro
# de un modal los botones son los de la capa (`HostConfirmation*`); sin modal abierto, la pregunta es un
# ContentDialog propio y sus botones son los del diccionario del host.
QUESTION_LAYER = "HostConfirmationDialog"
INLINE_ACCEPT = "HostConfirmationAccept"
INLINE_CANCEL = "HostConfirmationCancel"
ACCEPT_NAMES = ("Aceptar", "Accept", "Aceptar y cerrar")
CANCEL_NAMES = ("Cancelar", "Cancel", "Cerrar", "Close")

# El cajon y el estudio de temas.
MENU_BUTTON = "ControlBarMenuButton"
DRAWER_THEME_STUDIO = "ControlBarDrawerThemeStudioButton"
DRAWER_NEW_FLOW = "ControlBarDrawerNewButton"
STUDIO_NEW = "ThemeStudioNewButton"
STUDIO_DELETE = "ThemeStudioDeleteButton"
STUDIO_CLOSE = "ThemeStudioCloseButton"
STUDIO_LIST = "ThemeStudioThemeList"
# El estudio ABIERTO se reconoce por sus CONTROLES anclados (la lista y sus botones). Su raiz es un Grid
# con AutomationId, y un Grid no tiene peer de automatizacion: el canal externo nunca ve `ThemeStudioBody`
# —medido en esta sesion—, asi que medir por el seria medir el vacio.
STUDIO_CONTROLS = (STUDIO_LIST, STUDIO_NEW, STUDIO_DELETE, STUDIO_CLOSE)

# Las tarjetas del flujo de ejemplo (los titulos que ve el canal externo en el lienzo).
EXAMPLE_CARDS = ("Folder Source", "Optimizador de Imágenes", "Destination Sink")

RESULT = os.path.join(sess.SHOTS, "destructivas-session.json")


def md5(path):
    if not os.path.exists(path):
        return None
    with open(path, "rb") as fh:
        return hashlib.md5(fh.read()).hexdigest()


def read_themes():
    """El almacen de temas propios, leido por FUERA de la aplicacion (la evidencia del valor escrito)."""
    if not os.path.exists(THEMES):
        return []
    with open(THEMES, "r", encoding="utf-8-sig") as fh:
        return json.load(fh)


def theme_names():
    return [str(t.get("Name") or "") for t in read_themes()]


def prefs_essentials():
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


def press(win, aid, wait=STEP):
    el = dlg.by_aid(win, aid)
    if el is None:
        return False
    drv.invoke(el)
    time.sleep(wait)
    return True


def question(win):
    """Las anclas de la pregunta que hay en pantalla AHORA (vacio = no hay pregunta)."""
    found = aid_set(win)
    anchors = {"layer": QUESTION_LAYER in found, "inline_accept": INLINE_ACCEPT in found,
               "inline_cancel": INLINE_CANCEL in found}
    if anchors["inline_accept"]:
        anchors["kind"] = "dentro del modal"
    elif anchors["layer"]:
        anchors["kind"] = "modal propio del host"
    else:
        anchors["kind"] = ""
    return anchors


def answer(win, accept):
    """Contesta la pregunta: por el boton de la CAPA si la pregunta vive dentro de un modal, y si no por el
    boton del modal propio, buscado por su rotulo DENTRO del rectangulo de la pregunta (el marco tiene otros
    botones y contestar el de otro control seria contestar otra cosa)."""
    inline = INLINE_ACCEPT if accept else INLINE_CANCEL
    if dlg.by_aid(win, inline) is not None:
        drv.invoke(dlg.by_aid(win, inline))
        time.sleep(STEP)
        return "capa:" + inline

    layer = dlg.by_aid(win, QUESTION_LAYER)
    if layer is None:
        return ""
    try:
        rect = layer.rectangle()
    except Exception:
        return ""
    wanted = ACCEPT_NAMES if accept else CANCEL_NAMES
    for name, el, r in dlg.elements(win):
        if not (rect.left <= r.left and r.right <= rect.right and rect.top <= r.top and r.bottom <= rect.bottom):
            continue
        if name in wanted:
            drv.invoke(el)
            time.sleep(STEP)
            return "modal:%s" % name
    return ""


def cards(win):
    """Las tarjetas del flujo de ejemplo que el canal externo ve en el LIENZO (el inspector repite el
    titulo del nodo inspeccionado, por eso se filtra por la zona del lienzo)."""
    titles = [n for n, _, _ in dlg.elements(win, lambda n: n in EXAMPLE_CARDS, CANVAS)]
    return titles


def studio_open(win):
    """Los controles del estudio que el canal externo ve AHORA (vacio = estudio cerrado)."""
    found = aid_set(win)
    return [a for a in STUDIO_CONTROLS if a in found]


def card_pixels(img):
    """Las tarjetas CONTADAS POR PIXEL (la barra de acento de cada tarjeta): la medida independiente del
    arbol, la que no puede mentir sobre lo que hay dibujado en el lienzo."""
    return len(sess.accent_groups(img)[0])


def prefs_raw():
    if not os.path.exists(PREFS):
        return {}
    with open(PREFS, "r", encoding="utf-8-sig") as fh:
        return json.load(fh)


def open_drawer(win):
    if dlg.by_aid(win, DRAWER_NEW_FLOW) is not None:
        return True
    press(win, MENU_BUTTON, STEP + 0.6)
    return dlg.by_aid(win, DRAWER_NEW_FLOW) is not None


def main():
    if "--session" not in sys.argv:
        drv.log("[destructivas] nada que hacer: usa --session")
        return 2

    os.makedirs(sess.SHOTS, exist_ok=True)
    win = drv.connect()
    if win is None:
        drv.log("[destructivas] FALLO: la app no aparece por UIA")
        return 3

    hwnd = int(json.load(open(sess.STATE))["hwnd"])
    results = []

    def check(condition, what, detail=""):
        results.append(bool(condition))
        drv.log("[destructivas] %s %s%s" % ("[OK]   " if condition else "[FALLO]", what,
                                           ("  <- " + detail) if detail else ""))
        return bool(condition)

    # ── 0. La linea base: lo que el usuario tiene en disco y en el lienzo ──
    themes_before = theme_names()
    themes_md5 = md5(THEMES)
    prefs_before = prefs_essentials()
    prefs_data = prefs_raw()
    prefs_md5 = md5(PREFS)
    presets_md5 = md5(PRESETS)
    img0 = sess.grab(hwnd, "40_base")
    c0 = menu.dominant(img0, CENTER)
    baseline_cards = cards(win)
    drv.log("[destructivas] base: temas propios=%d %s | tarjetas=%s | pixel centro=%s (%.1f%%)"
            % (len(themes_before), themes_before, baseline_cards, menu.hexs(c0[0]), 100 * c0[1]))
    drv.log("[destructivas] ficheros del usuario: temas md5=%s | preferencias md5=%s | presets md5=%s"
            % (themes_md5, prefs_md5, presets_md5))
    check(len(baseline_cards) == 3,
          "el lienzo arranca con el flujo de ejemplo: SUS tres tarjetas estan en el arbol",
          str(baseline_cards))

    # ── 1. «ELIMINAR TEMA»: la orden del tramo que estrena puerta ──
    check(open_drawer(win), "el cajon del menu se despliega con su boton")
    press(win, DRAWER_THEME_STUDIO, STEP + 1.4)
    opened = studio_open(win)
    drv.log("[destructivas] estudio abierto: %s" % opened)
    check(opened == list(STUDIO_CONTROLS),
          "el cajon abre el ESTUDIO DE TEMAS del host con su catalogo y sus ordenes",
          "anclas=%s" % opened)

    delete_el = dlg.by_aid(win, STUDIO_DELETE)
    check(delete_el is not None, "el estudio dibuja el boton «Eliminar» (el que el tramo rescato de pendientes)")
    if delete_el is not None:
        check(not delete_el.is_enabled(),
              "y con un tema de fabrica elegido esta DESHABILITADO: los del sistema son inmutables")

    # Se CREA un tema propio: es la unica escritura de la sesion en el almacen del usuario y la deja medida.
    press(win, STUDIO_NEW, STEP + 1.4)
    after_new = theme_names()
    created = after_new[-1] if after_new else ""
    drv.log("[destructivas] altas: %d -> %d temas propios; el ultimo es %r"
            % (len(themes_before), len(after_new), created))
    check(len(after_new) == len(themes_before) + 1 and created not in themes_before,
          "«Nuevo tema» da de alta un tema propio EN EL ALMACEN: el fichero pasa de %d a %d"
          % (len(themes_before), len(after_new)))
    delete_el = dlg.by_aid(win, STUDIO_DELETE)
    check(delete_el is not None and delete_el.is_enabled(),
          "y con un tema PROPIO elegido el boton «Eliminar» queda habilitado")

    # La orden, con la pregunta delante. El veredicto es el FICHERO en cada paso.
    press(win, STUDIO_DELETE, STEP + 1.2)
    q_studio = question(win)
    with_question = theme_names()
    img_q1 = sess.grab(hwnd, "41_tema_pregunta")
    c_q1 = menu.dominant(img_q1, CENTER)
    drv.log("[destructivas] pregunta del borrado de tema: %s | almacen=%d | pixel centro=%s"
            % (q_studio, len(with_question), menu.hexs(c_q1[0])))
    check(q_studio["inline_accept"] and q_studio["inline_cancel"],
          "«Eliminar» del estudio PREGUNTA antes de destruir: la pregunta esta EN PANTALLA con sus dos botones",
          "kind=%s" % q_studio["kind"])
    check(q_studio["kind"] == "dentro del modal" and studio_open(win) == list(STUDIO_CONTROLS),
          "y se monta DENTRO del estudio abierto —el estudio sigue debajo— (WinUI no admite un segundo "
          "ContentDialog)",
          "estudio=%s" % studio_open(win))
    check(len(with_question) == len(themes_before) + 1,
          "con la pregunta en pantalla todavia no se ha borrado nada: el tema propio sigue en el almacen",
          "%d temas" % len(with_question))

    drv.log("[destructivas] un «no»: %s" % answer(win, accept=False))
    after_refuse = theme_names()
    img_r1 = sess.grab(hwnd, "42_tema_cancelado")
    c_r1 = menu.dominant(img_r1, CENTER)
    drv.log("[destructivas] tras cancelar: almacen=%d %s | pixel centro=%s (baseline %s)"
            % (len(after_refuse), after_refuse, menu.hexs(c_r1[0]), menu.hexs(c0[0])))
    check(not question(win)["layer"], "el «no» cierra la pregunta")
    check(after_refuse == after_new,
          "UN «NO» NO DESTRUYE: el tema propio sigue en el almacen y el estudio sigue abierto")
    check(c_r1[0] == c_q1[0] or c_r1[0] != c0[0],
          "y la escena vuelve al estudio, no a la linea base: el modal sigue abierto",
          "%s -> %s" % (menu.hexs(c_q1[0]), menu.hexs(c_r1[0])))

    press(win, STUDIO_DELETE, STEP + 1.2)
    drv.log("[destructivas] un «si»: %s" % answer(win, accept=True))
    after_accept = theme_names()
    remaining_md5 = md5(THEMES)
    img_a1 = sess.grab(hwnd, "43_tema_borrado")
    c_a1 = menu.dominant(img_a1, CENTER)
    drv.log("[destructivas] tras confirmar: almacen=%d %s | md5=%s (antes %s) | pixel centro=%s"
            % (len(after_accept), after_accept, remaining_md5, themes_md5, menu.hexs(c_a1[0])))
    check(created not in after_accept and len(after_accept) == len(themes_before),
          "UN «SI» SI DESTRUYE: el tema propio desaparece del almacen y el catalogo vuelve a sus %d temas"
          % len(themes_before))
    check(after_accept == themes_before,
          "y el catalogo del usuario queda EXACTAMENTE como estaba (mismos temas, mismo orden)",
          "%s" % after_accept)
    check(remaining_md5 == themes_md5,
          "el fichero del almacen vuelve a su md5 de partida (la sesion mide y no deja rastro)",
          "%s vs %s" % (remaining_md5, themes_md5))

    press(win, STUDIO_CLOSE, STEP + 1.2)
    img_c1 = sess.grab(hwnd, "44_tema_estudio_cerrado")
    c_c1 = menu.dominant(img_c1, CENTER)
    check(not studio_open(win),
          "el estudio se cierra por su boton y la ventana vuelve al lienzo",
          "pixel centro=%s (base %s)" % (menu.hexs(c_c1[0]), menu.hexs(c0[0])))
    check(cards(win) == baseline_cards and card_pixels(img_c1) == len(baseline_cards),
          "el lienzo sigue con sus tres tarjetas (arbol y pixeles de acento): la orden de temas no toca el flujo",
          "pixeles=%d" % card_pixels(img_c1))

    # ── 2. «NUEVO FLUJO»: la pregunta del host sin modal abierto ──
    check(open_drawer(win), "el cajon vuelve a desplegarse para la orden de flujo")
    press(win, DRAWER_NEW_FLOW, STEP + 1.4)
    q_flow = question(win)
    cards_with_question = cards(win)
    img_q2 = sess.grab(hwnd, "45_flujo_pregunta")
    c_q2 = menu.dominant(img_q2, CENTER)
    drv.log("[destructivas] pregunta de flujo nuevo: %s | tarjetas=%s | pixel centro=%s"
            % (q_flow, cards_with_question, menu.hexs(c_q2[0])))
    check(q_flow["layer"] and not q_flow["inline_accept"],
          "«Nuevo Flujo» PREGUNTA antes de vaciar el lienzo, en su PROPIO modal (no hay otro abierto)",
          "kind=%s" % q_flow["kind"])
    check(len(cards_with_question) == 3,
          "con la pregunta en pantalla el lienzo sigue con sus tres tarjetas", str(cards_with_question))

    drv.log("[destructivas] un «no»: %s" % answer(win, accept=False))
    cards_after_refuse = cards(win)
    img_r2 = sess.grab(hwnd, "46_flujo_cancelado")
    c_r2 = menu.dominant(img_r2, CENTER)
    drv.log("[destructivas] tras cancelar: tarjetas=%s | pixel centro=%s (baseline %s)"
            % (cards_after_refuse, menu.hexs(c_r2[0]), menu.hexs(c0[0])))
    check(not question(win)["layer"], "el «no» cierra la pregunta")
    check(cards_after_refuse == baseline_cards,
          "UN «NO» NO DESTRUYE: las tres tarjetas del lienzo siguen donde estaban",
          str(cards_after_refuse))
    check(c_r2[0] == c0[0], "y la escena vuelve a su pixel de linea base",
          "%s -> %s" % (menu.hexs(c_q2[0]), menu.hexs(c_r2[0])))

    check(open_drawer(win), "el cajon se despliega otra vez")
    press(win, DRAWER_NEW_FLOW, STEP + 1.4)
    drv.log("[destructivas] un «si»: %s" % answer(win, accept=True))
    cards_after_accept = cards(win)
    img_a2 = sess.grab(hwnd, "47_flujo_vaciado")
    c_a2 = menu.dominant(img_a2, CENTER)
    drv.log("[destructivas] tras confirmar: tarjetas=%s | pixel centro=%s" % (cards_after_accept, menu.hexs(c_a2[0])))
    check(len(cards_after_accept) == 0,
          "UN «SI» SI DESTRUYE: el lienzo queda vacio (sin tarjetas del ejemplo)",
          str(cards_after_accept))
    # La medida INDEPENDIENTE del arbol: las tarjetas contadas por su barra de acento en la captura. El
    # pixel del centro no sirve aqui —el fondo del lienzo es el mismo con tarjetas y sin ellas—, asi que la
    # prueba de que se vacio la da el recuento por pixel.
    check(card_pixels(img_a2) == 0,
          "y la captura lo confirma: ninguna barra de tarjeta en el lienzo vaciado",
          "pixeles=%d (base %d)" % (card_pixels(img_a2), card_pixels(img0)))

    # ── 3. El grafo no se persiste: al reiniciar, la escena vuelve. Y los datos del usuario, intactos ──
    #
    # Las DOS ordenes, ya ejercidas: las preferencias del usuario tienen que seguir donde estaban. La medida
    # va ANTES del reinicio a proposito: el arranque de la app reescribe su propia marca de comprobacion de
    # actualizaciones (`LastUpdateCheckUtc`), y ese cambio es del arranque, no de las ordenes.
    prefs_mid = md5(PREFS)
    drv.log("[destructivas] preferencias tras las dos ordenes: md5=%s (al empezar %s)" % (prefs_mid, prefs_md5))
    check(prefs_mid == prefs_md5,
          "ejercer las dos ordenes NO toca las preferencias del usuario (mismo md5 antes y despues)",
          str(prefs_mid))

    drv.log("[destructivas] reiniciando la app para comprobar que el grafo no se persiste")
    sess.stop()
    time.sleep(2.0)
    if sess.launch() != 0:
        check(False, "la app se relanza para medir la escena de arranque")
    else:
        win = drv.connect()
        if win is None:
            check(False, "la app relanzada aparece por UIA")
        else:
            hwnd = int(json.load(open(sess.STATE))["hwnd"])
            back = cards(win)
            img_b = sess.grab(hwnd, "48_reinicio")
            c_b = menu.dominant(img_b, CENTER)
            drv.log("[destructivas] tras el reinicio: tarjetas=%s | pixel centro=%s (baseline %s)"
                    % (back, menu.hexs(c_b[0]), menu.hexs(c0[0])))
            check(back == baseline_cards,
                  "el lienzo del ejemplo vuelve a estar ENTERO al arrancar: la destruccion vivia en memoria",
                  str(back))

    # ── 4. Los datos y las preferencias del usuario, intactos ──
    prefs_after = prefs_essentials()
    themes_after = theme_names()
    prefs_data_after = prefs_raw()
    touched = sorted(k for k in set(prefs_data) | set(prefs_data_after)
                     if prefs_data.get(k) != prefs_data_after.get(k))
    drv.log("[destructivas] claves de preferencias reescritas en toda la sesion: %s" % touched)
    drv.log("[destructivas] ficheros del usuario al final: temas md5=%s | preferencias md5=%s | presets md5=%s"
            % (md5(THEMES), md5(PREFS), md5(PRESETS)))
    check(themes_after == themes_before and md5(THEMES) == themes_md5,
          "el catalogo de temas propios del usuario queda byte a byte como estaba",
          "%s" % themes_after)
    check(prefs_after == prefs_before,
          "sus preferencias esenciales quedan intactas (tema, idioma, carpeta de salida y favoritos)",
          "%s" % prefs_after)
    # Lo unico que el arranque de la app reescribe por su cuenta es su marca de la ultima comprobacion de
    # actualizaciones. Si apareciera CUALQUIER otra clave tocada, la sesion habria cambiado algo del usuario.
    check(touched <= ["LastUpdateCheckUtc"],
          "y lo unico que el arranque reescribe es su marca de comprobaciones (LastUpdateCheckUtc)",
          str(touched))
    check(md5(PRESETS) == presets_md5,
          "y el almacen de presets no se toco en toda la sesion", str(md5(PRESETS)))

    ok = all(results)
    drv.log("[destructivas] === RESULTADO: %s (%d de %d pasos) ==="
            % ("VERIFICADO" if ok else "FALLOS", sum(results), len(results)))
    with open(RESULT, "w", encoding="utf-8") as fh:
        json.dump({
            "pasos": len(results), "verificados": sum(results),
            "temas_antes": themes_before, "tema_creado": created, "temas_despues": themes_after,
            "tarjetas_base": baseline_cards, "tarjetas_con_pregunta": cards_with_question,
            "tarjetas_tras_no": cards_after_refuse, "tarjetas_tras_si": cards_after_accept,
            "pixel_centro": {"base": menu.hexs(c0[0]), "tema_pregunta": menu.hexs(c_q1[0]),
                             "flujo_pregunta": menu.hexs(c_q2[0])},
            "md5": {"temas": themes_md5, "preferencias": prefs_md5, "presets": presets_md5},
        }, fh, ensure_ascii=False, indent=2)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
