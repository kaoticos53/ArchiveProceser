# -*- coding: utf-8 -*-
"""Sesion manual de gestos 3.2/3.3 del host Uno CON PUNTERO REAL (hito 247).

Este instrumento NO inyecta puntero: lo mueve el OPERADOR con su raton. Hace lo que el operador
no puede hacer con precision: (1) arranca y maximiza la app; (2) calibra la escena por pixel
(tarjetas, sockets, cables); (3) tras cada gesto del operador CAPTURA la pantalla y MIDE el
delta (acento de seleccion, posicion de tarjeta, cable del hueco, panel del spotlight).

Reparto de responsabilidades, que es el punto del instrumento:
  - el veredicto del GESTO lo firma el operador (el gesto es suyo);
  - la evidencia de que el gesto LLEGO AL PRODUCTO la firma esta medicion.
Asi la sesion manual no se sostiene sobre la memoria del operador: se sostiene sobre pixeles.

Por que no reutiliza la inyeccion de qa_manual.py: la sesion del hito 231 midio que el puntero
inyectado sin UIAccess no llega al contenido de WinAppSDK (0 px en toda configuracion, matriz de
9 tecnicas en guion_manual_32_33_resultado.md). Con puntero REAL la puerta esta abierta; lo que
hace falta es medir. La segmentacion por acento SI se reutiliza de qa_manual.py (una sola fuente
para la metrica de pixel).

Modos:
  --launch        arranca la app (detached), la maximiza, captura 00_base y calibra -> calib_manual.json
  --shot <label>  captura qa-manual-249/<label>.png y mide la escena (bloque METRIC)
  --watch <seg>   VIGILANTE: mide la escena cada ~0.8 s mientras el operador gesticula y guarda la
                  linea de tiempo (timeline.jsonl) + una captura por cambio material. Es el modo de
                  la sesion manual: el operador no tiene que avisar de nada ni posar para la foto.
                  Termina solo (o antes si aparece el fichero stop_watch).
  --timeline      resume la linea de tiempo: cada CAMBIO de la escena con lo que cambio
  --status        dice si la ventana sigue viva (y el rect)
  --stop          cierra la app por WM_CLOSE (con taskkill de respaldo)

La CARPETA de la sesion la elige quien la conduce con la variable de entorno FILEFLOW_QA_WORK
(por omision `qa-manual-247`, la sesion que estreno el instrumento): cada sesion manual deja su
evidencia en su carpeta, sin mezclar la toma del defecto con la de su arreglo.
    FILEFLOW_QA_WORK=qa-manual-249 python qa_manual_session.py --launch
"""
import ctypes
import glob
import json
import os
import subprocess
import sys
import time
from ctypes import wintypes

import numpy as np

sys.dont_write_bytecode = True   # el import de qa_manual no debe dejar __pycache__ en el repo
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from qa_manual import (ACCENT, BG, CARD_BODY, GRID, accent_groups, cable_pixels_in,  # noqa: E402
                       find_main_window, foreground, socket_scan, u32)

# accent_groups (de qa_manual) queda para la CALIBRACION (una sola vez); el vigilante usa
# accent_mask/global_cards, que hacen una sola pasada de pixeles por fotograma sobre 4K.

PRIMARY = (99, 102, 241)   # #6366F1 CanvasAccentPrimaryBrush: borde de SELECCION

HERE = os.path.dirname(os.path.abspath(__file__))
BIN = os.path.normpath(os.path.join(HERE, "..", "..", "FileFlow.App.Uno", "bin",
                                    "Debug", "net10.0-windows10.0.19041.0"))
EXE = os.path.join(BIN, "FileFlow.App.Uno.exe")
SHOTS = os.path.join(HERE, os.environ.get("FILEFLOW_QA_WORK", "qa-manual-247"))
STATE = os.path.join(SHOTS, "session_state.json")
CALIB = os.path.join(SHOTS, "calib_manual.json")
os.makedirs(SHOTS, exist_ok=True)

