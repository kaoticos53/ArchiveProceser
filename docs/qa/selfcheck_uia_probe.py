# -*- coding: utf-8 -*-
"""El instrumento externo del modo --selfcheck-uia (hito 239).

La app corre con --selfcheck-uia y un hijo EXTERNO (este fichero: python + pywinauto 0.6.9, sin
UIAccess) la observa por UI Automation con las anclas del hito 238. La app le pasa su pid en
FILEFLOW_UIA_TARGET_PID (la app vive: no la lanza la sonda). La leccion A1 del 238 aplica: ni la
ventana ni un Grid raiz sin peer materializan en el arbol UIA, asi que la app se confirma por sus
ANCLAS con peer (CanvasRoot) y sondea:

  S1. Las anclas del lienzo y la barra en el arbol por su AutomationId (CanvasRoot, ZoomLevelText,
      ZoomInButton, ZoomOutButton, FitToScreenButton).
  S2. set_focus UIA sobre CanvasRoot ENTRA (el foco del lienzo sin puntero, hallazgo del 238).
  S3. Invoke de ZoomInButton/ZoomOutButton cambia y restaura el nivel leido en ZoomLevelText.
  S4. Shift+A con el foco en el lienzo abre el spotlight y Escape lo cierra (el canal de teclado
      del lienzo abierto por el 238), restaurando el estado.
  S5. El buscador del cajon escribe por teclado UIA-inyectado ('fold', la via de la Sonda B del
      237) y el texto queda en la caja, con restauracion por backspaces.

Veredicto por codigo de salida: 0 = verificado, 2 = algun sondeo fallo, 3 = la app nunca aparecio.
"""
import ctypes
import os
import sys
import time

u32 = ctypes.windll.user32
u32.SetProcessDPIAware()

VK_MAP = {c: 0x41 + (ord(c) - ord("A")) for c in "ABCDEFGHIJKLMNOPQRSTUVWXYZ"}
VK_MAP.update({str(d): 0x30 + d for d in range(10)})
VK_BACK = 0x08
VK_SHIFT = 0x10

ANCHORS = ["CanvasRoot", "ZoomLevelText", "ZoomInButton", "ZoomOutButton", "FitToScreenButton"]


def log(msg):
    print(msg, flush=True)


def key(vk, up=False):
    u32.keybd_event(vk, 0, 2 if up else 0, 0)


def type_text(text):
    for c in text:
        key(VK_MAP[c.upper()])
        time.sleep(0.03)
        key(VK_MAP[c.upper()], up=True)
        time.sleep(0.05)


def press_shift_a():
    key(VK_SHIFT)
    key(VK_MAP["A"])
    time.sleep(0.05)
    key(VK_MAP["A"], up=True)
    key(VK_SHIFT, up=True)


def press_escape():
    key(0x1B)
    time.sleep(0.05)
    key(0x1B, up=True)


def by_aid(root, aid):
    """pywinauto 0.6.9: descendants() no acepta automation_id (leccion del 237) - filtrar aqui."""
    for el in root.descendants():
        try:
            if (el.element_info.automation_id or "") == aid:
                return el
        except Exception:
            continue
    return None


def connect_to_app(pid, probe_timeout=60.0):
    """Conecta al proceso vivo y confirma la app por sus ANCLAS (la leccion A1 del 238: ni la
    ventana ni un Grid raiz sin peer materializan en el arbol UIA; la identidad honesta es
    CanvasRoot, que SI tiene peer). Reintenta hasta el timeout."""
    from pywinauto.application import Application

    deadline = time.time() + probe_timeout
    while time.time() < deadline:
        try:
            app = Application(backend="uia").connect(process=pid, timeout=1)
            win = app.top_window()
            if by_aid(win, "CanvasRoot") is not None:
                win.set_focus()
                time.sleep(1.0)
                return win
            log("[selfcheck-uia] el arbol todavia no expone CanvasRoot; reintento")
        except Exception as ex:
            log("[selfcheck-uia] reintento de conexion: %s" % type(ex).__name__)
        time.sleep(1.0)
    return None


