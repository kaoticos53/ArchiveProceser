# -*- coding: utf-8 -*-
"""Sesion del MENU PRINCIPAL del host Uno (hito 257) con DRIVER EXTERNO por UIA.

Reparto de responsabilidades (el mismo de la sesion de los ajustes, 255): quien ACTUA es este driver
—otro proceso le da a los controles REALES por sus AutomationId, con los patrones de UIA, el canal
que usa un lector de pantalla—; quien MIDE es el vigilante de qa_manual_session.py (`--watch`), que
captura la pantalla y saca el delta por pixel mientras el driver actua. Aqui ademas el driver mide su
PROPIO pixel por bandas antes y despues de cada entrada, para que la evidencia no dependa de que el
vigilante sepa mirar donde mira un menu.

Que se ejercita, y por que cada paso mide lo que mide:
  - la BARRA: sus 14 entradas ancladas, con su nombre visible (que es el texto del diccionario del
    host) y su estado habilitado/deshabilitado (Deshacer y Rehacer dependen del editor);
  - el MENU: pulsar «Menu» despliega el cajon. Su pixel es el VELO (`#A6000000` sobre toda la
    ventana): la banda central del lienzo se oscurece, y ese es el delta que se mide;
  - el CAJON: sus entradas aparecen en el arbol solo con el cajon desplegado, y su entrada
    «Ajustes» abre la MISMA superficie de ajustes del host (hito 255);
  - el INSPECTOR: conmutarlo hace desaparecer la columna derecha del marco, que es un cambio de
    pixel en la banda derecha (el panel no expone AutomationId: se construye por codigo);
  - el MODO PRUEBA: su casilla se conmuta por TogglePattern y se devuelve a como estaba.

Nada de esto escribe preferencias ni ejecuta el flujo: abrir una superficie no guarda, y el ciclo se
deja al sondeo en proceso (--selfcheck-controlbar), que ya lo ejerce en modo simulacion.

Uso (con la app ya lanzada por el instrumento, que maximiza y escribe session_state.json):
  FILEFLOW_QA_WORK=qa-manual-268 python qa_menu_uia.py --session

Salida: una linea por paso (`[menu] ...`) con lo medido ANTES y DESPUES, capturas rotuladas en la
carpeta de la sesion, y exit 0 solo si todos los pasos salieron como dicen.
"""
import ctypes
import json
import os
import sys
import time

import numpy as np

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

# El instrumento de la sesion manual (grab/win_rect) y el fontanero de UIA del host (connect/by_aid/
# invoke/iface/tree). Se IMPORTAN en vez de copiarse: una sola fuente para la metrica de pixel y para
# el acceso a los controles, que es lo que hace comparables las sesiones entre si.
import qa_manual_session as sess  # noqa: E402
import qa_ajustes_uia as drv  # noqa: E402

BAR = ("ControlBarMenuButton", "ControlBarDryRunToggle", "ControlBarWatchButton", "ControlBarRunButton",
       "ControlBarDebugButton", "ControlBarStepButton", "ControlBarContinueButton", "ControlBarPauseButton",
       "ControlBarStopButton", "ControlBarUndoButton", "ControlBarRedoButton", "ControlBarRollbackButton",
       "ControlBarInspectorButton", "SettingsButton")
DRAWER = ("ControlBarThemeCombo", "ControlBarLanguageCombo", "ControlBarDrawerSettingsButton",
          "ControlBarDrawerInspectorButton", "ControlBarDrawerCloseButton")
SETTINGS_ANCHORS = ("SettingsTabStorage", "SettingsTabAppearance", "SettingsTabPerformance",
                    "SettingsTabTools", "SettingsSaveButton", "SettingsCloseButton")
# Las cuatro entradas de la isla del ciclo que NO estan en el arbol cuando no hay nada corriendo ni
# depurando: su visibilidad es por contexto (Step/Continue son de la depuracion, Pause/Stop del ciclo
# en marcha). Que falten es la prueba de que el contexto manda, no un hueco de la barra.
RESTING_ONLY = ("ControlBarStepButton", "ControlBarContinueButton", "ControlBarPauseButton",
                "ControlBarStopButton")
QUANT = 4          # cubo de cuantizacion del color dominante: 4 niveles basta para separar velo y fondo
STEP = 1.4         # asentamiento tras cada orden, en segundos


def band(share):
    """Una banda de la ventana en fracion de ancho (lo que se mide, independiente de la resolucion)."""
    return share


