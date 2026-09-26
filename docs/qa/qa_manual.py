# -*- coding: utf-8 -*-
"""Guion manual de interacciones (fases 3.2/3.3 del plan Uno) sobre la app VIVA del host Uno.

Metodo: puntero y teclado REALES inyectados (SetCursorPos/mouse_event/keybd_event) + evidencia por
capturas de PANTALLA (ImageGrab; BitBlt por ventana devuelve negro en este WinUI flip-model) medidas
con PIL/numpy. No toca codigo del producto: es el instrumento de la sesion de QA que el plan reserva
a 'sesion con puntero real'.

Segmentacion de tarjetas: la BARRA DE ACENTO llena filas horizontales completas (~500 px por tarjeta);
los cables (mismo color #818CF8) son finos y nunca llenan una fila. Se detectan bandas de filas con
mucho acento y dentro de cada banda, los tramos de columnas = tarjetas.

Modos:
  --calibrate   lanza la app, maximiza, captura y calibra tarjetas/sockets (guarda calib.json)
  --run         ejecuta el guion completo con los datos de calib.json y escribe qa-manual-report.md
"""
import ctypes
import json
import os
import subprocess
import sys
import time
from ctypes import wintypes

import numpy as np
from PIL import ImageGrab

u32 = ctypes.windll.user32
u32.SetProcessDPIAware()

HERE = os.path.dirname(os.path.abspath(__file__))
EXE = os.path.join(HERE, "FileFlow.App.Uno.exe")
CALIB = os.path.join(HERE, "calib.json")
REPORT = os.path.join(HERE, "qa-manual-report.md")
SHOTS = os.path.join(HERE, "qa-manual")
os.makedirs(SHOTS, exist_ok=True)

ACCENT = (129, 140, 248)   # #818CF8: barra de acento, glow/borde de seleccion, cable
BG = (16, 19, 27)          # #10131B fondo del lienzo
GRID = (33, 38, 45)        # #21262D
CARD_BODY = 460            # alto fisico del cuerpo de tarjeta bajo la barra (2.5x de ~172 log.)

VK_SHIFT = 0x10
VK_CTRL = 0x11


def log(msg):
    print(msg, flush=True)


class RECT(ctypes.Structure):
    _fields_ = [("left", ctypes.c_long), ("top", ctypes.c_long),
                ("right", ctypes.c_long), ("bottom", ctypes.c_long)]


def find_main_window(pid):
    res = []

    def cb(h, l):
        buf = ctypes.create_unicode_buffer(256)
        u32.GetWindowTextW(h, buf, 256)
        p = wintypes.DWORD()
        u32.GetWindowThreadProcessId(h, ctypes.byref(p))
        if p.value == pid and u32.IsWindowVisible(h) and buf.value.strip():
            r = RECT()
            u32.GetWindowRect(h, ctypes.byref(r))
            res.append((h, (r.right - r.left) * (r.bottom - r.top)))
        return True

    CMPFUNC = ctypes.WINFUNCTYPE(ctypes.c_bool, wintypes.HWND, wintypes.LPARAM)
    u32.EnumWindows(CMPFUNC(cb), 0)
    res.sort(key=lambda t: -t[1])
    return res[0][0] if res else None


def win_rect(hwnd):
    r = RECT()
    u32.GetWindowRect(hwnd, ctypes.byref(r))
    return r.left, r.top, r.right, r.bottom


def foreground(hwnd):
    u32.keybd_event(0x12, 0, 0, 0)
    u32.keybd_event(0x12, 0, 2, 0)
    time.sleep(0.1)
    u32.ShowWindow(hwnd, 3)
    u32.SetForegroundWindow(hwnd)
    time.sleep(1.2)


def shot(hwnd, name=None):
    l, t, r, b = win_rect(hwnd)
    img = ImageGrab.grab(bbox=(l, t, r, b))
    if name:
        img.save(os.path.join(SHOTS, name))
    return img


def move(x, y):
    u32.SetCursorPos(int(x), int(y))
    time.sleep(0.03)


def click(x, y, right=False):
    move(x, y)
    time.sleep(0.08)
    down, up = (8, 16) if right else (2, 4)
    u32.mouse_event(down, 0, 0, 0, 0)
    time.sleep(0.05)
    u32.mouse_event(up, 0, 0, 0, 0)
    time.sleep(0.35)


