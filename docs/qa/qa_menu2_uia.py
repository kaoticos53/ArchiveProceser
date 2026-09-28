# -*- coding: utf-8 -*-
"""Sesion de las ENTRADAS Y ATAJOS nuevos del menu del host Uno (hito 258), con DRIVER EXTERNO.

El reparto es el de siempre (255/257/258): quien ACTUA es este driver —otro proceso le da a los
controles REALES por sus AutomationId, o inyecta la tecla fisica con keybd_event— y quien MIDE es el
vigilante de qa_manual_session.py (`--watch`) ademas del propio driver, que mide su pixel por bandas.

Que se ejercita, y por que cada paso mide lo que mide:

  - el CAJON con sus 11 entradas (las 5 de antes + las 3 de FLUJO y las 3 de AYUDA que estrena este
    hito) presentes en el arbol mientras esta desplegado;
  - «Acerca de» (orden canonica del nucleo -> ventana servida por el host): la superficie aparece con
    sus anclas y la version del producto, el pixel central cambia con el modal, y al cerrarla el pixel
    vuelve al de la linea base;
  - «Nuevo Flujo» (orden cumplida por el canal ASINCRONO del host): la confirmacion aparece —leida por
    el texto del dialogo— y CANCELAR deja el lienzo con las MISMAS tarjetas; ademas se mide el pixel;
  - el ATAJO Ctrl+N por tecla FISICA: abre la MISMA confirmacion que la entrada del cajon, y cancelarla
    deja el lienzo igual;
  - el ATAJO F5 por tecla FISICA: se mide por el RASTRO (`canvas-focus-trace.txt`, con
    FILEFLOW_CANVAS_TRACE=1), que es lo unico observable de una tecla cuyo comando no tiene efecto sin
    una ejecucion en marcha (igual que en el escritorio).

Nada de esto escribe preferencias: se comprueba el md5 del fichero del usuario antes y despues.

Uso (con la app ya lanzada por el instrumento, que maximiza y escribe session_state.json):
  FILEFLOW_QA_WORK=qa-manual-269 FILEFLOW_CANVAS_TRACE=1 python qa_manual_session.py --launch
  FILEFLOW_QA_WORK=qa-manual-269 python qa_menu2_uia.py --session

Salida: una linea por paso (`[menu2] ...`) con lo medido antes y despues, capturas rotuladas en la
carpeta de la sesion, y exit 0 solo si todos los pasos salieron como dicen.
"""
import ctypes
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
import qa_dialogs_uia as dlg  # noqa: E402  (by_aid/elements sin cache: la lisa miente tras un Invoke)

u32 = ctypes.windll.user32

VK_CONTROL, VK_SHIFT, VK_MENU = 0x11, 0x10, 0x12
VK_F5, VK_F10, VK_N = 0x74, 0x79, 0x4E
KEYEVENTF_KEYUP = 0x0002

STEP = 1.6
CENTER = menu.CENTER
CANVAS = dlg.CANVAS
PREFS = dlg.PREFS
REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
TRACE = os.path.join(REPO, "FileFlow.App.Uno", "bin", "Debug",
                     "net10.0-windows10.0.19041.0", "canvas-focus-trace.txt")

# Las 11 entradas del cajon: las 5 de los hitos anteriores y las 6 que estrena este.
DRAWER_OLD = ("ControlBarThemeCombo", "ControlBarLanguageCombo", "ControlBarDrawerSettingsButton",
              "ControlBarDrawerInspectorButton", "ControlBarDrawerCloseButton")
DRAWER_NEW = ("ControlBarDrawerNewButton", "ControlBarDrawerLoadButton", "ControlBarDrawerSaveButton",
              "ControlBarDrawerManualButton", "ControlBarDrawerExamplesButton",
              "ControlBarDrawerAboutButton")
DRAWER = DRAWER_OLD + DRAWER_NEW

# La ancla de la superficie «Acerca de»: el AutomationId del propio modal (un ContentDialog SI tiene
# peer de automatizacion, a diferencia de un Border) mas las dos lineas de texto que el cuerpo declara.
ABOUT_ANCHORS = ("AboutDialog", "AboutVersionText", "AboutDescriptionText")

# El texto de la confirmacion de «Nuevo Flujo» (clave Msg_NewWorkflowConfirm del diccionario del producto).
CONFIRM_HINT = "crear un nuevo flujo"

RESULT = os.path.join(sess.SHOTS, "menu2-session.json")


def md5(path):
    if not os.path.exists(path):
        return None
    with open(path, "rb") as fh:
        return hashlib.md5(fh.read()).hexdigest()