WM_CLOSE = 0x0010


class RECT(ctypes.Structure):
    _fields_ = [("left", ctypes.c_long), ("top", ctypes.c_long),
                ("right", ctypes.c_long), ("bottom", ctypes.c_long)]


def log(msg):
    print(msg, flush=True)


def win_rect(hwnd):
    r = RECT()
    u32.GetWindowRect(hwnd, ctypes.byref(r))
    return r.left, r.top, r.right, r.bottom


def grab(hwnd, name=None):
    from PIL import ImageGrab
    l, t, r, b = win_rect(hwnd)
    try:
        img = ImageGrab.grab(bbox=(l, t, r, b), all_screens=True)
    except Exception:
        img = ImageGrab.grab(bbox=(l, t, r, b))
    if name:
        path = os.path.join(SHOTS, name + ".png")
        img.save(path)
        log("[captura] %s" % path)
    return img


def live_window(pid):
    return find_main_window(pid)


def exe_of(pid):
    """Nombre del ejecutable de un pid (para saber QUE ventana cubre la region capturada)."""
    PROCESS_QUERY_LIMITED_INFORMATION = 0x1000
    k32 = ctypes.windll.kernel32
    h = k32.OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, False, pid)
    if not h:
        return "?"
    try:
        buf = ctypes.create_unicode_buffer(512)
        size = wintypes.DWORD(512)
        if k32.QueryFullProcessImageNameW(h, 0, buf, ctypes.byref(size)):
            return os.path.basename(buf.value)
        return "?"
    finally:
        k32.CloseHandle(h)


def foreground_info(hwnd):
    """(la app es la ventana en primer plano, titulo en primer plano)."""
    fg = u32.GetForegroundWindow()
    buf = ctypes.create_unicode_buffer(256)
    u32.GetWindowTextW(fg, buf, 256)
    return (fg == hwnd, buf.value[:40])


def overlaps(r1, r2):
    return not (r1[2] <= r2[0] or r2[2] <= r1[0] or r1[3] <= r2[1] or r2[3] <= r1[1])


def windows():
    """Ventanas visibles en Z-ORDER (la primera es la de arriba): que cubre la region capturada.

    La captura de pantalla devuelve lo que este ARRIBA en esa region, no el contenido de la app;
    sin este volcado, una ventana encima se confunde con la app y la sesion mide otra cosa."""
    res = []

    def cb(h, l):
        if u32.IsWindowVisible(h):
            buf = ctypes.create_unicode_buffer(256)
            u32.GetWindowTextW(h, buf, 256)
            r = RECT()
            u32.GetWindowRect(h, ctypes.byref(r))
            p = wintypes.DWORD()
            u32.GetWindowThreadProcessId(h, ctypes.byref(p))
            res.append((buf.value[:55], (r.left, r.top, r.right, r.bottom), int(p.value)))
        return True

    CMPFUNC = ctypes.WINFUNCTYPE(ctypes.c_bool, wintypes.HWND, wintypes.LPARAM)
    u32.EnumWindows(CMPFUNC(cb), 0)
    reg = None
    if os.path.exists(STATE):
        pid = json.load(open(STATE))["pid"]
        hwnd = live_window(pid)
        if hwnd:
            reg = win_rect(hwnd)
            log("[region] la app ocupa %s (pid=%d)" % (reg, pid))
    for i, (t, rc, pid) in enumerate(res):
        mark = ""
        if reg and overlaps(rc, reg):
            mark = "  <== SOLAPA LA REGION"
        log("%2d z=%02d %-55s pid=%-7d %-22s rect=%s%s"
            % (i, i, t or "(sin titulo)", pid, exe_of(pid), rc, mark))
    return 0


