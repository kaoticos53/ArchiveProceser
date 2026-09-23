using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.App.Views;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Views;

/// <summary>
/// La capa de <b>interacción</b>: estados de estilo, foco, atajos de teclado, el buscador rápido y soltar un
/// nodo del cajón en el lienzo, todo con entrada <b>real</b> simulada sobre la sesión headless.
///
/// <para><b>Por qué existe</b>: el suite no simulaba ni un clic, ni una tecla, ni un arrastre. Las 29 capturas
/// visuales congelan estados quietos y los lints de estilo comprueban el texto de las reglas, así que nada
/// verificaba que el estado se <b>aplique</b> al interactuar: un selector mal escrito, un atajo que no llega o
/// un foco que no aterriza solo se veían usando la aplicación. Ésta es la mitad «viva» del rediseño.</para>
///
/// <para><b>Qué se afirma</b>: donde el sistema de diseño declara un token concreto (el fondo de hover de la
/// cara de un botón, el anillo de foco del campo, el fondo de un elemento del cajón), la prueba exige
/// <b>ese token</b> —resuelto del tema activo—, no «un color distinto». Así el fallo dice qué regla se rompió
/// en vez de solo que algo cambió.</para>
///
/// <para><b>Colección exclusiva</b>: las pruebas abren ventanas y entran por el pipeline de entrada de la
/// sesión headless, que las capturas de otras clases están usando; van serializadas como el resto de la
/// interfaz.</para>
/// </summary>
[Collection(VisualSnapshotsCollection.Name)]
public class InputInteractionTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Estados de estilo (hover · pressed · focus · disabled)
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void HoveringAPrimaryButton_ShouldPaintTheHoverTokenOnItsFace()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var button = PrimaryButton();
            var window = Show(button);

            try
            {
                FaceOf(button).Should().Be(Token("AccentPrimaryBrush"), "en reposo la cara lleva el acento");

                InputSimulator.Hover(window, button);

                FaceOf(button).Should().Be(Token("AccentHoverBrush"),
                    "Button.primary:pointerover debe pintar el acento claro en la cara");
            }
            finally
            {
                Close(window);
            }
        });
    }

    [Fact]
    public void PressingAPrimaryButton_ShouldShowThePressedState()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var button = PrimaryButton();
            var window = Show(button);

            try
            {
                InputSimulator.Hover(window, button);

                InputSimulator.Press(window, button);

                Surface(button).Opacity.Should().BeApproximately(0.82, 0.01,
                    "Button.primary:pressed atenúa la cara (es lo que separa presionado de hover)");
                FaceOf(button).Should().Be(Token("AccentPrimaryBrush"),
                    "presionado vuelve al acento base, sobre el claro del hover");
            }
            finally
            {
                Close(window);
            }
        });
    }

    [Fact]
    public void ADisabledButton_ShouldLookDisabled_AndIgnoreTheClick()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var command = new RecordingCommand();
            var button = PrimaryButton();
            button.Command = command;
            var window = Show(button);

            try
            {
                button.IsEnabled = false;
                InputSimulator.Settle();

                FaceOf(button).Should().Be(Token("AccentPrimaryMutedBrush"),
                    "el deshabilitado conserva la identidad de la variante en una cara propia del tema (el acento" +
                    " mezclado con su superficie), y no en una opacidad sobre lo que hubiera detrás");

                Surface(button).Opacity.Should().Be(1.0,
                    "la opacidad se aplicaba a la parte entera, y dentro de ella vive la etiqueta, que el tema base" +
                    " ya atenúa por su cuenta: las dos atenuaciones juntas dejaban el texto deshabilitado en 2,13:1" +
                    " sobre el tema oscuro y en 1,20:1 sobre el claro");

                LabelOf(button).Should().Be(Token("TextPrimaryBrush"),
                    "sobre una cara de acento el primer plano se declara explícitamente y es el único nivel de" +
                    " atenuación que le queda al texto");

                InputSimulator.Click(window, button);

                command.Executions.Should().Be(0,
                    "un clic real sobre un botón deshabilitado no puede ejecutar nada");
            }
            finally
            {
                Close(window);
            }
        });
    }

    [Fact]
    public void FocusingATextField_ShouldRingItWithTheAccent_AndUndoItWhenTheFocusLeaves()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var field = new TextBox { Width = 240, Height = 34 };
            var other = new TextBox { Width = 240, Height = 34 };
            var panel = new StackPanel { Spacing = 24 };
            panel.Children.Add(field);
            panel.Children.Add(other);
            var window = Show(panel);

            try
            {
                BorderRing(field).Should().Be(Token("BorderDarkBrush"), "sin foco el campo lleva el borde neutro");

                InputSimulator.Click(window, field);

                field.IsFocused.Should().BeTrue("el clic debe llevar el foco al campo");
                BorderRing(field).Should().Be(Token("AccentPrimaryBrush"),
                    "TextBox:focus marca el anillo con el acento: es la señal visible de «aquí se escribe»");

                InputSimulator.Click(window, other);

                other.IsFocused.Should().BeTrue();
                BorderRing(field).Should().Be(Token("BorderDarkBrush"),
                    "al irse el foco el anillo vuelve al borde neutro");
            }
            finally
            {
                Close(window);
            }
        });
    }

    [Fact]
    public void HoveringATab_ShouldHighlightIt_AndClickingShouldSelectIt()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var first = new TabItem { Header = "Inspector", Content = new TextBlock { Text = "uno" } };
            var second = new TabItem { Header = "Métricas", Content = new TextBlock { Text = "dos" } };
            var tabs = new TabControl();
            tabs.Items.Add(first);
            tabs.Items.Add(second);

            var window = Show(tabs);
            try
            {
                // El texto de la pestaña lleva BrushTransition: el asentado de cada interacción avanza el reloj
                // de animación virtual, así que se afirma el color <b>animado</b> y ya no hay que desactivar la
                // transición para poder mirarlo.
                first.IsSelected.Should().BeTrue("la primera pestaña arranca activa");
                PipeOf(first).IsVisible.Should().BeTrue("la pestaña activa muestra su indicador");
                PipeOf(second).IsVisible.Should().BeFalse("la inactiva no: el indicador es la marca de cuál está activa");
                ForegroundOf(second).Should().Be(Token("TextSecondaryBrush"),
                    "en reposo el texto de una pestaña inactiva lleva el color secundario");

                InputSimulator.Hover(window, second);

                TrackOf(second).Should().Be(Token("BgSurfaceBrush"),
                    "TabItem:pointerover ilumina el carril de la pestaña bajo el puntero");

                InputSimulator.Click(window, second);

                second.IsSelected.Should().BeTrue("el clic selecciona la pestaña");
                first.IsSelected.Should().BeFalse();
                PipeOf(second).IsVisible.Should().BeTrue("el indicador viaja a la pestaña recién activada");
                PipeOf(first).IsVisible.Should().BeFalse("y desaparece de la que deja de estar activa");
                ForegroundOf(second).Should().Be(Token("TextPrimaryBrush"),
                    "TabItem:selected sube el texto al color primario para marcar la activa");
                ForegroundOf(first).Should().Be(Token("TextSecondaryBrush"),
                    "y la que deja de estar activa vuelve al color secundario");
            }
            finally
            {
                Close(window);
            }
        });
    }

    [Fact]
    public void HoveringAToolboxItem_ShouldHighlightIt()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            using var fixture = AppVisualFixture.Create();
            var view = new NodeToolboxView { DataContext = fixture.Toolbox };
            var window = Show(view, 340, 720);

            try
            {
                var item = FirstRealizedNodeMenuItem(view);
                BackgroundOf(item).Should().Be(Colors.Transparent, "en reposo el ítem del cajón es transparente");

                // El fondo del ítem se <b>anima</b> (BrushTransition) y aquí se afirma su valor animado: el
                // asentado de cada interacción avanza el reloj de animación virtual, así que el ítem llega al
                // token de forma exacta. Antes había que desconectar la transición y, aun así, el ítem se
                // quedaba a medio camino bajo la carga de la suite completa.
                InputSimulator.Hover(window, item);
                InputSimulator.SettleUntil(() => BackgroundOf(item) == Token("BgHoverBrush"));

                // El diagnóstico nombra el tema: si vuelve a fallar por un cambio de tema a medio aplicar
                // (la causa que costó encontrar), el mensaje lo dice en vez de dejar un color suelto.
                string diag = $"tema={ThemeManager.Instance.CurrentThemeId} " +
                              $"variante={window.ActualThemeVariant} " +
                              $"tokenVentana={WindowToken(window, "BgHoverBrush")}";

                BackgroundOf(item).Should().Be(Token("BgHoverBrush"),
                    "Border.nodeMenuItem:pointerover debe iluminar el ítem que se está a punto de arrastrar" +
                    $" [{diag}]");
            }
            finally
            {
                Close(window);
            }
        });
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Atajos de teclado sobre un nodo del lienzo
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Delete_ShouldRemoveTheSelectedNode_AndCtrlZ_CtrlY_ShouldUndoAndRedoIt()
    {
        WithEditor((window, view, editor) =>
        {
            var node = editor.Nodes[0];

            InputSimulator.Click(window, NodeTitle(view, node));
            node.IsSelected.Should().BeTrue("hacer clic en el nodo lo selecciona: sin selección Delete no tiene objetivo");

            int before = editor.Nodes.Count;

            InputSimulator.Key(window, Key.Delete);

            editor.Nodes.Should().HaveCount(before - 1, "Delete borra el nodo seleccionado");
            editor.Nodes.Should().NotContain(node);

            InputSimulator.Key(window, Key.Z, RawInputModifiers.Control);
            editor.Nodes.Should().HaveCount(before, "Ctrl+Z deshace el borrado");

            InputSimulator.Key(window, Key.Y, RawInputModifiers.Control);
            editor.Nodes.Should().HaveCount(before - 1, "Ctrl+Y rehace el borrado");

            InputSimulator.Key(window, Key.Z, RawInputModifiers.Control);
            editor.Nodes.Should().HaveCount(before, "el deshacer sigue funcionando tras rehacer");
        });
    }

    [Fact]
    public void F2_ShouldStartRenaming_AndEnterShouldCommitTheNewTitle()
    {
        WithEditor((window, view, editor) =>
        {
            var node = editor.Nodes[0];

            InputSimulator.Click(window, NodeTitle(view, node));
            InputSimulator.Key(window, Key.F2);

            node.IsEditingTitle.Should().BeTrue("F2 inicia el renombrado del nodo seleccionado");

            var box = CardRenameBox(view, node);
            InputSimulator.Click(window, box);

            box.IsFocused.Should().BeTrue("el renombrado ocurre donde el usuario escribe");

            InputSimulator.Key(window, Key.A, RawInputModifiers.Control);
            InputSimulator.Type(window, "Renombrado por teclado");
            InputSimulator.Key(window, Key.Enter);

            node.IsEditingTitle.Should().BeFalse("Enter cierra el renombrado");
            node.Title.Should().Be("Renombrado por teclado", "Enter confirma lo que se escribió");
        });
    }

    [Fact]
    public void Escape_ShouldCancelTheRename_AndKeepTheTitle()
    {
        WithEditor((window, view, editor) =>
        {
            var node = editor.Nodes[0];
            string original = node.Title;

            InputSimulator.Click(window, NodeTitle(view, node));
            InputSimulator.Key(window, Key.F2);

            var box = CardRenameBox(view, node);
            InputSimulator.Click(window, box);

            InputSimulator.Key(window, Key.A, RawInputModifiers.Control);
            InputSimulator.Type(window, "Esto no debe quedar");
            InputSimulator.Key(window, Key.Escape);

            node.IsEditingTitle.Should().BeFalse("Escape cierra el renombrado");
            node.Title.Should().Be(original, "Escape descarta lo escrito en lugar de confirmarlo");
        });
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Soltar un nodo del cajón en el lienzo
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DroppingAToolboxItem_ShouldCreateTheNodeWhereItWasDropped()
    {
        WithEditor((window, view, editor, fixture) =>
        {
            var canvas = Canvas(view);
            string typeName = fixture.Toolbox.CategoryGroups
                .SelectMany(group => group.Items)
                .First()
                .TypeName;

            // El fixture siembra un grafo de ejemplo, así que la prueba mide el <b>delta</b> en lugar de un
            // total absoluto: así el lienzo de partida puede cambiar sin reescribir la prueba.
            Point first = DropAt(window, canvas, editor, new Point(420, 260), typeName);
            Point second = DropAt(window, canvas, editor, new Point(680, 430), typeName);

            second.X.Should().BeGreaterThan(first.X, "el nodo cae donde se suelta, no siempre en el mismo sitio");
            second.Y.Should().BeGreaterThan(first.Y);
        });
    }

    [Fact]
    public void DroppingSomethingThatIsNotANodeType_ShouldCreateNothing()
    {
        WithEditor((window, view, editor) =>
        {
            var canvas = Canvas(view);
            int before = editor.Nodes.Count;
            var untouched = editor.Nodes.Select(n => (n.Id, n.Location.X, n.Location.Y)).ToList();

            InputSimulator.DropText(window, InputSimulator.CenterOf(canvas, window), "NodoQueNoExiste");

            editor.Nodes.Should().HaveCount(before,
                "un texto que no es un tipo de nodo no puede crear nada (ni dejar un nodo a medio construir)");
            editor.Nodes.Select(n => (n.Id, n.Location.X, n.Location.Y)).Should().Equal(untouched,
                "un soltado inválido tampoco puede mover ni recrear los nodos que ya estaban");
        });
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Buscador rápido (spotlight): atajo, foco, posición, filtrado y confirmación
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheQuickAddShortcut_ShouldOpenTheSpotlight_AtThePointer_WithItsBoxFocused()
    {
        WithEditor((window, view, editor) =>
        {
            var canvas = Canvas(view);

            // El puntero marca dónde nace el nodo: es el gesto real (colocar el ratón y pulsar el atajo).
            var pointer = new Point(canvas.Bounds.Width * 0.35, canvas.Bounds.Height * 0.4);
            Point windowPoint = canvas.TranslatePoint(pointer, window) ?? pointer;
            InputSimulator.MovePointer(window, windowPoint);

            InputSimulator.Key(window, Key.Space);

            editor.IsSpotlightOpen.Should().BeTrue("Espacio abre el buscador rápido (Blender / ComfyUI)");

            double zoom = editor.ViewportZoom > 0 ? editor.ViewportZoom : 1.0;
            editor.SpotlightCanvasPosition.X.Should().BeApproximately(
                editor.ViewportLocation.X + (pointer.X / zoom), 2.0,
                "el buscador nace donde está el puntero, no siempre en el centro del viewport");
            editor.SpotlightCanvasPosition.Y.Should().BeApproximately(
                editor.ViewportLocation.Y + (pointer.Y / zoom), 2.0);

            SearchBox(view).IsFocused.Should().BeTrue(
                "el buscador debe recibir el foco solo: el atajo se usa con el teclado, sin ratón");
        });
    }

    [Fact]
    public void ShiftA_ShouldAlsoOpenTheSpotlight()
    {
        WithEditor((window, _, editor) =>
        {
            InputSimulator.Key(window, Key.A, RawInputModifiers.Shift);

            editor.IsSpotlightOpen.Should().BeTrue("Shift+A es el atajo alternativo de quick-add");
        });
    }

    [Fact]
    public void InTheSpotlight_ShouldFocus_TypeToFilter_AndConfirmWithTheKeyboard()
    {
        WithEditor((window, view, editor) =>
        {
            InputSimulator.Key(window, Key.Space);

            // El primer ítem de la lista completa da un término que existe de verdad, en lugar de una cadena
            // fija que la localización podría dejar sin coincidencias.
            string first = editor.FilteredSpotlightItems.First().Name;
            string term = first.Length > 4 ? first[..4] : first;

            InputSimulator.Type(window, term);

            editor.SpotlightSearchText.Should().Be(term, "las teclas deben llegar al buscador con el foco");

            editor.FilteredSpotlightItems.Should().NotBeEmpty($"«{term}» debe seguir encontrando nodos");

            foreach (var item in editor.FilteredSpotlightItems)
            {
                bool matches =
                    item.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    item.Category.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    item.Description.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (item.Tags?.Any(tag => tag.Contains(term, StringComparison.OrdinalIgnoreCase)) ?? false);

                matches.Should().BeTrue($"el filtro dejó pasar '{item.Name}', que no contiene «{term}»");
            }

            // Navegar con las flechas y confirmar con Enter: el camino completo sin tocar el ratón.
            InputSimulator.Key(window, Key.Down);
            editor.SelectedSpotlightItem.Should().NotBeNull();

            string expectedType = editor.SelectedSpotlightItem!.TypeName;
            string expectedSimpleName = expectedType.Split('.').Last();

            int before = editor.Nodes.Count;
            InputSimulator.Key(window, Key.Enter);

            editor.IsSpotlightOpen.Should().BeFalse("confirmar cierra el buscador");
            editor.Nodes.Should().HaveCount(before + 1, "confirmar crea el nodo seleccionado");
            editor.Nodes[^1].NodeInstance.GetType().Name.Should().Be(
                expectedSimpleName,
                "el nodo creado debe ser el tipo que estaba seleccionado en la lista");
        });
    }

    [Fact]
    public void Escape_ShouldCloseTheSpotlight_WithoutCreatingAnything()
    {
        WithEditor((window, _, editor) =>
        {
            InputSimulator.Key(window, Key.Space);
            editor.IsSpotlightOpen.Should().BeTrue();

            int before = editor.Nodes.Count;
            InputSimulator.Key(window, Key.Escape);

            editor.IsSpotlightOpen.Should().BeFalse("Escape cierra el buscador");
            editor.Nodes.Should().HaveCount(before, "cerrar no puede crear ningún nodo");
        });
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Andamiaje
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Botón de la variante de acento, que es la que declara tokens en hover y pressed.</summary>
    private static Button PrimaryButton() => new()
    {
        Content = "Ejecutar",
        Classes = { "primary" },
        Width = 150,
        Height = 34
    };

    /// <summary>
    /// Muestra un control en su ventana, con el tema fijado, y deja el primer fotograma asentado.
    ///
    /// <para><b>El tema se fija aquí a propósito</b>: los estados de estilo se afirman contra los tokens del
    /// tema activo, y el tema es estado de proceso que esta colección comparte con las capturas. Sin fijarlo,
    /// un cambio de tema a medio aplicar dejaba al elemento animando hacia el token de <b>un</b> tema mientras
    /// la aserción leía el de <b>otro</b>: un fallo intermitente que solo aparecía en la suite completa y que
    /// no tenía nada que ver con la interacción que se estaba probando.</para>
    /// </summary>
    private static Window Show(Control content, double width = 420, double height = 240)
    {
        PinTheme();

        var window = new Window { Content = content, Width = width, Height = height };
        window.Show();
        InputSimulator.Settle();
        return window;
    }

    /// <summary>Fija el tema del proceso en el preset oscuro, que es el de partida del producto.</summary>
    private static void PinTheme() => ThemeManager.Instance.SetThemeById(DarkTheme);

    private static void Close(Window window)
    {
        VisualSnapshot.DetachTree(window);
        window.Close();
    }

    /// <summary>
    /// Monta el editor completo (con el cajón, para tener tipos de nodo reales), da el foco al lienzo —como
    /// hace el usuario al hacer clic en él— y entrega la ventana al cuerpo de la prueba.
    /// </summary>
    private static void WithEditor(Action<Window, EditorView, EditorViewModel> body) =>
        WithEditor((window, view, editor, _) => body(window, view, editor));

    private static void WithEditor(Action<Window, EditorView, EditorViewModel, AppVisualFixture> body)
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            using var fixture = AppVisualFixture.Create();
            var view = new EditorView { DataContext = fixture.Editor };
            var window = Show(view, 1280, 820);

            try
            {
                // El atajo vive en EditorView y se enruta desde el elemento con el foco hacia arriba: sin el
                // lienzo enfocado (lo que ocurre al primer clic sobre él) las teclas no llegarían. Es una
                // condición real del producto, no un apaño de la prueba.
                Canvas(view).Focus();
                InputSimulator.Settle();

                body(window, view, fixture.Editor, fixture);
            }
            finally
            {
                Close(window);
            }
        });
    }

    /// <summary>Suelta un ítem del cajón en el punto dado y devuelve dónde quedó el nodo creado.</summary>
    private static Point DropAt(
        Window window,
        Control canvas,
        EditorViewModel editor,
        Point windowPoint,
        string typeName)
    {
        int before = editor.Nodes.Count;

        InputSimulator.DropText(window, windowPoint, typeName);

        editor.Nodes.Should().HaveCount(before + 1, "un ítem del cajón soltado en el lienzo crea su nodo");

        // El destino se calcula en coordenadas del lienzo con el zoom y el desplazamiento del viewport, así
        // que la comprobación replica esa cuenta en lugar de comparar con un número mágico.
        Point canvasOrigin = canvas.TranslatePoint(new Point(0, 0), window) ?? new Point(0, 0);
        double zoom = editor.ViewportZoom > 0 ? editor.ViewportZoom : 1.0;

        var expected = new Point(
            editor.ViewportLocation.X + ((windowPoint.X - canvasOrigin.X) / zoom),
            editor.ViewportLocation.Y + ((windowPoint.Y - canvasOrigin.Y) / zoom));

        var node = editor.Nodes[^1];
        node.NodeInstance.GetType().Name.Should().Be(typeName.Split('.').Last(), "se crea el tipo arrastrado");
        node.Location.X.Should().BeApproximately(expected.X, 2.0, "el nodo cae donde se soltó");
        node.Location.Y.Should().BeApproximately(expected.Y, 2.0, "el nodo cae donde se soltó");

        return node.Location;
    }

    private const string DarkTheme = "dark_fluent";

    private static Nodify.Avalonia.NodifyEditor Canvas(EditorView view) =>
        view.GetVisualDescendants().OfType<Nodify.Avalonia.NodifyEditor>().First();

    private static TextBox SearchBox(EditorView view) =>
        view.GetVisualDescendants().OfType<TextBox>().First(box => box.Name == "SpotlightSearchBox");

    /// <summary>El texto del título del nodo en la tarjeta: es una zona no interactiva, como la que usa el usuario para seleccionarlo.</summary>
    private static TextBlock NodeTitle(EditorView view, NodeViewModel node) =>
        view.GetVisualDescendants().OfType<TextBlock>()
            .First(block => block.Text == node.Title && block.IsEffectivelyVisible && block.Bounds.Width > 0);

    /// <summary>La caja de renombrado en sitio de la tarjeta (solo es visible mientras se renombra).</summary>
    private static TextBox CardRenameBox(EditorView view, NodeViewModel node) =>
        view.GetVisualDescendants().OfType<TextBox>()
            .First(box => box.Name == "TitleEditBox" && ReferenceEquals(box.DataContext, node));

    /// <summary>Primer ítem del cajón ya materializado (los no visibles no tienen geometría ni reciben el puntero).</summary>
    private static Border FirstRealizedNodeMenuItem(Visual root) =>
        root.GetVisualDescendants().OfType<Border>()
            .First(border => border.Classes.Contains("nodeMenuItem") && border.Bounds is { Width: > 0, Height: > 0 });

    /// <summary>La cara del botón: donde el tema Fluent y nuestro sistema de diseño declaran hover y pressed.</summary>
    private static ContentPresenter Surface(Button button) =>
        button.GetVisualDescendants().OfType<ContentPresenter>()
            .First(presenter => presenter.Name == "PART_ContentPresenter");

    private static Color FaceOf(Button button) => ColorOf(Surface(button).Background, "la cara del botón");

    /// <summary>
    /// El color de la etiqueta: el que la parte de plantilla declara para su contenido. Es lo que el estado
    /// deshabilitado tiene que declarar en lugar de atenuar la parte, porque el tema base ya atenúa ese mismo
    /// primer plano por su cuenta.
    /// </summary>
    private static Color LabelOf(Button button) => ColorOf(Surface(button).Foreground, "la etiqueta del botón");

    /// <summary>El borde que pinta el anillo de foco de un campo de texto.</summary>
    private static Color BorderRing(TextBox field) => ColorOf(
        field.GetVisualDescendants().OfType<Border>()
            .First(border => border.Name == "PART_BorderElement").BorderBrush,
        "el borde del campo");

    private static Color TrackOf(TabItem tab) => ColorOf(
        tab.GetVisualDescendants().OfType<Border>()
            .First(border => border.Name == "PART_LayoutRoot").Background,
        "el carril de la pestaña");

    private static Color ForegroundOf(TabItem tab) => ColorOf(tab.Foreground, "el texto de la pestaña");

    /// <summary>El indicador de la pestaña activa (la línea de acento que viaja con la selección).</summary>
    private static Border PipeOf(TabItem tab) =>
        tab.GetVisualDescendants().OfType<Border>().First(border => border.Name == "PART_SelectedPipe");

    private static Color BackgroundOf(Border border) => ColorOf(border.Background, "el fondo del ítem");

    /// <summary>
    /// Color de un pincel exigiendo que sea sólido: si el tema lo cambia por un degradado, la prueba lo dice
    /// en lugar de comparar <c>null</c> con <c>null</c> y pasar en falso.
    /// </summary>
    private static Color ColorOf(IBrush? brush, string what) => brush switch
    {
        ISolidColorBrush solid => solid.Color,
        null => throw new InvalidOperationException($"{what} no tiene pincel asignado."),
        _ => throw new InvalidOperationException($"{what} no es un color sólido sino {brush.GetType().Name}.")
    };

    /// <summary>Color del token tal y como lo resuelve la <b>ventana</b> de la prueba (no la aplicación).</summary>
    private static Color? WindowToken(Window window, string key) =>
        window.TryFindResource(key, out var value) && value is ISolidColorBrush solid ? solid.Color : null;

    /// <summary>Color del token del tema activo, para afirmar la regla y no solo «algo cambió».</summary>
    private static Color Token(string key) =>
        VisualSnapshot.ResolveToken<IBrush>(key) is ISolidColorBrush solid
            ? solid.Color
            : throw new InvalidOperationException($"El token '{key}' no es un color sólido.");

    /// <summary>Comando que solo cuenta ejecuciones, para probar que un control no actúa cuando no debe.</summary>
    private sealed class RecordingCommand : System.Windows.Input.ICommand
    {
        public int Executions { get; private set; }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Executions++;
    }
}
