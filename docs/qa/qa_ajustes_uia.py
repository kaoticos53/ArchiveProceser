# -*- coding: utf-8 -*-
"""La sesion de la superficie de AJUSTES del host Uno (hito 255) con DRIVER EXTERNO por UIA.

Reparto de responsabilidades (el mismo del 247, con otro dedo):
  - quien ACTUA es este driver: otro proceso le da a los controles REALES por sus AutomationId,
    con los patrones de UIA (Invoke / SelectionItem), el canal que abrio el 245;
  - quien MIDE es el vigilante de docs/qa/qa_manual_session.py (`--watch`), que captura la pantalla
    y saca el delta por pixel mientras el driver actua.

Por que un driver y no el puntero: la sesion del 231 midio que el puntero INYECTADO sin UIAccess no
llega al contenido de WinAppSDK (0 px en toda configuracion), asi que el clic fisico es humano. Lo que
este driver ejercita es la MISMA superficie por el canal de automatizacion —el que usa un lector de
pantalla—: los controles de verdad, no la sonda en proceso (que ademas restaura: mide, no configura).

Uso (con la app ya lanzada por el instrumento y el vigilante midiendo su pixel):
  python qa_ajustes_uia.py --state                 lee la superficie: anclas, tema e idioma elegidos
  python qa_ajustes_uia.py --open [seccion]        abre los ajustes y conmuta de seccion (por omision
                                                   Apariencia; la superficie abre en Almacenamiento)
  python qa_ajustes_uia.py --theme Claro           elige el tema cuyo nombre CONTIENE el texto
  python qa_ajustes_uia.py --language English      elige el idioma por su nombre visible
  python qa_ajustes_uia.py --save                  el boton de guardar del pie
  python qa_ajustes_uia.py --close                 cierra la superficie por su boton
  python qa_ajustes_uia.py --toggle <AutomationId> conmuta una casilla (TogglePattern) y lee su estado
  python qa_ajustes_uia.py --value <AutomationId> <texto>  escribe un campo (ValuePattern) y relee

Salida: una linea por accion (`[driver] ...`), con lo que READ BACK despues de actuar; sin codigo de
salida propio mas alla de 0 = la accion se hizo, 2 = no se pudo, 3 = la app no aparece.
"""
import ctypes
import json
import os
import sys
import time

try:
    import comtypes
    import comtypes.client
except ImportError:
    comtypes = None

from pywinauto.application import Application

# La consola de Windows por omision usa cp1252 y los nombres de los temas llevan emoji (🌙, ☀️, 🌸):
# imprimir la lista de items mataba al driver con UnicodeEncodeError ANTES de elegir nada (medido en
# esta sesion: el `--theme` no llegaba ni a seleccionar). Se fija la salida a UTF-8 con reemplazo,
# para que un nombre raro no tumbe la medicion que el driver existe para hacer.
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

u32 = ctypes.windll.user32
VK_UP = 0x26
VK_DOWN = 0x28


def key(vk, up=False):
    u32.keybd_event(vk, 0, 2 if up else 0, 0)


def iface(el, name):
    """El patron de UIA del elemento, si lo sostiene.

    OJO, medido en esta sesion: el respaldo de comtypes de este driver llamaba a
    `comtypes.client.GetPattern`, que NO EXISTE (`AttributeError`), y lo tragaba un `except` ancho:
    por ese camino los patrones no llegaban NUNCA y varias lecturas del driver salian por el
    `except` como "sin lectura", culpando a WinUI de lo que era un fallo del propio driver. El
    wrapper de pywinauto ya expone cada patron como atributo `iface_*`, y si el elemento no lo
    sostiene el atributo vale None.
    """
    try:
        return getattr(el, name, None)
    except Exception:
        return None


HERE = os.path.dirname(os.path.abspath(__file__))
SHOTS = os.path.join(HERE, os.environ.get("FILEFLOW_QA_WORK", "qa-manual-261"))
STATE = os.path.join(SHOTS, "session_state.json")

# Las anclas de la superficie (hoja de la vista) y el boton que la abre (marco del host).
SETTINGS_BUTTON = "SettingsButton"
# OJO: `SettingsPanel` NO sirve como prueba de presencia. El AutomationId esta puesto en el Border del
# marco del panel y un Border no tiene peer de automatizacion, asi que ese id NUNCA aparece en el
# arbol (medido en esta sesion: `--tree` lo da AUSENTE con la superficie abierta y todo lo de dentro
# presente). Como prueba de que la superficie esta desplegada se usan sus PROPIAS anclas: el
# conmutador de secciones y el pie, que son controles y si tienen peer.
PANEL = "SettingsPanel"                      # se conserva para el diagnostico (--dump)
PANEL_ANCHORS = ("SettingsTabStorage", "SettingsTabAppearance", "SettingsTabPerformance",
                 "SettingsTabTools", "SettingsSaveButton", "SettingsCloseButton")
