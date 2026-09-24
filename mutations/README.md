# Mutaciones declaradas

Una **mutación** es un defecto deliberado y pequeño sobre el código de producto. Si la suite sigue en verde con
el defecto dentro, la prueba que decía cubrir ese comportamiento no lo cubre: por eso el andamiaje lo cuenta como
**fallo**, no como curiosidad.

```powershell
.\mutate.ps1 -List                       # qué hay declarado y qué testigo tiene cada una
.\mutate.ps1 -Name limpiador-vuelve-a-mirar-el-disco
.\mutate.ps1 -All
.\mutate.ps1 -Coverage                   # publica lo declarado y los huecos (lee mutations/COVERAGE.md)
```

En Linux o macOS: `pwsh ./mutate.ps1 -All` (una sola implementación, también para el andamiaje: duplicar la lógica
de restauración en un script de shell es duplicar la parte que no puede fallar).

## El contrato del andamiaje

El ejecutor es responsable de tres cosas, y las tres están probadas:

1. **Restaurar siempre.** La restauración vive en un `finally`: da igual si el mutante no compila, si los tests
   revientan o si una mutación resulta obsoleta.
2. **Recompilar siempre después de restaurar.** Las fuentes restauradas no arreglan los binarios. Una corrida
   posterior con `--no-build` mediría el mutante: está medido, y costó dos pruebas «rotas» que eran el mutante
   que quedó compilado de una corrida anterior.
3. **Negarse a dejar el mutante en el árbol.** Antes de tocar nada se escribe un diario en disco
   (`.mutation-journal/`, ignorado por git) con una copia y el hash de cada fichero. Al terminar se restaura por
   bytes, se recompila y se verifica por hash; un diario sin cerrar (proceso matado) se recupera al arrancar la
   siguiente corrida. Nada de esto se fía de la memoria del proceso.

La comprobación final de cada mutación es la que cierra el círculo: el testigo se vuelve a ejecutar **sin
recompilar** y tiene que estar en verde. Sólo puede pasar si la recompilación tras restaurar ocurrió de verdad,
así que cuando el andamiaje termina, las fuentes *y* los binarios son los originales.

## Formato

Un fichero JSON por mutación (`mutations/<id>.json`). Las líneas se comparan normalizadas (el fichero se lee sin
CRLF, se edita y se vuelve a escribir con su terminador original), así que los fragmentos se declaran en una sola
convención.

| Campo | Obligatorio | Qué es |
| :--- | :--- | :--- |
| `id` | sí | Identificador único (el mismo del fichero). |
| `hito` | no | Hito del walkthrough del que sale la mutación. |
| `claim` | sí | Qué comportamiento afirma la mutación, en positivo: **lo que la suite tiene que saber defender**. |
| `why` | no | Por qué ese defecto es real y qué rompe. |
| `edits[]` | sí | Ficheros a mutar, con su ruta relativa a la raíz del repositorio. |
| `edits[].replacements[].old` | sí | Fragmento **literal** que tiene que existir. Aparece por omisión **exactamente una vez**; si aparece más, se declara `count`. |
| `edits[].replacements[].new` | sí | Lo que lo sustituye (cadena vacía para borrar el fragmento). |
| `witness.filter` | sí | Filtro de `dotnet test` que tiene que ponerse **rojo**. Sin testigo no hay nada que morder. |
| `witness.why` | no | Por qué ese testigo es el que detecta el defecto. |
| `control.filter` | no | Filtro que tiene que **seguir verde**: es lo que convierte la mutación en precisa (rompe lo que dice romper y nada más). |
| `control.why` | no | Por qué ese control no depende de lo que se muta. |

Todas las sustituciones se validan y se aplican **en memoria** antes de escribir un solo byte, así que una
mutación obsoleta (el producto cambió desde que se declaró) se rechaza con el fichero y el fragmento citados y
**no deja media mutación aplicada**.

## Veredictos

| Veredicto | Significado | Salida |
| :--- | :--- | :--- |
| `MUERDE` | El testigo se puso rojo y el control siguió verde. Es lo que se busca. | 0 |
| `SOBREVIVE` | El testigo siguió verde: la prueba que dice cubrir esto no lo detecta. | 1 |
| `IMPRECISA` | El testigo se puso rojo, pero también el control: el mutante no dice lo que la tesis afirma. | 1 |
| `NO-COMPILA` | El defecto declarado no compila: no es un defecto silencioso. | 1 |
| `RECHAZO` | Mutación obsoleta, restauración incompleta o binarios que no corresponden al árbol restaurado. | 2 |

## Cobertura publicada

[`COVERAGE.md`](file:///mutations/COVERAGE.md) publica lo que el catálogo cubre **y lo que no**: qué declara cada
mutación, **los subsistemas del producto que no tienen ninguna** y **las guardias del repositorio que nadie ha
demostrado que muerdan**. Lo genera `MutationDeclarationCoverageTests` desde los JSON y el árbol de fuentes, y la
guardia falla si el documento publicado se queda atrás:

```powershell
FILEFLOW_UPDATE_MUTATION_COVERAGE=1 dotnet test --filter MutationDeclarationCoverageTests
```

Tres reglas de clasificación, para leerlo sin engaños:

- Un **subsistema** es un proyecto del producto (`FileFlow.*` menos el suite), y una mutación cubre el del
  **fichero que muta**. Una mutación sobre la *declaración* de una guardia (el censo, el analizador) cuenta como
  **infraestructura de pruebas**: es honesto y útil, pero no cubre ningún subsistema del producto.
- Una **guardia del repositorio** es una prueba que audita el árbol —usa `SourceTree`, `TestRepositoryLocator` o
  `TestSuiteIndex`—; una guardia sin ninguna mutación que la cite como testigo está escrita y nadie ha demostrado
  que muerda.
- Los **huecos** son el resultado esperado, no una anomalía: la lista de proyectos sin mutación es la lista de
trabajo.

## Añadir una mutación

1. Elige un **comportamiento que importa** y escribe una sustitución mínima que lo rompa (una llamada por otra,
   un filtro que desaparece, una condición que se cae).
2. Nombra el **testigo**: la prueba que sin ella no existiría. Si no hay ninguno, lo que falta es la prueba.
3. Nombra un **control** siempre que exista uno barato: es lo que distingue «el mutante rompe esto» de «el
   mutante lo rompe todo».
4. `.\mutate.ps1 -Name <id>` y comprueba que **muerde**. Es el único criterio de aceptación.
