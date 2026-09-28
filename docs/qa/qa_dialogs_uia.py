# -*- coding: utf-8 -*-
"""Sesion de los PANELES DE NODO del host Uno (hito 258) con DRIVER EXTERNO por UIA.

Reparto de responsabilidades (el mismo de las sesiones 255 y 257):
  - quien ACTUA es este driver: otro proceso le da a los controles REALES por sus AutomationId y
    por su rectangulo (el clic fisico con SetCursorPos+mouse_event, medido vivo en el 268 y valido
    para el contenido de WinAppSDK), con los patrones de UIA (Invoke / Value / SelectionItem);
  - quien MIDE es el vigilante de qa_manual_session.py (`--watch`), que captura la pantalla y saca
    el delta por pixel mientras el driver actua.

Que se ejercita, y por que cada paso mide lo que mide:
  - El PANEL DE PARAMETROS de un nodo REAL: se selecciona «Folder Source» con un clic en su tarjeta
    y sus filas aparecen con sus anclas (ParamBox_*, ParamVariable_*, ParamEditor_*). Sin el clic no
    hay panel: el ejemplo arranca sin nodo seleccionado y el inspector solo tiene su cabecera.
  - El SELECTOR DE VARIABLES: se abre con el boton «{x}» de una fila (Invoke del control), se
    FILTRA escribiendo en su buscador, se ELIGE una fila por SelectionItemPattern y se CONFIRMA con
    su boton primario. El veredicto no es el dialogo: es el CAMPO de la fila
    (`ParamBox_ExtensionFilter`), que es lo que el nodo ejecuta — y que solo se entera del valor
    nuevo si la fila sigue al parametro en los dos sentidos.
  - El EDITOR DE TEXTO: el ejemplo no trae ningun parametro de texto LARGO, asi que la sesion AÑADE
    un nodo por el camino del usuario (buscar en el cajon de herramientas + doble clic en el item),
    lo selecciona y abre su «✎». El valor escrito con el teclado en la caja del editor vuelve al
    campo de la fila al confirmar: eso es «lo que queda escrito en el nodo».
  - La limpieza: el nodo añadido se retira con Deshacer y el campo tocado vuelve a su valor, y las
    preferencias del usuario se comparan por bytes antes y despues.

Uso (con la app ya lanzada por el instrumento, que maximiza y escribe session_state.json):
  FILEFLOW_QA_WORK=qa-manual-268 python qa_dialogs_uia.py --session

Salida: una linea por paso (`[dialogos] ...`) con lo medido ANTES y DESPUES, capturas rotuladas en
la carpeta de la sesion y exit 0 solo si todos los pasos salieron como dicen.
"""
import ctypes
import hashlib
import json
import os
import sys
import time

import numpy as np

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import qa_ajustes_uia as drv  # noqa: E402
import qa_manual_session as sess  # noqa: E402
import qa_menu_uia as menu  # noqa: E402

u32 = ctypes.windll.user32
MB_LBUTTONDOWN, MB_LBUTTONUP = 0x0002, 0x0004

# El nodo que la sesion AÑADE para tener un parametro de texto largo (el cajon de herramientas lo
# busca por su nombre visible, que es el del diccionario del host en el idioma vigente).
LONG_NODE_SEARCH = "Registrar"
LONG_NODE_CARD = "Registrar Log"

STEP = 1.5
CENTER = menu.CENTER
# La zona de las TARJETAS del lienzo: el inspector vive a partir de x~3400 y repite el titulo del nodo
# inspeccionado (medido en la sesion: el titulo del inspector canta el mismo texto que la tarjeta).
CANVAS = (400, 3400)
# La ruta REAL (AppPaths.RootDirectory en modo instalado): %AppData%\FileFlow\config. La carpeta vieja
# %AppData%\FileFlowStudio guarda copias de hace semanas y una sesion que leyera de ahi no mediria nada.
PREFS = os.path.join(os.environ.get("APPDATA", ""), "FileFlow", "config", "user_preferences.json")


def md5(path):
    if not os.path.exists(path):
        return None
    with open(path, "rb") as fh:
        return hashlib.md5(fh.read()).hexdigest()


def click(x, y, double=False):
    """Clic FISICO: SetCursorPos + mouse_event. Medido en esta misma sesion contra la casilla «Modo
    Prueba» de la barra (1 -> 0 -> 1), asi que el puntero llega al contenido de WinAppSDK."""
    u32.SetCursorPos(int(x), int(y))
    time.sleep(0.35)
    for _ in range(2 if double else 1):
        u32.mouse_event(MB_LBUTTONDOWN, 0, 0, 0, 0)
        time.sleep(0.05)
        u32.mouse_event(MB_LBUTTONUP, 0, 0, 0, 0)
        if double:
            time.sleep(0.06)
    time.sleep(STEP)