def drag(x1, y1, x2, y2, steps=28):
    move(x1, y1)
    time.sleep(0.12)
    u32.mouse_event(2, 0, 0, 0, 0)
    time.sleep(0.12)
    for i in range(1, steps + 1):
        move(x1 + (x2 - x1) * i / steps, y1 + (y2 - y1) * i / steps)
        time.sleep(0.02)
    time.sleep(0.15)
    u32.mouse_event(4, 0, 0, 0, 0)
    time.sleep(0.4)


def key(mod, vk):
    if mod is not None:
        u32.keybd_event(mod, 0, 0, 0)
    u32.keybd_event(vk, 0, 0, 0)
    time.sleep(0.04)
    u32.keybd_event(vk, 0, 2, 0)
    if mod is not None:
        u32.keybd_event(mod, 0, 2, 0)
    time.sleep(0.35)


def type_text(s):
    for ch in s:
        vk = u32.VkKeyScanW(ord(ch)) & 0xFF
        u32.keybd_event(vk, 0, 0, 0)
        time.sleep(0.02)
        u32.keybd_event(vk, 0, 2, 0)
        time.sleep(0.03)
    time.sleep(0.3)


def accent_mask(img, tol=28):
    a = np.asarray(img).astype(int)
    return ((np.abs(a[:, :, 0] - ACCENT[0]) < tol)
            & (np.abs(a[:, :, 1] - ACCENT[1]) < tol)
            & (np.abs(a[:, :, 2] - ACCENT[2]) < tol))


def accent_groups(img, tol=28):
    """Una 'tarjeta' = banda de filas con mucho acento (la barra) x tramo de columnas ancho.
    count = acento en la region del cuerpo (barra + borde/glow si esta seleccionada)."""
    mask = accent_mask(img, tol)
    h, w = mask.shape
    row_counts = mask.sum(axis=1)
    bar_rows = np.where(row_counts > 300)[0]
    cards = []
    if len(bar_rows) == 0:
        return cards, mask
    # bandas contiguas (hueco <= 4 filas); puede haber varias si una tarjeta se movio en Y
    bands = []
    start = prev = bar_rows[0]
    for r in bar_rows[1:]:
        if r - prev <= 4:
            prev = r
            continue
        bands.append((start, prev))
        start = prev = r
    bands.append((start, prev))
    for y0, y1 in bands:
        sub = mask[y0:y1 + 1, :]
        col_counts = sub.sum(axis=0)
        cols = np.where(col_counts > 3)[0]
        if len(cols) == 0:
            continue
        spans = []
        s0 = p0 = cols[0]
        for c in cols[1:]:
            if c - p0 > 30:
                spans.append((s0, p0))
                s0 = p0 = c
            p0 = c
        spans.append((s0, p0))
        for x0, x1 in spans:
            if x1 - x0 < 100:
                continue  # ruido: cables que cruzan la banda
            y_end = min(y0 + CARD_BODY, h)
            count = int(mask[y0:y_end, x0:x1 + 1].sum())
            cards.append({"x0": int(x0), "x1": int(x1), "y0": int(y0), "y1": int(y1),
                          "count": count})
    return cards, mask


def cable_pixels_in(img, box, tol=30):
    a = np.asarray(img.crop(box)).astype(int)
    mask = ((np.abs(a[:, :, 0] - ACCENT[0]) < tol)
            & (np.abs(a[:, :, 1] - ACCENT[1]) < tol)
            & (np.abs(a[:, :, 2] - ACCENT[2]) < tol))
    return int(mask.sum())


def launch():
    proc = subprocess.Popen([EXE], cwd=HERE)
    hwnd = None
    for _ in range(60):
        time.sleep(1)
        hwnd = find_main_window(proc.pid)
        if hwnd:
            time.sleep(3)
            break
    if not hwnd:
        raise RuntimeError("no aparecio la ventana")
    foreground(hwnd)
    time.sleep(2)
    return proc, hwnd