def key(vk, ctrl=False, shift=False):
    """La tecla FISICA, al proceso en primer plano: keybd_event es el canal del hardware (el mismo que
    midio la sesion 268 contra la casilla «Modo Prueba»), no el canal mediado de UIA."""
    if ctrl:
        u32.keybd_event(VK_CONTROL, 0, 0, 0)
    if shift:
        u32.keybd_event(VK_SHIFT, 0, 0, 0)
    time.sleep(0.05)
    u32.keybd_event(vk, 0, 0, 0)
    time.sleep(0.06)
    u32.keybd_event(vk, 0, KEYEVENTF_KEYUP, 0)
    if shift:
        u32.keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, 0)
    if ctrl:
        u32.keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0)
    time.sleep(STEP)


def aid_set(win):
    """Las anclas de automatizacion que el arbol expone AHORA (indice fresco, sin cache)."""
    found = set()
    for _, el, _ in dlg.elements(win):
        try:
            aid = el.element_info.automation_id or ""
        except Exception:
            continue
        if aid:
            found.add(aid)
    return found


def names(win, pred):
    hits = []
    for name, el, _ in dlg.elements(win):
        if pred(name):
            hits.append((name, el))
    return hits


def trace_lines():
    if not os.path.exists(TRACE):
        return []
    with open(TRACE, "r", encoding="utf-8", errors="replace") as fh:
        return [line.strip() for line in fh if line.strip()]


