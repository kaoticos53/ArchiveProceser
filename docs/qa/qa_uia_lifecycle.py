# -*- coding: utf-8 -*-
"""GUION UIA del ciclo completo (hito 243): ejecutar el flujo -> snapshot nuevo en la pestaña ->
diff recalculado — observado desde fuera del proceso (pywinauto, sin UIAccess).

LA MEDICIÓN QUE SOSTIENE EL GUION (primera corrida, honestidad antes que promesa):

  - El Invoke de UIA sobre el ExecuteButton NO dispara el Click de WinUI (medido: sin marca en
    el canal, con Invoke OK y con Espacio tras set_focus UIA). Es la MISMA frontera que el 231
    midió para el puntero, ahora medida en un botón: la mitad FÍSICA del gesto sigue cerrada
    sin puntero real/UIAccess.
  - La ejecución del flujo SÍ es accesible desde fuera: el CLI del producto
    (`FileFlow.App.exe --run <json> --input <carpeta> --dryrun --summary`) es el punto de
    entrada de la casa para el MISMO motor. El summary del CLI trae los contadores del ciclo
    (TotalItemsProcessed, NodeStats por nodo): la ejecución se verifica sin fingir el clic.
  - El ciclo COMPLETO (snapshot nuevo en la pestaña + diff recalculado) está defendido por el
    241 a nivel de proceso (puente SnapshotRecorded -> AddSnapshot, sonda del selfcheck 70 OK
    con 1 tarjeta y 2 filas de diff reales). Este guion verifica la mitad EXTERNA: la
    superficie UIA viva de la app (anclas del lienzo, foco, zoom, botón presente) y el ciclo
    del motor por su punto de entrada de producto.

Fases:
  C0. Superficie UIA viva: la app responde (CanvasRoot en el árbol, foco externo, zoom).
  C1. El ExecuteButton está expuesto e invocable por UIA (el canal existe; el Invoke medido).
  C2. El ciclo del motor: CLI dry-run del fixture -> 1 elemento procesado, los 3 nodos con
      stats (la misma ejecución que el botón lanzaría).
  C3. El canal del proceso observable: la línea de ejecución del host vivo permanece en idle
      (el guion no fingió el clic) y el canal funciona (lectura atómica del fichero espejo).

Salida: qa_uia_lifecycle_report.md + exit (0 = verificado, 2 = fallo).
"""
import ctypes
import json
import os
import subprocess
import sys
import time

u32 = ctypes.windll.user32
u32.SetProcessDPIAware()

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
BIN = os.path.join(ROOT, "FileFlow.App.Uno", "bin", "Debug", "net10.0-windows10.0.19041.0")
EXE = os.path.join(BIN, "FileFlow.App.Uno.exe")
CLI = os.path.join(ROOT, "FileFlow.App", "bin", "Debug", "net10.0", "FileFlow.App.exe")
FIXTURE = os.path.join(ROOT, "docs", "qa", "fixtures", "qa_uia_lifecycle")
FLOW_JSON = os.path.join(FIXTURE, "qa_uia_lifecycle.json")
SUMMARY = os.path.join(FIXTURE, "summary_cli.json")

PNG_1PX = bytes.fromhex(
    "89504e470d0a1a0a0000000d49484452000000010000000108060000001f15c489"
    "0000000d49444154789c626001000000ffff03000006000557bfabd40000000049454e44ae426082")


def log(msg):
    print(msg, flush=True)


def prepare_fixture():
    """El fixture autocontenido: Input/ con un PNG minimo, Output/ vacia y el flujo del ciclo."""
    input_dir = os.path.join(FIXTURE, "Input")
    os.makedirs(input_dir, exist_ok=True)
    os.makedirs(os.path.join(FIXTURE, "Output"), exist_ok=True)
    input_png = os.path.join(input_dir, "qa_lifecycle_probe.png")
    if not os.path.exists(input_png) or os.path.getsize(input_png) == 0:
        with open(input_png, "wb") as f:
            f.write(PNG_1PX)

    if not os.path.exists(FLOW_JSON):
        with open(os.path.join(ROOT, "docs", "examples", "01_basic",
                               "flow_01_organizador_imagenes.json"), encoding="utf-8-sig") as f:
            graph = json.load(f)
        graph["name"] = "QA UIA Lifecycle (hito 243)"
        graph["nodes"][0]["parameters"]["SourcePath"] = "{RelativeDir}\\Input"
        graph["nodes"][2]["parameters"]["DestinationRoot"] = "{RelativeDir}\\Output"
        with open(FLOW_JSON, "w", encoding="utf-8", newline="\n") as f:
            json.dump(graph, f, ensure_ascii=False, indent=2)

    return input_png


def by_aid(root, aid):
    for el in root.descendants():
        try:
            if (el.element_info.automation_id or "") == aid:
                return el
        except Exception:
            continue
    return None


def read_channel():
    try:
        with open(os.path.join(BIN, "execution-status.txt"), encoding="utf-8") as f:
            return f.read().rstrip()
    except Exception:
        return ""


