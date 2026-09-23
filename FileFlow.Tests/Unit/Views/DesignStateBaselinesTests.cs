using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace FileFlow.Tests.Unit.Views;

/// <summary>
/// Las <b>líneas base de los estados</b> del sistema de diseño: cómo se ve un botón, un campo, una pestaña o un
/// chip cuando el puntero está encima, cuando se pulsa, cuando tiene el foco, cuando está deshabilitado y cuando
/// está seleccionado.
///
/// <para><b>Por qué</b>: los estados se afirmaban sólo con <b>tokens</b> (el fondo de la cara de un botón es este
/// recurso del tema) y con un lint de texto sobre las reglas. Eso dice que el valor correcto se aplica, pero no
/// <i>cómo se ve</i>: un radio, un borde, una opacidad o un tamaño de letra cambiados en un estado pasaban sin
/// que nadie lo notara. Aquí cada estado tiene su celda en el tablero (<see cref="DesignStateBoard"/>), su captura
/// congelada y su píxel sondeado.</para>
///
/// <para><b>Dos niveles, y cada uno ve lo que el otro no</b>: la captura compara la imagen entera —un cambio de
/// fondo, de geometría o de composición la falla—, pero reparte su tolerancia entre toda la superficie y por eso
/// no ve un cambio de 1 px de grosor (el borde de foco de un campo es el 0,1 % de la imagen). Las <b>sondas de
/// píxel</b> cubren justo eso: señalan un punto concreto de una celda y exigen <b>el token</b> que ese estado
/// declara, con una tolerancia de 3 canales en lugar de 12, porque un relleno opaco o un borde se pintan exactos
/// y ahí no hace falta margen para antialias.</para>
///
/// <para><b>El tablero fuerza los estados de entrada</b>, porque una fotografía tiene un puntero y un foco y no
/// puede tener dos. Eso obliga a demostrar que forzar es <b>fiel</b>: la primera parte de esta clase compara el
/// píxel de un estado forzado con el que produce la entrada real sobre la misma superficie, y falla si difieren.
/// Sin esa prueba, la línea base podría estar congelando un estado que el usuario nunca ve —el mismo fallo que el
/// asentado de capturas (hito 181) vino a cerrar—.</para>
///
/// <para><b>Colección exclusiva</b>: captura ventanas y usa la sesión headless, así que va serializada como el
/// resto de la interfaz.</para>
/// </summary>
[Collection(VisualSnapshotsCollection.Name)]
public class DesignStateBaselinesTests
{
    private const string DarkTheme = "dark_fluent";
    private const string LightTheme = "light_studio";

    private const int ProbeWidth = 220;
    private const int ProbeHeight = 70;

    /// <summary>
    /// Distancia desde el borde izquierdo de la pieza a la que se muestrea su cara: por dentro del borde y de la
    /// esquina redondeada, y por fuera del texto (que va centrado).
    /// </summary>
    private const int FaceInset = 6;

    /// <summary>Diferencia por canal que se admite en una sonda: un relleno opaco se pinta exacto.</summary>
    private const int ProbeTolerance = 3;

    /// <summary>
    /// Contraste mínimo que se le exige a la etiqueta de un control deshabilitado: el AA de texto (4,5:1), no un
    /// umbral de compromiso. El estado que esta guardia protege medía <b>1,20:1</b> antes de declarar la cara y el
    /// primer plano en lugar de atenuar la parte entera, y <b>3,30:1</b> en el campo de texto —que no declaraba
    /// ningún primer plano— hasta que esta guardia empezó a mirarlo.
    ///
    /// <para>Se mide sobre el <b>píxel pintado</b> y no sobre el token: es la mitad que las sondas de token no
    /// cubren, porque un token correcto se puede pintar mal (era el caso: el token del tema base era correcto y el
    /// resultado ilegible).</para>
    /// </summary>
    private const double MinimumDisabledLabelContrast = 4.5;

