using System;
using System.Collections.Generic;
using System.Linq;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Services;

/// <summary>
/// La tabla compartida de atajos de teclado del lienzo (fase 3.2 del plan Uno: «las claves de atajos son
/// las mismas que las del host Avalonia (tabla compartida si hace falta)»).
///
/// <para><b>Por qué vive en el núcleo</b>: los atajos son de teclado (del host), pero las CLAVES son del
/// producto — el usuario aprende Ctrl+Z, no «la tecla que la vista X decida». Con la tabla aquí, cada host
/// la consume para su bucle de teclado y una guardia compara su uso contra la referencia del escritorio:
/// si un host cambia una clave, la guardia se entera.</para>
///
/// <para><b>Qué NO entra en la tabla</b>: la navegación del spotlight (flechas/Enter/Escape) y el
/// renombrado (Enter/Escape dentro de la caja) son de sus propias ventanas/cajas de texto, no del
/// lienzo — así lo declara el §2.1 del plan («los atajos son código de vista hoy») y así lo mantiene la
/// guardia de atajos, que censará lo declarado aquí.</para>
/// </summary>
public static class EditorKeyboardShortcuts
{
    /// <summary>Una clave canónica del producto: modificadores + tecla principal.</summary>
    public enum ShortcutKey
    {
        /// <summary>Ctrl+Z: deshacer.</summary>
        Undo,

        /// <summary>Ctrl+Y o Ctrl+Shift+Z: rehacer (las dos claves canónicas).</summary>
        Redo,

        /// <summary>Ctrl+C: copiar la selección.</summary>
        Copy,

        /// <summary>Ctrl+V: pegar (con la posición de referencia del host).</summary>
        Paste,

        /// <summary>Ctrl+X: cortar la selección.</summary>
        Cut,

        /// <summary>Ctrl+D: duplicar la selección.</summary>
        Duplicate,

        /// <summary>Delete o Backspace: borrar la selección.</summary>
        Delete,

        /// <summary>F2: renombrar el nodo seleccionado.</summary>
        Rename,

        /// <summary>Shift+A o Espacio: abrir el spotlight (añadir nodo) en el cursor.</summary>
        Spotlight,

        /// <summary>Escape: cerrar el spotlight abierto.</summary>
        Escape
    }

    /// <summary>Un modificador de la clave canónica, portable entre hosts.</summary>
    [Flags]
    public enum Modifiers
    {
        /// <summary>Sin modificadores.</summary>
        None = 0,

        /// <summary>Ctrl (Win/Linux) o Meta del producto.</summary>
        Control = 1,

        /// <summary>Shift.</summary>
        Shift = 2
    }

    /// <summary>Una tecla física de teclado, portable entre hosts.</summary>
    public enum PhysicalKey
    {
        /// <summary>A.</summary>
        A,

        /// <summary>Z.</summary>
        Z,

        /// <summary>Y.</summary>
        Y,

        /// <summary>C.</summary>
        C,

        /// <summary>V.</summary>
        V,

        /// <summary>X.</summary>
        X,

        /// <summary>D.</summary>
        D,

        /// <summary>Delete.</summary>
        Delete,

        /// <summary>Backspace.</summary>
        Back,

        /// <summary>F2.</summary>
        F2,

        /// <summary>Barra espaciadora.</summary>
        Space,

        /// <summary>Escape.</summary>
        Escape
    }

    /// <summary>Un atajo: tecla física + modificadores, con el comando del producto que ejecuta.</summary>
    public sealed record Binding(PhysicalKey Key, Modifiers Modifiers, ShortcutKey Command);