def launch():
    if not os.path.exists(EXE):
        log("[FALLO] no existe el binario: %s" % EXE)
        return 1
    # Start-Process deja el proceso FUERA del job del terminal (sobrevive a este comando).
    ps = ("Start-Process -FilePath '%s' -WorkingDirectory '%s' -PassThru | "
          "Select-Object -ExpandProperty Id" % (EXE, BIN))
    out = subprocess.run(["powershell", "-NoProfile", "-NonInteractive", "-Command", ps],
                         capture_output=True, text=True)
    try:
        pid = int(out.stdout.strip().splitlines()[-1])
    except Exception:
        log("[FALLO] no se pudo lanzar: %s / %s" % (out.stdout, out.stderr))
        return 1
    log("[app] pid=%d" % pid)
    hwnd = None
    for _ in range(60):
        time.sleep(1)
        hwnd = live_window(pid)
        if hwnd:
            break
    if not hwnd:
        log("[FALLO] la ventana no aparecio")
        return 1
    time.sleep(4)
    foreground(hwnd)          # maximiza + primer plano (el operador necesita la escena grande)
    time.sleep(2.5)
    img = grab(hwnd, "00_base")
    cards, _ = accent_groups(img)
    log("[calibracion] tarjetas detectadas: %d" % len(cards))
    cal = []
    for g in cards:
        sock_in, sock_out = socket_scan(g, img)
        cal.append(dict(g, sock_in=sock_in, sock_out=sock_out,
                        center_x=(g["x0"] + g["x1"]) // 2))
        log("  tarjeta x[%d..%d] y=%d acc=%d sock_in=%s sock_out=%s"
            % (g["x0"], g["x1"], g["y0"], g["count"], sock_in, sock_out))
    json.dump({"cards": cal, "win": list(win_rect(hwnd)), "pid": pid},
              open(CALIB, "w"))
    json.dump({"pid": pid, "hwnd": int(hwnd)}, open(STATE, "w"))
    log("[estado] %s" % STATE)
    return 0


def accent_mask(img, tol=28):
    """Mascara del acento #818CF8 en int16: sobre 4K la version en int64 costaba ~1 s por fotograma.

    OJO: el acento es la BARRA del nodo y los CABLES. NO es el borde de seleccion (ver abajo):
    medir la seleccion con esta mascara da 0 y hace creer que el gesto no llego (el error que
    costo la primera corrida de esta sesion).
    """
    a = np.asarray(img)
    d = np.abs(a.astype(np.int16) - np.array(ACCENT, dtype=np.int16))
    return (d < tol).all(axis=2)


def primary_mask(img, tol=24):
    """Mascara del borde de SELECCION (CanvasAccentPrimaryBrush = #6366F1).

    Es OTRA tonalidad que el acento (la separacion en R es 30 > tol): la tarjeta seleccionada
    pinta un borde de 2 px de este color, asi que contar estos pixeles en la caja de la tarjeta
    es la senal de seleccion que el instrumento del 231 no tenia.
    """
    a = np.asarray(img)
    d = np.abs(a.astype(np.int16) - np.array(PRIMARY, dtype=np.int16))
    return (d < tol).all(axis=2)


def global_cards(mask, min_bar_px=220, min_width=100):
    """Tarjetas por la BARRA de acento: la MISMA segmentacion de qa_manual.accent_groups, pero
    reusando la mascara ya calculada (una sola pasada de pixeles por fotograma)."""
    out = []
    rows = np.where(mask.sum(axis=1) > min_bar_px)[0]
    if len(rows) == 0:
        return out
    bands, start, prev = [], rows[0], rows[0]
    for r in rows[1:]:
        if r - prev <= 4:
            prev = r
            continue
        bands.append((start, prev))
        start = prev = r
    bands.append((start, prev))
    for y0, y1 in bands:
        cols = np.where(mask[y0:y1 + 1, :].sum(axis=0) > 3)[0]
        if len(cols) == 0:
            continue
        spans, s0, p0 = [], cols[0], cols[0]
        for c in cols[1:]:
            if c - p0 > 30:
                spans.append((s0, p0))
                s0 = p0 = c
            p0 = c
        spans.append((s0, p0))
        out += [(int(a), int(b), int(y0)) for a, b in spans if b - a >= min_width]
    return out


def center_nonbg(img, half_w=380, half_h=260, up=140):
    """Fraccion de pixeles no-fondo en el centro de la VENTANA (el panel del spotlight)."""
    a = np.asarray(img).astype(np.int16)
    cx, cy = img.width // 2, img.height // 2
    region = a[cy - half_h:cy + up, cx - half_w:cx + half_w]
    if region.size == 0:
        return 0.0
    bg = np.array(BG, dtype=np.int16)
    return float((np.abs(region - bg) > 12).any(axis=2).mean())


def band_of(mask, c, w, tol_pad=40):
    """Metricas de UNA tarjeta calibrada, medidas en SU caja (inmune a que se fusionen spans)."""
    h, _ = mask.shape
    x0 = max(c["x0"] - tol_pad, 0)
    x1 = min(c["x1"] + tol_pad, w)
    y0 = max(c["y0"] - tol_pad, 0)
    y1 = min(c["y0"] + CARD_BODY, h)
    bar0 = max(c["y0"] - 2, 0)
    bar1 = min(c["y0"] + 8, h)
    cols = np.where(mask[bar0:bar1, x0:x1].sum(axis=0) > 0)[0]
    return {"acc": int(mask[y0:y1, x0:x1].sum()),
            "cx": int(cols.mean()) + x0 if len(cols) else -1,
            "w": int(cols[-1] - cols[0] + 1) if len(cols) else 0}


def measure(img, cal):
    """Mide la escena: por tarjeta CALIBRADA (robusto a la seleccion) y global (robusto al pan)."""
    mask = accent_mask(img)
    h, w = mask.shape
    log("METRIC total_accent_px=%d" % int(mask.sum()))
    log("METRIC center_nonbg=%.3f" % center_nonbg(img))
    for i, c in enumerate(cal["cards"]):
        b = band_of(mask, c, w)
        log("METRIC card%d acc=%d cx=%d bar_w=%d" % (i, b["acc"], b["cx"], b["w"]))
    cards = cal["cards"]
    for i in range(len(cards) - 1):
        a, b = cards[i], cards[i + 1]
        box = (a["x1"] + 8, a["y0"] - 60, b["x0"] - 8, a["y0"] + 120)
        if box[2] - box[0] > 4:
            log("METRIC cable_gap%d_%d=%d" % (i, i + 1, cable_pixels_in(img, box, tol=30)))
    g = global_cards(mask)
    log("METRIC global_cards=%d spans=%s" % (len(g), ",".join(
        "%d..%d@y%d" % s for s in g)))
    # El TEMA en PIXELES: la tonalidad que MAS superficie ocupa de la ventana es el fondo del lienzo
    # (el area mayor de la escena), asi que su color es la huella del tema vigente — y, al reabrir la
    # app, la prueba de que el tema GUARDADO es el que se aplico. Se cuenta sobre una rejilla (::3)
    # para que el coste no dependa del tamano de pantalla.
    a = np.asarray(img.convert("RGB"))[::3, ::3].reshape(-1, 3).astype(np.uint32)
    packed = (a[:, 0] << 16) | (a[:, 1] << 8) | a[:, 2]
    values, counts = np.unique(packed, return_counts=True)
    order = np.argsort(counts)[::-1][:3]
    total = int(counts.sum())
    log("METRIC top_colors=%s" % ",".join(
        "#%06X:%.1f%%" % (int(values[i]), 100.0 * counts[i] / total) for i in order))


def snapshot_metrics(img, cal, ref=None):
    """Metricas de un fotograma: las que permiten atribuir un gesto a un cambio de escena.

    ref: fotograma de referencia (el primero del vigilante). Con el se mide la DIFERENCIA GENERAL
    de pixeles: el detector ciego a los colores, que es el que no se puede equivocar de tonalidad.
    """
    mask = accent_mask(img)
    pmasks = primary_mask(img)
    h, w = mask.shape
    pt = wintypes.POINT()
    u32.GetCursorPos(ctypes.byref(pt))
    m = {"total": int(mask.sum()), "center": round(center_nonbg(img), 3),
         "sel_total": int(pmasks.sum()), "curx": int(pt.x), "cury": int(pt.y)}
    if ref is not None:
        d = (np.abs(np.asarray(img).astype(np.int16) - ref) > 24).any(axis=2)
        m["dpx"] = int(d[::2, ::2].sum()) * 4
    for i, c in enumerate(cal.get("cards", [])):
        b = band_of(mask, c, w)
        m["acc%d" % i] = b["acc"]
        m["cx%d" % i] = b["cx"]
        m["w%d" % i] = b["w"]
        x0, x1 = max(c["x0"] - 24, 0), min(c["x1"] + 24, w)
        y0, y1 = max(c["y0"] - 24, 0), min(c["y0"] + CARD_BODY, h)
        m["sel%d" % i] = int(pmasks[y0:y1, x0:x1].sum())
    cards = cal.get("cards", [])
    for i in range(len(cards) - 1):
        a, b = cards[i], cards[i + 1]
        box = (a["x1"] + 8, a["y0"] - 60, b["x0"] - 8, a["y0"] + 120)
        if box[2] - box[0] > 4:
            m["gap%d" % i] = int(mask[box[1]:box[3], box[0]:box[2]].sum())
    g = global_cards(mask)
    m["nglobal"] = len(g)
    m["gspans"] = [list(s) for s in g]
    # proxies del zoom/camara: barra mediana de las tarjetas detectadas y bordes del conjunto
    if g:
        widths = sorted(s[1] - s[0] for s in g)
        m["medw"] = int(widths[len(widths) // 2])
        m["minx"] = int(min(s[0] for s in g))
        m["miny"] = int(min(s[2] for s in g))
    else:
        m["medw"] = m["minx"] = m["miny"] = -1
    return m


def signature(m):
    """Firma gruesa del fotograma: dos fotogramas con la misma firma son la misma escena."""
    sig = [m["total"] // 400, int(m["center"] * 50), m["nglobal"], m["medw"] // 8,
           m["minx"] // 12, m["miny"] // 12, m.get("fg"), m.get("dpx", 0) // 200]
    for k in sorted(k for k in m if k.startswith("acc")):
        sig.append(m[k] // 400)
    for k in sorted(k for k in m if k.startswith("sel")):
        sig.append(m[k] // 200)
    for k in sorted(k for k in m if k.startswith("cx")):
        sig.append(m[k] // 8)
    for k in sorted(k for k in m if k.startswith("gap")):
        sig.append(m[k] // 60)
    for s in m["gspans"]:
        sig += [s[0] // 16, s[1] // 16, s[2] // 16]
    return tuple(sig)


def inject_test():
    """Prueba de UNA inyeccion: ¿el puntero inyectado llega HOY al contenido de la app?

    Repite la medicion del hito 231 (que lo declaro bloqueado: 0 px en toda configuracion) sobre
    una escena CALIBRADA, para que el veredicto de esta sesion no dependa de la memoria de aquel
    informe: una seleccion real suma ~2.800 px de acento (el borde de 2 px del glow) a la tarjeta.
    Es la unica forma honesta de decidir si la sesion puede automatizarse o exige puntero humano.
    """
    import qa_manual
    if not os.path.exists(STATE):
        log("[FALLO] sin estado: usa --launch primero")
        return 2
    pid = json.load(open(STATE))["pid"]
    hwnd = live_window(pid)
    if not hwnd:
        log("STATUS app=DEAD")
        return 2
    foreground(hwnd)
    time.sleep(1.5)
    img0 = grab(hwnd, "inj_antes")
    cards, _ = accent_groups(img0)
    if not cards:
        log("[FALLO] no se detectan tarjetas: la escena no es la esperada")
        return 2
    cal = {"cards": [dict(c, center_x=(c["x0"] + c["x1"]) // 2) for c in cards]}
    log("[escena] %d tarjetas: %s" % (len(cards), [c["center_x"] for c in cal["cards"]]))
    ref = np.asarray(img0).astype(np.int16)
    m0 = snapshot_metrics(img0, cal, ref=ref)
    l, t, _, _ = win_rect(hwnd)
    c = cal["cards"][0]
    sx, sy = l + c["center_x"], t + c["y0"] + 30
    log("[inyeccion] SetCursorPos+mouse_event (boton izq) en pantalla (%d,%d)" % (sx, sy))
    qa_manual.click(sx, sy)
    time.sleep(1.0)
    img1 = grab(hwnd, "inj_despues")
    m1 = snapshot_metrics(img1, cal, ref=ref)
    log("antes:    cab=%.3f acc0=%d sel0=%d sel_total=%d total=%d"
        % (m0["center"], m0["acc0"], m0["sel0"], m0["sel_total"], m0["total"]))
    log("despues:  cab=%.3f acc0=%d sel0=%d sel_total=%d total=%d"
        % (m1["center"], m1["acc0"], m1["sel0"], m1["sel_total"], m1["total"]))
    log("delta: acc0=%+d sel0=%+d sel_total=%+d total=%+d | pixeles cambiados (general)=%d"
        % (m1["acc0"] - m0["acc0"], m1["sel0"] - m0["sel0"],
           m1["sel_total"] - m0["sel_total"], m1["total"] - m0["total"], m1["dpx"]))
    if m1["dpx"] > 2000 or m1["sel0"] > 200:
        log("VEREDICTO: EL PUNTERO INYECTADO LLEGA (la escena cambia) — la sesion puede automatizarse")
    else:
        log("VEREDICTO: el puntero inyectado NO llega (la escena no cambia por ninguna via: "
            "ni acento, ni borde de seleccion, ni diferencia general de pixeles) — exige puntero real")
    json.dump(cal, open(CALIB, "w"))
    return 0


def watch(seconds, interval=0.8):
    if not os.path.exists(STATE) or not os.path.exists(CALIB):
        log("[FALLO] sin estado/calibracion: usa --launch primero")
        return 2
    st = json.load(open(STATE))
    pid = st["pid"]
    cal = json.load(open(CALIB))
    tl_path = os.path.join(SHOTS, "timeline.jsonl")
    stop_path = os.path.join(SHOTS, "stop_watch")
    if os.path.exists(stop_path):
        os.remove(stop_path)
    t0 = time.time()
    last_sig = None
    saved = 0
    frames = 0
    ref = None
    with open(tl_path, "a", encoding="utf-8") as tl:
        tl.write(json.dumps({"event": "watch_start", "ts": time.time(), "seconds": seconds}) + "\n")
        while time.time() - t0 < seconds:
            if os.path.exists(stop_path):
                tl.write(json.dumps({"event": "stop_requested", "t": round(time.time() - t0, 2)}) + "\n")
                break
            hwnd = live_window(pid)
            if not hwnd:
                tl.write(json.dumps({"event": "app_dead", "t": round(time.time() - t0, 2)}) + "\n")
                tl.flush()
                log("[vigilante] la app murio a los %.1f s" % (time.time() - t0))
                return 3
            try:
                img = grab(hwnd)
            except Exception as ex:
                tl.write(json.dumps({"event": "grab_failed", "t": round(time.time() - t0, 2),
                                     "error": str(ex)}) + "\n")
                time.sleep(interval)
                continue
            # OJO: el cursor NO entra en la firma (cada movimiento seria un "cambio"): se anota en
            # cada fotograma y el timeline lo imprime al lado de cada cambio material. Es la pieza
            # que convierte "clico aqui y no pasa nada" en una distancia medida en pixeles.
            m = snapshot_metrics(img, cal, ref=ref)
            # La captura devuelve lo que esta ARRIBA: si la app no es la de primer plano, la
            # medicion de este fotograma puede ser de otra ventana encima. Queda en la linea de tiempo.
            m["fg"], m["fgt"] = foreground_info(hwnd)
            if ref is None:
                ref = np.asarray(img).astype(np.int16)
                m["dpx"] = 0
            m["t"] = round(time.time() - t0, 2)
            m["ts"] = round(time.time(), 3)
            tl.write(json.dumps(m) + "\n")
            tl.flush()
            frames += 1
            sig = signature(m)
            if sig != last_sig:
                last_sig = sig
                saved += 1
                if saved <= 40:
                    img.save(os.path.join(SHOTS, "f%05d.png" % int(m["t"] * 10)))
            time.sleep(interval)
        tl.write(json.dumps({"event": "watch_end", "t": round(time.time() - t0, 2),
                             "frames": frames, "saved": saved}) + "\n")
    log("[vigilante] %d fotogramas, %d cambios de escena guardados en %s" % (frames, saved, SHOTS))
    return 0


def frames():
    """Forense de los fotogramas guardados: ¿la region capturada sigue siendo la APP y que cambio?

    Sirve para lo que paso en la primera corrida de la sesion: sin esto, un cambio de escena solo
    dice QUE cambio (dos tarjetas se movieron) y no SI lo que se estaba midiendo seguia siendo la
    app o una ventana encima. Se anade la cobertura del fondo del lienzo, la de la rejilla y el
    rectangulo del cambio contra la base."""
    from PIL import Image
    paths = sorted(glob.glob(os.path.join(SHOTS, "f*.png")) +
                   [p for p in [os.path.join(SHOTS, "00_base.png")] if os.path.exists(p)])
    if not paths:
        log("[FALLO] sin fotogramas guardados")
        return 2
    base = None
    for p in paths:
        img = Image.open(p).convert("RGB")
        a = np.asarray(img).astype(np.int16)
        bg = np.array(BG, dtype=np.int16)
        grid = np.array(GRID, dtype=np.int16)
        bg_cov = float((np.abs(a - bg) < 8).all(axis=2).mean())
        grid_cov = float((np.abs(a - grid) < 10).all(axis=2).mean())
        mask = accent_mask(img)
        spans = global_cards(mask)
        line = ("%s %dx%d bg=%.3f grid=%.3f accent=%d centro=%.3f cards=%d"
                % (os.path.basename(p), img.width, img.height, bg_cov, grid_cov,
                   int(mask.sum()), center_nonbg(img), len(spans)))
        if base is None:
            base = a
        else:
            diff = (np.abs(a - base) > 24).any(axis=2)
            n = int(diff.sum())
            if n:
                ys, xs = np.where(diff)
                line += " | vs_base=%d (%.2f%%) bbox=(%d,%d)-(%d,%d)" % (
                    n, 100.0 * n / diff.size, xs.min(), ys.min(), xs.max(), ys.max())
            else:
                line += " | vs_base=0"
        log(line)
        log("    spans=%s" % ",".join("%d..%d@y%d" % s for s in spans))
    return 0


def timeline(verbose=False):
    """Resume timeline.jsonl: cada cambio material de la escena, con lo que cambio."""
    path = os.path.join(SHOTS, "timeline.jsonl")
    if not os.path.exists(path):
        log("[FALLO] sin linea de tiempo: usa --watch")
        return 2
    prev = None
    changes = 0
    for line in open(path, encoding="utf-8"):
        try:
            m = json.loads(line)
        except Exception:
            continue
        if "event" in m:
            log("== %s %s" % (m["event"], {k: v for k, v in m.items() if k != "event"}))
            continue
        if prev is None:
            prev = m
            log("[t=%7.2f] base  cards=%d acc=%s cx=%s w=%s gaps=%s center=%.3f total=%d"
                % (m["t"], m["nglobal"], [m.get("acc%d" % i) for i in range(3)],
                   [m.get("cx%d" % i) for i in range(3)], [m.get("w%d" % i) for i in range(3)],
                   [m.get("gap%d" % i) for i in range(2)], m["center"], m["total"]))
            if not m.get("fg", True):
                log("    AVISO: la app NO era el primer plano (%r) — este fotograma puede medir otra ventana"
                    % m.get("fgt"))
            continue
        diffs = []
        for k in ("total", "center", "nglobal", "medw", "minx", "miny", "fg", "fgt",
                  "dpx", "sel_total", "curx", "cury"):
            if m.get(k) != prev.get(k):
                diffs.append("%s %s->%s" % (k, prev.get(k), m.get(k)))
        for i in range(3):
            for p in ("acc", "cx", "w", "sel"):
                k = "%s%d" % (p, i)
                if m.get(k) != prev.get(k):
                    diffs.append("%s %s->%s" % (k, prev.get(k), m.get(k)))
        for i in range(2):
            k = "gap%d" % i
            if m.get(k) != prev.get(k):
                diffs.append("%s %s->%s" % (k, prev.get(k), m.get(k)))
        if m.get("gspans") != prev.get("gspans"):
            diffs.append("gspans %s->%s" % (prev.get("gspans"), m.get("gspans")))
        if diffs:
            changes += 1
            log("[t=%7.2f] %s" % (m["t"], " | ".join(diffs)))
            prev = m
    log("== %d cambios materiales" % changes)
    return 0


def shot(label):
    if not os.path.exists(STATE):
        log("[FALLO] sin estado: lanza la app con --launch")
        return 2
    st = json.load(open(STATE))
    pid = st["pid"]
    hwnd = live_window(pid)
    if not hwnd:
        log("STATUS app=DEAD pid=%d" % pid)
        return 2
    img = grab(hwnd, label)
    fg, fgt = foreground_info(hwnd)
    log("STATUS app=ALIVE pid=%d rect=%s fg=%s (primer plano: %r)"
        % (pid, win_rect(hwnd), fg, fgt))
    if os.path.exists(CALIB):
        measure(img, json.load(open(CALIB)))
    else:
        log("[aviso] sin calibracion: mide solo lo global")
        measure(img, {"cards": []})
    return 0


def status():
    if not os.path.exists(STATE):
        log("STATUS sin estado (nunca se lanzo)")
        return 1
    pid = json.load(open(STATE))["pid"]
    hwnd = live_window(pid)
    if not hwnd:
        log("STATUS app=DEAD pid=%d" % pid)
        return 2
    log("STATUS app=ALIVE pid=%d rect=%s" % (pid, win_rect(hwnd)))
    return 0


def stop():
    if not os.path.exists(STATE):
        log("STATUS sin estado")
        return 1
    pid = json.load(open(STATE))["pid"]
    hwnd = live_window(pid)
    if hwnd:
        u32.PostMessageW(hwnd, WM_CLOSE, 0, 0)
        for _ in range(10):
            time.sleep(0.5)
            if not live_window(pid):
                log("STATUS app=CLOSED pid=%d" % pid)
                return 0
    subprocess.run(["taskkill", "/PID", str(pid), "/T", "/F"], capture_output=True)
    log("STATUS app=KILLED pid=%d" % pid)
    return 0


if __name__ == "__main__":
    if "--launch" in sys.argv:
        sys.exit(launch())
    if "--shot" in sys.argv:
        sys.exit(shot(sys.argv[sys.argv.index("--shot") + 1]))
    if "--inject-test" in sys.argv:
        sys.exit(inject_test())
    if "--watch" in sys.argv:
        secs = float(sys.argv[sys.argv.index("--watch") + 1])
        sys.exit(watch(secs))
    if "--timeline" in sys.argv:
        sys.exit(timeline())
    if "--frames" in sys.argv:
        sys.exit(frames())
    if "--windows" in sys.argv:
        sys.exit(windows())
    if "--status" in sys.argv:
        sys.exit(status())
    if "--stop" in sys.argv:
        sys.exit(stop())
    log(__doc__)
    sys.exit(0)