    /// <summary>
    /// Margen desde el borde de la celda para medir su interior: deja fuera el borde (1 px) y la esquina
    /// redondeada, que si no serían los extremos que mide el contraste en lugar de la etiqueta.
    /// </summary>
    private const int LabelInset = 4;

    /// <summary>Las celdas del tablero que representan el estado deshabilitado de un botón o un conmutador.</summary>
    private static readonly string[] DisabledControlCells =
    [
        "primary/deshabilitado", "success/deshabilitado", "danger/deshabilitado",
        "ghost/deshabilitado", "chip/deshabilitado", "toggleChip/deshabilitado", "toggleIcon/deshabilitado"
    ];

    /// <summary>
    /// Las del tablero de campos: la entrada de texto y el desplegable.
    ///
    /// <para>Estaban fuera de la guardia, y al mirarlas se vio por qué había que meterlas: el campo deshabilitado no
    /// declaraba <b>ningún</b> primer plano, así que el que se leía era el del tema base y quedaba por debajo del AA
    /// de texto —4,00:1 en el campo de texto y 3,50:1 en el desplegable sobre el tema oscuro, 3,30:1 y 2,62:1 sobre
    /// el claro—. Un umbral que ninguna de sus celdas comprobaba.</para>
    /// </summary>
    private static readonly string[] DisabledFieldCells = ["texto/deshabilitado", "desplegable/deshabilitado"];

    /// <summary>
    /// Los tableros que tienen celdas deshabilitadas, con las suyas. La guardia recorre <b>los dos</b>: los campos
    /// viven en su propio tablero y una lista por tablero deja claro a cuál pertenece cada incumplimiento.
    /// </summary>
    private static readonly (string Board, Func<DesignStateBoard> Build, string[] Cells)[] DisabledBoards =
    [
        ("botones", DesignStateBoard.BuildButtons, DisabledControlCells),
        ("campos", DesignStateBoard.BuildFieldsAndContainers, DisabledFieldCells)
    ];

    // ─────────────────────────────────────────────────────────────────────────────
    // El deshabilitado tiene que poder leerse
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Mide el contraste <b>realmente pintado</b> entre la etiqueta y la cara de cada celda deshabilitada, en los
    /// dos temas.
    ///
    /// <para>Es la mitad que las sondas de token no cubren: un token puede ser el correcto y el píxel no —eso es
    /// exactamente lo que pasaba, con el token correcto atenuado dos veces por el tema base y por nuestra opacidad—.
    /// Aquí se mide el extremo dentro de la celda (el núcleo de la etiqueta, que en la captura alcanza el color
    /// puro del token) contra su cara, con la luminancia relativa de WCAG.</para>
    /// </summary>
    [Fact]
    public void EveryDisabledCell_ShouldRenderItsLabelAboveAaContrast()
    {
        var findings = new List<string>();

        foreach (var (board, build, cells) in DisabledBoards)
        {
            foreach (string theme in new[] { DarkTheme, LightTheme })
            {
                findings.AddRange(DisabledLabelFindings(board, build, cells, theme));
            }
        }

        findings.Should().BeEmpty(
            "la etiqueta de un control deshabilitado por debajo de 4,5:1 no se lee: cuando la cara y el texto se " +
            "atenuaban los dos (nuestra opacidad sobre la parte, que ya venía atenuada por el tema base) el estado " +
            "medía 2,13:1 en oscuro y 1,20:1 en claro, y en los campos —donde el tema base atenúa la etiqueta por su " +
            "cuenta y nuestra capa no declaraba ninguna— el texto quedaba en 4,00:1 y 3,30:1. Medido ahora: {0}",
            string.Join(" | ", findings));
    }