LEFT = band((0.0, 0.06))          # la columna del cajon de herramientas (y el cajon cuando se despliega)
CENTER = band((0.35, 0.65))       # el centro del lienzo: donde se ve el VELO del cajon
RIGHT = band((0.94, 1.0))         # la columna del inspector


def dominant(img, cols, rows=(0.15, 0.85)):
    """(color dominante, fraccion) de la banda, sobre la imagen REDUCIDA a 1/4 (el color que manda no
    necesita 8 millones de pixeles: la reduccion lo deja igual y la medicion tarda una fraccion)."""
    small = img.resize((max(1, img.width // 4), max(1, img.height // 4)))
    w, h = small.size
    box = (int(cols[0] * w), int(rows[0] * h), max(1, int(cols[1] * w)), max(1, int(rows[1] * h)))
    a = np.asarray(small.crop(box).convert("RGB")).reshape(-1, 3)
    q = (a // QUANT) * QUANT
    keys, counts = np.unique(q, axis=0, return_counts=True)
    i = int(counts.argmax())
    return tuple(int(v) for v in keys[i]), float(counts[i]) / len(a)


def share_of(img, cols, color, tol=6, rows=(0.15, 0.85)):
    """Que FRACCION de la banda es del color dado. Es la medida que distingue una columna del marco
    de lo que hay detras: cuando el inspector se recoge, su banda pasa a ser lienzo y la fraccion sube
    (el color dominante solo no lo dice: dos tonos vecinos pueden alternarse sin que cambie el veredicto)."""
    small = img.resize((max(1, img.width // 4), max(1, img.height // 4)))
    w, h = small.size
    box = (int(cols[0] * w), int(rows[0] * h), max(1, int(cols[1] * w)), max(1, int(rows[1] * h)))
    a = np.asarray(small.crop(box).convert("RGB")).reshape(-1, 3).astype(int)
    return float((np.abs(a - np.array(color)).max(axis=1) <= tol).mean())


def hexs(c):
    return "#%02X%02X%02X" % c


def present(win):
    """Las anclas que el arbol expone AHORA. Indice fresco: tras un Invoke un cache miente (medido)."""
    drv._TREE = None
    index = drv.tree(win)
    return index


def state(win):
    index = present(win)

    def has(aid):
        return aid in index

    def el(aid):
        return index[aid][0] if aid in index else None

    und = el("ControlBarUndoButton")
    red = el("ControlBarRedoButton")
    labels = {}
    for aid in BAR:
        e = el(aid)
        if e is not None:
            try:
                labels[aid] = (e.element_info.name or "").strip()
            except Exception:
                labels[aid] = "?"
    return {
        "barPresentes": [a for a in BAR if has(a)],
        "cajonPresentes": [a for a in DRAWER if has(a)],
        "ajustesPresentes": [a for a in SETTINGS_ANCHORS if has(a)],
        "deshacerHabilitado": und.is_enabled() if und is not None else None,
        "rehacerHabilitado": red.is_enabled() if red is not None else None,
        "etiquetas": labels,
    }


def main():
    if "--session" not in sys.argv:
        drv.log("[menu] nada que hacer: usa --session")
        return 2

    os.makedirs(sess.SHOTS, exist_ok=True)
    win = drv.connect()
    if win is None:
        drv.log("[menu] FALLO: la app no aparece por UIA")
        return 3

    hwnd = int(json.load(open(sess.STATE))["hwnd"])
    results = []

    def check(condition, what, detail=""):
        results.append(bool(condition))
        drv.log("[menu] %s %s%s" % ("[OK]   " if condition else "[FALLO]", what,
                                    ("  <- " + detail) if detail else ""))
        return bool(condition)

    # ── 0. La linea base: la barra tal y como el usuario la encuentra al abrir ──
    before = state(win)
    img0 = sess.grab(hwnd, "10_barra_base")
    c0 = dominant(img0, CENTER)
    r0 = dominant(img0, RIGHT)
    drv.log("[menu] base: barra=%d/14 cajon=%d/5 ajustes=%d/6 deshacer=%s rehacer=%s"
            % (len(before["barPresentes"]), len(before["cajonPresentes"]),
               len(before["ajustesPresentes"]), before["deshacerHabilitado"], before["rehacerHabilitado"]))
    drv.log("[menu] base: etiquetas=%s" % json.dumps(before["etiquetas"], ensure_ascii=False))
    drv.log("[menu] base: pixel centro=%s (%.1f%%) derecha=%s (%.1f%%)"
            % (hexs(c0[0]), 100 * c0[1], hexs(r0[0]), 100 * r0[1]))
    check(set(before["barPresentes"]) == set(BAR) - set(RESTING_ONLY),
          "en reposo la barra expone 10 de sus 14 entradas, y las 4 que faltan son EXACTAMENTE las del "
          "contexto (Paso/Continuar son de la depuracion; Pausar/Detener, del ciclo en marcha)",
          "presentes: %s" % ", ".join(before["barPresentes"]))
    check(all(a not in before["barPresentes"] for a in RESTING_ONLY),
          "las 4 entradas de contexto NO estan en el arbol mientras no hay ejecucion: la barra no dibuja "
          "botones que no pueden hacer nada")
    check(before["cajonPresentes"] == [],
          "con el menu recogido, las entradas del cajon NO estan en el arbol")

    # ── 1. «Menu»: el cajon se despliega por el MISMO estado del view model ──
    menu = drv.by_aid(win, "ControlBarMenuButton")
    assert menu is not None
    drv.invoke(menu)
    time.sleep(STEP)
    opened = state(win)
    img1 = sess.grab(hwnd, "11_menu_desplegado")
    c1 = dominant(img1, CENTER)
    drv.log("[menu] tras «Menu»: cajon=%d/5 pixel centro=%s (%.1f%%)"
            % (len(opened["cajonPresentes"]), hexs(c1[0]), 100 * c1[1]))
    check(set(opened["cajonPresentes"]) == set(DRAWER),
          "pulsar «Menu» despliega el cajon: sus 5 entradas ancladas aparecen en el arbol")
    check(c1[0] != c0[0],
          "y el VELO del cajon se ve en el pixel: la banda central del lienzo cambia de color dominante",
          "%s -> %s" % (hexs(c0[0]), hexs(c1[0])))

    # ── 2. La entrada «Ajustes» del cajon abre la MISMA superficie del host ──
    settings = drv.by_aid(win, "ControlBarDrawerSettingsButton")
    if settings is not None:
        drv.invoke(settings)
        time.sleep(STEP + 0.8)
        with_settings = state(win)
        sess.grab(hwnd, "12_ajustes_desde_el_cajon")
        drv.log("[menu] tras «Ajustes» del cajon: ajustes=%d/6" % len(with_settings["ajustesPresentes"]))
        check(len(with_settings["ajustesPresentes"]) == len(SETTINGS_ANCHORS),
              "la entrada «Ajustes» del cajon abre la superficie de ajustes del host (sus 6 anclas en el arbol)")
        close = drv.by_aid(win, "SettingsCloseButton")
        if close is not None:
            drv.invoke(close)
            time.sleep(STEP)
    else:
        check(False, "la entrada «Ajustes» del cajon no esta en el arbol", "el cajon no se desplego")

    # ── 3. El boton de cerrar del cajon lo recoge y la escena vuelve ──
    close_drawer = drv.by_aid(win, "ControlBarDrawerCloseButton")
    if close_drawer is not None:
        drv.invoke(close_drawer)
        time.sleep(STEP)
    closed = state(win)
    img2 = sess.grab(hwnd, "13_menu_recogido")
    c2 = dominant(img2, CENTER)
    drv.log("[menu] tras cerrar el cajon: cajon=%d/5 pixel centro=%s (%.1f%%)"
            % (len(closed["cajonPresentes"]), hexs(c2[0]), 100 * c2[1]))
    check(closed["cajonPresentes"] == [], "el boton de cerrar del cajon lo recoge (sus entradas salen del arbol)")
    check(c2[0] == c0[0],
          "y el pixel central vuelve al de la linea base: la escena queda como estaba",
          "%s -> %s" % (hexs(c1[0]), hexs(c2[0])))

    # ── 4. El INSPECTOR: conmuta la columna derecha del marco (cambio de pixel) ──
    inspector = drv.by_aid(win, "ControlBarInspectorButton")
    if inspector is not None:
        # La medida fuerte: que fraccion de la columna derecha es LIENZO. Con el inspector abierto su
        # banda es del panel (fraccion baja); recogido, la columna pasa a ser lienzo (fraccion alta).
        canvas = c0[0]
        share_open = share_of(img0, RIGHT, canvas)
        drv.invoke(inspector)
        time.sleep(STEP + 0.6)
        img3 = sess.grab(hwnd, "14_inspector_oculto")
        r3 = dominant(img3, RIGHT)
        share_hidden = share_of(img3, RIGHT, canvas)
        drv.log("[menu] tras «Inspector»: pixel derecha=%s (%.1f%%) lienzo en la banda=%.1f%%"
                % (hexs(r3[0]), 100 * r3[1], 100 * share_hidden))
        check(share_hidden > 0.70 > share_open,
              "pulsar el Inspector recoge la columna derecha: su banda pasa a ser lienzo"
              " (abierto %.1f%% de lienzo -> recogido %.1f%%)" % (100 * share_open, 100 * share_hidden))
        drv.invoke(inspector)
        time.sleep(STEP + 0.6)
        img4 = sess.grab(hwnd, "15_inspector_vuelto")
        r4 = dominant(img4, RIGHT)
        share_back = share_of(img4, RIGHT, canvas)
        drv.log("[menu] e insistiendo: pixel derecha=%s (%.1f%%) lienzo en la banda=%.1f%%"
                % (hexs(r4[0]), 100 * r4[1], 100 * share_back))
        check(abs(share_back - share_open) < 0.10,
              "y volver a pulsarlo devuelve la columna derecha a su estado de la linea base"
              " (%.1f%% -> %.1f%% -> %.1f%% de lienzo)"
              % (100 * share_open, 100 * share_hidden, 100 * share_back))

    # ── 5. El estado por contexto: Deshacer y Rehacer los dice el editor ──
    drv.log("[menu] estado por contexto: deshacer=%s rehacer=%s (CanUndo/CanRedo del editor portable)"
            % (closed["deshacerHabilitado"], closed["rehacerHabilitado"]))
    check(closed["deshacerHabilitado"] == False and closed["rehacerHabilitado"] == False,  # noqa: E712
          "sin acciones en el lienzo, Deshacer y Rehacer llegan DESHABILITADOS al canal externo")
    undo = drv.by_aid(win, "ControlBarUndoButton")
    if undo is not None:
        refused = not drv.invoke(undo)
        drv.log("[menu] invocar «Deshacer» deshabilitado: %s"
                % ("rechazado por el control" if refused else "ACEPTADO (no deberia)"))
        check(refused, "el control deshabilitado RECHAZA la orden: no es un boton que no hace nada, es uno que dice que no")

    # ── 6. El Modo Prueba: la casilla conmuta y se devuelve a como estaba ──
    toggle = drv.by_aid(win, "ControlBarDryRunToggle")
    if toggle is not None:
        pattern = drv.iface(toggle, "iface_toggle")
        if pattern is not None:
            was = int(pattern.CurrentToggleState)
            try:
                pattern.Toggle()
            except Exception:
                pass
            time.sleep(0.8)
            now = int(pattern.CurrentToggleState)
            drv.log("[menu] Modo Prueba: %d -> %d" % (was, now))
            check(now != was, "la casilla «Modo Prueba» conmuta por el patron del control (TogglePattern)")
            if now != was:
                try:
                    pattern.Toggle()
                except Exception:
                    pass
                time.sleep(0.8)
                check(int(pattern.CurrentToggleState) == was, "y se devuelve a su estado original")

    # ── 7. Cierre: la escena queda como al entrar ──
    img5 = sess.grab(hwnd, "99_final")
    c5 = dominant(img5, CENTER)
    r5 = dominant(img5, RIGHT)
    drv.log("[menu] final: pixel centro=%s (%.1f%%) derecha=%s (%.1f%%)"
            % (hexs(c5[0]), 100 * c5[1], hexs(r5[0]), 100 * r5[1]))
    check(c5[0] == c0[0] and r5[0] == r0[0],
          "la sesion termina con la escena igual a la de la linea base (centro y derecha)",
          "centro %s/%s derecha %s/%s" % (hexs(c0[0]), hexs(c5[0]), hexs(r0[0]), hexs(r5[0])))
    check((share_of(img5, RIGHT, c0[0]) < 0.70) == (share_of(img0, RIGHT, c0[0]) < 0.70),
          "y la columna derecha termina con el inspector otra vez dentro (banda del panel, no lienzo)",
          "lienzo %.1f%% -> %.1f%%" % (100 * share_of(img0, RIGHT, c0[0]),
                                       100 * share_of(img5, RIGHT, c0[0])))

    ok = all(results)
    drv.log("[menu] === RESULTADO: %s (%d de %d pasos) ==="
            % ("VERIFICADO" if ok else "FALLOS", sum(results), len(results)))
    with open(os.path.join(sess.SHOTS, "menu-session.json"), "w", encoding="utf-8") as fh:
        json.dump({"pasos": len(results), "verificados": sum(results),
                   "base": {"centro": hexs(c0[0]), "derecha": hexs(r0[0]),
                            "barra": before["barPresentes"], "etiquetas": before["etiquetas"]}},
                  fh, ensure_ascii=False, indent=2)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
