# -*- coding: utf-8 -*-
"""Sesion de las VENTANAS nuevas del host Uno (hito 259), con DRIVER EXTERNO.

El reparto es el de siempre (255/257/258/259): quien ACTUA es este driver —otro proceso pulsa los
controles REALES por sus AutomationId— y quien MIDE es el propio driver: las anclas de automatizacion
del arbol, el TEXTO que expone el canal externo y el pixel dominante de una banda de la captura.

Que se ejercita, y por que cada paso mide lo que mide:

  - el CAJON con sus 14 entradas (las 11 de los hitos anteriores + las 3 de VENTANA que estrena este:
    Estudio de Temas, Metricas y VFS);
  - «ESTUDIO DE TEMAS» (orden canonica del nucleo -> ventana servida por el host): la superficie aparece
    con sus anclas, el cajon se recoge al elegir la entrada, el NOMBRE del tema en edicion se lee por
    UIA y el pixel central cambia con el modal;
  - «METRICAS Y RENDIMIENTO»: su superficie aparece con sus anclas, la tabla expone una fila por nodo
    del lienzo y el pie dice cuantos nodos analizo (leido por UIA);
  - el CIERRE de cada una por su propio boton: sus anclas salen del arbol y el pixel vuelve al de la
    linea base.

Nada de esto escribe preferencias: se comprueba el md5 del fichero del usuario antes y despues.

Uso (con la app ya lanzada por el instrumento, que maximiza y escribe session_state.json):
  FILEFLOW_QA_WORK=qa-manual-270 python qa_manual_session.py --launch
  FILEFLOW_QA_WORK=qa-manual-270 python qa_menu3_uia.py --session

Salida: una linea por paso (`[menu3] ...`) con lo medido antes y despues, capturas rotuladas en la
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
import qa_manual_session as sess  # noqa: E402
import qa_menu_uia as menu  # noqa: E402
import qa_dialogs_uia as dlg  # noqa: E402  (by_aid/elements sin cache: la lisa miente tras un Invoke)

STEP = 1.6
CENTER = menu.CENTER
CANVAS = dlg.CANVAS
PREFS = dlg.PREFS

# Las 14 entradas del cajon: las 11 de los hitos 257/258 y las 3 que estrena este.
DRAWER_OLD = ("ControlBarThemeCombo", "ControlBarLanguageCombo", "ControlBarDrawerSettingsButton",
              "ControlBarDrawerInspectorButton", "ControlBarDrawerCloseButton",
              "ControlBarDrawerNewButton", "ControlBarDrawerLoadButton", "ControlBarDrawerSaveButton",
              "ControlBarDrawerManualButton", "ControlBarDrawerExamplesButton",
              "ControlBarDrawerAboutButton")
DRAWER_NEW = ("ControlBarDrawerThemeStudioButton", "ControlBarDrawerMetricsButton",
              "ControlBarDrawerVfsButton")
DRAWER = DRAWER_OLD + DRAWER_NEW

# Las anclas de cada superficie QUE EL CANAL EXTERNO PUEDE VER. WinUI solo expone peer de
# automatizacion en los CONTROLES que lo declaran (botones, cajas, listas, textos), no en los
# contenedores: el ancla del propio cuerpo —«ThemeStudioBody», «MetricsDashboardBody», puesta en su
# rejilla raiz— la lee la sonda EN PROCESO, y aqui se miden las que un lector de pantalla alcanza.
STUDIO_ANCHORS = ("ThemeStudioThemeList", "ThemeStudioNameBox", "ThemeStudioStatusText",
                  "ThemeStudioApplyButton", "ThemeStudioSaveButton", "ThemeStudioCloseButton")
METRICS_ANCHORS = ("MetricsTotalDuration", "MetricsNodeList", "MetricsStatusText",
                   "MetricsCloseButton")

# Las anclas de CONTENEDOR (sin peer): se declaran para dejar escrito que existen y donde se leen.
STUDIO_CONTAINER_ANCHORS = ("ThemeStudioBody", "ThemeStudioSections")
METRICS_CONTAINER_ANCHORS = ("MetricsDashboardBody",)

# El pie del panel de metricas (clave Metrics_NodesProfiled del diccionario, copiada del escritorio).
METRICS_HINT = "nodos analizados"

RESULT = os.path.join(sess.SHOTS, "menu3-session.json")


def md5(path):
    if not os.path.exists(path):
        return None
    with open(path, "rb") as fh:
        return hashlib.md5(fh.read()).hexdigest()


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
    return [(name, el) for name, el, _ in dlg.elements(win) if pred(name)]


def text_like(win, needle):
    """El primer texto del arbol que contiene ese fragmento (lo que un lector de pantalla leeria)."""
    for name, _ in names(win, lambda n: needle.lower() in n.lower()):
        return name
    return ""


def rows_of(win, aid):
    """Los hijos que el canal externo ve de una lista (sus filas reales, no su contenedor)."""
    el = dlg.by_aid(win, aid)
    if el is None:
        return []
    try:
        return [child.window_text() for child in el.children()]
    except Exception:
        return []


def press(win, aid, wait=STEP):
    el = dlg.by_aid(win, aid)
    if el is None:
        return False
    drv.invoke(el)
    time.sleep(wait)
    return True


def main():
    if "--session" not in sys.argv:
        drv.log("[menu3] nada que hacer: usa --session")
        return 2

    os.makedirs(sess.SHOTS, exist_ok=True)
    win = drv.connect()
    if win is None:
        drv.log("[menu3] FALLO: la app no aparece por UIA")
        return 3

    hwnd = int(json.load(open(sess.STATE))["hwnd"])
    results = []
    prefs_before = md5(PREFS)

    def check(condition, what, detail=""):
        results.append(bool(condition))
        drv.log("[menu3] %s %s%s" % ("[OK]   " if condition else "[FALLO]", what,
                                     ("  <- " + detail) if detail else ""))
        return bool(condition)

    def cards():
        titles = ("Folder Source", "Optimizador de Imágenes", "Destination Sink")
        return len(dlg.elements(win, lambda n: n in titles, CANVAS))

    drv.log("[menu3] preferencias del usuario: md5=%s" % prefs_before)

    # ── 0. Linea base: la barra en reposo y el lienzo del ejemplo ──
    img0 = sess.grab(hwnd, "60_base")
    c0 = menu.dominant(img0, CENTER)
    base_anchors = aid_set(win)
    base_cards = cards()
    drv.log("[menu3] base: tarjetas=%d pixel centro=%s (%.1f%%) anclas nuevas del cajon=%d/3"
            % (base_cards, menu.hexs(c0[0]), 100 * c0[1],
               len([a for a in DRAWER_NEW if a in base_anchors])))
    check(base_cards == 3, "el lienzo del ejemplo arranca con sus 3 tarjetas",
          "tarjetas=%d" % base_cards)
    check(not any(a in base_anchors for a in DRAWER),
          "con el menu recogido, las entradas del cajon NO estan en el arbol")

    # ── 1. El cajon con sus 14 entradas ──
    check(press(win, "ControlBarMenuButton"), "el boton «Menu» se pulsa")
    opened = aid_set(win)
    img1 = sess.grab(hwnd, "61_cajon_abierto")
    c1 = menu.dominant(img1, CENTER)
    missing = [a for a in DRAWER if a not in opened]
    drv.log("[menu3] cajon abierto: entradas=%d/14 pixel centro=%s (%.1f%%)"
            % (len([a for a in DRAWER if a in opened]), menu.hexs(c1[0]), 100 * c1[1]))
    check(not missing, "el cajon expone sus 14 entradas, incluidas las 3 que estrena el hito",
          ("faltan: %s" % ", ".join(missing)) if missing else "")
    check(c1[0] != c0[0], "y su velo se ve en el pixel (la banda central cambia de dominante)",
          "%s -> %s" % (menu.hexs(c0[0]), menu.hexs(c1[0])))

    # ── 2. «Estudio de Temas»: la ventana servida por el catalogo de dialogos del host ──
    check(press(win, "ControlBarDrawerThemeStudioButton", STEP + 1.2),
          "la entrada «Estudio de Temas» esta en el arbol y se pulsa")
    studio_anchors = aid_set(win)
    img2 = sess.grab(hwnd, "62_estudio_de_temas")
    c2 = menu.dominant(img2, CENTER)
    studio_name = text_like(win, "Theme") or text_like(win, "Tema")
    theme_rows = rows_of(win, "ThemeStudioThemeList")
    container_seen = [a for a in STUDIO_CONTAINER_ANCHORS if a in studio_anchors]
    drv.log("[menu3] estudio: anclas=%s pixel centro=%s (%.1f%%) temas=%d %s nombre=%r"
            % (sorted(a for a in studio_anchors if a in STUDIO_ANCHORS), menu.hexs(c2[0]),
               100 * c2[1], len(theme_rows), json.dumps(theme_rows[:2], ensure_ascii=False), studio_name))
    present = [a for a in STUDIO_ANCHORS if a in studio_anchors]
    check(len(present) == len(STUDIO_ANCHORS),
          "pulsar la entrada abre el ESTUDIO DE TEMAS del host: su catalogo, su editor, su estado y sus ordenes",
          "presentes: %s" % ", ".join(present))
    check(len(theme_rows) > 1,
          "y su catalogo NO esta vacio: la lista expone los temas del nucleo, leidos por el canal externo",
          "%d temas: %s" % (len(theme_rows), ", ".join(r.split("\n")[0][:28] for r in theme_rows[:4])))
    check(not any(a in studio_anchors for a in DRAWER),
          "y elegir la entrada recoge el cajon (la orden del nucleo cierra el menu)")
    check(c2[0] != c0[0], "el estudio se ve en el pixel central",
          "%s -> %s" % (menu.hexs(c0[0]), menu.hexs(c2[0])))

    check(press(win, "ThemeStudioCloseButton", STEP), "el estudio se cierra por su propio boton")
    closed_anchors = aid_set(win)
    img3 = sess.grab(hwnd, "63_estudio_cerrado")
    c3 = menu.dominant(img3, CENTER)
    drv.log("[menu3] tras cerrar el estudio: pixel centro=%s (%.1f%%) anclas=%s"
            % (menu.hexs(c3[0]), 100 * c3[1],
               sorted(a for a in closed_anchors if a in STUDIO_ANCHORS)))
    check(not any(a in closed_anchors for a in STUDIO_ANCHORS),
          "y su superficie sale del arbol: no queda ninguna ancla suya")
    check(c3[0] == c0[0], "el pixel central vuelve al de la linea base",
          "%s -> %s -> %s" % (menu.hexs(c0[0]), menu.hexs(c2[0]), menu.hexs(c3[0])))

    # ── 3. «Metricas y Rendimiento»: la segunda ventana nueva ──
    check(press(win, "ControlBarMenuButton"), "el cajon se vuelve a desplegar para la entrada que queda")
    check(press(win, "ControlBarDrawerMetricsButton", STEP + 1.2),
          "la entrada «Metricas y Rendimiento» esta en el arbol y se pulsa")
    metrics_anchors = aid_set(win)
    img4 = sess.grab(hwnd, "64_metricas")
    c4 = menu.dominant(img4, CENTER)
    metrics_status = text_like(win, METRICS_HINT)
    node_rows = rows_of(win, "MetricsNodeList")
    drv.log("[menu3] metricas: anclas=%s pixel centro=%s (%.1f%%) filas=%d pie=%r"
            % (sorted(a for a in metrics_anchors if a in METRICS_ANCHORS), menu.hexs(c4[0]),
               100 * c4[1], len(node_rows), metrics_status))
    present_metrics = [a for a in METRICS_ANCHORS if a in metrics_anchors]
    check(len(present_metrics) == len(METRICS_ANCHORS),
          "pulsar la entrada abre el PANEL DE METRICAS del host: su tabla, sus numeros y su cierre",
          "presentes: %s" % ", ".join(present_metrics))
    check(len(node_rows) == 3,
          "y su tabla tiene UNA FILA POR NODO del lienzo (3 en el ejemplo), leidas por el canal externo",
          "%d filas: %s" % (len(node_rows), "; ".join(r.split("\n")[0][:34] for r in node_rows[:4])))
    check(METRICS_HINT in metrics_status,
          "y su pie dice cuantos nodos analizo, leido por el canal externo", repr(metrics_status))
    check(c4[0] != c0[0], "el panel se ve en el pixel central",
          "%s -> %s" % (menu.hexs(c0[0]), menu.hexs(c4[0])))

    check(press(win, "MetricsCloseButton", STEP), "el panel se cierra por su propio boton")
    closed2 = aid_set(win)
    img5 = sess.grab(hwnd, "65_metricas_cerrado")
    c5 = menu.dominant(img5, CENTER)
    drv.log("[menu3] tras cerrar el panel: pixel centro=%s (%.1f%%) anclas=%s"
            % (menu.hexs(c5[0]), 100 * c5[1], sorted(a for a in closed2 if a in METRICS_ANCHORS)))
    check(not any(a in closed2 for a in METRICS_ANCHORS),
          "y su superficie sale del arbol")
    check(c5[0] == c0[0], "el pixel central vuelve al de la linea base",
          "%s -> %s -> %s" % (menu.hexs(c0[0]), menu.hexs(c4[0]), menu.hexs(c5[0])))

    # ── 4. Cierre: la escena y tus preferencias, como al entrar ──
    img6 = sess.grab(hwnd, "99_final")
    c6 = menu.dominant(img6, CENTER)
    prefs_after = md5(PREFS)
    drv.log("[menu3] final: tarjetas=%d pixel centro=%s (%.1f%%) preferencias md5=%s"
            % (cards(), menu.hexs(c6[0]), 100 * c6[1], prefs_after))
    check(c6[0] == c0[0], "la sesion termina con la escena igual a la de la linea base",
          "centro %s/%s" % (menu.hexs(c0[0]), menu.hexs(c6[0])))
    check(cards() == base_cards, "y el lienzo con las mismas tarjetas", "%d" % cards())
    check(prefs_after == prefs_before,
          "el fichero de preferencias del usuario queda byte-identico (las ventanas de consulta no escriben)",
          "%s / %s" % (prefs_before, prefs_after))

    ok = all(results)
    drv.log("[menu3] === RESULTADO: %s (%d de %d pasos) ==="
            % ("VERIFICADO" if ok else "FALLOS", sum(results), len(results)))
    with open(RESULT, "w", encoding="utf-8") as fh:
        json.dump({"pasos": len(results), "verificados": sum(results),
                   "tarjetas": base_cards,
                   "anclas": {"estudio": present, "metricas": present_metrics},
                   "contenedores_sin_peer": {"estudio": container_seen},
                   "filas": {"temas": len(theme_rows), "nodos": len(node_rows), "nodos_detalle": node_rows},
                   "textos": {"nombre_tema": studio_name, "pie_metricas": metrics_status},
                   "pixel_centro": {"base": menu.hexs(c0[0]), "cajon": menu.hexs(c1[0]),
                                    "estudio": menu.hexs(c2[0]), "metricas": menu.hexs(c4[0]),
                                    "final": menu.hexs(c6[0])},
                   "preferencias_md5": {"antes": prefs_before, "despues": prefs_after}},
                  fh, ensure_ascii=False, indent=2)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