    private static IEnumerable<string> DisabledLabelFindings(
        string board, Func<DesignStateBoard> build, string[] cells, string theme)
    {
        DesignStateBoard? stateBoard = null;
        var rects = new Dictionary<string, Rect>(StringComparer.Ordinal);

        byte[] capture = VisualSnapshot.CaptureNaturalHeight(
            () => (stateBoard = build()).Root,
            DesignStateBoard.Width,
            theme,
            window =>
            {
                stateBoard!.Activate();
                InputSimulator.Pump();

                foreach (string cell in cells)
                {
                    Control control = stateBoard.Cells[cell];
                    Point? origin = control.TranslatePoint(new Point(LabelInset, LabelInset), window);

                    if (origin is not null)
                    {
                        rects[cell] = new Rect(
                            origin.Value.X,
                            origin.Value.Y,
                            control.Bounds.Width - (LabelInset * 2),
                            control.Bounds.Height - (LabelInset * 2));
                    }
                }
            });

        foreach (string cell in cells)
        {
            if (!rects.TryGetValue(cell, out var rect))
            {
                yield return $"'{board}/{cell}' [{theme}]: la celda no está en el árbol de la ventana";
                continue;
            }

            var range = VisualSnapshot.ExtremeLuminance(
                capture,
                (int)Math.Round(rect.X),
                (int)Math.Round(rect.Y),
                (int)Math.Round(rect.Width),
                (int)Math.Round(rect.Height));

            if (range.Contrast < MinimumDisabledLabelContrast)
            {
                yield return $"'{board}/{cell}' [{theme}]: {range.Describe()}";
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Fidelidad del forzado: la celda dice lo que diría el puntero
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ForcedHover_ShouldRenderExactlyLikeARealHover()
    {
        Button? button = null;
        Func<Control> surface = () => button = PrimaryButton();

        byte[] real = VisualSnapshot.Capture(
            surface, ProbeWidth, ProbeHeight, DarkTheme,
            window => InputSimulator.Hover(window, button!));

        byte[] forced = VisualSnapshot.Capture(
            surface, ProbeWidth, ProbeHeight, DarkTheme,
            _ => DesignStateBoard.Apply(button!, DesignStateBoard.Hover));

        VisualSnapshot.AssertImagesMatch(real, forced,
            "La celda 'hover' del tablero se produce forzando ':pointerover' en el control, porque una fotografía " +
            "no puede tener el puntero en cinco sitios a la vez. Si el forzado deja de equivaler al hover real, " +
            "las líneas base de los estados congelan un aspecto que el usuario no ve: revisa " +
            "DesignStateBoard.Pseudo antes de dar por buenas esas capturas.");
    }

    [Fact]
    public void ForcedPressed_ShouldRenderExactlyLikeARealPress()
    {
        Button? button = null;
        Func<Control> surface = () => button = PrimaryButton();

        byte[] real = VisualSnapshot.Capture(
            surface, ProbeWidth, ProbeHeight, DarkTheme,
            window => InputSimulator.Press(window, button!));

        byte[] forced = VisualSnapshot.Capture(
            surface, ProbeWidth, ProbeHeight, DarkTheme,
            _ => DesignStateBoard.Apply(button!, DesignStateBoard.Pressed));

        VisualSnapshot.AssertImagesMatch(real, forced,
            "La celda 'pulsado' se produce forzando ':pressed', y el pulso de verdad se captura con el puntero " +
            "sobre el botón: lo que aquí se compara es que el estado forzado pinte lo mismo que ese pulso. Éste es " +
            "el estado que más fácil se pierde —'Button' borra el ':pressed' forzado al aplicar su plantilla, y " +
            "por eso el tablero los aplica con el árbol vivo—, así que su fidelidad se comprueba tal y como lo " +
            "aplica el tablero.");
    }

    [Fact]
    public void ForcedFocus_ShouldRenderExactlyLikeARealFocus()
    {
        TextBox? field = null;
        bool gotFocus = false;
        Func<Control> surface = () => field = Field();

        byte[] real = VisualSnapshot.Capture(
            surface, ProbeWidth, ProbeHeight, DarkTheme,
            window =>
            {
                InputSimulator.Pump();
                gotFocus = field!.Focus();
            });

        gotFocus.Should().BeTrue(
            "sin foco real no se está comparando nada: la celda 'foco' del tablero sería un estado que nadie " +
            "produce. Si el foco dejara de funcionar en la sesión headless, el fallo tiene que decirlo aquí.");

        byte[] forced = VisualSnapshot.Capture(
            surface, ProbeWidth, ProbeHeight, DarkTheme,
            _ => DesignStateBoard.Apply(field!, DesignStateBoard.Focus));

        VisualSnapshot.AssertImagesMatch(real, forced,
            "La celda 'foco' se produce forzando ':focus'. El anillo de foco es la única señal de «aquí se " +
            "escribe»: si el forzado no pintara lo mismo que el foco real, la línea base del estado estaría " +
            "enseñando un anillo que no es el que aparece.");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Sondas de token: el tablero dice el token, no sólo lo parece
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheButtonBoard_ShouldPaintEveryStateWithItsOwnToken() =>
        AssertBoardPaints(DesignStateBoard.BuildButtons,
        [
            new("primary/reposo", "AccentPrimaryBrush"),
            new("primary/hover", "AccentHoverBrush"),
            new("success/reposo", "AccentSuccessBrush"),
            new("danger/reposo", "AccentErrorBrush"),
            new("ghost/reposo", "AppBackgroundBrush", Because: "un botón fantasma en reposo no pinta cara"),
            new("ghost/hover", "BgHoverBrush"),
            new("chip/reposo", "BgSurfaceBrush"),
            new("chip/hover", "BgHoverBrush"),
            new("chip/seleccionada", "AccentPrimaryBrush"),
            new("chip/seleccionada+hover", "AccentHoverBrush"),
            new("toggleChip/seleccionada", "AccentPrimaryBrush"),
            new("toggleChip/seleccionada+hover", "AccentHoverBrush"),
            new("toggleIcon/hover", "BgHoverBrush"),
            new("toggleIcon/seleccionada", "AccentPrimaryBrush"),
            new("toggleIcon/seleccionada+hover", "AccentHoverBrush",
                Because: "pasar el puntero por un conmutador ya activado tiene que encenderlo"),

            // Deshabilitado: cada variante pinta SU cara atenuada —un color propio del tema, igual sobre cualquier
            // superficie— y no el relleno neutro que el tema base aplicaba a todas por igual (que a un botón sin
            // fondo le añadía una caja). Antes esto era el acento al 45 % <i>de opacidad</i> sobre lo que hubiera
            // detrás; ahora es un token, así que la sonda exige el token y no una mezcla dependiente del fondo.
            new("primary/deshabilitado", "AccentPrimaryMutedBrush",
                Because: "el acento atenuado, no un gris neutro"),
            new("success/deshabilitado", "AccentSuccessMutedBrush",
                Because: "el éxito atenuado, no un gris neutro"),
            new("danger/deshabilitado", "AccentErrorMutedBrush",
                Because: "el peligro atenuado, no un gris neutro"),
            new("ghost/deshabilitado", "AppBackgroundBrush",
                Because: "un botón sin fondo sigue sin fondo al deshabilitarse"),
            new("chip/deshabilitado", "BgSurfaceBrush"),
            new("toggleChip/deshabilitado", "BgSurfaceBrush"),
            new("toggleIcon/deshabilitado", "AppBackgroundBrush",
                Because: "un conmutador de icono sin fondo sigue sin fondo al deshabilitarse")
        ]);

    [Fact]
    public void TheFieldBoard_ShouldPaintEveryStateWithItsOwnToken() =>
        AssertBoardPaints(DesignStateBoard.BuildFieldsAndContainers,
        [
            // El campo declara sus estados en el borde, que es 1 px: por eso estas sondas van al borde (inset 0) y
            // no a la cara, y por eso existen además de la captura.
            new("texto/reposo", "BorderDarkBrush", Inset: 0),
            new("texto/hover", "AccentGlowBrush", Inset: 0),
            new("texto/foco", "AccentPrimaryBrush", Inset: 0),
            new("desplegable/reposo", "BgSurfaceBrush"),
            new("desplegable/hover", "BgHoverBrush",
                Because: "el desplegable se enciende al pasar el puntero, no se oscurece"),

            // El campo deshabilitado declara cara, borde y primer plano, y las sondas exigen esos tokens
            // <b>opacos</b>: una opacidad repuesta sobre la parte cambiaría el píxel —la cara pasaría a ser la
            // mezcla— y estas sondas, con 3 canales de tolerancia, la delatarían, que es justo lo que ninguna
            // aserción sobre propiedades veía. Sin la cara ni el borde, además, el estado volvería a depender de
            // los colores del tema base.
            new("texto/deshabilitado", "BorderDarkBrush", Inset: 0,
                Because: "el borde del sistema, no el de la plantilla deshabilitada del tema base"),
            new("texto/deshabilitado · cara", "BgSurfaceBrush", Cell: "texto/deshabilitado",
                Because: "cara propia y opaca, no el relleno neutro del tema base"),
            new("desplegable/deshabilitado", "BgSurfaceBrush",
                Because: "deshabilitado conserva su superficie, ya sin la mezcla del 50 %"),
            new("desplegable/deshabilitado · borde", "BorderDarkBrush", Inset: 0,
                Cell: "desplegable/deshabilitado",
                Because: "el borde del sistema, no el de la plantilla deshabilitada del tema base"),
            new("fila de menú/reposo", "AppBackgroundBrush", Because: "la fila del cajón no pinta fondo en reposo"),
            new("fila de menú/hover", "BgHoverBrush"),
            new("separador/hover", "AccentPrimaryBrush", Inset: 3),
            new("pestaña/hover", "BgSurfaceBrush", Element: SecondTab),
            new("pestaña pastilla/hover", "BgHoverBrush", Element: SecondTab),
            new("pestaña pastilla/seleccionada", "AccentPrimaryBrush", Element: SecondTab)
        ]);

    // ─────────────────────────────────────────────────────────────────────────────
    // Cobertura: ningún estado de los estilos sin celda
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EveryPseudoClassDeclaredInTheStyles_ShouldHaveACellInTheBoard()
    {
        var declared = new SortedSet<string>(StringComparer.Ordinal);

        foreach (string file in StyleFiles())
        {
            foreach (string selector in Selectors(File.ReadAllText(file)))
            {
                foreach (Match match in Regex.Matches(selector, ":[a-z][a-z-]*"))
                {
                    declared.Add(match.Value);
                }
            }
        }

        declared.Should().NotBeEmpty(
            "si el lint no encuentra ni un selector con pseudo-clase, es que ya no está leyendo los estilos que " +
            "cree leer (y entonces su verde no significa nada)");

        var covered = DesignStateBoard.CoveredPseudoClasses.Select(c => c.PseudoClass).ToHashSet(StringComparer.Ordinal);

        // Cada pseudo-clase declarada en los estilos tiene que estar cubierta por el tablero o exenta con razón.
        var uncovered = declared
            .Where(pseudo => !covered.Contains(pseudo) && !DesignStateBoard.ExemptSelectors.ContainsKey(pseudo))
            .ToList();

        uncovered.Should().BeEmpty(
            "un estado nuevo en los estilos sin celda en el tablero es un estado sin contrato visual: añádelo a " +
            "DesignStateBoard (y a su matriz) o exímelo con la razón de por qué no es fotografiable. Sin cubrir: " +
            string.Join(", ", uncovered));

        // La exención se gana el sitio: si el token ya no se declara, la razón sobra y miente.
        foreach (var exemption in DesignStateBoard.ExemptSelectors)
        {
            declared.Should().Contain(exemption.Key,
                $"'{exemption.Key}' está exento con la razón «{exemption.Value}» pero ya no lo declara ningún " +
                "estilo: retira la exención en DesignStateBoard en lugar de dejarla ahí");
        }

        // Y la lista de cobertura no puede ser una promesa sin mecanismo: cada entrada cita el código que la produce.
        string board = SourceText.CodeWithoutComments("FileFlow.Tests/TestHelpers/DesignStateBoard.cs");

        foreach (var (pseudoClass, witness) in DesignStateBoard.CoveredPseudoClasses)
        {
            board.Should().Contain(witness,
                $"la cobertura afirma producir '{pseudoClass}' pero su testigo («{witness}») ya no está en " +
                "DesignStateBoard: o cambió el mecanismo o la lista de cobertura está mintiendo");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Líneas base
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ButtonStates_ShouldMatchTheirBaseline_InTheDarkPreset() =>
        AssertBoardBaseline(DesignStateBoard.BuildButtons, "design-states-buttons-dark", DarkTheme);

    [Fact]
    public void ButtonStates_ShouldMatchTheirBaseline_InTheLightPreset() =>
        AssertBoardBaseline(DesignStateBoard.BuildButtons, "design-states-buttons-light", LightTheme);

    [Fact]
    public void FieldAndContainerStates_ShouldMatchTheirBaseline_InTheDarkPreset() =>
        AssertBoardBaseline(DesignStateBoard.BuildFieldsAndContainers, "design-states-fields-dark", DarkTheme);

    [Fact]
    public void FieldAndContainerStates_ShouldMatchTheirBaseline_InTheLightPreset() =>
        AssertBoardBaseline(DesignStateBoard.BuildFieldsAndContainers, "design-states-fields-light", LightTheme);

    // ─────────────────────────────────────────────────────────────────────────────
    // Utilidades
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Una celda que se sondea: su clave, el token que debe pintar y, si el estado no vive en el control que ocupa
    /// la celda, cómo llegar a él. Con <paramref name="Over"/> y <paramref name="Alpha"/> el token va <b>atenuado</b>
    /// y la expectativa es la mezcla calculada, no un color escrito a mano.
    ///
    /// <para><paramref name="Cell"/> permite sondear <b>dos puntos de la misma celda</b> —la cara y el borde de un
    /// campo, que son tokens distintos— sin repetir la clave de la celda, que tiene que seguir siendo única para
    /// que ninguna sonda pise el punto de otra.</para>
    /// </summary>
    private readonly record struct CellProbe(
        string Key,
        string Token,
        int Inset = FaceInset,
        Func<Control, Control>? Element = null,
        string? Because = null,
        string? Over = null,
        double Alpha = 1.0,
        string? Cell = null);

    /// <summary>
    /// Captura un tablero una sola vez, localiza el punto de muestreo de cada sonda y exige <b>el token</b> en él.
    /// La captura es compartida a propósito: una captura por sonda multiplicaría por catorce el coste de esta
    /// prueba para mirar la misma imagen.
    /// </summary>
    private static void AssertBoardPaints(Func<DesignStateBoard> build, CellProbe[] probes)
    {
        DesignStateBoard? board = null;
        var points = new Dictionary<string, Point>(StringComparer.Ordinal);
        var expected = new Dictionary<string, Color>(StringComparer.Ordinal);

        byte[] capture = VisualSnapshot.CaptureNaturalHeight(
            () =>
            {
                board = build();

                foreach (var probe in probes)
                {
                    Color token = TokenColor(probe.Token);

                    expected[probe.Key] = probe.Over is null
                        ? token
                        : Blend(token, TokenColor(probe.Over), probe.Alpha);
                }

                return board.Root;
            },
            DesignStateBoard.Width,
            DarkTheme,
            window =>
            {
                // El mismo momento que usa la captura de línea base: los estados se aplican con el árbol vivo. Aquí
                // la interacción además <b>observa</b>: mide dónde quedó cada pieza con el layout ya hecho, para
                // muestrear después el píxel que de verdad se pintó en ella.
                board!.Activate();
                InputSimulator.Pump();

                foreach (var probe in probes)
                {
                    Control cell = board.Cells[probe.Cell ?? probe.Key];
                    Control element = probe.Element is null ? cell : probe.Element(cell);

                    if (element.TranslatePoint(new Point(probe.Inset, element.Bounds.Height / 2), window) is not { } point)
                    {
                        throw new InvalidOperationException($"La celda '{probe.Key}' no está en el árbol de la ventana.");
                    }

                    points[probe.Key] = point;
                }
            });

        foreach (var probe in probes)
        {
            Point point = points[probe.Key];

            Rgba32 pixel = VisualSnapshot.PixelAt(capture, (int)Math.Round(point.X), (int)Math.Round(point.Y));

            Color token = expected[probe.Key];

            string because = $"La celda '{probe.Key}' del tablero ({((int)point.X)},{(int)point.Y}) debe pintar " +
                             $"{Hex(token)} ('{probe.Token}'" +
                             (probe.Over is null ? "" : $" al {probe.Alpha:P0} sobre '{probe.Over}'") + ")" +
                             (probe.Because is null ? string.Empty : $": {probe.Because}");

            pixel.R.Should().BeCloseTo(token.R, ProbeTolerance, because);
            pixel.G.Should().BeCloseTo(token.G, ProbeTolerance, because);
            pixel.B.Should().BeCloseTo(token.B, ProbeTolerance, because);
        }
    }

    /// <summary>
    /// Captura un tablero y lo compara con su línea base. Los estados se aplican con el árbol ya vivo
    /// (<see cref="DesignStateBoard.Activate"/>), que es el único momento en el que un control no los revierte.
    /// </summary>
    private static void AssertBoardBaseline(Func<DesignStateBoard> build, string name, string themeId)
    {
        DesignStateBoard? board = null;

        byte[] capture = VisualSnapshot.CaptureNaturalHeight(
            () => (board = build()).Root,
            DesignStateBoard.Width,
            themeId,
            _ => board!.Activate());

        VisualSnapshot.AssertMatchesBaseline(name, capture);
    }

    /// <summary>
    /// Color que resulta de pintar <paramref name="front"/> con opacidad <paramref name="alpha"/> sobre
    /// <paramref name="back"/>: es lo que hace el renderizador con una cara atenuada y lo que la sonda espera
    /// encontrar en el píxel.
    /// </summary>
    private static Color Blend(Color front, Color back, double alpha) => Color.FromArgb(
        255,
        (byte)Math.Round(front.R * alpha + back.R * (1 - alpha)),
        (byte)Math.Round(front.G * alpha + back.G * (1 - alpha)),
        (byte)Math.Round(front.B * alpha + back.B * (1 - alpha)));

    private static string Hex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    /// <summary>La segunda pestaña de una celda de pestañas: es la que lleva el estado (la primera es la activa).</summary>
    private static Control SecondTab(Control cell) =>
        cell.GetVisualDescendants().OfType<TabItem>().ElementAt(1);

    private static Button PrimaryButton()
    {
        var button = new Button { Content = "Ejecutar" };
        button.Classes.Add("primary");
        return button;
    }

    private static TextBox Field() => new()
    {
        Text = "ruta/al/fichero.txt",
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
    };

    /// <summary>
    /// Color del token del tema activo. Falla si el token no existe o no es un color sólido: un token muerto haría
    /// que la sonda comparase contra la nada y pasara.
    /// </summary>
    private static Color TokenColor(string key) =>
        VisualSnapshot.ResolveToken<IBrush>(key) is ISolidColorBrush brush
            ? brush.Color
            : throw new InvalidOperationException($"El token '{key}' no es un color sólido con el que sondear un píxel.");

    private static IEnumerable<string> StyleFiles() =>
        Directory.EnumerateFiles(
            Path.Combine(TestRepositoryLocator.RepositoryRoot(), "FileFlow.App", "Styles"),
            "*.axaml",
            SearchOption.TopDirectoryOnly);

    /// <summary>
    /// Selectores declarados en un diccionario de estilos. Se lee el selector y no el archivo entero: una
    /// pseudo-clase mencionada en un comentario o en el nombre de un recurso no es un estado que haya que cubrir.
    /// </summary>
    private static IEnumerable<string> Selectors(string xaml) =>
        Regex.Matches(xaml, "Selector=\"([^\"]*)\"").Select(match => match.Groups[1].Value);
}
