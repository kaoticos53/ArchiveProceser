# -*- coding: utf-8 -*-
"""SONDA B — ¿el teclado inyectado llega al contenido WinUI cuando el foco está bien puesto?

El hito 231 midió que Shift+A no ejecutó el spotlight, pero el foco del lienzo exigía puntero
(bloqueado): la tecla quizá SÍ llegaba a un contenido que nunca tuvo foco. La rebanada 4 trajo
un TextBox (el buscador del cajón): si UIA puede darle foco (SetFocus por el proveedor del propio
proceso — no es input físico) y las teclas inyectadas por keybd_event alteran SearchText, la
respuesta del filtro es observable por UIA (los grupos del cajón cambian) SIN tocar puntero.

Tres medidas:
  1. SetFocus UIA al TextBox del buscador (vía proveedor; sin UIAccess).
  2. keybd_event de 'f','o','l','d' (inyección de teclado — la vía que el 231 demostró que SÍ llega
     al sistema: NumLock cambió de estado).
  3. Lectura UIA del arbol: los grupos del cajón antes/después (si el filtro reacciona, el teclado
     inyectado LLEGA al contenido WinUI con foco bien puesto — y el guion 3.2 gana su vía).
"""
import os
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
BIN = os.path.normpath(os.path.join(HERE, "..", "..", "FileFlow.App.Uno", "bin", "Debug", "net10.0-windows10.0.19041.0"))
EXE = os.path.join(BIN, "FileFlow.App.Uno.exe")

u32 = None
import ctypes
u32 = ctypes.windll.user32
u32.SetProcessDPIAware()

VK_MAP = {c: 0x41 + (ord(c) - ord('A')) for c in "ABCDEFGHIJKLMNOPQRSTUVWXYZ"}
VK_MAP.update({str(d): 0x30 + d for d in range(10)})


def log(msg):
    print(msg, flush=True)


def key(char, up=False):
    vk = VK_MAP[char.upper()]
    u32.keybd_event(vk, 0, 2 if up else 0, 0)


def type_text(text):
    for c in text:
        key(c)
        time.sleep(0.03)
        key(c, up=True)
        time.sleep(0.05)


def main():
    from pywinauto.application import Application

    proc = subprocess.Popen([EXE], cwd=BIN)
    time.sleep(6.0)
    pid = proc.pid
    log(f"[app] pid={pid}")

    verdict = "NO-GO"
    try:
        app = Application(backend="uia").connect(process=pid, timeout=10)
        win = app.top_window()
        win.set_focus()
        time.sleep(2.0)

        # 1. El buscador del cajon (rebanada 4) por su AutomationId o por el TextBox
        boxes = win.descendants(control_type="Edit")
        log(f"[probe-b] TextBox (Edit) expuestos: {len(boxes)}")
        search = None
        for b in boxes:
            try:
                aid = b.element_info.automation_id or ""
                name = b.window_text() or ""
                if "Search" in aid or "Search" in name or "Buscar" in name:
                    search = b
                    break
            except Exception:
                continue
        if search is None and boxes:
            # el buscador es el primer Edit del arbol (el cajon es la primera columna)
            search = boxes[0]
            log(f"[probe-b] sin AutomationId 'Search': usando el primer Edit ({search.element_info.automation_id!r})")

        if search is None:
            log("[probe-b] sin TextBox en el arbol: no hay via de teclado observable")
            return 2

        # 2. Estado inicial de los grupos del cajon
        def group_names():
            names = []
            for t in win.descendants(control_type="Text"):
                try:
                    v = t.window_text() or ""
                    if v in ("⭐ Favoritos", "🔥 Más Usados") or v in ("Archives", "AudioVoice", "Data", "ImageVision"):
                        names.append(v)
                except Exception:
                    pass
            return names

        groups_before = group_names()
        items_before = len(win.descendants(control_type="Text"))
        log(f"[probe-b] grupos antes: {groups_before} | textos: {items_before}")

        # 3. SetFocus UIA (por el proveedor: no es input fisico ni exige UIAccess)
        try:
            search.set_focus()
            log("[probe-b] set_focus UIA ejecutado")
        except Exception as ex:
            log(f"[probe-b] set_focus fallo: {type(ex).__name__}: {ex}")

        time.sleep(0.5)

        # 4. Teclas inyectadas (keybd_event: la via que SI llega al sistema)
        type_text("fold")
        time.sleep(1.5)

        # 5. Lectura UIA despues
        search_value = ""
        try:
            search_value = search.window_text() or ""
        except Exception:
            pass
        groups_after = group_names()
        items_after = len(win.descendants(control_type="Text"))

        log(f"[probe-b] valor del buscador tras teclear: {search_value!r}")
        log(f"[probe-b] grupos despues: {groups_after} | textos: {items_after}")

        if search_value.strip().lower().endswith("fold") or "fold" in search_value.lower():
            verdict = "GO: el teclado inyectado LLEGA al contenido WinUI con foco UIA (filtro observable)"
        elif groups_after != groups_before or items_after != items_before:
            verdict = "GO parcial: el arbol cambio (el teclado llego) aunque no se lea el valor del TextBox"
        else:
            verdict = "NO-GO: ni el valor ni el arbol cambiaron (el teclado no llega al contenido)"

        log(f"[probe-b] VEREDICTO SONDA B: {verdict}")
        return 0 if verdict.startswith("GO") else 2
    finally:
        try:
            proc.terminate()
        except Exception:
            pass


if __name__ == "__main__":
    sys.exit(main())