TAB_APPEARANCE = "SettingsTabAppearance"
# Resultado del driver cuando la seleccion SE ENVIO pero el canal externo no la puede leer: no es
# "no se encontro" ni "no se aplico", y confundirlos fue un defecto del propio driver (su mensaje
# culpaba a la busqueda del nombre cuando lo que faltaba era la lectura).
SIN_LECTURA = "sin-lectura"
THEME_COMBO = "SettingsThemeCombo"
LANGUAGE_COMBO = "SettingsLanguageCombo"
SAVE = "SettingsSaveButton"
CLOSE = "SettingsCloseButton"


def log(msg):
    print(msg, flush=True)


_TREE = None


def tree(root):
    """UNA pasada por el arbol, indexada por AutomationId.

    Medido en esta sesion: recorrer el descendiente de la ventana cuesta segundos y pedirlo por cada
    ancla multiplicaba esa pasada por cada ancla (la primera version del driver no terminaba: el
    `--state` se quedo colgado y no llego a imprimir). El indice se arma una vez por invocacion.
    """
    global _TREE
    if _TREE is None:
        start = time.time()
        index = {}
        count = 0
        for el in root.descendants():
            count += 1
            try:
                aid = el.element_info.automation_id or ""
            except Exception:
                continue
            if aid:
                index.setdefault(aid, []).append(el)
        _TREE = index
        log("[driver] arbol: %d elementos, %d con AutomationId (%.1f s)"
            % (count, len(index), time.time() - start))
    return _TREE


def by_aid(root, aid):
    """El primer elemento con ese AutomationId (pywinauto 0.6.9 no filtra por automation_id)."""
    found = tree(root).get(aid)
    return found[0] if found else None


def invoke(el):
    """Invoke del boton: primero el wrapper de pywinauto (el camino PROBADO por el sondeo UIA del
    245 sobre la barra de zoom), y el patron Invoke del elemento como respaldo.

    El orden importa y se midio en una sesion anterior: la primera version pedia el patron por
    comtypes y devolvia True sin que el panel se abriera —una llamada que dice que si y no cambia
    nada es peor que una que falla—, asi que primero va el camino que el sondeo ya demostro que llega.
    """
    try:
        el.invoke()
        return True
    except Exception:
        pass
    pattern = iface(el, "iface_invoke")
    if pattern is not None:
        try:
            pattern.Invoke()
            return True
        except Exception:
            pass
    return False


def select_item(el):
    """SelectionItemPattern: el patron con el que el 245 conmutaba pestañas (el Invoke no llega)."""
    pattern = iface(el, "iface_selection_item")
    if pattern is not None:
        try:
            pattern.Select()
            return True
        except Exception:
            pass
    try:
        el.select()
        return True
    except Exception:
        return False


def combo_items(combo):
    """Los items del desplegable, EXPANDIENDOLO: medido en esta sesion, un ComboBox de WinUI no
    expone ni su nombre ni sus hijos en el arbol mientras esta cerrado (el `--dump` del combo
    devolvio una linea vacia), asi que la unica lectura honesta es abrirlo.

    Devuelve [(nombre, seleccionado, elemento)] con el popup materializado.
    """
    try:
        combo.expand()
    except Exception:
        try:
            combo.element_info.element.CurrentExpandCollapsePattern.Expand()
        except Exception:
            return []
    time.sleep(0.9)

    items = []
    for el in combo.descendants():
        try:
            info = el.element_info
        except Exception:
            continue
        selected = False
        pattern = iface(el, "iface_selection_item")
        if pattern is not None:
            try:
                selected = bool(pattern.CurrentIsSelected)
            except Exception:
                pass
        name = (info.name or "").strip()
        # El desplegable de WinUI expone CADA item DOS veces mientras esta abierto (medido: 20 items
        # para 10 temas). Se queda la primera aparicion por nombre: elegir la copia equivocada es una
        # de las razones por las que la seleccion resultaba intermitente.
        if name and all(name != n for n, _, _ in items):
            items.append((name, selected, el))
    return items


def combo_collapse(combo):
    try:
        combo.collapse()
    except Exception:
        pass