def main():
    if "--session" not in sys.argv:
        drv.log("[menu2] nada que hacer: usa --session")
        return 2

    os.makedirs(sess.SHOTS, exist_ok=True)
    win = drv.connect()
    if win is None:
        drv.log("[menu2] FALLO: la app no aparece por UIA")
        return 3

    hwnd = int(json.load(open(sess.STATE))["hwnd"])
    results = []
    prefs_before = md5(PREFS)

    def check(condition, what, detail=""):
        results.append(bool(condition))
        drv.log("[menu2] %s %s%s" % ("[OK]   " if condition else "[FALLO]", what,
                                     ("  <- " + detail) if detail else ""))
        return bool(condition)

    def cards():
        titles = ("Folder Source", "Optimizador de Imágenes", "Destination Sink")
        return len(dlg.elements(win, lambda n: n in titles, CANVAS))

    drv.log("[menu2] preferencias del usuario: md5=%s" % prefs_before)
    drv.log("[menu2] rastro: %s (lineas=%d)" % (TRACE, len(trace_lines())))

    # ── 0. Linea base: la barra en reposo y el lienzo del ejemplo ──
    img0 = sess.grab(hwnd, "50_base")
    c0 = menu.dominant(img0, CENTER)
    base_anchors = aid_set(win)
    base_cards = cards()
    drv.log("[menu2] base: tarjetas=%d pixel centro=%s (%.1f%%) anclas cajon=%d/11"
            % (base_cards, menu.hexs(c0[0]), 100 * c0[1],
               len([a for a in DRAWER if a in base_anchors])))
    check(base_cards == 3, "el lienzo del ejemplo arranca con sus 3 tarjetas",
          "tarjetas=%d" % base_cards)
    check(not any(a in base_anchors for a in DRAWER),
          "con el menu recogido, las entradas del cajon NO estan en el arbol")

    # ── 1. El cajon con sus 11 entradas (5 de antes + 6 nuevas) ──
    menu_button = dlg.by_aid(win, "ControlBarMenuButton")
    check(menu_button is not None, "el boton «Menu» esta en el arbol")
    if menu_button is not None:
        drv.invoke(menu_button)
        time.sleep(STEP)
    opened = aid_set(win)
    img1 = sess.grab(hwnd, "51_cajon_abierto")
    c1 = menu.dominant(img1, CENTER)
    missing = [a for a in DRAWER if a not in opened]
    drv.log("[menu2] cajon abierto: entradas=%d/11 pixel centro=%s (%.1f%%)"
            % (len([a for a in DRAWER if a in opened]), menu.hexs(c1[0]), 100 * c1[1]))
    check(not missing, "el cajon expone sus 11 entradas, incluidas las 6 que estrena el hito",
          "faltan: %s" % ", ".join(missing) if missing else "")
    check(c1[0] != c0[0], "y su velo se ve en el pixel: la banda central cambia de color dominante",
          "%s -> %s" % (menu.hexs(c0[0]), menu.hexs(c1[0])))

    # ── 2. «Acerca de»: la orden canonica llega a la ventana del host ──
    about = dlg.by_aid(win, "ControlBarDrawerAboutButton")
    check(about is not None, "la entrada «Acerca de» esta en el arbol con el cajon desplegado")
    if about is not None:
        drv.invoke(about)
        time.sleep(STEP + 0.8)
    about_anchors = aid_set(win)
    img2 = sess.grab(hwnd, "52_acerca_de")
    c2 = menu.dominant(img2, CENTER)
    version_name = ""
    for name, _ in names(win, lambda n: "net10.0" in n):
        version_name = name
    drv.log("[menu2] «Acerca de»: anclas=%s pixel centro=%s (%.1f%%) version=%r"
            % (sorted(a for a in about_anchors if a in ABOUT_ANCHORS), menu.hexs(c2[0]), 100 * c2[1],
               version_name))
    check("AboutDialog" in about_anchors,
          "pulsar «Acerca de» abre la superficie del host (su modal con su ancla en el arbol)",
          "presentes: %s" % ", ".join(a for a in ABOUT_ANCHORS if a in about_anchors))
    check("net10.0" in version_name,
          "y la superficie dice la version del producto, leida por el canal externo", repr(version_name))
    check(c2[0] != c0[0], "el modal se ve en el pixel (la banda central cambia de color dominante)",
          "%s -> %s" % (menu.hexs(c0[0]), menu.hexs(c2[0])))

    # Cerrar: el boton del modal (su texto es el del diccionario del host).
    close = None
    for candidate in ("Aceptar", "Accept"):
        hits = names(win, lambda n: n == candidate)
        if hits:
            close = hits[0][1]
            break
    check(close is not None, "el boton de aceptar del modal esta en el arbol")
    if close is not None:
        drv.invoke(close)
        time.sleep(STEP)
    closed_anchors = aid_set(win)
    img3 = sess.grab(hwnd, "53_acerca_de_cerrado")
    c3 = menu.dominant(img3, CENTER)
    drv.log("[menu2] tras cerrar «Acerca de»: pixel centro=%s (%.1f%%) anclas=%s"
            % (menu.hexs(c3[0]), 100 * c3[1], sorted(a for a in closed_anchors if a in ABOUT_ANCHORS)))
    check(not any(a in closed_anchors for a in ABOUT_ANCHORS),
          "y cerrarla la retira: su superficie sale del arbol")
    check(c3[0] == c0[0], "el pixel central vuelve al de la linea base",
          "%s -> %s -> %s" % (menu.hexs(c0[0]), menu.hexs(c2[0]), menu.hexs(c3[0])))

    # ── 3. «Nuevo Flujo» desde el cajon: confirmacion asincrona y CANCELAR intacto ──
    #
    # Elegir «Acerca de» cerró el cajón (la orden del núcleo pone IsMenuOpen en falso): se vuelve a
    # desplegar, que es exactamente lo que haría el usuario para pedir la entrada siguiente.
    again = dlg.by_aid(win, "ControlBarMenuButton")
    if again is not None:
        drv.invoke(again)
        time.sleep(STEP)
    reopened = aid_set(win)
    check(any(a in reopened for a in DRAWER_NEW),
          "el cajón se vuelve a desplegar para las entradas que quedan",
          "entradas nuevas en el arbol: %d/6" % len([a for a in DRAWER_NEW if a in reopened]))

    before_cards = cards()
    new_button = dlg.by_aid(win, "ControlBarDrawerNewButton")
    check(new_button is not None, "la entrada «Nuevo Flujo» esta en el arbol")
    if new_button is not None:
        drv.invoke(new_button)
        time.sleep(STEP + 0.8)
    confirm_names = [n for n, _ in names(win, lambda n: CONFIRM_HINT in n.lower())]
    img4 = sess.grab(hwnd, "54_confirmacion_nuevo")
    c4 = menu.dominant(img4, CENTER)
    drv.log("[menu2] «Nuevo Flujo»: confirmacion=%r pixel centro=%s (%.1f%%)"
            % (confirm_names[0] if confirm_names else "", menu.hexs(c4[0]), 100 * c4[1]))
    check(bool(confirm_names), "«Nuevo Flujo» pide confirmacion (el texto del dialogo, leido por UIA)",
          "; ".join(confirm_names[:1]))
    check(c4[0] != c0[0], "y la confirmacion se ve en el pixel",
          "%s -> %s" % (menu.hexs(c0[0]), menu.hexs(c4[0])))

    cancel = None
    for candidate in ("Cancelar", "Cancel"):
        hits = names(win, lambda n: n == candidate)
        if hits:
            cancel = hits[0][1]
            break
    check(cancel is not None, "el boton de cancelar del dialogo esta en el arbol")
    if cancel is not None:
        drv.invoke(cancel)
        time.sleep(STEP)
    after_cards = cards()
    img5 = sess.grab(hwnd, "55_confirmacion_cancelada")
    c5 = menu.dominant(img5, CENTER)
    drv.log("[menu2] tras cancelar: tarjetas=%d pixel centro=%s (%.1f%%)"
            % (after_cards, menu.hexs(c5[0]), 100 * c5[1]))
    check(after_cards == before_cards,
          "CANCELAR deja el lienzo intacto: las mismas tarjetas que antes de pedirlo",
          "%d -> %d" % (before_cards, after_cards))
    check(c5[0] == c0[0], "y el pixel central vuelve al de la linea base",
          "%s -> %s" % (menu.hexs(c4[0]), menu.hexs(c5[0])))

    # ── 4. El ATAJO Ctrl+N por tecla FISICA: la MISMA confirmacion ──
    u32.SetForegroundWindow(hwnd)
    time.sleep(0.6)
    ctrl_cards = cards()
    key(VK_N, ctrl=True)
    ctrl_confirms = [n for n, _ in names(win, lambda n: CONFIRM_HINT in n.lower())]
    img6 = sess.grab(hwnd, "56_ctrl_n")
    c6 = menu.dominant(img6, CENTER)
    drv.log("[menu2] Ctrl+N (tecla fisica): confirmacion=%r pixel centro=%s (%.1f%%)"
            % (ctrl_confirms[0] if ctrl_confirms else "", menu.hexs(c6[0]), 100 * c6[1]))
    check(bool(ctrl_confirms),
          "el atajo Ctrl+N llega a la MISMA confirmacion que la entrada del cajon",
          "; ".join(ctrl_confirms[:1]))
    check(c6[0] == c4[0], "y su dialogo deja el mismo pixel que el de la entrada",
          "%s / %s" % (menu.hexs(c4[0]), menu.hexs(c6[0])))
    cancel2 = None
    for candidate in ("Cancelar", "Cancel"):
        hits = names(win, lambda n: n == candidate)
        if hits:
            cancel2 = hits[0][1]
            break
    if cancel2 is not None:
        drv.invoke(cancel2)
        time.sleep(STEP)
    check(cards() == ctrl_cards,
          "y cancelar el suyo deja el lienzo como estaba", "%d tarjetas" % cards())

    # ── 5. El ATAJO F5 por tecla FISICA: medido por el RASTRO ──
    lines_before = trace_lines()
    key(VK_F5)
    lines_after = trace_lines()
    new_lines = lines_after[len(lines_before):]
    f5_hit = [line for line in new_lines if "atajo=F5" in line and "ContinueWorkflowCommand" in line]
    drv.log("[menu2] F5 (tecla fisica): lineas nuevas del rastro=%d %s"
            % (len(new_lines), json.dumps(new_lines[-2:], ensure_ascii=False)))
    check(bool(f5_hit),
          "el atajo F5 se enruta a la orden del ciclo y lo deja escrito en el rastro "
          "(no hay efecto visible sin ejecucion, igual que en el escritorio)",
          f5_hit[-1] if f5_hit else "sin linea de F5")

    lines_before10 = trace_lines()
    key(VK_F10)
    lines_after10 = trace_lines()
    f10_hit = [line for line in lines_after10[len(lines_before10):] if "atajo=F10" in line]
    drv.log("[menu2] F10 (tecla fisica): %s" % json.dumps(f10_hit[-1:], ensure_ascii=False))
    check(bool(f10_hit), "y F10 llega a la orden del paso a paso por el mismo camino",
          f10_hit[-1] if f10_hit else "sin linea de F10")

    # ── 6. Cierre: la escena y tus preferencias, como al entrar ──
    img7 = sess.grab(hwnd, "99_final")
    c7 = menu.dominant(img7, CENTER)
    prefs_after = md5(PREFS)
    drv.log("[menu2] final: tarjetas=%d pixel centro=%s (%.1f%%) preferencias md5=%s"
            % (cards(), menu.hexs(c7[0]), 100 * c7[1], prefs_after))
    check(c7[0] == c0[0], "la sesion termina con la escena igual a la de la linea base",
          "centro %s/%s" % (menu.hexs(c0[0]), menu.hexs(c7[0])))
    check(cards() == base_cards, "y el lienzo con las mismas tarjetas", "%d" % cards())
    check(prefs_after == prefs_before,
          "el fichero de preferencias del usuario queda byte-identico (las entradas de menu no escriben)",
          "%s / %s" % (prefs_before, prefs_after))

    ok = all(results)
    drv.log("[menu2] === RESULTADO: %s (%d de %d pasos) ==="
            % ("VERIFICADO" if ok else "FALLOS", sum(results), len(results)))
    with open(RESULT, "w", encoding="utf-8") as fh:
        json.dump({"pasos": len(results), "verificados": sum(results),
                   "tarjetas": base_cards,
                   "pixel_centro": {"base": menu.hexs(c0[0]), "acerca_de": menu.hexs(c2[0]),
                                    "confirmacion": menu.hexs(c4[0]), "final": menu.hexs(c7[0])},
                   "rastro": {"F5": f5_hit[-1] if f5_hit else None,
                              "F10": f10_hit[-1] if f10_hit else None},
                   "preferencias_md5": {"antes": prefs_before, "despues": prefs_after}},
                  fh, ensure_ascii=False, indent=2)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