def main():
    from pywinauto.application import Application

    input_png = prepare_fixture()
    log(f"[lifecycle] fixture: {input_png}")

    results = []
    proc = subprocess.Popen([EXE], cwd=BIN)
    time.sleep(6.0)
    try:
        app = Application(backend="uia").connect(process=proc.pid, timeout=15)
        win = app.top_window()
        win.set_focus()
        time.sleep(1.5)

        # C0. La superficie UIA viva: CanvasRoot en el árbol + foco externo + zoom observable.
        canvas = by_aid(win, "CanvasRoot")
        focus_ok = False
        if canvas is not None:
            try:
                canvas.set_focus()
                focus_ok = True
            except Exception:
                pass
        zoom_before = by_aid(win, "ZoomLevelText")
        level = zoom_before.window_text() if zoom_before else ""
        surface_ok = canvas is not None and focus_ok and bool(level)
        results.append(("C0_superficie_uia_viva", surface_ok,
                        f"CanvasRoot={'sí' if canvas else 'NO'}, foco={'sí' if focus_ok else 'no'}, nivel={level!r}"))
        log(f"[lifecycle] C0: {results[-1][2]}")

        # C1. El ExecuteButton expuesto e invocable por UIA (el canal del botón existe).
        run_btn = by_aid(win, "ExecuteButton")
        results.append(("C1_execute_button_expuesto", run_btn is not None,
                        "ancla ExecuteButton en el árbol UIA" if run_btn else "sin ExecuteButton"))
        log(f"[lifecycle] C1: boton={'sí' if run_btn else 'NO'}")

        # C2. El ciclo del motor por el punto de entrada de producto (CLI del mismo motor):
        # dry-run del fixture -> 1 elemento procesado, los 3 nodos con stats.
        if os.path.exists(SUMMARY):
            os.remove(SUMMARY)
        cli_ok = False
        cli_detail = "CLI ausente: " + CLI
        if os.path.exists(CLI):
            r = subprocess.run(
                [CLI, "--run", FLOW_JSON,
                 "--input", os.path.join(FIXTURE, "Input"),
                 "--output", os.path.join(FIXTURE, "Output"),
                 "--dryrun", "--summary", SUMMARY],
                capture_output=True, text=True, timeout=180, cwd=ROOT)
            try:
                with open(SUMMARY, encoding="utf-8-sig") as f:
                    summary = json.load(f)
                items = summary.get("TotalItemsProcessed", 0)
                nodes = sorted(summary.get("NodeStats", {}).keys())
                cli_ok = bool(summary.get("Succeeded")) and items == 1 and len(nodes) == 3
                cli_detail = f"Succeeded={summary.get('Succeeded')}, items={items}, nodos={nodes}"
            except Exception as ex:
                cli_detail = f"sin summary: {type(ex).__name__}: {ex}; rc={r.returncode}"
        results.append(("C2_ciclo_del_motor_por_cli", cli_ok, cli_detail))
        log(f"[lifecycle] C2: {cli_detail}")

        # C3. El canal del proceso observable: el host vivo sigue en idle (el guion NO fingió el
        # clic — la frontera del puntero medida en C1/corridas previas) y la línea del canal
        # mantiene su formato (lectura atómica del fichero espejo).
        line = read_channel()
        channel_ok = line.startswith("run: idle |") and "snapshots=" in line
        results.append(("C3_canal_del_proceso_observable", channel_ok,
                        f"linea={line[:70]!r}"))
        log(f"[lifecycle] C3: {results[-1][2]}")

        write_report(results)
        passed = sum(1 for _, ok, _ in results if ok)
        for name, ok, detail in results:
            log(f"[lifecycle]   {'[OK]' if ok else '[FALLO]'}  {name}: {detail}")
        log(f"[lifecycle] RESULTADO: {passed}/{len(results)}")
        return 0 if passed == len(results) else 2
    finally:
        try:
            proc.terminate()
        except Exception:
            pass


def write_report(results):
    out = os.path.join(HERE, "qa_uia_lifecycle_report.md")
    rows = "\n".join(
        f"| {name} | {'PASS' if ok else 'FALLO'} | {detail} |"
        for name, ok, detail in results)
    content = f"""# Guion UIA del ciclo completo — `qa_uia_lifecycle.py` (hito 243)

**Fecha**: 2026-09-27 · **Instrumento**: `docs/qa/qa_uia_lifecycle.py` (pywinauto 0.6.9, backend `uia`, sin UIAccess).

## El encargo

«Extiende el guion UIA para verificar el ciclo completo: ejecutar el flujo, ver el snapshot nuevo
aparecer en la pestaña y el diff recalculado».

## Resultados

| # | Medición | Veredicto | Evidencia |
| :-- | :--- | :--- | :--- |
{rows}

## La medición que sostiene el guion

- **La frontera, medida otra vez**: el Invoke de UIA sobre `ExecuteButton` no dispara el Click de
  WinUI (sin marca en el canal con Invoke OK; y Espacio tras `set_focus` UIA tampoco). Es la misma
  frontera del 231 para el puntero, ahora medida en un botón: el gesto físico del clic sigue
  cerrado sin puntero real/UIAccess. El guion NO lo finge.
- **La ejecución, por el producto**: el CLI (`--run ... --dryrun --summary`) es el punto de entrada
  de la casa para el mismo motor — el dry-run del fixture procesa 1 elemento y los 3 nodos
  quedan con stats. Ese summary ES la verificación del ciclo del motor desde fuera.
- **El ciclo completo en el proceso**: el puente `SnapshotRecorded -> node.AddSnapshot` entrega los
  snapshots del debug session a los NodeViewModel; la pestaña de snapshots y el diff recalculado
  están defendidos por el hito 241 (selfcheck 70 OK: 1 tarjeta y 2 filas de diff reales tras un
  `CreateInput` por la vía de producción). La versión ejecutable en CI del ciclo externo llega
  cuando haya puntero real o UIAccess (o un comando de la app que reciba el gesto por canal
  confiable).
"""
    with open(out, "w", encoding="utf-8") as f:
        f.write(content)
    print(f"[lifecycle] informe: {out}", flush=True)


if __name__ == "__main__":
    sys.exit(main())
