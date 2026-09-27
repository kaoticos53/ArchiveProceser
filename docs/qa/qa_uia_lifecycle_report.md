# Guion UIA del ciclo completo — `qa_uia_lifecycle.py` (hito 243)

**Fecha**: 2026-09-27 · **Instrumento**: `docs/qa/qa_uia_lifecycle.py` (pywinauto 0.6.9, backend `uia`, sin UIAccess).

## El encargo

«Extiende el guion UIA para verificar el ciclo completo: ejecutar el flujo, ver el snapshot nuevo
aparecer en la pestaña y el diff recalculado».

## Resultados

| # | Medición | Veredicto | Evidencia |
| :-- | :--- | :--- | :--- |
| C0_superficie_uia_viva | PASS | CanvasRoot=sí, foco=sí, nivel='100 %' |
| C1_execute_button_expuesto | PASS | ancla ExecuteButton en el árbol UIA |
| C2_ciclo_del_motor_por_cli | PASS | Succeeded=True, items=1, nodos=['node-opt', 'node-snk', 'node-src'] |
| C3_canal_del_proceso_observable | PASS | linea='run: idle | node=Folder Source snapshots=0 diff=0' |

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