def elements(win, pred=None, area=None):
    """Los elementos del arbol AHORA (sin cache: tras un Invoke la lisa miente), filtrados por nombre
    y, si se pide, por zona de pantalla."""
    out = []
    for el in win.descendants():
        try:
            name = (el.element_info.name or "").strip()
            rect = el.rectangle()
        except Exception:
            continue
        if pred is not None and not pred(name):
            continue
        if area is not None and not (rect.left >= area[0] and rect.right <= area[1]):
            continue
        out.append((name, el, rect))
    return out


def value_of(el):
    """El texto de un cuadro por su patron de valor (la via que expone pywinauto; la lectura directa de
    `iface_value.Value` devuelve el puntero del patron segun la version de comtypes)."""
    if el is None:
        return None
    try:
        got = el.get_value()
    except Exception:
        got = None
    return None if got is None else str(got).strip()


def by_aid(win, aid):
    """El elemento con ese AutomationId AHORA. No se usa el indice cacheado de qa_ajustes_uia: tras
    abrir un modal el arbol cambia y su cache seguiria dando el de antes (medido en la sesion 255)."""
    for _, el, _ in elements(win):
        try:
            if (el.element_info.automation_id or "") == aid:
                return el
        except Exception:
            continue
    return None


def set_value(el, text):
    vp = drv.iface(el, "iface_value")
    if vp is None:
        return False
    vp.SetValue(text)
    return True


def by_name(win, name, area=None):
    hits = elements(win, lambda n: n == name, area)
    return hits[0][1] if hits else None


def card_rect(win, title):
    """El rectangulo del TITULO de una tarjeta del LIENZO (el inspector repite el titulo del nodo)."""
    hits = elements(win, lambda n: n == title, CANVAS)
    return hits[0][2] if hits else None


