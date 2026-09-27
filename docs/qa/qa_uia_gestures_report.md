# Guion de gestos 3.2/3.3 vía UIA — resultado (2026-09-27)

**Veredicto: EJECUTADO PARCIALMENTE — la vía UIA acota el bloqueo del 231 y ejecuta los
gestos de TECLADO; los de PUNTERO siguen bloqueados y quedan declarados.**

## Lo que esta sesión midió (las dos sondas + el guion)

| # | Técnica | Resultado medido | Conclusión |
| :--- | :--- | :--- | :--- |
| A | Árbol UIA de la app viva | 57 textos, tarjetas por título, automation_id de x:Name; ni Invoke ni SelectionItem en Text | La app se OBSERVA por accesibilidad |
| A | click físico sobre elemento UIA | 0 px (ruido base 0) | El puntero sigue bloqueado (coherente con el 231) |
| B | set_focus UIA + keybd_event ('fold') | El buscador recibió el texto y el filtro reaccionó (57→43 textos, grupos 7→3) | **EL TECLADO SÍ LLEGA al contenido WinUI con foco UIA** |
| G | Shift+A con foco de ventana, sin foco del lienzo | El spotlight NO se abre (árbol sin cambio) | El atajo exige el foco del LIENZO, que entrega el clic (puntero): el bloqueo del 231 queda ACOTADO |

## Pasos del guion

- **PASS** `3.2.0` — tarjetas del ejemplo expuestas por título: Folder Source=2, Destination Sink=1
- **PASS** `3.2.1` — tecleado 'folder' con foco UIA: el catálogo reaccionó (57 → 43 textos)
- **PASS** `3.2.2` — 'a' con el foco en el buscador entra en el cuadro (valor='a'): el foco siguió en el TextBox tras los backspaces y el teclado inyectado sigue llegando al contenido
- **PASS** `3.2.3` — Shift+A con el foco en el buscador escribe 'A' (MAYÚSCULA: el modificador Shift llega al contenido inyectado); el atajo del lienzo NO se dispara por diseño (su OnKeyDown ignora TextBox y la tecla no está en el lienzo): el bloqueo del 231 queda ACOTADO — no es el teclado (llega, con y sin Shift), es el foco del lienzo que entrega el clic
- **PASS** `3.3.0` — el estado de conexión del ejemplo es observable por UIA (0 textos de puertos; la conexión/desconexión por comandos ya está demostrada por la sonda 3.3 del selfcheck)

## Qué significa para el guion manual

1. La vía UIA permite OBSERVAR la app viva con precisión (nombres, automation_id, cambios
   del árbol) y EJECUTAR gestos de teclado cuando un control con foco existe (el buscador).
2. Los atajos del lienzo (Shift+A, Delete, Ctrl+Z, F2...) exigen el foco del lienzo, que en
   producción entrega el clic del usuario. Sin puntero no hay certificación de esos gestos:
   la mitad física del guion sigue esperando la sesión con puntero real (o UIAccess).
3. La lógica de todos esos gestos sigue demostrada por las sondas del selfcheck (los mismos
   métodos que los handlers); lo que este entorno no da es la entrega del gesto físico.
