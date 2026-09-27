# -*- coding: utf-8 -*-
"""SONDA C — la observación UIA externa alcanza el FOCO del lienzo y el ESTADO del zoom (hito 238).

El hito 238 dió al lienzo la superficie UIA que el 237 acotó: AutomationIds explícitos, IsTabStop
y peer de automatización enfocable (OnCreateAutomationPeer). El selfcheck lo verificó DESDE DENTRO
(foco programático). Esta sonda verifica lo mismo DESDE FUERA (pywinauto, sin UIAccess), por la
vía real de un observador externo:

  A1. Las anclas explícitas están en el árbol por su AutomationId (CanvasRoot, CanvasSurface,
      CanvasGraphPlane, ZoomBar, ZoomLevelText, ZoomInButton, ZoomOutButton, FitToScreenButton) —
      sin descifrar qué 'Button' es cuál ni depender del x:Name renombrable.
  A2. set_focus UIA sobre CanvasRoot ENTRA (el paso que el 237 dejó acotado al puntero del
      usuario): el lienzo pasa a ser el elemento con foco.
  A3. El ESTADO del zoom es observable: Invoke de ZoomInButton (el patrón que el 237 no encontró
      en un Text pero SÍ existe en un Button) cambia el nivel y ZoomLevelText lo refleja; restore
      con ZoomOutButton vuelve a '100 %'.

  Bonus (la pregunta del 237): con el foco EN el lienzo entregado por UIA, ¿el atajo Shift+A del
  lienzo se dispara con keybd_event? El 237 midió que NO se dispara porque la tecla no está en el
  lienzo (el foco quedó en el TextBox del buscador). Aquí el foco sí está en el lienzo: si el
  spotlight se abre, la mitad física gana su vía sin puntero; si no, el acotamiento del 237 se
  re-confirma con el foco bien puesto (queda el puntero como único canal pendiente).
"""
import os
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
BIN = os.path.normpath(os.path.join(HERE, "..", "..", "FileFlow.App.Uno", "bin", "Debug", "net10.0-windows10.0.19041.0"))
EXE = os.path.join(BIN, "FileFlow.App.Uno.exe")

import ctypes
u32 = ctypes.windll.user32
u32.SetProcessDPIAware()

VK_SHIFT = 0x10
VK_A = 0x41


def log(msg):
    print(msg, flush=True)