def probe(win):
    results = []

    # S1. Las anclas del lienzo y la barra por su AutomationId.
    found = {aid: by_aid(win, aid) for aid in ANCHORS}
    missing = [aid for aid in ANCHORS if found[aid] is None]
    results.append(("S1_anclas_en_el_arbol", not missing,
                    "5/5" if not missing else "faltan: %s" % missing))

    # S2. El foco del lienzo entra por UIA.
    focus_ok = False
    focus_detail = "sin CanvasRoot"
    if found["CanvasRoot"] is not None:
        try:
            found["CanvasRoot"].set_focus()
            time.sleep(0.8)
            focus_ok = True
            focus_detail = "set_focus ejecutado sobre CanvasRoot"
        except Exception as ex:
            focus_detail = "set_focus fallo: %s: %s" % (type(ex).__name__, ex)
    results.append(("S2_foco_del_lienzo", focus_ok, focus_detail))

    # S3. El estado del zoom, observable y restaurado.
    zoom_ok = False
    zoom_detail = "sin anclas de la barra"
    try:
        before = found["ZoomLevelText"].window_text() or ""
        found["ZoomInButton"].invoke()
        time.sleep(1.0)
        mid = found["ZoomLevelText"].window_text() or ""
        found["ZoomOutButton"].invoke()
        time.sleep(1.0)
        after = found["ZoomLevelText"].window_text() or ""
        zoom_ok = (mid != before) and (after == before)
        zoom_detail = "nivel '%s' -> '%s' -> '%s'" % (before, mid, after)
    except Exception as ex:
        zoom_detail = "invoke fallo: %s: %s" % (type(ex).__name__, ex)
    results.append(("S3_zoom_observable", zoom_ok, zoom_detail))

    # S4. El atajo del lienzo con foco UIA: Shift+A abre el spotlight, Escape lo cierra.
    spotlight_ok = False
    spotlight_detail = "sin CanvasRoot"
    try:
        found["CanvasRoot"].set_focus()
        time.sleep(0.5)
        was_open = by_aid(win, "SpotlightSearchBox") is not None
        press_shift_a()
        time.sleep(1.5)
        now_open = by_aid(win, "SpotlightSearchBox") is not None
        opened = now_open and not was_open
        if opened:
            press_escape()
            time.sleep(1.0)
            closed = by_aid(win, "SpotlightSearchBox") is None
        else:
            closed = False
        spotlight_ok = opened and closed
        spotlight_detail = ("spotlight abierto y cerrado por Escape"
                            if spotlight_ok else
                            "abierto=%s cerrado=%s" % (opened, closed))
    except Exception as ex:
        spotlight_detail = "atajo fallo: %s: %s" % (type(ex).__name__, ex)
    results.append(("S4_atajo_del_lienzo", spotlight_ok, spotlight_detail))

    # S5. El buscador del cajon escribe por teclado UIA-inyectado, con restauracion.
    search_ok = False
    search_detail = "sin SearchBox"
    try:
        search = by_aid(win, "SearchBox")
        if search is not None:
            search.set_focus()
            time.sleep(0.5)
            type_text("fold")
            time.sleep(1.5)
            typed = (search.window_text() or "")
            for _ in range(4):
                key(VK_BACK)
                time.sleep(0.03)
                key(VK_BACK, up=True)
                time.sleep(0.05)
            time.sleep(1.0)
            restored = (search.window_text() or "")
            search_ok = "fold" in typed.lower() and restored.strip() == ""
            search_detail = "texto '%s' (restaurado '%s')" % (typed, restored)
        else:
            search_detail = "el buscador del cajon no esta en el arbol"
    except Exception as ex:
        search_detail = "buscador fallo: %s: %s" % (type(ex).__name__, ex)
    results.append(("S5_buscador_ui_inyectado", search_ok, search_detail))

    return results


def main():
    pid_env = os.environ.get("FILEFLOW_UIA_TARGET_PID", "")
    if not pid_env.isdigit():
        log("[selfcheck-uia] FALLO: sin FILEFLOW_UIA_TARGET_PID (el modo lo pasa la app)")
        return 3
    pid = int(pid_env)

    win = connect_to_app(pid)
    if win is None:
        log("[selfcheck-uia] FALLO: la app no aparecio por UIA en el tiempo previsto")
        return 3

    log("[selfcheck-uia] ventana ancla encontrada (pid %s)" % pid)
    results = probe(win)

    passed = 0
    log("[selfcheck-uia] RESULTADOS:")
    for name, ok, detail in results:
        passed += 1 if ok else 0
        log("[selfcheck-uia]   %s  %s: %s" % ("[OK]" if ok else "[FALLO]", name, detail))
    verdict = "VERIFICADO" if passed == len(results) else "FALLO (%d/%d)" % (passed, len(results))
    log("[selfcheck-uia] RESULTADO: %s" % verdict)
    return 0 if passed == len(results) else 2


if __name__ == "__main__":
    sys.exit(main())
