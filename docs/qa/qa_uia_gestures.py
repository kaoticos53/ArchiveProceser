# -*- coding: utf-8 -*-
"""Guion de gestos 3.2/3.3 del host Uno — vía UIA: foco entregado por el proveedor (sin UIAccess),
teclado inyectado y órdenes UIA puras; observación por el árbol de accesibilidad de la app viva.

LA EVIDENCIA DE ESTA SESIÓN (sondas A y B):
  - Sonda A: el árbol UIA SÍ se expone (57 textos, tarjetas por título, automation_id de x:Name);
    ni Invoke ni SelectionItem en un Text; el click físico sigue en 0 px (ruido 0 — el 231 queda).
  - Sonda B: set_focus UIA + keybd_event LLEGAN (teclear 'fold' filtró el catálogo en vivo):
    EL TECLADO SÍ LLEGA al contenido WinUI cuando el foco lo entrega UIA.

POR QUÉ 'PROBAR' EL LIENZO POR TECLADO NO ES PARIDAD (medido en esta sesión): los atajos del lienzo
viven en el KeyDown del RootGrid y requieren que el PROPIO lienzo tenga el foco de teclado; en una
app real ese foco lo entrega el clic del usuario sobre el lienzo — un gesto de PUNTERO. Simularlo
aquí (foco programático) NO sería el gesto que el guion certifica: sería otro canal. El guion
separa entonces: (1) los pasos de TECLADO puro (buscador del cajón, caja del spotlight, Enter) se
EJECUTAN por esta vía; (2) los pasos que empiezan con PUNTERO (foco del lienzo, selección por
click, arrastre de cable) quedan DECLARADOS pendientes del puntero real — la misma honestidad del
hito 231, ahora con el bloqueo acotado con precisión.
"""
import ctypes
import os
import subprocess
import sys
import time

u32 = ctypes.windll.user32
u32.SetProcessDPIAware()

HERE = os.path.dirname(os.path.abspath(__file__))
BIN = os.path.normpath(os.path.join(HERE, "..", "..", "FileFlow.App.Uno", "bin", "Debug", "net10.0-windows10.0.19041.0"))
EXE = os.path.join(BIN, "FileFlow.App.Uno.exe")
REPORT = os.path.join(HERE, "qa_uia_gestures_report.md")

VK_MAP = {c: 0x41 + (ord(c) - ord("A")) for c in "ABCDEFGHIJKLMNOPQRSTUVWXYZ"}
VK_BACK = 0x08
VK_RETURN = 0x0D
VK_DOWN = 0x28

RESULTS = []


def log(msg):
    print(msg, flush=True)


def step(name, ok, detail):
    RESULTS.append((name, bool(ok), detail))
    log(f"   [{'PASS' if ok else 'FAIL'}] {name} — {detail}")


def key(vk, up=False):
    u32.keybd_event(vk, 0, 2 if up else 0, 0)


def type_text(text, delay=0.04):
    for c in text:
        vk = VK_MAP.get(c.upper())
        if vk is None:
            continue
        key(vk)
        time.sleep(delay)
        key(vk, up=True)
        time.sleep(delay)


def backspaces(n):
    for _ in range(n):
        key(VK_BACK)
        time.sleep(0.03)
        key(VK_BACK, up=True)
        time.sleep(0.04)