def socket_scan(card, img):
    """Localiza el hueco de socket a izquierda (In) y derecha (Out) en la fila de la barra."""
    a = np.asarray(img).astype(int)
    y = card["y0"] + 2
    row = a[y]
    sock_in = None
    sock_out = None
    for x in range(card["x0"] - 1, max(card["x0"] - 90, 1), -1):
        px = tuple(row[x])
        pxl = tuple(row[x - 1])
        is_bg = abs(px[0] - BG[0]) < 8 and abs(px[1] - BG[1]) < 8 and abs(px[2] - BG[2]) < 8
        is_grid = abs(px[0] - GRID[0]) < 10 and abs(px[1] - GRID[1]) < 10 and abs(px[2] - GRID[2]) < 10
        if px != pxl and not is_bg and not is_grid:
            sock_in = x - 2
            break
    for x in range(card["x1"] + 1, min(card["x1"] + 90, img.width - 1)):
        px = tuple(row[x])
        pxr = tuple(row[x + 1])
        is_bg = abs(px[0] - BG[0]) < 8 and abs(px[1] - BG[1]) < 8 and abs(px[2] - BG[2]) < 8
        is_grid = abs(px[0] - GRID[0]) < 10 and abs(px[1] - GRID[1]) < 10 and abs(px[2] - GRID[2]) < 10
        if px != pxr and not is_bg and not is_grid:
            sock_out = x + 2
            break
    return sock_in, sock_out


