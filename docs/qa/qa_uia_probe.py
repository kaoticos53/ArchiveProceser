# -*- coding: utf-8 -*-
"""SONDA UIA — ¿la vía UI Automation entrega el gesto que la inyección de puntero no entrega?

Hito 231 midió el bloqueo irreductible: el input de puntero inyectado (mouse_event, SendInput,
PostMessage) NO llega al contenido de WinAppSDK/WinUI 3 sin UIAccess, y InjectTouchInput (WM_POINTER
real) está denegado (error 5). Pero UIA es OTRA via de input: el patron Invoke/Selection/DoDefaultAction
viaja por el PROVEEDOR de UIA del propio proceso de la app (por IPC del sistema, no por la cola de
input fisica), y las apps WinUI 3/WinAppSDK exponen su arbol UIA nativamente (la accesibilidad es
obligatoria en WinUI). Si el proveedor entrega la accion al contenido, el gesto "fisico" queda
ejecutable sin UIAccess.

Modos:
  --probe   arbol UIA de la app viva + INTENTO de seleccion por UIA en la primera tarjeta
  --cal     calibracion de coordenadas UIA (BoundingRectances) para los gestos por punto
"""
import ctypes
import json
import os
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
BIN = os.path.normpath(os.path.join(HERE, "..", "..", "FileFlow.App.Uno", "bin", "Debug", "net10.0-windows10.0.19041.0"))
EXE = os.path.join(BIN, "FileFlow.App.Uno.exe")
SHOTS = os.path.join(HERE, "qa-uia")
os.makedirs(SHOTS, exist_ok=True)

u32 = ctypes.windll.user32
u32.SetProcessDPIAware()


def log(msg):
    print(msg, flush=True)


def launch_app():
    proc = subprocess.Popen([EXE], cwd=BIN)
    time.sleep(6.0)
    return proc


def main():
    from pywinauto.application import Application
    from pywinauto import uia_element_info as uiainfo

    proc = launch_app()
    pid = proc.pid
    log(f"[app] pid={pid}")

    try:
        # Conectar por PID (UIA acona por ventana top-level del proceso)
        app = Application(backend="uia").connect(process=pid, timeout=10)
        mainwin = app.top_window()
        mainwin.set_focus()
        time.sleep(1.0)
        log(f"[uia] ventana top-level: '{mainwin.window_text()}' class={mainwin.class_name()}")

        # 1. El arbol UIA expuesto: contar tarjetas
        cards = mainwin.descendants(title="Folder Source")
        log(f"[uia] elementos 'Folder Source' en el arbol: {len(cards)}")

        all_text = mainwin.descendants(control_type="Text")
        log(f"[uia] total textos expuestos: {len(all_text)}")

        titles = []
        for t in all_text[:80]:
            try:
                titles.append(t.window_text())
            except Exception:
                pass
        log(f"[uia] muestra de textos: {titles[:14]}")

        # 2. La sonda: click UIA en la tarjeta (por el proveedor, no por la cola de input)
        target = cards[0] if cards else None
        if target is None:
            # buscar por nombre parcial en Text elements
            for t in all_text:
                try:
                    if "Folder" in (t.window_text() or ""):
                        target = t
                        break
                except Exception:
                    continue

        if target is None:
            log("[probe] NO HAY objetivo UIA: el arbol no expone las tarjetas")
            return 1

        log(f"[probe] objetivo: '{target.window_text()}' control_type={target.element_info.control_type}")

        # captura antes
        import numpy as np
        from PIL import ImageGrab

        # Estabilizar ANTES de la primera captura: la ventana recién lanzada sigue pintando
        # (arranque, materialización); sin esto, la primera comparación mide el arranque, no el gesto.
        time.sleep(2.0)
        desktop = (0, 0, u32.GetSystemMetrics(0), u32.GetSystemMetrics(1))
        before = np.array(ImageGrab.grab(bbox=desktop))

        # Dos capturas separadas sin gesto: el RUIDO del arranque/animaciones (si before==after con
        # la ventana quieta, el umbral de 30 mide gesto y no ruido).
        time.sleep(0.8)
        quiet = np.array(ImageGrab.grab(bbox=desktop))
        noise = np.abs(before.astype(int) - quiet.astype(int)).sum(axis=2)
        noise_px = int((noise > 30).sum())
        log(f"[probe] ruido base sin gesto (captura a captura): {noise_px} px")

        try:
            target.click_input()   # click por UIA (mouse_event por pywinauto) -> NO, esto es input fisico
            log("[probe] click_input ejecutado (via fisica)")
        except Exception as ex:
            log(f"[probe] click_input fallo: {type(ex).__name__}: {ex}")

        time.sleep(1.2)
        after = np.array(ImageGrab.grab(bbox=desktop))

        diff = np.abs(before.astype(int) - after.astype(int)).sum(axis=2)
        changed = int((diff > 30).sum())
        log(f"[probe] pixeles cambiados tras click_input (via fisica): {changed} (ruido base {noise_px})")

        # 3. La via UIA PURA: Invoke/Selection pattern si existe
        try:
            import comtypes.gen.UIAutomationClient as UIAClient
        except Exception:
            UIAClient = None

        try:
            el = target.element_info.element  # IUIAutomationElement comtypes
            patterns = []
            # UIA_SelectionItemPattern
            if UIAClient is not None:
                for pid_, pname in [
                    (UIAClient.UIA_SelectionItemPatternId, "SelectionItem"),
                    (UIAClient.UIA_InvokePatternId, "Invoke"),
                    (UIAClient.UIA_LegacyIAccessiblePatternId, "LegacyIAccessible"),
                ]:
                    try:
                        ptr = el.GetCurrentPattern(pid_)
                        if ptr:
                            patterns.append(pname)
                    except Exception:
                        pass
            log(f"[probe] patrones UIA del objetivo: {patterns}")

            if "SelectionItem" in patterns:
                ptr = el.GetCurrentPattern(UIAClient.UIA_SelectionItemPatternId)
                sel = ptr.QueryInterface(UIAClient.IUIAutomationSelectionItemPattern)
                sel.Select()
                log("[probe] SelectionItem.Select() ENVIADO (via proveedor UIA)")
                time.sleep(1.2)
                after2 = np.array(ImageGrab.grab(bbox=desktop))
                diff2 = np.abs(before.astype(int) - after2.astype(int)).sum(axis=2)
                changed2 = int((diff2 > 30).sum())
                log(f"[probe] pixeles cambiados tras SelectionItem.Select(): {changed2}")
                verdict = "GO" if changed2 > 500 else "NO-GO"
                log(f"[probe] VEREDICTO SONDA: {verdict} (cambio de {changed2} px)")
                return 0 if verdict == "GO" else 2
        except Exception as ex:
            log(f"[probe] via patterns fallo: {type(ex).__name__}: {ex}")

        log("[probe] VEREDICTO SONDA: NO-GO (sin patron accionable)")
        return 2
    finally:
        try:
            proc.terminate()
        except Exception:
            pass


if __name__ == "__main__":
    sys.exit(main())
