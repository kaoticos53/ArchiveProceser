using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// El <b>tablero de estados</b> del sistema de diseño: la misma pieza —botón, campo, pestaña, chip— repetida
/// una vez por estado (<c>reposo</c>, <c>hover</c>, <c>pulsado</c>, <c>foco</c>, <c>deshabilitado</c>,
/// <c>seleccionada</c>), de modo que una sola captura congela la matriz completa.
///
/// <para><b>Por qué existe</b>: hasta ahora el suite afirmaba los estados con aserciones de <b>token</b> (el
/// fondo de la cara es este recurso del tema) y una guardia de texto sobre las reglas. Eso verifica que el valor
/// correcto se aplique, pero no <i>cómo se ve</i>: un cambio de radio, de borde, de opacidad o de tipografía en
/// un estado pasaba sin que nadie lo viera. Aquí cada estado tiene su celda y la captura es su contrato
/// visual.</para>
///
/// <para><b>Una foto sólo tiene un puntero y un foco</b>: por eso los estados que dependen de la entrada
/// (<c>:pointerover</c>, <c>:pressed</c>, <c>:focus</c>) se <b>fuerzan</b> en la colección de clases del propio
/// control, que es exactamente la que el sistema de estilos consulta para resolverlos. Los que no dependen de
/// ella se producen de verdad: <c>deshabilitado</c> con <c>IsEnabled = false</c>, <c>seleccionada</c> con
/// <c>IsChecked</c>, <c>SelectedIndex</c> o la clase <c>selected</c>. Y que forzar sea <b>fiel</b> no se asume:
/// <c>DesignStateBaselinesTests</c> compara el píxel renderizado por una celda forzada con el que produce la
/// entrada real y falla si difieren.</para>
///
/// <para><b>Los estados se aplican con el árbol ya vivo</b> (<see cref="Activate"/>), no al construir: al
/// aplicar la plantilla, un control vuelve a declarar sus propias pseudo-clases y <c>Button</c> borra el
/// <c>:pressed</c> que se le hubiera forzado antes de existir su plantilla —medido: la celda «pulsado» salía en
/// reposo y sólo el aserto de token lo delataba—. Por eso la captura pasa <see cref="Activate"/> como su paso
/// de interacción: es el mismo momento en el que una interacción real dispararía su entrada, y el asentado
/// posterior deja en su valor final lo que el estado haya puesto en marcha.</para>
///
/// <para><see cref="Cells"/> expone cada celda para que una prueba pueda sondear el píxel que de verdad se pinta
/// en ella y comprobar que el tablero (y su línea base) dicen el token que el tema publica.</para>
/// </summary>
public sealed class DesignStateBoard
{
    /// <summary>
    /// Anchura de la captura. Fija —las celdas tienen tamaño propio y la comparación no depende del texto— y ancha
    /// como la matriz más larga: la de selección tiene cinco estados (incluido «deshabilitado», que en un
    /// conmutador también conserva su cara).
    /// </summary>
    public const int Width = 740;

    private const int LabelWidth = 104;
    private const int CellWidth = 112;
    private const int CellHeight = 46;

    /// <summary>Estado en reposo: la pieza tal y como se ve sin interacción.</summary>
    public const string Rest = "reposo";

    /// <summary><c>:pointerover</c> — el puntero está encima.</summary>
    public const string Hover = "hover";

    /// <summary><c>:pressed</c> — el botón está pulsado.</summary>
    public const string Pressed = "pulsado";

    /// <summary><c>:focus</c> — el control tiene el foco del teclado.</summary>
    public const string Focus = "foco";

    /// <summary><c>:disabled</c> — el control está deshabilitado (aquí de verdad, con <c>IsEnabled = false</c>).</summary>
    public const string Disabled = "deshabilitado";

    /// <summary>Selección real (<c>chipButton.selected</c>, <c>TabItem:selected</c>, <c>chip:checked</c>).</summary>
    public const string Selected = "seleccionada";