def combo_selected(combo):
    """Lo que el desplegable tiene ELEGIDO, por el SelectionPattern del PROPIO ComboBox.

    Medido en esta sesion: el patron SelectionItem de los items devolvia 'no seleccionado' para TODOS
    (incluso cuando la seleccion habia llegado de verdad al producto: la preferencia se guardo), asi
    que no servia de lectura; el `GetCurrentSelection` del ComboBox si dice lo que el control tiene.
    """
    pattern = iface(combo, "iface_selection_pattern")
    if pattern is not None:
        try:
            selection = pattern.GetCurrentSelection()
            if selection is not None and selection.Length > 0:
                return (selection.GetElement(0).CurrentName or "").strip()
        except Exception:
            pass
    items = combo_items(combo)
    chosen = next((name for name, selected, _ in items if selected), None)
    combo_collapse(combo)
    return chosen


def combo_select(combo, text, log=log):
    """Abre el desplegable, elige el item cuyo nombre CONTIENE `text` y COMPRUEBA la eleccion.

    El patron de seleccion sobre un item de un ComboBox de WinUI es intermitente (los contenedores
    del popup se reciclan): medido en la sesion, elegir tema y guardar dejo la preferencia ANTERIOR
    en dos intentos seguidos mientras el idioma si cambiaba. Por eso cada intento se verifica leyendo
    la seleccion del control y se reintenta; sin esa lectura, un fallo del driver se confundiria con
    un defecto del producto.
    """
    before = combo_selected(combo)
    for attempt in range(1, 4):
        items = combo_items(combo)
        if not items:
            log("[driver]   intento %d: el desplegable no expuso ningun item al abrirlo" % attempt)
            return None
        if attempt == 1:
            log("[driver]   items: %s" % " | ".join("%s%s" % (n, "*" if s else "") for n, s, _ in items))
        target = next((el for name, _, el in items if text.lower() in name.lower()), None)
        if target is None:
            combo_collapse(combo)
            return None

        select_item(target)
        time.sleep(0.6)
        combo_collapse(combo)
        time.sleep(0.4)

        after = combo_selected(combo)
        log("[driver]   intento %d: '%s' -> '%s'" % (attempt, before, after))
        if after is None:
            # Sin lectura: los ComboBox de WinUI de esta pantalla no exponen su seleccion al canal
            # externo (medido: `GetCurrentSelection` no devuelve nada ni cuando el cambio SI llego al
            # producto). Se declara en vez de inventar un veredicto: lo que zanja la sesion es la
            # preferencia GUARDADA y el pixel al reabrir, no esta lectura.
            log("[driver]   sin lectura de la seleccion por UIA: la comprobacion es la preferencia guardada")
            return SIN_LECTURA
        if text.lower() in after.lower():
            return True
        time.sleep(0.4)

    return False


def describe(el, depth=0, out=None):
    """Una linea por nodo: lo que UIA dice que es (nombre, clase, AutomationId) y si sostiene el
    patron de seleccion con su estado. Es el diagnostico con el que se aprende QUE expone un control
    de WinUI — el dato que decide como se le da, no una suposicion."""
    out = out if out is not None else []
    try:
        info = el.element_info
    except Exception:
        return out
    selected = ""
    pattern = iface(el, "iface_selection_item")
    if pattern is not None:
        try:
            selected = " [sel=%s]" % bool(pattern.CurrentIsSelected)
        except Exception:
            pass
    out.append("%s- %-18s %-22s %r%s" % ("  " * depth, info.control_type or "",
                                         info.automation_id or "", (info.name or "")[:48], selected))
    if depth < 3:
        for child in el.children():
            describe(child, depth + 1, out)
    return out


def connect(timeout=30.0):
    state = json.load(open(STATE))
    pid = int(state["pid"])
    deadline = time.time() + timeout
    while time.time() < deadline:
        try:
            app = Application(backend="uia").connect(process=pid, timeout=1)
            win = app.top_window()
            aid = None
            try:
                aid = win.element_info.automation_id
            except Exception:
                pass
            if by_aid(win, "CanvasRoot") is not None or by_aid(win, SETTINGS_BUTTON) is not None:
                win.set_focus()
                time.sleep(0.6)
                return win
            log("[driver] el arbol todavia no expone las anclas (%s); reintento" % aid)
        except Exception as ex:
            log("[driver] reintento de conexion: %s" % type(ex).__name__)
        time.sleep(1.0)
    return None