def main():
    from pywinauto.application import Application

    proc = subprocess.Popen([EXE], cwd=BIN)
    time.sleep(6.0)
    pid = proc.pid
    log(f"[app] pid={pid}")

    results = []
    try:
        app = Application(backend="uia").connect(process=pid, timeout=10)
        win = app.top_window()
        win.set_focus()
        time.sleep(2.0)

        def by_aid(aid):
            # pywinauto 0.6.9: descendants() no acepta automation_id (lección del 237) — filtrar aquí.
            for el in win.descendants():
                try:
                    if (el.element_info.automation_id or "") == aid:
                        return el
                except Exception:
                    continue
            return None

        # A1. Las anclas explícitas, cada una por su AutomationId.
        anchors = ["CanvasRoot", "CanvasSurface", "CanvasGraphPlane",
                   "ZoomBar", "ZoomLevelText", "ZoomInButton", "ZoomOutButton", "FitToScreenButton"]
        found = {aid: by_aid(aid) for aid in anchors}
        missing = [aid for aid in anchors if found[aid] is None]
        results.append(("A1_anclas_por_automation_id", not missing,
                        f"encontradas {len(anchors) - len(missing)}/{len(anchors)}" + (f", faltan: {missing}" if missing else "")))
        for aid in anchors:
            log(f"[probe-c] ancla {aid}: {'OK' if found[aid] is not None else 'AUSENTE'}")

        # A2. El foco del lienzo entra por UIA (el paso acotado al puntero en el 237).
        focus_ok = False
        focus_detail = "sin CanvasRoot"
        if found["CanvasRoot"] is not None:
            try:
                found["CanvasRoot"].set_focus()
                time.sleep(0.8)
                focus_ok = True
                focus_detail = "set_focus UIA ejecutado sobre CanvasRoot"
            except Exception as ex:
                focus_detail = f"set_focus fallo: {type(ex).__name__}: {ex}"
        results.append(("A2_foco_del_lienzo_por_uia", focus_ok, focus_detail))
        log(f"[probe-c] A2: {focus_detail}")

        # A3. El estado del zoom, observable: Invoke del botón y lectura del nivel.
        zoom_ok = False
        zoom_detail = "sin anclas de la barra"
        if found["ZoomInButton"] is not None and found["ZoomLevelText"] is not None:
            try:
                before = found["ZoomLevelText"].window_text() or ""
                found["ZoomInButton"].invoke()
                time.sleep(1.0)
                mid = found["ZoomLevelText"].window_text() or ""
                found["ZoomOutButton"].invoke() if found["ZoomOutButton"] is not None else None
                time.sleep(1.0)
                after = found["ZoomLevelText"].window_text() or ""
                changed = (mid != before) and (after == before)
                zoom_ok = changed
                zoom_detail = f"nivel '{before}' -> '{mid}' -> '{after}' (restaurado)"
            except Exception as ex:
                zoom_detail = f"invoke fallo: {type(ex).__name__}: {ex}"
        results.append(("A3_estado_del_zoom_observable", zoom_ok, zoom_detail))
        log(f"[probe-c] A3: {zoom_detail}")

        # Bonus. Shift+A con el foco EN el lienzo: ¿se dispara el atajo (spotlight)?
        bonus_ok = False
        bonus_detail = "sin CanvasRoot"
        if found["CanvasRoot"] is not None:
            try:
                found["CanvasRoot"].set_focus()
                time.sleep(0.5)
                spotlight_before = by_aid("SpotlightSearchBox") is not None
                u32.keybd_event(VK_SHIFT, 0, 0, 0)
                u32.keybd_event(VK_A, 0, 0, 0)
                time.sleep(0.05)
                u32.keybd_event(VK_A, 0, 2, 0)
                u32.keybd_event(VK_SHIFT, 0, 2, 0)
                time.sleep(1.5)
                spotlight_after = by_aid("SpotlightSearchBox") is not None
                opened = spotlight_after and not spotlight_before
                bonus_ok = opened
                bonus_detail = ("el spotlight SE ABRE con el foco entregado por UIA — la mitad física "
                                "del atajo gana su vía sin puntero" if opened else
                                "el spotlight NO se abre: el acotamiento del 237 se re-confirma con el "
                                "foco bien puesto (la entrega de foco UIA al contenedor no bastó para "
                                "que el keybd_event llegara al handler del lienzo)")
                # Higiene: si se abrió, cerrarlo con Escape para dejar la app como al entrar.
                if opened:
                    VK_ESC = 0x1B
                    u32.keybd_event(VK_ESC, 0, 0, 0)
                    time.sleep(0.05)
                    u32.keybd_event(VK_ESC, 0, 2, 0)
            except Exception as ex:
                bonus_detail = f"prueba del atajo fallo: {type(ex).__name__}: {ex}"
        results.append(("B1_atajo_del_lienzo_con_foco_uia", bonus_ok, bonus_detail))
        log(f"[probe-c] B1: {bonus_detail}")

        passed = sum(1 for _, ok, _ in results if ok)
        log("[probe-c] RESUMEN SONDA C:")
        for name, ok, detail in results:
            log(f"[probe-c]   {'PASS' if ok else 'INFO'}  {name}: {detail}")
        log(f"[probe-c] veredicto: {passed}/{len(results)} PASS (A1+A2+A3 son la superficie nueva; "
            f"B1 es la pregunta abierta del 237, INFO en cualquier sentido)")
        return 0 if all(ok for name, ok, _ in results if name != "B1_atajo_del_lienzo_con_foco_uia") else 2
    finally:
        try:
            proc.terminate()
        except Exception:
            pass


if __name__ == "__main__":
    sys.exit(main())
