# -*- coding: utf-8 -*-
"""Sesion de las DOS VENTANAS que faltaban por ejercer en la app abierta (hito 261).

El reparto es el de siempre (255/257/258/259/260): quien ACTUA es este driver —otro proceso pulsa los
controles REALES por sus AutomationId— y quien MIDE es el propio driver: las anclas de automatizacion del
arbol, el TEXTO que expone el canal externo y el pixel dominante de una banda de la captura.

Que se ejercita, y por que cada paso mide lo que mide:

  - el EXPLORADOR DE ARCHIVOS VIRTUAL: la entrada del cajon («Explorador de Archivos Virtual») se pulsa,
    la superficie del host aparece con sus anclas, la lista expone UNA FILA POR ARCHIVO del almacen (leida
    por el canal externo) y el cierre la retira dejando el pixel como estaba;
  - el AVISO DE ACTUALIZACION: el DISTINTIVO de la barra se pulsa (la entrada con estado de contexto), la
    ventana del host aparece, la version que anuncia se lee por UIA y la orden «recordarmelo luego» lo
    retira.

El estado que estas dos superficies necesitan —una release nueva y una ejecucion que dejara archivos
virtuales— lo siembra el host por variable de entorno (FILEFLOW_QA_UPDATE / FILEFLOW_QA_VFS, ver
App.ApplyMeasurementSeeds): es lo unico que no puede producir esta maquina, y el camino medido —entrada,
superficie, contenido y cierre— es el del usuario.

Nada de esto escribe preferencias: se comprueba el md5 del fichero del usuario antes y despues.

Uso (con la app ya lanzada por el instrumento, que maximiza y escribe session_state.json):
  FILEFLOW_QA_WORK=qa-manual-271 FILEFLOW_QA_UPDATE=v9.9.9 FILEFLOW_QA_VFS=4 \
      python qa_manual_session.py --launch
  FILEFLOW_QA_WORK=qa-manual-271 python qa_windows2_uia.py --session

Salida: una linea por paso (`[win2] ...`) con lo medido antes y despues, capturas rotuladas en la carpeta
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
import qa_dialogs_uia as dlg  # noqa: E402  (by_aid/elements sin cache: la lista miente tras un Invoke)

STEP = 1.6
CENTER = menu.CENTER
PREFS = dlg.PREFS

# Las anclas de cada superficie QUE EL CANAL EXTERNO PUEDE VER (WinUI solo expone peer en los controles,
# no en los contenedores: el ancla del cuerpo la lee la sonda EN PROCESO).
VFS_ANCHORS = ("VfsSearchBox", "VfsFileList", "VfsTotalFiles", "VfsRefreshButton", "VfsCloseButton")
UPDATE_ANCHORS = ("UpdateCurrentVersion", "UpdateNewVersion", "UpdateReleaseNotes",
                  "UpdateRemindButton", "UpdateInstallButton", "UpdateSkipButton")

# Las anclas de CONTENEDOR: el AutomationId del cuerpo vive en su rejilla raiz, y una rejilla no tiene
# peer de automatizacion, asi que el canal externo ve sus CONTROLES pero no su ancla. Se dejan escritas
# para que no se tomen por ausencia: las lee la sonda EN PROCESO (--selfcheck-controlbar).
VFS_CONTAINER_ANCHORS = ("VfsExplorerBody",)
UPDATE_CONTAINER_ANCHORS = ("UpdateDialogBody",)

# Las anclas visibles del DISEÑADOR DE DATASETS, de las seis secciones de AJUSTES y de sus dos pestanas
# nuevas. Los controles son los del host: el catalogo, el arbol, las tres pestanas, el pie.
DESIGNER_ANCHORS = ("DataSetSearchBox", "DataSetList", "DataSetTabTree", "DataSetTabDsl",
                    "DataSetTabJson", "DataSetAddFileButton", "DataSetRemoveNodeButton",
                    "DataSetCloseButton")
DESIGNER_CONTAINER_ANCHORS = ("DataSetDesignerBody",)
SETTINGS_TABS = ("SettingsTabStorage", "SettingsTabAppearance", "SettingsTabPerformance",
                 "SettingsTabTools", "SettingsTabAiModels", "SettingsTabUpdates")
AI_ANCHORS = ("SettingsAiModelsList", "SettingsAiModelsRefreshButton",
              "SettingsAiModelsDownloadMissingButton", "SettingsAiModelsOpenFolderButton")
SETTINGS_UPDATES_ANCHORS = ("SettingsUpdateCurrentVersion", "SettingsUpdateChannelCombo",
                            "SettingsAutoCheckUpdatesCheck", "SettingsCheckUpdatesButton")

# El distintivo de la barra (la entrada con estado de contexto) y la entrada del cajon.
BADGE = "ControlBarUpdateBadge"
VFS_ENTRY = "ControlBarDrawerVfsButton"
MENU = "ControlBarMenuButton"

RESULT = os.path.join(sess.SHOTS, "windows2-session.json")


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
    """El texto que el canal externo lee de un control por su ancla (y el de sus descendientes: el rotulo de
    un chip vive en un TextBlock hijo, no en el boton)."""
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


def press(win, aid, wait=STEP):
    el = dlg.by_aid(win, aid)
    if el is None:
        return False
    drv.invoke(el)
    time.sleep(wait)
    return True


def select(win, aid, wait=STEP):
    """Selecciona un control de SELECCION (las pestanas de una superficie son RadioButton): su patron es
    SelectionItem, no Invoke —el Invoke de un RadioButton devuelve que si y no conmuta nada, medido—."""
    el = dlg.by_aid(win, aid)
    if el is None:
        return False
    drv.select_item(el)
    time.sleep(wait)
    return True


def main():
    if "--session" not in sys.argv:
        drv.log("[win2] nada que hacer: usa --session")
        return 2

    os.makedirs(sess.SHOTS, exist_ok=True)
    win = drv.connect()
    if win is None:
        drv.log("[win2] FALLO: la app no aparece por UIA")
        return 3

    hwnd = int(json.load(open(sess.STATE))["hwnd"])
    results = []
    prefs_before = md5(PREFS)

    def check(condition, what, detail=""):
        results.append(bool(condition))
        drv.log("[win2] %s %s%s" % ("[OK]   " if condition else "[FALLO]", what,
                                    ("  <- " + detail) if detail else ""))
        return bool(condition)

    def cards():
        titles = ("Folder Source", "Optimizador de Imágenes", "Destination Sink")
        return len(dlg.elements(win, lambda n: n in titles, dlg.CANVAS))

    drv.log("[win2] preferencias del usuario: md5=%s" % prefs_before)

    # ── 0. Linea base: el chip del VFS y el distintivo, con su estado de contexto ──
    img0 = sess.grab(hwnd, "70_base")
    c0 = menu.dominant(img0, CENTER)
    base_anchors = aid_set(win)
    base_cards = cards()
    chip = text_of(win, "ControlBarVfsButton")
    badge = text_of(win, BADGE)
    drv.log("[win2] base: tarjetas=%d pixel centro=%s (%.1f%%) chip VFS=%r distintivo=%r"
            % (base_cards, menu.hexs(c0[0]), 100 * c0[1], chip, badge))
    check(base_cards == 3, "el lienzo del ejemplo arranca con sus 3 tarjetas",
          "tarjetas=%d" % base_cards)
    check("ControlBarVfsButton" in base_anchors and "ControlBarUpdateBadge" in base_anchors,
          "las dos entradas con estado de contexto estan en la barra (el chip del VFS y el distintivo)",
          "chip=%r distintivo=%r" % (chip, badge))
    check(not any(a in base_anchors for a in VFS_ANCHORS + UPDATE_ANCHORS),
          "y ninguna de las dos superficies esta abierta al arrancar",
          "contenedores (sin peer, los lee la sonda): %s"
          % ", ".join(VFS_CONTAINER_ANCHORS + UPDATE_CONTAINER_ANCHORS))

    # ── 1. El EXPLORADOR VIRTUAL: la entrada del cajon con el almacen de la semilla ──
    check(press(win, MENU), "el boton «Menu» se pulsa")
    check(press(win, VFS_ENTRY, STEP + 1.2),
          "la entrada «Explorador de Archivos Virtual» del cajon se pulsa")
    vfs_anchors = aid_set(win)
    img1 = sess.grab(hwnd, "71_vfs_explorador")
    c1 = menu.dominant(img1, CENTER)
    vfs_rows = rows_of(win, "VfsFileList")
    vfs_status = text_of(win, "VfsTotalFiles")
    present = [a for a in VFS_ANCHORS if a in vfs_anchors]
    drv.log("[win2] VFS: anclas=%s pixel centro=%s (%.1f%%) filas=%d estado=%r"
            % (present, menu.hexs(c1[0]), 100 * c1[1], len(vfs_rows), vfs_status))
    check(len(present) == len(VFS_ANCHORS),
          "pulsar la entrada abre el EXPLORADOR VIRTUAL del host: buscador, lista, estado y cierre",
          "presentes: %s (contenedor %s: lo lee la sonda)"
          % (", ".join(present), ", ".join(VFS_CONTAINER_ANCHORS)))
    check(len(vfs_rows) > 0,
          "y su lista expone los ARCHIVOS del almacen virtual, leidos por el canal externo",
          "%d filas: %s" % (len(vfs_rows), "; ".join(r.split("\n")[0][:30] for r in vfs_rows[:4])))
    check(c1[0] != c0[0], "el explorador se ve en el pixel central",
          "%s -> %s" % (menu.hexs(c0[0]), menu.hexs(c1[0])))

    check(press(win, "VfsCloseButton", STEP), "el explorador se cierra por su propio boton")
    closed = aid_set(win)
    img2 = sess.grab(hwnd, "72_vfs_cerrado")
    c2 = menu.dominant(img2, CENTER)
    drv.log("[win2] tras cerrar el VFS: pixel centro=%s (%.1f%%) anclas=%s"
            % (menu.hexs(c2[0]), 100 * c2[1], sorted(a for a in closed if a in VFS_ANCHORS)))
    check(not any(a in closed for a in VFS_ANCHORS), "y su superficie sale del arbol")
    check(c2[0] == c0[0], "el pixel central vuelve al de la linea base",
          "%s -> %s -> %s" % (menu.hexs(c0[0]), menu.hexs(c1[0]), menu.hexs(c2[0])))

    # ── 2. El AVISO DE ACTUALIZACION: el distintivo de la barra ──
    check(press(win, BADGE, STEP + 1.2),
          "el distintivo de actualizacion se pulsa (su orden abre el aviso por el catalogo del host)")
    update_anchors = aid_set(win)
    img3 = sess.grab(hwnd, "73_aviso_actualizacion")
    c3 = menu.dominant(img3, CENTER)
    new_version = text_of(win, "UpdateNewVersion")
    current_version = text_of(win, "UpdateCurrentVersion")
    present_update = [a for a in UPDATE_ANCHORS if a in update_anchors]
    drv.log("[win2] aviso: anclas=%s pixel centro=%s (%.1f%%) actual=%r nueva=%r"
            % (present_update, menu.hexs(c3[0]), 100 * c3[1], current_version, new_version))
    check(len(present_update) == len(UPDATE_ANCHORS),
          "pulsar el distintivo abre el AVISO DE ACTUALIZACION del host: versiones, novedades y sus ordenes",
          "presentes: %s (contenedor %s: lo lee la sonda)"
          % (", ".join(present_update), ", ".join(UPDATE_CONTAINER_ANCHORS)))
    check(new_version.strip() != "" and "9.9.9" in new_version,
          "y anuncia la version nueva sembrada, leida por el canal externo", repr(new_version))
    check(current_version.strip() != "" and current_version != new_version,
          "con la version ACTUAL del producto al lado (distinta de la nueva)", repr(current_version))
    check(c3[0] != c0[0], "el aviso se ve en el pixel central",
          "%s -> %s" % (menu.hexs(c0[0]), menu.hexs(c3[0])))

    check(press(win, "UpdateRemindButton", STEP), "el aviso se retira por su orden «recordarmelo luego»")
    closed2 = aid_set(win)
    img4 = sess.grab(hwnd, "74_aviso_cerrado")
    c4 = menu.dominant(img4, CENTER)
    badge_after = text_of(win, BADGE)
    drv.log("[win2] tras cerrar el aviso: pixel centro=%s (%.1f%%) anclas=%s distintivo=%r"
            % (menu.hexs(c4[0]), 100 * c4[1],
               sorted(a for a in closed2 if a in UPDATE_ANCHORS), badge_after))
    check(not any(a in closed2 for a in UPDATE_ANCHORS), "y su superficie sale del arbol")
    check(c4[0] == c0[0], "el pixel central vuelve al de la linea base",
          "%s -> %s -> %s" % (menu.hexs(c0[0]), menu.hexs(c3[0]), menu.hexs(c4[0])))

    # ── 3. El DISEÑADOR DE DATASETS: la superficie que declara el NODO al SDK ──
    #
    # Su orden NO es el comando canonico del nucleo (ese construye la ventana que monta el plugin con
    # Avalonia): el cajon declara la intencion y la ventana pide al nodo que superficie quiere y que
    # contiene. Aqui se mide el camino del usuario —la entrada del cajon— y lo que la superficie ensena.
    check(press(win, MENU), "el cajon se despliega para el Diseñador de Datasets")
    check(press(win, "ControlBarDrawerDataSetButton", STEP + 1.5),
          "la entrada «Diseñador de Datasets» del cajon se pulsa")
    designer_anchors = aid_set(win)
    img5 = sess.grab(hwnd, "75_disenador_datasets")
    c5 = menu.dominant(img5, CENTER)
    designer_rows = rows_of(win, "DataSetList")
    designer_present = [a for a in DESIGNER_ANCHORS if a in designer_anchors]
    drv.log("[win2] disenador: anclas=%s pixel centro=%s (%.1f%%) datasets=%d %s"
            % (designer_present, menu.hexs(c5[0]), 100 * c5[1], len(designer_rows),
               json.dumps(designer_rows[:2], ensure_ascii=False)))
    check(len(designer_present) == len(DESIGNER_ANCHORS),
          "la entrada abre el DISEÑADOR DE DATASETS del host: catalogo, arbol, pestanas, inspector y cierre",
          "presentes: %s (contenedor %s: lo lee la sonda)"
          % (", ".join(designer_present), ", ".join(DESIGNER_CONTAINER_ANCHORS)))
    check(len(designer_rows) > 0,
          "y su catalogo NO esta vacio: los datasets del plugin se leen por el canal externo",
          "%d datasets: %s" % (len(designer_rows), "; ".join(r.split("\n")[0][:26] for r in designer_rows[:3])))
    check(c5[0] != c0[0], "el disenador se ve en el pixel central",
          "%s -> %s" % (menu.hexs(c0[0]), menu.hexs(c5[0])))

    check(press(win, "DataSetCloseButton", STEP), "el disenador se cierra por su propio pie")
    closed3 = aid_set(win)
    img6 = sess.grab(hwnd, "76_disenador_cerrado")
    c6 = menu.dominant(img6, CENTER)
    drv.log("[win2] tras cerrar el disenador: pixel centro=%s (%.1f%%) anclas=%s"
            % (menu.hexs(c6[0]), 100 * c6[1], sorted(a for a in closed3 if a in DESIGNER_ANCHORS)))
    check(not any(a in closed3 for a in DESIGNER_ANCHORS), "y su superficie sale del arbol")
    check(c6[0] == c0[0], "el pixel central vuelve al de la linea base",
          "%s -> %s -> %s" % (menu.hexs(c0[0]), menu.hexs(c5[0]), menu.hexs(c6[0])))

    # ── 4. Las DOS SECCIONES nuevas de los ajustes: modelos de IA y actualizaciones ──
    check(press(win, "SettingsButton", STEP + 1.0), "el boton de ajustes de la barra se pulsa")
    opened_settings = aid_set(win)
    img7 = sess.grab(hwnd, "77_ajustes_seis_secciones")
    c7 = menu.dominant(img7, CENTER)
    six = [a for a in SETTINGS_TABS if a in opened_settings]
    drv.log("[win2] ajustes: pestanas=%s pixel centro=%s (%.1f%%)"
            % (sorted(six), menu.hexs(c7[0]), 100 * c7[1]))
    check(len(six) == len(SETTINGS_TABS),
          "la superficie de ajustes expone sus SEIS pestanas (las cuatro del 255 y las dos del 261)",
          "presentes: %s" % ", ".join(sorted(six)))

    check(select(win, "SettingsTabAiModels", STEP + 0.8), "la pestana «Modelos de IA» se selecciona")
    ai_anchors = aid_set(win)
    img8 = sess.grab(hwnd, "78_ajustes_modelos_ia")
    c8 = menu.dominant(img8, CENTER)
    ai_rows = rows_of(win, "SettingsAiModelsList")
    ai_dir = text_of(win, "SettingsAiModelsDirText")
    drv.log("[win2] modelos de IA: anclas=%s filas=%d carpeta=%r"
            % (sorted(a for a in ai_anchors if a in AI_ANCHORS), len(ai_rows), ai_dir))
    check(all(a in ai_anchors for a in AI_ANCHORS) and len(ai_rows) > 0,
          "y su seccion ensena el catalogo del gestor del nucleo (una fila por modelo), su carpeta y sus ordenes",
          "%d modelos; carpeta=%s" % (len(ai_rows), ai_dir))

    check(select(win, "SettingsTabUpdates", STEP + 0.8), "la pestana «Actualizaciones» se selecciona")
    update_anchors2 = aid_set(win)
    img9 = sess.grab(hwnd, "79_ajustes_actualizaciones")
    c9 = menu.dominant(img9, CENTER)
    settings_version = text_of(win, "SettingsUpdateCurrentVersion")
    check(all(a in update_anchors2 for a in SETTINGS_UPDATES_ANCHORS)
          and settings_version.strip() != "",
          "y su seccion ensena la version del PRODUCTO, el formato, el canal y la comprobacion automatica",
          "version='%s'" % settings_version)
    drv.log("[win2] actualizaciones: anclas=%s version=%r"
            % (sorted(a for a in update_anchors2 if a in SETTINGS_UPDATES_ANCHORS), settings_version))

    check(press(win, "SettingsCancelButton", STEP), "la superficie de ajustes se recoge sin guardar nada")
    closed4 = aid_set(win)
    img10 = sess.grab(hwnd, "80_ajustes_cerrados")
    c10 = menu.dominant(img10, CENTER)
    check(not any(a in closed4 for a in SETTINGS_TABS),
          "y sus pestanas salen del arbol", "pixel centro=%s" % menu.hexs(c10[0]))
    check(c10[0] == c0[0], "el pixel central vuelve al de la linea base",
          "%s -> %s" % (menu.hexs(c0[0]), menu.hexs(c10[0])))

    # ── 5. Cierre: la escena y tus preferencias, como al entrar ──
    img11 = sess.grab(hwnd, "99_final")
    c11 = menu.dominant(img11, CENTER)
    prefs_after = md5(PREFS)
    drv.log("[win2] final: tarjetas=%d pixel centro=%s (%.1f%%) preferencias md5=%s"
            % (cards(), menu.hexs(c11[0]), 100 * c11[1], prefs_after))
    check(c11[0] == c0[0], "la sesion termina con la escena igual a la de la linea base",
          "centro %s/%s" % (menu.hexs(c0[0]), menu.hexs(c11[0])))
    check(cards() == base_cards, "y el lienzo con las mismas tarjetas", "%d" % cards())
    check(prefs_after == prefs_before,
          "el fichero de preferencias del usuario queda byte-identico (las dos ventanas de consulta no escriben)",
          "%s / %s" % (prefs_before, prefs_after))

    ok = all(results)
    drv.log("[win2] === RESULTADO: %s (%d de %d pasos) ==="
            % ("VERIFICADO" if ok else "FALLOS", sum(results), len(results)))
    with open(RESULT, "w", encoding="utf-8") as fh:
        json.dump({"pasos": len(results), "verificados": sum(results),
                   "tarjetas": base_cards,
                   "anclas": {"vfs": present, "actualizacion": present_update,
                              "disenador": designer_present, "ajustes": sorted(six)},
                   "filas": {"vfs": len(vfs_rows), "vfs_detalle": vfs_rows,
                             "datasets": len(designer_rows), "modelos_ia": len(ai_rows)},
                   "textos": {"chip_vfs": chip, "estado_vfs": vfs_status,
                              "version_actual": current_version, "version_nueva": new_version,
                              "version_en_ajustes": settings_version,
                              "carpeta_modelos": ai_dir,
                              "distintivo": badge, "distintivo_tras_cerrar": badge_after},
                   "pixel_centro": {"base": menu.hexs(c0[0]), "vfs": menu.hexs(c1[0]),
                                    "vfs_cerrado": menu.hexs(c2[0]), "aviso": menu.hexs(c3[0]),
                                    "aviso_cerrado": menu.hexs(c4[0]),
                                    "disenador": menu.hexs(c5[0]), "disenador_cerrado": menu.hexs(c6[0]),
                                    "ajustes": menu.hexs(c7[0]),
                                    "ajustes_modelos": menu.hexs(c8[0]),
                                    "ajustes_actualizaciones": menu.hexs(c9[0]),
                                    "final": menu.hexs(c11[0])},
                   "preferencias_md5": {"antes": prefs_before, "despues": prefs_after}},
                  fh, ensure_ascii=False, indent=2)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