def read_state(win):
    """Lo que la superficie esta diciendo AHORA: texto del boton (idioma vigente en el marco) y
    seleccion de los desplegables de apariencia."""
    # INDICE FRESCO: `tree()` cachea el arbol por invocacion y ese cache MIENTE despues de un Invoke
    # (medido en esta sesion: tras pulsar guardar, el panel ya estaba cerrado y el cache todavia
    # daba sus anclas por presentes — una lectura que decia lo contrario de lo que habia en pantalla).
    global _TREE
    _TREE = None
    button = by_aid(win, SETTINGS_BUTTON)
    present = [aid for aid in PANEL_ANCHORS if by_aid(win, aid) is not None]
    theme = by_aid(win, THEME_COMBO)
    language = by_aid(win, LANGUAGE_COMBO)
    save = by_aid(win, SAVE)

    state = {
        "botonAjustes": (button.element_info.name or "").strip() if button else None,
        "superficie": present,
        "temaElegido": combo_selected(theme) if theme else None,
        "idiomaElegido": combo_selected(language) if language else None,
        "guardar": (save.element_info.name or "").strip() if save else None,
    }
    log("[driver] estado: " + json.dumps(state, ensure_ascii=False))
    return state


def main():
    if not os.path.exists(STATE):
        log("[driver] FALLO: no hay sesion (falta %s): lanza la app con el instrumento" % STATE)
        return 3

    win = connect()
    if win is None:
        log("[driver] FALLO: la app no aparece por UIA")
        return 3

    if "--state" in sys.argv:
        read_state(win)
        return 0

    if "--dump" in sys.argv:
        aid = sys.argv[sys.argv.index("--dump") + 1]
        el = by_aid(win, aid)
        if el is None:
            log("[driver] FALLO: '%s' no esta en el arbol" % aid)
            return 2
        for line in describe(el):
            log("[driver] " + line)
        return 0

    if "--tree" in sys.argv:
        # Diagnostico: que anclas de la superficie estan en el arbol AHORA (una sola pasada).
        index = tree(win)
        for aid in (SETTINGS_BUTTON, PANEL, TAB_APPEARANCE, THEME_COMBO, LANGUAGE_COMBO, SAVE, CLOSE):
            log("[driver]   %-22s %s" % (aid, "presente" if aid in index else "AUSENTE"))
        return 0

    if "--open" in sys.argv:
        # Seccion opcional: `--open <storage|appearance|performance|tools>`. La superficie abre en
        # ALMACENAMIENTO (medido), asi que una seccion distinta hay que conmutarla.
        rest = [a for a in sys.argv[sys.argv.index("--open") + 1:] if not a.startswith("--")]
        want = rest[0].lower() if rest else "appearance"
        tabs = {"storage": "SettingsTabStorage", "appearance": TAB_APPEARANCE,
                "performance": "SettingsTabPerformance", "tools": "SettingsTabTools"}
        if want not in tabs:
            log("[driver] FALLO: seccion desconocida '%s' (usa %s)" % (want, "/".join(tabs)))
            return 2
        button = by_aid(win, SETTINGS_BUTTON)
        if button is None or not invoke(button):
            log("[driver] FALLO: el boton de ajustes no se pudo invocar")
            return 2
        time.sleep(1.5)
        # La prueba de que abrio: el indice del arbol se arma DESPUES del Invoke, y el panel tiene que
        # haber dejado sus anclas en el arbol.
        global _TREE
        _TREE = None
        tab = by_aid(win, tabs[want])
        if tab is None:
            log("[driver] FALLO: el Invoke no abrio la superficie (sus anclas no estan en el arbol)")
            return 2
        select_item(tab)
        time.sleep(1.2)
        log("[driver] ajustes abiertos por su boton y conmutados a %s" % want)
        read_state(win)
        return 0

    if "--theme" in sys.argv:
        text = sys.argv[sys.argv.index("--theme") + 1]
        combo = by_aid(win, THEME_COMBO)
        if combo is None:
            log("[driver] FALLO: el desplegable de tema no esta en el arbol")
            return 2
        combo.set_focus()
        ok = combo_select(combo, text)
        if ok is None:
            log("[driver] FALLO: no se encontro un tema cuyo nombre contenga '%s'" % text)
            return 2
        if ok == SIN_LECTURA:
            log("[driver] tema '%s' -> seleccion ENVIADA sin lectura por UIA; lo zanjan la preferencia"
                " guardada tras --save y el pixel al reabrir" % text)
            read_state(win)
            return 0
        log("[driver] tema '%s' -> %s" % (text, "elegido" if ok else "NO elegido"))
        read_state(win)
        return 0 if ok else 2

    if "--language" in sys.argv:
        text = sys.argv[sys.argv.index("--language") + 1]
        combo = by_aid(win, LANGUAGE_COMBO)
        if combo is None:
            log("[driver] FALLO: el desplegable de idioma no esta en el arbol")
            return 2
        combo.set_focus()
        ok = combo_select(combo, text)
        if ok is None:
            log("[driver] FALLO: no se encontro un idioma cuyo nombre contenga '%s'" % text)
            return 2
        if ok == SIN_LECTURA:
            log("[driver] idioma '%s' -> seleccion ENVIADA sin lectura por UIA; lo zanjan la preferencia"
                " guardada tras --save y el texto del marco" % text)
            read_state(win)
            return 0
        log("[driver] idioma '%s' -> %s" % (text, "elegido" if ok else "NO elegido"))
        read_state(win)
        return 0 if ok else 2

    if "--keyselect" in sys.argv:
        # El camino del USUARIO para un desplegable: enfocarlo y mover su seleccion con las flechas
        # (sin abrirlo). Se midio que el patron SelectionItem de UIA, sobre los items del ComboBox de
        # WinUI, dice que selecciona y la preferencia guardada no cambia: la flecha si llega.
        aid = sys.argv[sys.argv.index("--keyselect") + 1]
        direction = sys.argv[sys.argv.index("--keyselect") + 2]
        times = int(sys.argv[sys.argv.index("--keyselect") + 3])
        vk = VK_UP if direction.lower().startswith("up") else VK_DOWN
        el = by_aid(win, aid)
        if el is None:
            log("[driver] FALLO: '%s' no esta en el arbol" % aid)
            return 2
        el.set_focus()
        time.sleep(0.6)
        for _ in range(times):
            key(vk)
            time.sleep(0.08)
            key(vk, up=True)
            time.sleep(0.35)
        log("[driver] %s: %d flecha(s) %s" % (aid, times, direction))
        return 0

    if "--toggle" in sys.argv:
        aid = sys.argv[sys.argv.index("--toggle") + 1]
        el = by_aid(win, aid)
        if el is None:
            log("[driver] FALLO: '%s' no esta en el arbol (¿esta seccion a la vista?)" % aid)
            return 2
        pattern = iface(el, "iface_toggle")
        if pattern is None:
            log("[driver] FALLO: '%s' no sostiene TogglePattern" % aid)
            return 2
        before = int(pattern.CurrentToggleState)
        try:
            pattern.Toggle()
        except Exception as ex:
            log("[driver] FALLO al conmutar '%s': %s" % (aid, type(ex).__name__))
            return 2
        time.sleep(0.6)
        after = int(pattern.CurrentToggleState)
        log("[driver] %s: casilla %d -> %d" % (aid, before, after))
        return 0 if after != before else 2

    if "--value" in sys.argv:
        aid = sys.argv[sys.argv.index("--value") + 1]
        text = sys.argv[sys.argv.index("--value") + 2]
        el = by_aid(win, aid)
        if el is None:
            log("[driver] FALLO: '%s' no esta en el arbol (¿esta seccion a la vista?)" % aid)
            return 2
        pattern = iface(el, "iface_value")
        if pattern is None:
            log("[driver] FALLO: '%s' no sostiene ValuePattern" % aid)
            return 2
        before = pattern.CurrentValue
        try:
            pattern.SetValue(text)
        except Exception as ex:
            log("[driver] FALLO al escribir en '%s': %s" % (aid, type(ex).__name__))
            return 2
        time.sleep(0.6)
        log("[driver] %s: %r -> %r" % (aid, before, pattern.CurrentValue))
        return 0

    if "--save" in sys.argv:
        save = by_aid(win, SAVE)
        if save is None or not invoke(save):
            log("[driver] FALLO: el boton de guardar no se pudo invocar")
            return 2
        time.sleep(1.5)
        log("[driver] guardado por el boton del pie")
        read_state(win)
        return 0

    if "--close" in sys.argv:
        close = by_aid(win, CLOSE)
        if close is None or not invoke(close):
            log("[driver] FALLO: el boton de cerrar no se pudo invocar")
            return 2
        time.sleep(0.8)
        log("[driver] superficie recogida por su boton de cerrar")
        return 0

    log("[driver] nada que hacer: usa --state/--open/--theme/--language/--save/--close")
    return 2


if __name__ == "__main__":
    sys.exit(main())