    /// <summary>Selección con el puntero encima: el estado compuesto que más se declara en los estilos.</summary>
    public const string SelectedHover = "seleccionada+hover";

    /// <summary>
    /// Pseudo-clases que el tablero cubre, cada una con el <b>testigo</b>: el trozo de código de este fichero que la
    /// produce. Es la lista contra la que se compara lo declarado en <c>FileFlow.App/Styles</c>: un estado nuevo en
    /// los estilos sin celda aquí falla en <c>DesignStateBaselinesTests</c>. El testigo existe para que la cobertura
    /// no pueda ser una promesa sin mecanismo —una entrada que afirme cubrir un estado que ya nadie produce—,
    /// porque el lint exige encontrarlo en este código.
    /// </summary>
    public static IReadOnlyList<(string PseudoClass, string Witness)> CoveredPseudoClasses { get; } =
    [
        (":pointerover", "Pseudo(control, \":pointerover\")"),
        (":pressed", "Pseudo(control, \":pressed\")"),
        (":focus", "Pseudo(control, \":focus\")"),
        (":disabled", "control.IsEnabled = false;"),
        (":checked", "IsChecked = state is Selected or SelectedHover"),
        (":selected", "tabs.SelectedIndex = 1;")
    ];

    /// <summary>
    /// Tokens de selector que la guardia de cobertura ve en los estilos y que <b>no</b> son estados de
    /// interacción, con su razón. La guardia exige que sigan declarándose: una exención que ya no hace falta es
    /// una exención que miente y que esconde un estado sin cubrir.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ExemptSelectors { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [":focus-within"] = "se declara sobre una parte de plantilla (ButtonSpinner dentro de NumericUpDown) " +
                                "y no es alcanzable forzando desde el control exterior",
            [":is"] = "es un combinador de selector («cualquiera de éstos»), no un estado del control",
            [":horizontal"] = "describe la orientación del diseño, no la interacción del usuario",
            [":vertical"] = "describe la orientación del diseño, no la interacción del usuario"
        };

    private readonly Dictionary<string, Control> _cells = new(StringComparer.Ordinal);
    private readonly List<(string Key, Control Control, string State)> _pending = [];

    private DesignStateBoard()
    {
    }

    /// <summary>Raíz del tablero, lista para <see cref="VisualSnapshot"/>.</summary>
    public Control Root { get; private set; } = null!;

    /// <summary>
    /// El control que <b>lleva el estado</b> de cada celda, por su clave <c>«fila/estado»</c> (p. ej.
    /// <c>primary/hover</c>). Es lo que una prueba necesita para muestrear el píxel que ese estado pinta: la
    /// celda que lo contiene es sólo el marco con el tamaño de la rejilla.
    /// </summary>
    public IReadOnlyDictionary<string, Control> Cells => _cells;

    /// <summary>
    /// Tablero de botones: los estados del botón por variante (matriz A) y los de selección (matriz B).
    /// </summary>
    public static DesignStateBoard BuildButtons()
    {
        AvaloniaTestHelper.RequireUIThread($"{nameof(DesignStateBoard)}.{nameof(BuildButtons)}");

        var board = new DesignStateBoard();

        board.Root = board.Stack(
            board.Section("Botones · por variante", [Rest, Hover, Pressed, Disabled],
            [
                ("primary", state => Variant("Ejecutar", "primary", state)),
                ("success", state => Variant("Aplicar", "success", state)),
                ("danger", state => Variant("Detener", "danger", state)),
                ("ghost", state => Variant("Omitir", "ghost", state)),
                ("link", state => Variant("Ver detalle", "link", state))
            ]),

            board.Section("Selección · chip y conmutador", [Rest, Hover, Selected, SelectedHover, Disabled],
            [
                ("chip", Chip),
                ("toggleChip", state => Toggle("Dry run", "chip", state)),
                ("toggleIcon", state => Toggle("\u25CE", "icon", state))
            ]));

        return board;
    }

    /// <summary>
    /// Tablero de campos y contenedores: los estados de la entrada de texto, del desplegable, de la pestaña, de la
    /// fila del cajón y del separador de paneles.
    /// </summary>
    public static DesignStateBoard BuildFieldsAndContainers()
    {
        AvaloniaTestHelper.RequireUIThread($"{nameof(DesignStateBoard)}.{nameof(BuildFieldsAndContainers)}");

        var board = new DesignStateBoard();

        board.Root = board.Stack(
            board.Section("Campos", [Rest, Hover, Focus, Disabled],
            [
                ("texto", Field),
                ("desplegable", Dropdown)
            ]),

            board.Section("Contenedores", [Rest, Hover, Selected],
            [
                ("pestaña", state => Tabs(state, pill: false)),
                ("pestaña pastilla", state => Tabs(state, pill: true)),
                ("fila de menú", MenuRow),
                ("separador", Splitter)
            ]));

        return board;
    }

    /// <summary>
    /// Aplica los estados forzados del tablero. Debe llamarse con el árbol <b>ya vivo</b> —desde el paso de
    /// interacción de la captura, después de <c>Show()</c>—: un control vuelve a declarar sus pseudo-clases al
    /// aplicar su plantilla, así que lo forzado antes de que exista la plantilla se pierde (<c>Button</c> borra
    /// <c>:pressed</c>). Es idempotente, de modo que llamarlo dos veces no cambia nada.
    /// </summary>
    public void Activate()
    {
        AvaloniaTestHelper.RequireUIThread($"{nameof(DesignStateBoard)}.{nameof(Activate)}");

        foreach (var (key, control, state) in _pending)
        {
            try
            {
                Apply(control, state);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"La celda '{key}' del tablero no pudo aplicar su estado '{state}': la captura saldría con " +
                    "esa celda en reposo y la línea base congelaría un estado que el usuario nunca ve.",
                    exception);
            }
        }
    }

    /// <summary>
    /// Aplica un estado a un control. Los que dependen de la entrada se <b>fuerzan</b> en la colección de clases
    /// del propio control (la que el sistema de estilos consulta para resolver <c>:pointerover</c>,
    /// <c>:pressed</c> y <c>:focus</c>); el deshabilitado se produce de verdad.
    ///
    /// <para>Un estado desconocido <b>lanza</b> en vez de no hacer nada: una celda que se crea aplicada y no lo
    /// está es una línea base que congela el estado de reposo cinco veces, y eso es peor que no tener la
    /// captura.</para>
    /// </summary>
    public static void Apply(Control control, string state)
    {
        ArgumentNullException.ThrowIfNull(control);

        switch (state)
        {
            case Rest:
                return;

            case Hover:
                Pseudo(control, ":pointerover");
                return;

            case Pressed:
                Pseudo(control, ":pressed");
                return;

            case Focus:
                Pseudo(control, ":focus");
                return;

            case Disabled:
                control.IsEnabled = false;
                return;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(state),
                    state,
                    $"'{state}' no es un estado que el tablero sepa producir. Los de entrada son '{Rest}', " +
                    $"'{Hover}', '{Pressed}', '{Focus}' y '{Disabled}'; la selección la produce cada fila con su " +
                    "propiedad real (IsChecked / SelectedIndex / la clase 'selected').");
        }
    }

    /// <summary>
    /// Fuerza una pseudo-clase y <b>comprueba que quedó puesta</b>. La comprobación no es ceremonia: si Avalonia
    /// dejara de guardar las pseudo-clases en la colección de clases del control —que es la costura que este
    /// tablero usa—, <c>Add</c> se volvería un no-op silencioso y todas las celdas de estado saldrían en reposo
    /// sin que nada fallase hasta que alguien mirase las imágenes.
    /// </summary>
    private static void Pseudo(Control control, string pseudoClass)
    {
        var classes = (IPseudoClasses)control.Classes;
        classes.Add(pseudoClass);

        if (!classes.Contains(pseudoClass))
        {
            throw new InvalidOperationException(
                $"La pseudo-clase '{pseudoClass}' no quedó aplicada en '{control.GetType().Name}' " +
                $"(clases: '{string.Join(" ", control.Classes)}'): el control no la acepta, así que el estado no " +
                "se puede representar forzándolo.");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Piezas de cada celda
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Fila de variante de botón: los cuatro estados de entrada de <c>Button</c>.</summary>
    private static Control Variant(string text, string classes, string state)
    {
        var button = TextButton(text, classes);
        Apply(button, state);
        return button;
    }

    /// <summary>
    /// Chip de selección. La selección no es una pseudo-clase sino la clase <c>selected</c> (así la aplica la
    /// aplicación); el hover <b>puro</b> lo aplica el registro de la matriz, como a cualquier otra celda, y aquí
    /// queda sólo el estado <b>compuesto</b>: «seleccionada+hover» no es uno de los cuatro estados de entrada y por
    /// eso no lo alcanza el registro. Que sea uno solo importa —mientras las dos rutas lo aplicaban, quitar la de
    /// la fila no cambiaba el resultado y una mutación que debía fallar pasaba—.
    /// </summary>
    private static Control Chip(string state)
    {
        var chip = TextButton("Chip", "chipButton");

        if (state is Selected or SelectedHover)
        {
            chip.Classes.Add("selected");
        }

        if (state == SelectedHover)
        {
            Apply(chip, Hover);
        }

        return chip;
    }

    /// <summary>
    /// Conmutador: <c>:checked</c> se produce de verdad, con <c>IsChecked</c>, y el estado compuesto añade el
    /// hover que el registro no alcanza (el puro lo aplica él).
    /// </summary>
    private static Control Toggle(string text, string classes, string state)
    {
        var toggle = new ToggleButton
        {
            Content = text,
            IsChecked = state is Selected or SelectedHover,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        toggle.Classes.Add(classes);

        if (state == SelectedHover)
        {
            Apply(toggle, Hover);
        }

        return toggle;
    }

    /// <summary>Campo de texto con contenido: los cuatro estados del campo.</summary>
    private static Control Field(string state)
    {
        var field = new TextBox
        {
            Text = "ruta/al/fichero.txt",
            VerticalAlignment = VerticalAlignment.Center
        };

        Apply(field, state);
        return field;
    }

    /// <summary>Desplegable cerrado: el estado del selector, no el de su lista.</summary>
    private static Control Dropdown(string state)
    {
        var combo = new ComboBox
        {
            ItemsSource = new[] { "Todas", "Imágenes", "Vídeo" },
            SelectedIndex = 0,
            VerticalAlignment = VerticalAlignment.Center
        };

        Apply(combo, state);
        return combo;
    }

    /// <summary>
    /// Pestañas: la celda muestra un <c>TabControl</c> de dos pestañas y el estado se aplica a la segunda, para que
    /// se vea junto a la activa (que es como se comparan en la aplicación).
    /// </summary>
    private static Control Tabs(string state, bool pill)
    {
        var second = new TabItem { Header = "Vista" };

        var tabs = new TabControl { VerticalAlignment = VerticalAlignment.Center };

        if (pill)
        {
            tabs.Classes.Add("pill");
        }

        tabs.Items.Add(new TabItem { Header = "Datos" });
        tabs.Items.Add(second);

        if (state == Selected)
        {
            tabs.SelectedIndex = 1;
        }
        else if (state == Hover)
        {
            Apply(second, Hover);
        }

        return tabs;
    }

    /// <summary>Fila del cajón de nodos (<c>Border.nodeMenuItem</c>): declara el hover, no la selección.</summary>
    private static Control? MenuRow(string state)
    {
        if (state == Selected)
        {
            return null;
        }

        var row = new Border
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = new TextBlock
            {
                // Centrado y corto a propósito: la sonda de token muestrea el fondo por el borde izquierdo de la
                // celda y un texto alineado a la izquierda le pondría un glifo justo debajo del punto de muestreo.
                Text = "Filtro",
                Classes = { "bodySm", "primaryText" },
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        row.Classes.Add("nodeMenuItem");
        Apply(row, state);
        return row;
    }

    /// <summary>
    /// Separador de paneles: el hover con el acento es la señal de «se puede arrastrar». No declara selección, así
    /// que esa celda sale con la raya en lugar de con un separador que fingiría un estado inexistente.
    /// </summary>
    private static Control? Splitter(string state)
    {
        if (state == Selected)
        {
            return null;
        }

        var splitter = new GridSplitter
        {
            Width = 6,
            Height = CellHeight - 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        Apply(splitter, state);
        return splitter;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Composición de la matriz
    // ─────────────────────────────────────────────────────────────────────────────

    private static Button TextButton(string text, string classes)
    {
        var button = new Button
        {
            Content = text,

            // Tamaño de la celda y no del texto: la cara de la pieza tiene que medir lo mismo en cualquier
            // máquina, o la línea base dependería de la anchura de la fuente del sistema.
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        button.Classes.Add(classes);
        return button;
    }

    private Control Stack(params Control[] sections)
    {
        var panel = new StackPanel { Spacing = 16 };

        foreach (var section in sections)
        {
            panel.Children.Add(section);
        }

        return new Border
        {
            Padding = new Thickness(16),
            Child = panel
        };
    }

    /// <summary>
    /// Una matriz: la primera fila son los nombres de los estados y las siguientes, una pieza por fila con una
    /// celda por estado. Cada celda construida se registra en <see cref="Cells"/> con la clave «fila/estado», y las
    /// que llevan un estado de entrada quedan además pendientes para <see cref="Activate"/>.
    /// </summary>
    private Control Section(string title, IReadOnlyList<string> states,
        IReadOnlyList<(string Label, Func<string, Control?> Build)> rows)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(
                string.Join(",", Enumerable.Repeat(LabelWidth, 1).Concat(Enumerable.Repeat(CellWidth, states.Count)))),
            RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("auto", rows.Count + 1)))
        };

        for (int column = 0; column < states.Count; column++)
        {
            AddCaption(grid, states[column], row: 0, column: column + 1, ["micro", "muted"]);
        }

        for (int row = 0; row < rows.Count; row++)
        {
            var (label, build) = rows[row];

            AddCaption(grid, label, row + 1, 0, ["caption", "secondary"]);

            for (int column = 0; column < states.Count; column++)
            {
                string state = states[column];
                Control? content = build(state);

                var cell = new Border
                {
                    Width = CellWidth,
                    Height = CellHeight,
                    Child = content ?? Placeholder()
                };

                Grid.SetRow(cell, row + 1);
                Grid.SetColumn(cell, column + 1);
                grid.Children.Add(cell);

                if (content == null)
                {
                    continue;
                }

                string key = $"{label}/{state}";
                _cells[key] = content;

                // Sólo los estados de entrada se aplican tarde (son los que un control puede revertir al aplicar su
                // plantilla); la selección la resuelve cada fila con su propiedad real, que no se pierde.
                if (state is Hover or Pressed or Focus or Disabled)
                {
                    _pending.Add((key, content, state));
                }
            }
        }

        var stack = new StackPanel { Spacing = 0 };
        stack.Children.Add(new TextBlock
        {
            Text = title,
            Classes = { "sectionLabel" },
            Margin = new Thickness(0, 0, 0, 6)
        });
        stack.Children.Add(grid);

        return stack;
    }

    private static void AddCaption(Grid grid, string text, int row, int column, string[] classes)
    {
        var caption = new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center
        };

        foreach (string style in classes)
        {
            caption.Classes.Add(style);
        }

        Grid.SetRow(caption, row);
        Grid.SetColumn(caption, column);
        grid.Children.Add(caption);
    }

    /// <summary>
    /// Celda sin estado aplicable (p. ej. «seleccionada» en un separador): se marca con una raya para que se lea
    /// como «este estado no existe para esta pieza» y no como una celda que se quedó en blanco por error.
    /// </summary>
    private static Control Placeholder() => new TextBlock
    {
        Text = "—",
        Classes = { "micro", "muted" },
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };
}