def calibrate():
    log("== calibracion ==")
    proc, hwnd = launch()
    img = shot(hwnd, "calib.png")
    l, t, r, b = win_rect(hwnd)
    log("ventana: %d,%d %dx%d" % (l, t, r - l, b - t))
    groups, _ = accent_groups(img)
    log("tarjetas detectadas: %d" % len(groups))
    cards = []
    for g in groups:
        sock_in, sock_out = socket_scan(g, img)
        cards.append(dict(g, sock_in=sock_in, sock_out=sock_out,
                          center_x=(g["x0"] + g["x1"]) // 2))
        log("  tarjeta x[%d..%d] y=%d: sock_in=%s sock_out=%s count=%d"
            % (g["x0"], g["x1"], g["y0"], sock_in, sock_out, g["count"]))
    json.dump({"cards": cards, "win": [l, t, r, b]}, open(CALIB, "w"))
    log("calib.json escrito")
    try:
        proc.terminate()
    except Exception:
        pass


def run():
    proc, hwnd = launch()
    results = []

    def rec(step, ok, detail):
        results.append((step, ok, detail))
        log("[%s] %s :: %s" % ("PASS" if ok else "FAIL", step, detail))

    cal = json.load(open(CALIB))
    cards = cal["cards"]
    if len(cards) != 3:
        log("FALLO de calibracion: %d tarjetas (esperadas 3)" % len(cards))
        return
    c1, c2, c3 = cards

    def base_cards():
        img = shot(hwnd)
        groups, _ = accent_groups(img)
        return groups, img

    def card_at(groups, cx, span=60):
        for g in groups:
            if abs((g["x0"] + g["x1"]) // 2 - cx) < span:
                return g
        return None

    base, _ = base_cards()
    base1 = card_at(base, c1["center_x"])
    if base1 is None:
        log("FALLO: no se ve la tarjeta 1 en el estado base")
        return

    # ---------- FASE 3.2 ----------
    click(c1["center_x"], c1["y0"] + 30)
    img = shot(hwnd, "s1_click_selecciona.png")
    g1, _ = accent_groups(img)
    sel1 = card_at(g1, c1["center_x"])
    ok = sel1 is not None and sel1["count"] > base1["count"] * 2.2
    rec("3.2.1 click selecciona (glow/borde)", ok,
        "acento en la tarjeta 1: %d -> %d" % (base1["count"], sel1["count"] if sel1 else 0))

    click((c2["center_x"] + c3["center_x"]) // 2, c1["y0"] + 560)
    img = shot(hwnd, "s2_fondo_deselecciona.png")
    g2, _ = accent_groups(img)
    unsel = card_at(g2, c1["center_x"])
    ok = unsel is not None and unsel["count"] < base1["count"] * 1.5
    rec("3.2.2 clic en fondo deselecciona", ok,
        "acento en la tarjeta 1: %d (base %d)" % (unsel["count"] if unsel else 0, base1["count"]))

    cx1 = c1["center_x"]
    cy1 = c1["y0"] + 100
    dx = 150
    dy = -70
    drag(cx1, cy1, cx1 + dx, cy1 + dy)
    img = shot(hwnd, "s3_drag_mueve.png")
    g3, _ = accent_groups(img)
    d1 = card_at(g3, cx1 + dx)
    others = all(card_at(g3, c0["center_x"]) is not None for c0 in (c2, c3))
    rec("3.2.3 drag mueve la tarjeta", d1 is not None,
        "centro 1 desplazado +%d (encontrado=%s); otras fijas=%s" % (dx, d1 is not None, others))

    key(VK_CTRL, 0x5A)
    time.sleep(0.6)
    img = shot(hwnd, "s4_undo_deshace.png")
    g4, _ = accent_groups(img)
    back = card_at(g4, cx1)
    rec("3.2.4 Ctrl+Z deshace el drag", back is not None,
        "centro 1 de vuelta=%s" % (back is not None))

    key(VK_CTRL, 0x59)
    time.sleep(0.6)
    img = shot(hwnd, "s5_redo_rehace.png")
    g5, _ = accent_groups(img)
    again = card_at(g5, cx1 + dx)
    key(VK_CTRL, 0x5A)
    time.sleep(0.6)
    rec("3.2.5 Ctrl+Y rehace (y Ctrl+Z restaura)", again is not None,
        "desplazado de nuevo=%s" % (again is not None))

    rb_x0 = c2["center_x"] - 30
    rb_y0 = c1["y0"] + 700
    rb_x1 = c3["center_x"] + 40
    rb_y1 = c3["y0"] + 200
    drag(rb_x0, rb_y0, rb_x1, rb_y1)
    img = shot(hwnd, "s6_rubber_band.png")
    g6, _ = accent_groups(img)
    glow2 = card_at(g6, c2["center_x"])
    glow3 = card_at(g6, c3["center_x"])
    ok = (glow2 is not None and glow2["count"] > base1["count"] * 2.2
          and glow3 is not None and glow3["count"] > base1["count"] * 2.2)
    rec("3.2.6 rubber band selecciona (2 y 3)", ok,
        "glow en 2: %s, en 3: %s (base %d)" % (
            glow2["count"] if glow2 else 0, glow3["count"] if glow3 else 0, base1["count"]))
    click((c2["center_x"] + c3["center_x"]) // 2, c1["y0"] + 560)

    key(VK_SHIFT, 0x41)
    time.sleep(0.7)
    img = shot(hwnd, "s7_spotlight.png")
    a = np.asarray(img).astype(int)
    ccx = img.width // 2
    ccy = img.height // 2
    region = a[ccy - 260:ccy + 140, ccx - 380:ccx + 380]
    nonbg = ((np.abs(region[:, :, 0] - BG[0]) > 12)
             | (np.abs(region[:, :, 1] - BG[1]) > 12)
             | (np.abs(region[:, :, 2] - BG[2]) > 12)).mean()
    rec("3.2.7 Shift+A abre spotlight", nonbg > 0.35,
        "panel no-fondo en el centro: %.0f%%" % (nonbg * 100))
    key(None, 0x1B)
    time.sleep(0.5)
    img = shot(hwnd, "s7b_spotlight_cerrado.png")
    a = np.asarray(img).astype(int)
    region = a[ccy - 260:ccy + 140, ccx - 380:ccx + 380]
    nonbg = ((np.abs(region[:, :, 0] - BG[0]) > 12)
             | (np.abs(region[:, :, 1] - BG[1]) > 12)
             | (np.abs(region[:, :, 2] - BG[2]) > 12)).mean()
    rec("3.2.7b Escape cierra el spotlight", nonbg < 0.15,
        "no-fondo tras Escape: %.0f%%" % (nonbg * 100))

    click(c1["center_x"], c1["y0"] + 30)
    key(VK_CTRL, 0x44)
    time.sleep(0.8)
    g8, _ = base_cards()
    shot(hwnd, "s8_ctrl_d_duplica.png")
    ok = len(g8) == 4
    rec("3.2.8 Ctrl+D duplica", ok, "tarjetas: 3 -> %d" % len(g8))
    if ok:
        dup = None
        for g in g8:
            near = any(abs((g["x0"] + g["x1"]) // 2 - c0["center_x"]) < 80 for c0 in (c1, c2, c3))
            if not near:
                dup = g
                break
        if dup is None:
            dup = max(g8, key=lambda g: g["count"])
        dcx = (dup["x0"] + dup["x1"]) // 2
        dcy = dup["y0"] + 30
        click(dcx, dcy)
        key(None, 0x71)
        time.sleep(0.6)
        shot(hwnd, "s9_f2_caja.png")
        type_text("QA-Duplicado")
        key(None, 0x0D)
        time.sleep(0.5)
        shot(hwnd, "s9b_f2_confirmado.png")
        key(None, 0x2E)
        time.sleep(0.7)
        g9, _ = base_cards()
        ok2 = len(g9) == 3
        if not ok2:
            click(dcx, dcy)
            key(None, 0x2E)
            time.sleep(0.7)
            g9, _ = base_cards()
            ok2 = len(g9) == 3
        rec("3.2.9 F2 + Delete borra el duplicado", ok2,
            "tarjetas tras Delete: %d (esperadas 3)" % len(g9))
    else:
        rec("3.2.9 F2 + Delete", False, "sin duplicado que editar")

    # ---------- FASE 3.3 ----------
    gap = (c1["x1"] + 8, c1["y0"] - 40, c2["x0"] - 8, c1["y0"] + 200)
    img = shot(hwnd, "t0_hueco_base.png")
    wire_before = cable_pixels_in(img, gap)
    rec("3.3.0 base", True, "px de cable en el hueco 1-2: %d" % wire_before)

    click(c2["sock_in"], c2["y0"] + 2, right=True)
    time.sleep(0.5)
    img = shot(hwnd, "t1_desconecta.png")
    wire_after = cable_pixels_in(img, gap)
    rec("3.3.1 click derecho desconecta", wire_after < max(6, wire_before * 0.25),
        "px cable hueco 1-2: %d -> %d" % (wire_before, wire_after))

    pend_box = (c1["x1"] + 8, c1["y0"] - 60, c1["x1"] + 160, c1["y0"] + 60)
    move(c1["sock_out"], c1["y0"] + 2)
    time.sleep(0.12)
    u32.mouse_event(2, 0, 0, 0, 0)
    time.sleep(0.12)
    for i in range(1, 13):
        move(c1["sock_out"] + i * 5, c1["y0"] + 2 - i * 2)
    img = shot(hwnd, "t2_pendiente_sigue.png")
    pend = cable_pixels_in(img, pend_box)
    u32.mouse_event(4, 0, 0, 0, 0)
    time.sleep(0.4)
    rec("3.3.2 cable pendiente sigue al cursor", pend > 4,
        "px de pendiente fuera de la tarjeta: %d (soltado en el vacio = cancela)" % pend)

    img = shot(hwnd, "t3_cancelado.png")
    wire_cancel = cable_pixels_in(img, gap)
    rec("3.3.3 soltar en el vacio cancela", wire_cancel < max(6, wire_before * 0.25),
        "px cable hueco 1-2 tras cancelar: %d" % wire_cancel)

    drag(c1["sock_out"], c1["y0"] + 2, c2["sock_in"], c2["y0"] + 2)
    img = shot(hwnd, "t4_conecta.png")
    wire_re = cable_pixels_in(img, gap)
    rec("3.3.4 arrastrar cable conecta", wire_re > wire_after * 3,
        "px cable hueco 1-2: %d -> %d" % (wire_after, wire_re))

    if os.path.exists(CALIB):
        os.remove(CALIB)
    try:
        proc.terminate()
    except Exception:
        pass

    passed = sum(1 for _, ok, _ in results if ok)
    lines = ["# Guion manual de interacciones 3.2/3.3 - host Uno (puntero inyectado sobre la app viva)", "",
             "Fecha: %s | Pasos PASS: %d/%d" % (time.strftime("%Y-%m-%d %H:%M"), passed, len(results)), ""]
    for step, ok, detail in results:
        lines.append("- %s **%s** — %s" % ("[PASS]" if ok else "[FALLO]", step, detail))
    lines.append("")
    lines.append("Evidencia: capturas en `qa-manual/` (una por paso). Metodo: puntero/teclado inyectados con ctypes sobre la app viva; veredicto por medicion de pixeles (glow/borde de seleccion, posiciones de tarjeta, area de cable en el hueco, panel del spotlight) sobre capturas de pantalla.")
    with open(REPORT, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    log("reporte: %s" % REPORT)


if __name__ == "__main__":
    if "--calibrate" in sys.argv:
        calibrate()
    else:
        run()