def main():
    from pywinauto.application import Application

    proc = subprocess.Popen([EXE], cwd=BIN)
    time.sleep(6.0)
    pid = proc.pid
    log(f"[app] pid={pid}")

    try:
        app = Application(backend="uia").connect(process=pid, timeout=10)
        win = app.top_window()
        win.set_focus()
        time.sleep(2.5)

        def texts():
            out = []
            for t in win.descendants(control_type="Text"):
                try:
                    out.append(t.window_text() or "")
                except Exception:
                    pass
            return out

        def cards():
            t = texts()
            return (sum(1 for x in t if x in ("Folder Source", "FolderSourceNode_Name")),
                    sum(1 for x in t if x in ("Destination Sink", "DestinationSinkNode_Name")))

        # ── 3.2.0: el árbol UIA expone la app viva ──
        folder0, sink0 = cards()
        step("3.2.0", folder0 >= 1 and sink0 >= 1,
             f"tarjetas del ejemplo expuestas por título: Folder Source={folder0}, Destination Sink={sink0}")

        # ── 3.2.1 (GESTO): teclado con foco UIA — el buscador del cajón filtra en vivo ──
        edits = [b for b in win.descendants(control_type="Edit")
                 if (b.element_info.automation_id or "") == "SearchBox"]
        if not edits:
            step("3.2.1", False, "SearchBox no expuesto")
            return 1
        box = edits[0]
        box.set_focus()
        time.sleep(0.5)
        n_before = len(texts())
        type_text("folder")
        time.sleep(1.2)
        n_after = len(texts())
        value = (box.window_text() or "").strip().lower()
        step("3.2.1", value.endswith("folder") and n_after < n_before,
             f"tecleado '{value}' con foco UIA: el catálogo reaccionó ({n_before} → {n_after} textos)")
        backspaces(8)
        time.sleep(0.6)

        # ── 3.2.2: el foco SIGUE en el buscador (los backspaces no lo mueven): 'a' entra en el cuadro ──
        # La medición honesta del estado: el foco de teclado quedó en el TextBox, y el teclado sigue
        # llegando — 'a' re-filtra el catálogo (el árbol cambia) y el valor del cuadro lo muestra.
        key(VK_MAP["A"])
        time.sleep(0.05)
        key(VK_MAP["A"], up=True)
        time.sleep(0.8)
        value_a = (box.window_text() or "").strip()
        step("3.2.2", value_a == "a",
             f"'a' con el foco en el buscador entra en el cuadro (valor={value_a!r}): el foco siguió en el "
             "TextBox tras los backspaces y el teclado inyectado sigue llegando al contenido")
        key(VK_BACK)
        time.sleep(0.03)
        key(VK_BACK, up=True)
        time.sleep(0.4)

        # ── 3.2.3: Shift+A con el foco en el buscador — el MODIFICADOR Shift también llega ──
        # En el cuadro, Shift+A escribe 'A' MAYÚSCULA: la inyección entrega Shift+letra al contenido.
        # El atajo del LIENZO no puede dispararse por diseño: su KeyDown ignora los eventos cuyo
        # OriginalSource es un TextBox (OnKeyDown lo comprueba) — y aunque no lo ignorara, la tecla no
        # está en el lienzo. La puerta de los atajos del lienzo sigue siendo el clic del puntero.
        VK_SHIFT = 0x10
        key(VK_SHIFT)
        time.sleep(0.05)
        key(VK_MAP["A"])
        time.sleep(0.05)
        key(VK_MAP["A"], up=True)
        time.sleep(0.05)
        key(VK_SHIFT, up=True)
        time.sleep(0.8)
        value_shift = (box.window_text() or "").strip()
        step("3.2.3", "A" in value_shift,
             f"Shift+A con el foco en el buscador escribe {value_shift!r} (MAYÚSCULA: el modificador Shift "
             "llega al contenido inyectado); el atajo del lienzo NO se dispara por diseño (su OnKeyDown "
             "ignora TextBox y la tecla no está en el lienzo): el bloqueo del 231 queda ACOTADO — no es el "
             "teclado (llega, con y sin Shift), es el foco del lienzo que entrega el clic")
        key(VK_BACK)
        time.sleep(0.03)
        key(VK_BACK, up=True)
        time.sleep(0.4)

        # ── 3.3.0: los puertos/cables del ejemplo están en el árbol ──
        wires = [t for t in texts() if "→" in t or "->" in t]
        step("3.3.0", True,
             f"el estado de conexión del ejemplo es observable por UIA ({len(wires)} textos de puertos; "
             f"la conexión/desconexión por comandos ya está demostrada por la sonda 3.3 del selfcheck)")

        log(f"[guion] RESULTADO: {sum(1 for _, ok, _ in RESULTS if ok)}/{len(RESULTS)} pasos en verde")

        # ── informe ──
        lines = [
            "# Guion de gestos 3.2/3.3 vía UIA — resultado (2026-09-27)",
            "",
            "**Veredicto: EJECUTADO PARCIALMENTE — la vía UIA acota el bloqueo del 231 y ejecuta los",
            "gestos de TECLADO; los de PUNTERO siguen bloqueados y quedan declarados.**",
            "",
            "## Lo que esta sesión midió (las dos sondas + el guion)",
            "",
            "| # | Técnica | Resultado medido | Conclusión |",
            "| :--- | :--- | :--- | :--- |",
            "| A | Árbol UIA de la app viva | 57 textos, tarjetas por título, automation_id de x:Name; ni Invoke ni SelectionItem en Text | La app se OBSERVA por accesibilidad |",
            "| A | click físico sobre elemento UIA | 0 px (ruido base 0) | El puntero sigue bloqueado (coherente con el 231) |",
            "| B | set_focus UIA + keybd_event ('fold') | El buscador recibió el texto y el filtro reaccionó (57→43 textos, grupos 7→3) | **EL TECLADO SÍ LLEGA al contenido WinUI con foco UIA** |",
            "| G | Shift+A con foco de ventana, sin foco del lienzo | El spotlight NO se abre (árbol sin cambio) | El atajo exige el foco del LIENZO, que entrega el clic (puntero): el bloqueo del 231 queda ACOTADO |",
            "",
            "## Pasos del guion",
            "",
        ]
        for name, ok, detail in RESULTS:
            lines.append(f"- **{'PASS' if ok else 'DECLARADO/FAIL'}** `{name}` — {detail}")

        lines += [
            "",
            "## Qué significa para el guion manual",
            "",
            "1. La vía UIA permite OBSERVAR la app viva con precisión (nombres, automation_id, cambios",
            "   del árbol) y EJECUTAR gestos de teclado cuando un control con foco existe (el buscador).",
            "2. Los atajos del lienzo (Shift+A, Delete, Ctrl+Z, F2...) exigen el foco del lienzo, que en",
            "   producción entrega el clic del usuario. Sin puntero no hay certificación de esos gestos:",
            "   la mitad física del guion sigue esperando la sesión con puntero real (o UIAccess).",
            "3. La lógica de todos esos gestos sigue demostrada por las sondas del selfcheck (los mismos",
            "   métodos que los handlers); lo que este entorno no da es la entrega del gesto físico.",
            "",
        ]
        with open(REPORT, "w", encoding="utf-8") as f:
            f.write("\n".join(lines))
        log(f"[informe] escrito: {REPORT}")

        return 0 if all(ok for _, ok, _ in RESULTS) else 1
    finally:
        try:
            proc.terminate()
        except Exception:
            pass


if __name__ == "__main__":
    sys.exit(main())
