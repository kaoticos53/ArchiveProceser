# -*- coding: utf-8 -*-
"""Sesion del EDITOR DE URLs POR MODELO en la app abierta (hito 262).

El reparto es el de siempre (255/257/258/259/260/261): quien ACTUA es este driver —otro proceso pulsa los
controles REALES por su AutomationId— y quien MIDE es el propio driver: las anclas de automatizacion del
arbol, el TEXTO que expone el canal externo y el pixel dominante de una banda de la captura.

Que se ejercita, y por que cada paso mide lo que mide:

  - se pulsa la ACCION DE URLs de la primera FILA del catalogo de modelos (el boton dibujado, no el comando
    por su cuenta): es el punto de entrada que el tramo anterior dejo declarado como ausente;
  - el EDITOR del host aparece y se comprueba que habla del MISMO modelo que la fila pulsada;
  - se ESCRIBE una URL en su caja y se lee el RECUENTO del propio editor: la cifra la calcula el view model
    portable, asi que si cambia, lo tecleado llego al view model (y no solo al control);
  - se pulsa GUARDAR y se comprueba que el modal se cierra;
  - se vuelve a abrir el editor del MISMO modelo y se lee su DISTINTIVO: «Personalizado» significa que el
    valor quedo escrito DONDE LO ESCRIBE EL ESCRITORIO (el almacen del gestor del nucleo, que es el que lee
    el motor de descargas). Un editor que se cerrara sin escribir dejaria el distintivo como estaba;
  - se RESTABLECE lo que habia y se comprueba que el distintivo vuelve al de la linea base: la sesion mide,
    no configura.

Nada de esto escribe preferencias: se comprueba el md5 del fichero del usuario antes y despues.

Uso (con la app ya lanzada por el instrumento, que maximiza y escribe session_state.json):
  FILEFLOW_QA_WORK=qa-manual-272 python qa_manual_session.py --launch
  FILEFLOW_QA_WORK=qa-manual-272 python qa_urls_uia.py --session

Salida: una linea por paso (`[urls] ...`) con lo medido antes y despues, capturas rotuladas en la carpeta
de la sesion, y exit 0 solo si todos los pasos salieron como dicen.
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
import qa_dialogs_uia as dlg  # noqa: E402

STEP = 1.6
CENTER = menu.CENTER
PREFS = dlg.PREFS

# Las anclas del EDITOR que el canal externo puede ver (los controles del cuerpo; el AutomationId del
# contenedor lo lee la sonda EN PROCESO, porque WinUI no da peer a las rejillas).
URLS_ANCHORS = ("AiModelUrlsModelName", "AiModelUrlsTextBox", "AiModelUrlsCountText",
                "AiModelUrlsStatusBadge", "AiModelUrlsTestButton", "AiModelUrlsResetButton",
                "AiModelUrlsCategory")

ROW_ACTION = "SettingsAiModelUrlsButton"
MODELS_LIST = "SettingsAiModelsList"
MODELS_TAB = "SettingsTabAiModels"
PROBE_URL = "https://probe.invalid/fileflow.session.model.onnx"
PROBE_URL_2 = "https://probe.invalid/fileflow.session.mirror.onnx"

RESULT = os.path.join(sess.SHOTS, "urls-session.json")


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


def text_of(win, aid):
    """El texto que el canal externo lee de un control por su ancla (y el de sus descendientes)."""
    el = dlg.by_aid(win, aid)
    if el is None:
        return ""
    parts = []

    def collect(node, depth=0):
        if depth > 3:
            return
        try:
            own = node.window_text()
        except Exception:
            own = ""
        if own and own not in parts:
            parts.append(own)
        try:
            children = node.children()
        except Exception:
            return
        for child in children:
            collect(child, depth + 1)

    collect(el)
    return " | ".join(parts)


def rows_of(win, aid):
    """Los hijos que el canal externo ve de una lista (sus filas reales, no su contenedor)."""
    el = dlg.by_aid(win, aid)
    if el is None:
        return []
    try:
        return [child.window_text() for child in el.children()]
    except Exception:
        return []


def first_row_name(win):
    """El nombre del modelo de la PRIMERA fila del catalogo, leido del canal externo.

    No vale `window_text()` de la fila: el contenedor de una fila enlazada devuelve el nombre del TIPO del
    view model ('FileFlow.App.ViewModels.AiModelItemViewModel'), no lo que se ve. El nombre del modelo vive
    en el `AutomationProperties.Name` del panel de la fila, asi que se busca el primer nombre que parezca un
    nombre y no un tipo .NET (la leccion de esta misma sesion).
    """
    el = dlg.by_aid(win, MODELS_LIST)
    if el is None:
        return ""
    try:
        first = el.children()[0]
    except Exception:
        return ""
    try:
        nodes = [first] + list(first.descendants())
    except Exception:
        nodes = [first]
    for node in nodes:
        try:
            name = (node.element_info.name or "").strip()
        except Exception:
            continue
        if not name or "." in name or name.startswith("System"):
            continue
        return name
    return ""


def press(win, aid, wait=STEP):
    el = dlg.by_aid(win, aid)
    if el is None:
        return False
    drv.invoke(el)
    time.sleep(wait)
    return True


def select(win, aid, wait=STEP):
    """Selecciona un control de SELECCION (las pestanas son RadioButton: su patron es SelectionItem)."""
    el = dlg.by_aid(win, aid)
    if el is None:
        return False
    drv.select_item(el)
    time.sleep(wait)
    return True


def press_primary(win, wait=STEP):
    """El boton PRIMARIO del modal abierto: el de guardar. Se busca por su texto (el ContentDialog no le da
    AutomationId), igual que lo hacen las otras sesiones de diálogos."""
    for name, el, _ in dlg.elements(win, lambda n: n.startswith("💾") or "Guardar" in n):
        drv.invoke(el)
        time.sleep(wait)
        return name
    return ""


def press_close_button(win, wait=STEP):
    """El boton de CIERRE del modal: «Cancelar» (o «Cerrar»)."""
    for name, el, _ in dlg.elements(win, lambda n: n in ("Cancelar", "Cerrar", "Cancel", "Close")):
        drv.invoke(el)
        time.sleep(wait)
        return name
    return ""


def main():
    if "--session" not in sys.argv:
        drv.log("[urls] nada que hacer: usa --session")
        return 2

    os.makedirs(sess.SHOTS, exist_ok=True)
    win = drv.connect()
    if win is None:
        drv.log("[urls] FALLO: la app no aparece por UIA")
        return 3

    hwnd = int(json.load(open(sess.STATE))["hwnd"])
    results = []
    prefs_before = md5(PREFS)

    def check(condition, what, detail=""):
        results.append(bool(condition))
        drv.log("[urls] %s %s%s" % ("[OK]   " if condition else "[FALLO]", what,
                                    ("  <- " + detail) if detail else ""))
        return bool(condition)

    def cards():
        titles = ("Folder Source", "Optimizador de Imágenes", "Destination Sink")
        return len(dlg.elements(win, lambda n: n in titles, dlg.CANVAS))

    drv.log("[urls] preferencias del usuario: md5=%s" % prefs_before)

    # ── 0. Linea base ──
    img0 = sess.grab(hwnd, "90_base")
    c0 = menu.dominant(img0, CENTER)
    base_cards = cards()
    drv.log("[urls] base: tarjetas=%d pixel centro=%s (%.1f%%)"
            % (base_cards, menu.hexs(c0[0]), 100 * c0[1]))
    check(base_cards == 3, "el lienzo arranca con sus 3 tarjetas", "tarjetas=%d" % base_cards)

    # ── 1. La pestaña de modelos de IA ──
    check(press(win, "SettingsButton", STEP + 1.0), "el boton de ajustes de la barra se pulsa")
    check(select(win, MODELS_TAB, STEP + 0.8), "la pestaña «Modelos de IA» se selecciona")
    # El pixel de la superficie de AJUSTES abierta: el modal del editor se abre ENCIMA, asi que al cerrarse
    # lo que tiene que volver es ESTE, no el del lienzo (la superficie sigue abierta detras, como la dejo el
    # usuario). Comparar contra el lienzo mediria mal el producto.
    img_s = sess.grab(hwnd, "90b_ajustes_modelos")
    c_settings = menu.dominant(img_s, CENTER)
    model_rows = rows_of(win, MODELS_LIST)
    first_row = first_row_name(win)
    drv.log("[urls] catalogo: %d filas, la primera es %r (pixel de la superficie abierta=%s)"
            % (len(model_rows), first_row, menu.hexs(c_settings[0])))
    check(bool(first_row), "el catalogo de modelos expone sus filas al canal externo", first_row)

    # ── 2. La ACCION de URLs de la primera fila: el punto de entrada ──
    check(press(win, ROW_ACTION, STEP + 1.2),
          "la accion de URLs de la primera fila se pulsa (el boton dibujado, no el comando por su cuenta)")
    opened = aid_set(win)
    img1 = sess.grab(hwnd, "91_editor_urls")
    c1 = menu.dominant(img1, CENTER)
    present = [a for a in URLS_ANCHORS if a in opened]
    model_shown = text_of(win, "AiModelUrlsModelName")
    badge_before = text_of(win, "AiModelUrlsStatusBadge")
    count_before = text_of(win, "AiModelUrlsCountText")
    original_urls = dlg.value_of(dlg.by_aid(win, "AiModelUrlsTextBox")) or ""
    drv.log("[urls] editor: anclas=%s pixel centro=%s (%.1f%%) modelo=%r distintivo=%r recuento=%r"
            % (present, menu.hexs(c1[0]), 100 * c1[1], model_shown, badge_before, count_before))
    check(len(present) == len(URLS_ANCHORS),
          "pulsar la accion abre el EDITOR DE URLs del host: caja, recuento, distintivo, probar y restablecer",
          "presentes: %s" % ", ".join(present))
    check(model_shown.strip() == first_row,
          "y el editor habla del MISMO modelo que la fila pulsada", "%r / fila=%r" % (model_shown, first_row))
    check(c1[0] != c0[0], "el editor se ve en el pixel central",
          "%s -> %s" % (menu.hexs(c0[0]), menu.hexs(c1[0])))
    check(original_urls.strip() != "",
          "y se abre con las URLs que el gestor tiene configuradas para ese modelo", repr(original_urls[:70]))

    # ── 3. Escribir: el recuento del PROPIO editor tiene que seguirlo ──
    # Se escriben DOS urls (no una) a proposito: el recuento inicial tambien es «1 URL(s)», asi que escribir
    # una sola no distinguiria entre «el view model lo recalculo» y «el rotulo sigue con lo de antes».
    typed = PROBE_URL + "\n" + PROBE_URL_2
    dlg.set_value(dlg.by_aid(win, "AiModelUrlsTextBox"), typed)
    time.sleep(STEP)
    count_after = text_of(win, "AiModelUrlsCountText")
    drv.log("[urls] tras teclear: recuento=%r" % count_after)
    check(count_after.strip() == "2 URL(s)",
          "lo tecleado llega al VIEW MODEL portable: su recuento pasa de %r a %r" % (count_before, count_after))

    # ── 4. Guardar ──
    saved = press_primary(win, STEP + 1.2)
    closed = aid_set(win)
    img2 = sess.grab(hwnd, "92_guardado")
    c2 = menu.dominant(img2, CENTER)
    drv.log("[urls] tras guardar (%r): pixel centro=%s (%.1f%%) anclas=%s"
            % (saved, menu.hexs(c2[0]), 100 * c2[1], sorted(a for a in closed if a in URLS_ANCHORS)))
    check(saved != "", "el editor tiene su boton de guardar en el arbol", repr(saved))
    check(not any(a in closed for a in URLS_ANCHORS), "y guardar cierra el modal")
    check(c2[0] == c_settings[0],
          "el pixel central vuelve al de la superficie que sigue abierta detras (los ajustes)",
          "%s -> %s -> %s" % (menu.hexs(c_settings[0]), menu.hexs(c1[0]), menu.hexs(c2[0])))

    # ── 5. El valor quedo ESCRITO: se reabre el editor del mismo modelo y se lee su distintivo ──
    check(press(win, ROW_ACTION, STEP + 1.2),
          "la misma accion se vuelve a pulsar (segunda vuelta, para medir donde quedo lo escrito)")
    img3 = sess.grab(hwnd, "93_reabierto_personalizado")
    c3 = menu.dominant(img3, CENTER)
    badge_after = text_of(win, "AiModelUrlsStatusBadge")
    urls_after = dlg.value_of(dlg.by_aid(win, "AiModelUrlsTextBox")) or ""
    drv.log("[urls] reabierto: distintivo=%r caja=%r pixel centro=%s"
            % (badge_after, urls_after, menu.hexs(c3[0])))
    check(PROBE_URL in urls_after,
          "el editor reabre con la URL escrita: el cambio quedo guardado en el gestor", repr(urls_after[:80]))
    check("🔧" in badge_after and badge_after.strip() != badge_before.strip(),
          "y su distintivo pasa de %r a %r: la configuracion del modelo es ahora PERSONALIZADA"
          % (badge_before, badge_after))

    # ── 6. Restaurar: la sesion mide, no configura ──
    dlg.set_value(dlg.by_aid(win, "AiModelUrlsTextBox"), original_urls)
    time.sleep(STEP)
    press_primary(win, STEP + 1.2)
    check(press(win, ROW_ACTION, STEP + 1.2), "se reabre una tercera vez para comprobar la restauracion")
    img4 = sess.grab(hwnd, "94_restaurado")
    c4 = menu.dominant(img4, CENTER)
    badge_restored = text_of(win, "AiModelUrlsStatusBadge")
    urls_restored = dlg.value_of(dlg.by_aid(win, "AiModelUrlsTextBox")) or ""
    drv.log("[urls] restaurado: distintivo=%r caja=%r" % (badge_restored, repr(urls_restored)[:90]))
    check("🔧" not in badge_restored or badge_restored.strip() == badge_before.strip(),
          "el distintivo vuelve al de la linea base (%r -> %r -> %r)"
          % (badge_before, badge_after, badge_restored))
    check(urls_restored.strip() == original_urls.strip(),
          "y la caja trae las URLs que habia antes de la sesion")

    cancelled = press_close_button(win, STEP)
    after_cancel = aid_set(win)
    drv.log("[urls] cerrado con %r" % cancelled)
    check(not any(a in after_cancel for a in URLS_ANCHORS), "el editor se cierra sin guardar nada mas")

    # ── 7. Cierre: la superficie de ajustes, el lienzo y tus preferencias, como al entrar ──
    check(press(win, "SettingsCancelButton", STEP), "la superficie de ajustes se recoge")
    img5 = sess.grab(hwnd, "99_final")
    c5 = menu.dominant(img5, CENTER)
    prefs_after = md5(PREFS)
    drv.log("[urls] final: tarjetas=%d pixel centro=%s (%.1f%%) preferencias md5=%s"
            % (cards(), menu.hexs(c5[0]), 100 * c5[1], prefs_after))
    check(c5[0] == c0[0], "la sesion termina con la escena igual a la de la linea base",
          "centro %s/%s" % (menu.hexs(c0[0]), menu.hexs(c5[0])))
    check(cards() == base_cards, "y el lienzo con las mismas tarjetas", "%d" % cards())
    check(prefs_after == prefs_before,
          "el fichero de preferencias del usuario queda byte-identico",
          "%s / %s" % (prefs_before, prefs_after))

    ok = all(results)
    drv.log("[urls] === RESULTADO: %s (%d de %d pasos) ==="
            % ("VERIFICADO" if ok else "FALLOS", sum(results), len(results)))
    with open(RESULT, "w", encoding="utf-8") as fh:
        json.dump({"pasos": len(results), "verificados": sum(results),
                   "tarjetas": base_cards,
                   "modelo": first_row,
                   "anclas": {"editor": present},
                   "filas": {"modelos": len(model_rows)},
                   "textos": {"modelo_en_el_editor": model_shown,
                              "distintivo_antes": badge_before,
                              "distintivo_tras_guardar": badge_after,
                              "distintivo_restaurado": badge_restored,
                              "recuento_antes": count_before,
                              "recuento_tras_teclear": count_after,
                              "url_escrita": PROBE_URL,
                              "caja_tras_guardar": urls_after,
                              "caja_restaurada": urls_restored,
                              "boton_guardar": saved,
                              "boton_cerrar": cancelled},
                   "pixel_centro": {"base": menu.hexs(c0[0]), "ajustes": menu.hexs(c_settings[0]),
                                    "editor": menu.hexs(c1[0]),
                                    "tras_guardar": menu.hexs(c2[0]),
                                    "reabierto": menu.hexs(c3[0]),
                                    "restaurado": menu.hexs(c4[0]),
                                    "final": menu.hexs(c5[0])},
                   "preferencias_md5": {"antes": prefs_before, "despues": prefs_after}},
                  fh, ensure_ascii=False, indent=2)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