    /// <summary>
    /// La tabla canónica. Es la ÚNICA fuente: la guardia de atajos compara contra ella el uso de los dos
    /// hosts, y un cambio de clave aquí es un cambio de producto (con su guardia en rojo en los dos hosts
    /// si uno de los dos no la sigue).
    /// </summary>
    public static IReadOnlyList<Binding> Table { get; } =
    [
        new(PhysicalKey.Z, Modifiers.Control, ShortcutKey.Undo),
        new(PhysicalKey.Y, Modifiers.Control, ShortcutKey.Redo),
        new(PhysicalKey.Z, Modifiers.Control | Modifiers.Shift, ShortcutKey.Redo),
        new(PhysicalKey.C, Modifiers.Control, ShortcutKey.Copy),
        new(PhysicalKey.V, Modifiers.Control, ShortcutKey.Paste),
        new(PhysicalKey.X, Modifiers.Control, ShortcutKey.Cut),
        new(PhysicalKey.D, Modifiers.Control, ShortcutKey.Duplicate),
        new(PhysicalKey.Delete, Modifiers.None, ShortcutKey.Delete),
        new(PhysicalKey.Back, Modifiers.None, ShortcutKey.Delete),
        new(PhysicalKey.F2, Modifiers.None, ShortcutKey.Rename),
        new(PhysicalKey.A, Modifiers.Shift, ShortcutKey.Spotlight),
        new(PhysicalKey.Space, Modifiers.None, ShortcutKey.Spotlight),
        new(PhysicalKey.Escape, Modifiers.None, ShortcutKey.Escape)
    ];

    /// <summary>
    /// Traduce un evento de teclado nativo (teclas + modificadores ya normalizados por el host) al comando
    /// canónico, o <c>null</c> si la combinación no está en la tabla. Es la MISMA clasificación para los
    /// dos hosts: la semántica vive aquí y no en el switch de cada vista.
    /// </summary>
    public static ShortcutKey? Resolve(PhysicalKey key, Modifiers modifiers)
    {
        foreach (var binding in Table)
        {
            if (binding.Key == key && binding.Modifiers == modifiers)
            {
                return binding.Command;
            }
        }

        return null;
    }

    /// <summary>
    /// Ejecuta el comando canónico sobre el editor del núcleo. Devuelve <c>false</c> si no hay nada que
    /// ejecutar (el host decide si marca el evento como manejado). <paramref name="position"/> es el punto
    /// del grafo de referencia para los comandos que lo usan — pegar (Ctrl+V) y abrir el spotlight en el
    /// cursor (Shift+A / Espacio) —; los demás comandos lo ignoran. Cada host proyecta su punto nativo a
    /// <see cref="FileFlow.Sdk.Point"/> antes de llamar (la regla del 217: el cruce explícito).
    /// </summary>
    public static bool Execute(ShortcutKey command, EditorViewModel editor, Sdk.Point? position = null)
    {
        ArgumentNullException.ThrowIfNull(editor);

        switch (command)
        {
            case ShortcutKey.Undo:
                editor.UndoCommand.Execute(null);
                return true;
            case ShortcutKey.Redo:
                editor.RedoCommand.Execute(null);
                return true;
            case ShortcutKey.Copy:
                editor.CopySelectedNodesCommand.Execute(null);
                return true;
            case ShortcutKey.Paste:
                editor.PasteNodesCommand.Execute(position);
                return true;
            case ShortcutKey.Cut:
                editor.CutSelectedNodesCommand.Execute(null);
                return true;
            case ShortcutKey.Duplicate:
                editor.DuplicateSelectedNodesCommand.Execute(null);
            return true;
            case ShortcutKey.Delete:
                // Supr borra LA SELECCIÓN ENTERA —los cables marcados y los nodos elegidos, con lo que cuelga
                // de ellos— como UNA operación: una selección hecha de una vez (el rectángulo marca las dos
                // cosas) se borra de una vez y se deshace de una vez. Sin nada elegido, no hace nada.
                editor.DeleteSelectionCommand.Execute(null);
                return true;
            case ShortcutKey.Rename:
                var selected = editor.Nodes.FirstOrDefault(n => n.IsSelected);
                if (selected is null)
                {
                    return false;
                }

                selected.StartRenaming();
                return true;
            case ShortcutKey.Spotlight:
                editor.OpenSpotlight(position);
                return true;
            case ShortcutKey.Escape:
                if (!editor.IsSpotlightOpen)
                {
                    return false;
                }

                editor.CloseSpotlight();
                return true;
            default:
                return false;
        }
    }
}