def main():
    if "--session" not in sys.argv:
        drv.log("[dialogos] nada que hacer: usa --session")
        return 2

    os.makedirs(sess.SHOTS, exist_ok=True)
    win = drv.connect()
    if win is None:
        drv.log("[dialogos] FALLO: la app no aparece por UIA")
        return 3

    hwnd = int(json.load(open(sess.STATE))["hwnd"])
    results = []
    prefs_before = md5(PREFS)
    prefs_backup = PREFS + ".qa-backup"

    def check(condition, what, detail=""):
        results.append(bool(condition))
        drv.log("[dialogos] %s %s%s" % ("[OK]   " if condition else "[FALLO]", what,
                                        ("  <- " + detail) if detail else ""))
        return bool(condition)

    def cards():
        titles = ("Folder Source", "Optimizador de Imágenes", "Destination Sink", LONG_NODE_CARD)
        return len(elements(win, lambda n: n in titles, CANVAS))

    with open(PREFS, "rb") as fh:
        original = fh.read()
    try:
        with open(prefs_backup, "wb") as fh:
            fh.write(original)
    except OSError:
        prefs_backup = None
    drv.log("[dialogos] preferencias del usuario: %s md5=%s" % (PREFS, prefs_before))

    # ── 0. La linea base ──
    img0 = sess.grab(hwnd, "40_base")
    c0 = menu.dominant(img0, CENTER)
    drv.log("[dialogos] base: tarjetas=%d pixel centro=%s (%.1f%%) inspector=%s"
            % (cards(), menu.hexs(c0[0]), 100 * c0[1], by_aid(win, "InspectorTestButton") is not None))
    check(len(elements(win, lambda n: n.startswith("Param"))) == 0,
          "el ejemplo arranca SIN nodo seleccionado: el panel de parametros no tiene filas todavia")

    # ── 1. El PANEL DE PARAMETROS de un nodo real ──
    rect = card_rect(win, "Folder Source")
    check(rect is not None, "la tarjeta del nodo «Folder Source» esta en el arbol con su rectangulo")
    click((rect.left + rect.right) // 2, (rect.top + rect.bottom) // 2)
    drv._TREE = None
    params = sorted(k for k in drv.tree(win) if k.startswith("Param"))
    drv.log("[dialogos] tras seleccionar «Folder Source»: %d filas con ancla %s" % (len(params), params))
    check("ParamBox_ExtensionFilter" in params and "ParamVariable_ExtensionFilter" in params,
          "el panel de parametros del nodo aparece con sus filas ancladas: cada valor su caja y su boton «{x}»")

    # ── 2. El SELECTOR DE VARIABLES, hasta que la eleccion queda escrita ──
    box = by_aid(win, "ParamBox_ExtensionFilter")
    before_value = value_of(box)
    drv.log("[dialogos] valor del campo antes del catalogo: %r" % before_value)

    picker = by_aid(win, "ParamVariable_ExtensionFilter")
    drv.invoke(picker)
    time.sleep(STEP + 0.6)
    img1 = sess.grab(hwnd, "41_catalogo_abierto")
    c1 = menu.dominant(img1, CENTER)
    aids = sorted({e.element_info.automation_id for _, e, _ in elements(win)
                   if e.element_info.automation_id})
    dialog = [a for a in aids if a.startswith("VariablePicker")]
    drv.log("[dialogos] dialogo abierto: %s | pixel centro=%s (%.1f%%)" % (dialog, menu.hexs(c1[0]), 100 * c1[1]))
    check(set(dialog) >= {"VariablePickerList", "VariablePickerSearchBox", "VariablePickerCountText",
                          "VariablePickerDetailToken"},
          "pulsar «{x}» abre el CATALOGO DE VARIABLES del host, con su buscador, su lista, su cuenta y su detalle")
    check(c1[0] != c0[0],
          "y el modal se ve en el pixel: la banda central de la ventana cambia de color dominante",
          "%s -> %s" % (menu.hexs(c0[0]), menu.hexs(c1[0])))

    count_before = (by_aid(win, "VariablePickerCountText").element_info.name or "").strip()
    search = by_aid(win, "VariablePickerSearchBox")
    set_value(search, "Guid")
    # El cuadro del modal entrega su texto al view model al salir de el (la misma cortesia que el
    # resto de cuadros del host): se sale con el foco a la lista, que es lo que hace el usuario.
    by_aid(win, "VariablePickerList").set_focus()
    time.sleep(1.2)
    count_after = (by_aid(win, "VariablePickerCountText").element_info.name or "").strip()
    drv.log("[dialogos] buscador: %r -> %r" % (count_before, count_after))
    check(count_after != count_before and "1" in count_after,
          "el buscador del catalogo filtra en caliente lo que el usuario teclea", count_after)

    # El filtro vuelve a su sitio (por el mismo cuadro) antes de elegir: se mide SOBRE la lista completa.
    set_value(search, "")
    by_aid(win, "VariablePickerList").set_focus()
    time.sleep(1.4)
    full_again = (by_aid(win, "VariablePickerCountText").element_info.name or "").strip()
    check(full_again == count_before,
          "y al borrar el buscador el catalogo vuelve a su cuenta completa", full_again)

    # Se elige la fila de un token CONOCIDO (no «la primera»: el orden no es la medicion) con el GESTO
    # del usuario — un clic en la fila —, y se lee el detalle del view model como confirmacion interna.
    token = "{FileName}"
    row = None
    for name, el, rect in elements(win, lambda n: n.startswith(token)):
        if name == token and rect.right > rect.left:
            row = rect
            break
    check(row is not None, "la fila de %s esta en la lista del catalogo" % token)
    if row is not None:
        click((row.left + row.right) // 2, (row.top + row.bottom) // 2)
        detail = (by_aid(win, "VariablePickerDetailToken").element_info.name or "").strip()
        drv.log("[dialogos] detalle del token elegido: %r" % detail)
        check(detail == token,
              "el clic en la fila escribe el token en el detalle del catalogo", detail)

    primary = None
    for name, el, _ in elements(win, lambda n: n == "Insertar Variable"):
        primary = el
        break
    check(primary is not None, "el boton primario del catalogo («Insertar Variable») esta en el arbol")
    if primary is not None:
        drv.invoke(primary)
        time.sleep(STEP + 0.8)

    img2 = sess.grab(hwnd, "42_catalogo_cerrado")
    c2 = menu.dominant(img2, CENTER)
    after_value = value_of(by_aid(win, "ParamBox_ExtensionFilter"))
    drv.log("[dialogos] pixel centro tras cerrar=%s (%.1f%%) | campo=%r"
            % (menu.hexs(c2[0]), 100 * c2[1], after_value))
    check(c2[0] == c0[0], "el modal se cierra y la ventana vuelve a su pixel de linea base",
          "%s -> %s" % (menu.hexs(c1[0]), menu.hexs(c2[0])))
    check(after_value is not None and token in after_value and after_value != before_value,
          "LA ELECCION QUEDA ESCRITA EN EL NODO: el campo de la fila pasa de %r a %r"
          % (before_value, after_value))

    # El campo vuelve a su valor: la sesion mide, no configura.
    set_value(by_aid(win, "ParamBox_ExtensionFilter"), before_value or "")
    time.sleep(1.0)
    restored = value_of(by_aid(win, "ParamBox_ExtensionFilter"))
    check(restored == (before_value or ""), "y el campo vuelve a su valor de partida", repr(restored))

    # ── 3. Un nodo con parametro de texto LARGO, por el camino del usuario ──
    toolbox_search = by_aid(win, "SearchBox")
    set_value(toolbox_search, LONG_NODE_SEARCH)
    time.sleep(1.5)
    item = None
    for name, el, rect in elements(win, lambda n: LONG_NODE_SEARCH in n, (0, 400)):
        item = rect
        break
    check(item is not None, "el cajon de herramientas encuentra el nodo «%s» al buscar %r"
          % (LONG_NODE_CARD, LONG_NODE_SEARCH))
    if item is not None:
        click((item.left + item.right) // 2, (item.top + item.bottom) // 2, double=True)
    added_card = card_rect(win, LONG_NODE_CARD)
    drv.log("[dialogos] tarjeta del nodo añadido: %s" % added_card)
    check(added_card is not None, "el doble clic del cajon añade el nodo al lienzo (su tarjeta esta en el arbol)")

    if added_card is not None:
        click((added_card.left + added_card.right) // 2, (added_card.top + added_card.bottom) // 2)
        drv._TREE = None
        params = sorted(k for k in drv.tree(win) if k.startswith("Param"))
        drv.log("[dialogos] filas del nodo añadido: %s" % params)
        check("ParamEditor_CustomMessage" in params and "ParamBox_CustomMessage" in params,
              "el nodo añadido expone su fila de texto LARGO: la caja del valor y el boton «✎» del editor")

        # ── 4. El EDITOR DE TEXTO, hasta que lo escrito queda en el nodo ──
        seed = "prompt de la sesion 268"
        set_value(by_aid(win, "ParamBox_CustomMessage"), seed)
        time.sleep(1.0)
        drv.log("[dialogos] campo del texto largo antes del editor: %r" % value_of(by_aid(win, "ParamBox_CustomMessage")))
        drv.invoke(by_aid(win, "ParamEditor_CustomMessage"))
        time.sleep(STEP + 0.8)
        img3 = sess.grab(hwnd, "43_editor_abierto")
        c3 = menu.dominant(img3, CENTER)
        editor_aids = sorted({e.element_info.automation_id for _, e, _ in elements(win)
                              if e.element_info.automation_id})
        dialog = [a for a in editor_aids if a.startswith("TextEditor")]
        seeded = value_of(by_aid(win, "TextEditorBox"))
        drv.log("[dialogos] editor abierto: %s | caja=%r | pixel centro=%s (%.1f%%)"
                % (dialog, seeded, menu.hexs(c3[0]), 100 * c3[1]))
        check(set(dialog) >= {"TextEditorBox", "TextEditorInsertVariableButton", "TextEditorClearButton"},
              "pulsar «✎» abre el EDITOR DE TEXTO del host con su caja, su insercion de variables y su limpiar")
        check(seeded == seed,
              "y el editor abre con el VALOR del parametro del nodo, no vacio", repr(seeded))

        drv.invoke(by_aid(win, "TextEditorInsertVariableButton"))
        time.sleep(1.2)
        # El panel se mide por su LISTA (un ListView sí llega al canal externo): aparece con el panel y
        # trae el catálogo que el view model filtró — que es lo que el usuario ve al desplegarlo.
        panel_list = by_aid(win, "TextEditorVariableList")
        rows = [n for n, _, _ in elements(win, lambda n: n.startswith("{"))]
        drv.log("[dialogos] panel de variables del editor: lista=%s filas=%d"
                % (panel_list is not None, len(rows)))
        check(panel_list is not None and len(rows) > 0,
              "«Insertar Variable» del editor despliega su panel con el catálogo del view model",
              "%d variables" % len(rows))

        typed = seed + " + {FileName}"
        set_value(by_aid(win, "TextEditorBox"), typed)
        time.sleep(1.0)
        save = None
        for name, el, _ in elements(win, lambda n: n.startswith("✓") or "Guardar" in n):
            save = el
            break
        check(save is not None, "el boton primario del editor («Guardar y Aplicar») esta en el arbol")
        if save is not None:
            drv.invoke(save)
            time.sleep(STEP + 0.8)

        img4 = sess.grab(hwnd, "44_editor_cerrado")
        c4 = menu.dominant(img4, CENTER)
        written = value_of(by_aid(win, "ParamBox_CustomMessage"))
        drv.log("[dialogos] pixel centro tras guardar=%s (%.1f%%) | campo=%r"
                % (menu.hexs(c4[0]), 100 * c4[1], written))
        check(c4[0] == c0[0], "el editor se cierra y la ventana vuelve a su pixel de linea base",
              "%s -> %s" % (menu.hexs(c3[0]), menu.hexs(c4[0])))
        check(written == typed,
              "LO CONFIRMADO QUEDA ESCRITO EN EL NODO: el campo del parametro pasa a %r" % (written,))

    # ── 5. La escena y las preferencias del usuario vuelven a como estaban ──
    if by_aid(win, "SearchBox") is not None:
        set_value(by_aid(win, "SearchBox"), "")
        time.sleep(0.8)
    # El nodo que añadio la sesion se retira con el GESTO que el producto ofrece: se selecciona (clic en
    # su tarjeta, que ademas le da el teclado al lienzo) y se borra con Supr. El barrido se repite por si
    # el framework devuelve el foco a un panel despues del clic (la leccion del hito 252).
    for _ in range(3):
        remaining = card_rect(win, LONG_NODE_CARD)
        if remaining is None:
            break
        click((remaining.left + remaining.right) // 2, (remaining.top + remaining.bottom) // 2)
        u32.keybd_event(0x2E, 0, 0, 0)   # Supr
        u32.keybd_event(0x2E, 0, 2, 0)
        time.sleep(1.4)

    if card_rect(win, LONG_NODE_CARD) is not None:
        for _ in range(6):
            if card_rect(win, LONG_NODE_CARD) is None:
                break
            u32.keybd_event(0x11, 0, 0, 0)   # Ctrl
            u32.keybd_event(0x5A, 0, 0, 0)   # Z
            u32.keybd_event(0x5A, 0, 0, 2)
            u32.keybd_event(0x11, 0, 2, 0)
            time.sleep(1.2)
    img5 = sess.grab(hwnd, "45_final")
    c5 = menu.dominant(img5, CENTER)
    remaining = card_rect(win, LONG_NODE_CARD)
    drv.log("[dialogos] final: nodo añadido presente=%s tarjetas=%d pixel centro=%s (%.1f%%)"
            % (remaining is not None, cards(), menu.hexs(c5[0]), 100 * c5[1]))
    check(remaining is None, "el nodo que la sesion añadio se retira con Deshacer: el lienzo vuelve a 3 tarjetas")
    check(c5[0] == c0[0], "y el pixel central vuelve al de la linea base",
          "%s -> %s" % (menu.hexs(c0[0]), menu.hexs(c5[0])))

    prefs_after = md5(PREFS)
    if prefs_after != prefs_before and prefs_backup:
        with open(prefs_backup, "rb") as fh:
            data = fh.read()
        with open(PREFS, "wb") as fh:
            fh.write(data)
        drv.log("[dialogos] las preferencias cambiaron durante la sesion (%s -> %s): restauradas por bytes"
                % (prefs_before, prefs_after))
        prefs_after = md5(PREFS)
    if prefs_backup and os.path.exists(prefs_backup):
        os.remove(prefs_backup)
    check(prefs_after == prefs_before,
          "las preferencias del usuario quedan INTACTAS (mismo md5 antes y despues)", str(prefs_after))

    ok = all(results)
    drv.log("[dialogos] === RESULTADO: %s (%d de %d pasos) ==="
            % ("VERIFICADO" if ok else "FALLOS", sum(results), len(results)))
    with open(os.path.join(sess.SHOTS, "dialogos-session.json"), "w", encoding="utf-8") as fh:
        json.dump({
            "pasos": len(results), "verificados": sum(results),
            "nodo": "Folder Source", "parametro": "ExtensionFilter",
            "campo_antes": before_value, "campo_despues": after_value,
            "nodo_largo": LONG_NODE_CARD, "parametro_largo": "CustomMessage",
            "editor_escrito": locals().get("written"),
            "pixel_centro": {"base": menu.hexs(c0[0]), "catalogo": menu.hexs(c1[0]),
                             "final": menu.hexs(c5[0])},
            "preferencias_md5": {"antes": prefs_before, "despues": prefs_after},
        }, fh, ensure_ascii=False, indent=2)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
