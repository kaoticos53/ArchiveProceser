using System;
using System.IO;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Guardia del CABLE del lienzo Uno (hito 254): las líneas de conexión tienen que tocar sus sockets y seguir
/// tocándolos cuando el plano se mueve o cambia el zoom.
///
/// <para><b>Los dos defectos que guarda</b>, medidos con la sonda <c>ProbeWireTracking</c> y reportados por el
/// usuario («al mover o ajustar el zoom las líneas de conexión se desplazan quedando fuera de su sitio»):</para>
/// <list type="number">
/// <item>La figura del cable abría en el primer punto de control, así que el cable quedaba separado de cada
/// socket y salía invertido —el rulo con forma de «2»— cuando las anclas estaban cerca; su segundo intento
/// añadió los dos tramos rectos del trazo de la versión anterior y el resultado se leía como una <b>Z</b>.</item>
/// <item>El centro del socket se medía transformando el vértice <c>(0, 0)</c> y <b>sumando</b> después la mitad
/// del tamaño, lo que ignora la escala de la cadena: al 125 % el ancla se quedaba corta
/// <c>0,25 · (w/2)</c> y el cable aparecía desplazado en cuanto se tocaba el zoom.</item>
/// </list>
///
/// <para><b>Las dos mitades del control</b>: el dibujo de la capa de cables —el cable que se ve, su diana, el
/// menú de la diana y su sonda— vive en <c>EditorCanvasControl.Wires.cs</c> (hito 282), y el resto (pan/zoom,
/// tarjetas, gesto del puerto) en <c>EditorCanvasControl.xaml.cs</c>. El censo de la figura y el de la
/// selección leen <b>las dos mitades</b>, porque la figura se declara en una y la usa también la otra; el resto
/// de censos leen el fichero que les toca.</para>
///
/// <para><b>Por qué se censa la fuente</b>: el host Uno (WinUI) no se compila en el suite, así que lo que se
/// guarda es <i>cómo</i> se construye la figura y <i>cómo</i> se mide el ancla; la parte de matemática vive en
/// el núcleo portable (<see cref="FileFlow.App.Services.ConnectionGeometry.WirePath"/>) y ahí sí hay pruebas de
/// comportamiento —el trazado empieza y termina en las anclas— además de la sonda, que lo mide en la app viva.</para>
/// </summary>
public class UnoCanvasWireGuardTests
{
    private const string CanvasCodePath = "FileFlow.App.Uno/Controls/EditorCanvasControl.xaml.cs";

    /// <summary>
    /// La MITAD DE LOS CABLES del lienzo (hito 282): el dibujo de la capa —el cable que se ve y su diana—, el
    /// menú de la diana y su sonda viven aquí, aparte del pan/zoom y de las tarjetas.
    /// </summary>
    private const string WireCodePath = "FileFlow.App.Uno/Controls/EditorCanvasControl.Wires.cs";
    private const string CardXamlPath = "FileFlow.App.Uno/Controls/NodeCardView.xaml";
    private const string CanvasXamlPath = "FileFlow.App.Uno/Controls/EditorCanvasControl.xaml";
    private const string CardCodePath = "FileFlow.App.Uno/Controls/NodeCardView.xaml.cs";
    private const string SelfCheckPath = "FileFlow.App.Uno/SelfCheckPointerless.cs";

    /// <summary>La sonda del LIENZO: donde vive lo que se mide sin puntero sobre la capa de cables.</summary>
    private const string CanvasSelfCheckPath = "FileFlow.App.Uno/SelfCheckCanvas.cs";
    private const string GeometryPath = "FileFlow.App.Core/Services/ConnectionGeometry.cs";
    private const string FigureMutationPath = "mutations/cable-que-no-toca-su-socket.json";
    private const string AnchorMutationPath = "mutations/ancla-que-ignora-la-escala.json";
    private const string GestureMutationPath = "mutations/gesto-de-puerto-que-no-conecta.json";
    private const string WiringMutationPath = "mutations/socket-que-se-queda-sin-cablear.json";
    private const string SelectionMutationPath = "mutations/cable-que-no-se-puede-pulsar.json";
    private const string MarkMutationPath = "mutations/cable-marcado-que-no-se-ve.json";
    private const string MenuPlacementMutationPath = "mutations/menu-que-sale-en-una-esquina.json";

    /// <summary>El caso que es testigo de la mutación de la figura (la cobertura casa el filtro por nombre).</summary>
    private const string FigureWitnessCase = "TheWireFigure_ShouldBeOneBezier_FromAnchorToAnchor";

    /// <summary>El caso que es testigo de la mutación de la medida del ancla.</summary>
    private const string AnchorWitnessCase = "TheAnchorMeasurement_ShouldTransformTheCenter_NotSumIt";

    /// <summary>El caso que es testigo de la mutación del gesto del cable (la cobertura casa el filtro por nombre).</summary>
    private const string GestureWitnessCase = "TheSocketGesture_ShouldStartFollowAndDropTheCable_AndCancelWhatDoesNotLand";

    /// <summary>El caso que ata la SELECCIÓN del cable (poder pulsarlo para borrar su conexión).</summary>
    private const string SelectionWitnessCase = "TheWire_ShouldBeSelectableToBeDeleted_ThroughTheCoreOrder";

    private static string CanvasCode() => SourceText.CodeWithoutComments(CanvasCodePath);
    private static string WireCode() => SourceText.CodeWithoutComments(WireCodePath);

    /// <summary>
    /// Las dos mitades del control como UNA fuente: la figura del cable se declara en la mitad de los cables y
    /// la usan los tres trazados (el cable del grafo y su diana, allí; el pendiente del arrastre, en el
    /// fichero del control), así que el censo de usos se hace sobre el conjunto —el control es uno.
    /// </summary>
    private static string WireHalves() => WireCode() + "\n" + CanvasCode();
    private static string SelfCheck() => SourceText.CodeWithoutComments(SelfCheckPath);
    private static string CanvasSelfCheck() => SourceText.CodeWithoutComments(CanvasSelfCheckPath);
    private static string Geometry() => SourceText.CodeWithoutComments(GeometryPath);

    [Fact]
    public void TheWireFigure_ShouldBeOneBezier_FromAnchorToAnchor()
    {
        string code = WireHalves();

        code.Should().Contain(
            "StartPoint = ToWindowsPoint(wire.Source),",
            "la figura abre en el ANCLA de salida: abrirla en el cuello deja el cable separado del socket, que " +
            "es el primer defecto medido con la sonda");

        code.Should().Contain(
            "Point1 = ToWindowsPoint(wire.Exit),",
            "el cuello de salida es el primer punto de CONTROL de la curva, no el final de un tramo recto");

        code.Should().Contain(
            "Point2 = ToWindowsPoint(wire.Arrival),",
            "y el de llegada el segundo");

        code.Should().Contain(
            "Point3 = ToWindowsPoint(wire.Target)",
            "la curva muere en el ANCLA de destino: el cable toca sus dos sockets");

        code.Should().NotContain(
            "new LineSegment",
            "ni un tramo recto: los dos bajíos del trazo de la versión anterior son los que se leían como una Z " +
            "en pantalla, que es lo que el usuario pidió quitar. El cable sale del socket ya curvando");

        code.Split("CreateWireGeometry(wire)").Length.Should().Be(
            4,
            "el trazado se construye en UN sitio y lo usan los TRES cables (el del grafo, el pendiente del " +
            "arrastre y la DIANA del cable, que traza el suyo porque una `Geometry` de WinUI no se puede " +
            "compartir entre dos `Path` —medido—): una figura sin anclas se colaría por el camino que no se probó");

        code.Split("ConnectionGeometry.BuildWire(").Length.Should().Be(
            4,
            "y los tres trazados (el cable del grafo, el pendiente del arrastre y el caso del hueco estrecho de la " +
            "sonda) piden la curva al núcleo compartido, no a puntos de control sueltos");

        Geometry().Should().Contain(
            "public IReadOnlyList<Point> Trace => [Source, Exit, Arrival, Target];",
            "el trazado con las anclas dentro vive en el núcleo portable, que es donde tiene pruebas de " +
            "comportamiento y no sólo censo");

        string mutation = File.ReadAllText(Path.Combine(TestRepositoryLocator.RepositoryRoot(), FigureMutationPath));
        mutation.Should().Contain(
            $"\"FullyQualifiedName~{FigureWitnessCase}\"",
            "esta guardia es el testigo de la mutación de la figura: es lo que la convierte en una prueba que " +
            "muerde y no en una que se limita a describir el fuente");
    }

    [Fact]
    public void TheAnchorMeasurement_ShouldTransformTheCenter_NotSumIt()
    {
        string code = CanvasCode();

        code.Should().Contain(
            "var localCenter = UnoPointProjection.ToSdk(element.ActualWidth / 2, element.ActualHeight / 2);",
            "el centro que viaja por la cadena es el centro LOCAL del elemento, no su vértice");

        code.Should().Contain(
            "element.TransformToVisual(relativeTo).TransformPoint(new Windows.Foundation.Point(lx, ly))",
            "y se TRANSFORMA: sumarle la mitad del tamaño después de transformar el vértice ignora la escala " +
            "de la cadena, que es el defecto que sólo aparecía al ajustar el zoom");

        code.Should().NotContain(
            "topLeft.X + (element.ActualWidth / 2)",
            "el ancla corta no puede volver: al 125 % se quedaba 0,25 · (w/2) por debajo del centro real y esa " +
            "ancla viajaba al cable");

        // El centro mal medido no sólo movía los cables: es la misma medida del hit-testing del lienzo y de las
        // tarjetas, así que la corrección tiene que estar en el único sitio donde se calcula.
        code.Split("TransformToVisualCenter(").Length.Should().Be(
            8,
            "la medida del centro se declara una vez y tiene seis usos declarados (las cajas del hit-testing, " +
            "los dos anclas del cable, la tarjeta bajo el puntero, la sonda del foco y la caja del rectángulo de " +
            "selección, hito 286): una copia con la suma vieja se colaría sin que nadie la mida");

        string mutation = File.ReadAllText(Path.Combine(TestRepositoryLocator.RepositoryRoot(), AnchorMutationPath));
        mutation.Should().Contain(
            $"\"FullyQualifiedName~{AnchorWitnessCase}\"",
            "esta guardia es el testigo de la mutación de la medida del ancla");
    }

    [Fact]
    public void TheSocketGesture_ShouldStartFollowAndDropTheCable_AndCancelWhatDoesNotLand()
    {
        string xaml = File.ReadAllText(Path.Combine(TestRepositoryLocator.RepositoryRoot(), CardXamlPath));
        string card = SourceText.CodeWithoutComments(CardCodePath);
        string code = CanvasCode();

        // 1. La FILA del puerto es la zona activa. El socket dibujado mide ~13 px y su `StackPanel` no tenía
        //    fondo, así que el hit-test caía en la cara de la tarjeta —que sí lo tiene— y la pulsación sobre el
        //    puerto arrancaba un ARRASTRE del nodo: es el primer síntoma que reportó el usuario.
        int portRows = 0;
        foreach (string row in xaml.Split("<StackPanel"))
        {
            if (!row.Contains("PointerPressed=\"OnSocketPressed\""))
            {
                continue;
            }

            portRows++;
            row.Should().Contain(
                "Background=\"Transparent\"",
                "la fila del puerto entera tiene que ser hit-testeable: sin fondo, el hit-test cae en la cara de la " +
                "tarjeta y la pulsación arrastra el nodo en vez de arrancar el cable");
        }

        portRows.Should().Be(
            2,
            "los dos puertos del nodo (entrada y salida) montan el mismo gesto: es el único sitio donde el gesto nace");

        card.Should().Contain(
            "element.CapturePointer(e.Pointer);",
            "la fila del puerto se queda con el PUNTERO: sin captura el lienzo recibía UN solo movimiento —en la " +
            "posición de la pulsación, medido con el ratón inyectado— y el cable no seguía al cursor");
        card.Should().Contain(
            "ReleasePointerCapture(e.Pointer);",
            "y lo suelta por los DOS finales del gesto (soltar y perder la captura), para no dejar una captura colgada");

        xaml.Should().Contain(
            "PointerReleased=\"OnSocketReleased\"",
            "la fila suelta la captura cuando se levanta el botón");
        xaml.Should().Contain(
            "PointerCaptureLost=\"OnSocketReleased\"",
            "y también si la captura se pierde por el camino: el gesto puede acabar de las dos formas");
        xaml.Should().Contain(
            "Padding=\"4,3\"",
            "la fila tiene relleno: la diana del puerto era el texto de la etiqueta (~19 px) y pulsar al lado " +
            "arrastraba el nodo o no hacía nada");

        // 2b. El cableado de los sockets. El evento vive en la VISTA, y la vista sólo existe cuando su
        //     contenedor se ha materializado: cablear en Rebuild (o buscar la vista en el Content, que es el
        //     ViewModel) dejaba el evento SIN suscriptor y pulsar un puerto no arrancaba nada.
        int positionsDone = code.IndexOf("_positionsPending = false;", StringComparison.Ordinal);
        int wiredAfterLayout = code.LastIndexOf("WireCardEvents();", StringComparison.Ordinal);
        positionsDone.Should().BeGreaterThan(-1, "el pase de layout que cierra el posicionamiento existe");
        wiredAfterLayout.Should().BeGreaterThan(
            positionsDone,
            "el cableado se hace en el pase de layout que YA tiene contenedores, no en Rebuild (donde el " +
            "ItemsSource acaba de asignarse y no hay ninguna vista que enganchar)");
        code.Should().Contain(
            "FirstDescendant<NodeCardView>(pair.Value)",
            "y la vista se busca en el ÁRBOL VISUAL del contenedor: su Content es el ViewModel, así que buscarla " +
            "ahí no podía dar true nunca");

        // 2. El lienzo: tres tiempos, una casa cada uno, y la pulsación no puede robar el gesto.
        code.Should().Contain(
            "if (properties.IsLeftButtonPressed && WouldArmCardDrag(point))",
            "la pulsación arma arrastre SÓLO si no hay un gesto de puerto vivo: es el síntoma que reportó el usuario " +
            "(pulsar el puerto movía la tarjeta)");

        code.Should().Contain(
            "=> !SocketGestureOwnsThePress && CardAt(point) is not null;",
            "y la decisión es UNA, citada por el handler y por la sonda del gesto");

        code.Should().Contain("private void BeginSocketGesture(PortViewModel port)",
            "el tiempo de PULSAR, en su método (la tarjeta sube el puerto por SocketRequested)");
        code.Should().Contain("private void UpdateSocketGesture(Windows.Foundation.Point screenPoint)",
            "el tiempo de MOVER: dibuja el extremo libre y resuelve el destino");
        code.Should().Contain("private void EndSocketGesture(PortViewModel? target)",
            "el tiempo de SOLTAR, que es el que decide el desenlace");
        code.Should().Contain("UpdateSocketGesture(_lastPointerPosition);",
            "el movimiento del puntero entra por el gesto, no por una copia del cableado");
        code.Should().Contain("EndSocketGesture(_pendingHoverPort);",
            "soltar cierra el gesto donde el último movimiento resolvió el destino");
        code.Should().Contain(
            "_editor?.FinishConnectionCommand.Execute(target);",
            "con destino COMPATIBLE el gesto CREA la conexión por el comando del núcleo: es el desenlace que el " +
            "usuario no tenía y el que la mutación de esta guardia quita");
        code.Should().Contain("EndSocketGesture(null);",
            "Escape cancela por el mismo camino que soltar en el vacío");
        code.Should().Contain("private const double SocketDropTolerance",
            "la diana del gesto se declara una vez (~13 px de socket dibujado no son una diana humana)");
        code.Should().Contain(
            "CardBoxIsWithin(container, screenPoint)",
            "el destino se busca en toda tarjeta a tiro (con su borde de tolerancia), no sólo en la que está justo " +
            "debajo del puntero: soltar unos píxeles corto cancelaba conexiones que el usuario creía hechas");

        // 3. La sonda que lo mide en la app viva, y su testigo.
        code.Should().Contain(
            "internal (bool Started, bool Followed, bool Connected, bool Cancelled, string Detail) ProbeSocketGesture()",
            "el gesto se mide por los MISMOS métodos que ejecutan los handlers: el host Uno no se materializa en la " +
            "sesión de pruebas y el puntero inyectado no entrega movimientos");
        SelfCheck().Should().Contain("canvas.ProbeSocketGesture()",
            "y el selfcheck corre la sonda en la app viva");
        SelfCheck().Should().Contain(
            "pulsar un puerto arranca el cable y NO arma el arrastre de la tarjeta",
            "el renglón del primer desenlace nombra lo que mide: se lee en el informe sin abrir el código");
        SelfCheck().Should().Contain(
            "soltarlo en el vacío o sobre un destino incompatible la CANCELA",
            "y el del desenlace que deja el estado limpio, que es la otra mitad del encargo");

        string mutation = File.ReadAllText(Path.Combine(TestRepositoryLocator.RepositoryRoot(), GestureMutationPath));
        mutation.Should().Contain(
            $"\"FullyQualifiedName~{GestureWitnessCase}\"",
            "esta guardia es el testigo de la mutación del gesto: es lo que la convierte en una prueba que muerde y " +
            "no en una que se limita a describir el fuente");

        string wiringMutation = File.ReadAllText(
            Path.Combine(TestRepositoryLocator.RepositoryRoot(), WiringMutationPath));
        wiringMutation.Should().Contain(
            $"\"FullyQualifiedName~{GestureWitnessCase}\"",
            "y esta misma guardia es testigo de la mutación del CABLEADO: si alguien vuelve a buscar la vista en " +
            "el Content, el evento se queda sin suscriptor y el caso cae");
    }

    /// <summary>
    /// La SELECCIÓN del cable: el defecto que reportó el usuario («no puedo seleccionar las conexiones para
    /// borrarlas»). El cable era un <c>Path</c> de 3,5 px dentro de una capa <c>IsHitTestVisible=False</c>, así
    /// que no existía para el ratón: el clic derecho caía al fondo del lienzo y <b>paneaba</b>. Lo que se mide
    /// aquí son las DOS mitades que hacen la conexión borrable —la diana que se puede pulsar y la orden del
    /// núcleo que su menú cumple—, más la sonda que las mide en la app viva.
    /// </summary>
    [Fact]
    public void TheWire_ShouldBeSelectableToBeDeleted_ThroughTheCoreOrder()
    {
        string xaml = File.ReadAllText(Path.Combine(TestRepositoryLocator.RepositoryRoot(), CanvasXamlPath));
        string code = WireHalves();

        // 1. La CAPA tiene que ser alcanzable por el puntero.
        xaml.Should().Contain(
            "<Canvas x:Name=\"WireLayer\" IsHitTestVisible=\"True\"",
            "la capa de cables no puede estar fuera del hit-test: con ella fuera, pulsar un cable no llega a nadie " +
            "y el clic derecho panea el lienzo en vez de ofrecer su borrado");

        // 2. La DIANA: la MISMA Bézier con un trazo grueso e invisible (un trazo de 3,5 px no se puede pulsar).
        code.Should().Contain(
            "private const double WireHitThickness = 14;",
            "el grosor de la diana vive en UNA constante, para que la guardia y la sonda midan con la misma vara");
        code.Should().Contain(
            "Stroke = new SolidColorBrush(Microsoft.UI.Colors.Transparent),",
            "la diana se pinta con Transparent y no con nulo: sin trazo no hay clic que valga");
        int hitAt = code.IndexOf("var hit = new Microsoft.UI.Xaml.Shapes.Path", StringComparison.Ordinal);
        hitAt.Should().BeGreaterThan(-1, "la diana del cable se construye en DrawWires, junto al cable que se ve");
        int hitEnd = code.IndexOf("WireLayer.Children.Add(hit);", hitAt, StringComparison.Ordinal);
        hitEnd.Should().BeGreaterThan(hitAt, "y entra en la capa como el cable dibujado");
        string hitBlock = code[hitAt..hitEnd];

        hitBlock.Should().Contain(
            "StrokeThickness = WireHitThickness,",
            "su trazo es el grueso: es lo que hace acertable un cable fino");
        hitBlock.Should().Contain(
            "Data = CreateWireGeometry(wire)",
            "su figura se traza desde el MISMO `wire` del núcleo que la del cable dibujado (una diana con otra " +
            "curva sería otro cable), y es una figura PROPIA: una `Geometry` de WinUI no se puede compartir " +
            "entre dos `Path` —medido: la segunda asignación levanta excepción y la capa queda a medias—");

        // 3. El botón derecho sobre el cable no es el pan del lienzo (la versión anterior abre su menú por el mismo
        //    motivo: el desplazamiento no puede robarse el gesto de la conexión).
        code.Should().Contain(
            "hit.PointerPressed += (_, e) => OnWirePressed(connection, e);",
            "el cable atiende su pulsación: sin eso el fondo arranca el PAN y el menú aparecería con el lienzo movido");
        code.Should().Contain(
            "private void OnWirePressed(ConnectionViewModel connection, PointerRoutedEventArgs e)",
            "y lo hace en su método, que marca atendido el clic (el derecho abre el menú; el izquierdo marca el " +
            "cable para que el Supr lo borre)");

        // 3b. La MARCA VISIBLE: marcar el cable cambia el trazo que se dibuja (el acento de selección y más
        //     grueso) y el Supr de la tabla del núcleo borra lo marcado. Una marca invisible sería un borrado
        //     de algo que nadie ve elegido, y un Supr que borra el cable marcado sin que el cable se marque
        //     sería un atajo que no se puede disparar.
        code.Should().Contain(
            "Stroke = CanvasBrush(selected ? SelectedWireBrushKey : \"CanvasWireBrush\"),",
            "el cable marcado se pinta con el acento de selección: es lo que el usuario ve al marcarlo");
        code.Should().Contain(
            "StrokeThickness = selected ? WireSelectedThickness : WireThickness,",
            "y engorda: un color parecido en un trazo fino no se lee como selección");
        code.Should().Contain(
            "_editor?.SelectConnection(connection, add: IsKeyDown(Windows.System.VirtualKey.Control));",
            "el clic izquierdo pide la marca al NÚCLEO (el host no tiene estado de selección propio) y el Ctrl " +
            "decide si AÑADE a lo ya marcado o REEMPLAZA la selección");
        code.Should().Contain(
            "_editor.SelectedConnections.CollectionChanged += OnSelectedConnectionsChanged;",
            "y el resalte se repinta cuando cambia la MARCA (la colección, no un campo suelto que se quedaría " +
            "con el cable anterior al reemplazar), para que lo vean todas las puertas (el clic, el Ctrl, el Supr, " +
            "el undo, un clic en el vacío)");
        code.Should().Contain(
            "_editor.ClearSelection();",
            "el clic en el vacío desmarca TODO —nodos y cables—: es la otra mitad de una regla que REEMPLAZA, " +
            "porque sin esto no habría forma de dejar de tener algo elegido");

        // 3c. La REGLA que pidió el usuario: pulsar con el izquierdo REEMPLAZA (los demás se sueltan) y el Ctrl
        //     AÑADE a lo que ya estaba. El estado vive en el NÚCLEO (SelectNode/SelectConnection con `add`), así
        //     que la vista sólo le pasa el modificador; y el rectángulo de selección cumple la misma regla.
        code.Should().Contain(
            "bool selected = _editor.SelectedConnections.Contains(connection);",
            "la marca se pregunta a la COLECCIÓN del núcleo en cada trazado: un campo suelto podría quedarse con " +
            "el cable anterior cuando el clic reemplaza la selección");
        code.Should().Contain(
            "_editor?.SelectNode(card.Node, add: IsKeyDown(Windows.System.VirtualKey.Control));",
            "pulsar una TARJETA pasa por la misma regla: sin Ctrl suelta los demás nodos y los cables marcados; " +
            "con Ctrl añade, y el estado lo gobierna el núcleo");
        code.Should().Contain(
            "BeginRubberBand(point, add: IsKeyDown(Windows.System.VirtualKey.Control));",
            "el rectángulo de selección pregunta el modificador al PULSAR, en el mismo sitio que el clic, y el " +
            "gesto tiene sus tres tiempos en sus métodos (arrancar, mover, soltar): así la sonda y los handlers " +
            "miden por el mismo camino");
        code.Should().Contain(
            "if (!add)",
            "y sin Ctrl REEMPLAZA: el rectángulo suelta lo elegido antes de trazar, igual que el clic");

        // 3d. El RECTÁNGULO también marca CABLES (hito 286): dice «lo que cae aquí dentro está elegido», y lo
        //     que cae dentro puede ser un nodo, un cable o los dos. La decisión es del NÚCLEO
        //     (`ApplyRubberSelection`) y la vista sólo decide QUÉ quedó dentro: un cable entra con sus DOS
        //     anclas dentro, las mismas que trazan su curva.
        code.Should().Contain(
            "_editor.ApplyRubberSelection(nodes, connections, _rubberAdditive, _rubberBaseNodes, _rubberBaseConnections);",
            "el rectángulo pide la decisión al NÚCLEO con lo que quedó dentro y con la base que Ctrl no suelta");
        code.Should().Contain(
            "_rubberBaseConnections = _editor?.SelectedConnections.ToHashSet() ?? [];",
            "y al arrancar guarda también los CABLES marcados: con Ctrl el rectángulo no suelta la marca de fuera");
        code.Should().Contain(
            "private Dictionary<ConnectionViewModel, (Sdk.Point Source, Sdk.Point Target)> MeasureWireAnchors()",
            "las anclas de los cables se miden UNA vez al arrancar el rectángulo: hacerlo en cada movimiento sería " +
            "dos recorridos del árbol de sockets por cable y por movimiento");
        code.Should().Contain(
            "if (AnchorOf(connection.Source) is { } source && AnchorOf(connection.Target) is { } target)",
            "y son las anclas REALES (el write-back del árbol), las mismas con las que se traza el cable: sin las " +
            "dos medidas ese cable no entra, porque el rectángulo no adivina");
        code.Should().Contain(
            "&& IsInside(pair.Value.Target, left, top, right, bottom))",
            "un cable entra con sus DOS anclas dentro: con una sola no basta");
        code.Should().Contain(
            "var from = GraphPointFromScreen(_rubberStart);",
            "y el rectángulo se decide en espacio de GRAFO (el de las tarjetas y las anclas): comparar los puntos " +
            "del puntero en crudo contra posiciones del grafo sólo acertaba con el plano sin mover");
        code.Should().Contain(
            "Canvas.GetLeft(container) + card.Width / 2,",
            "el centro de la tarjeta se compara en espacio de grafo, sin escalar la posición: el pan y el zoom los " +
            "lleva el plano");
        CanvasSelfCheck().Should().Contain(
            "el rectángulo elige tarjetas y cables A LA VEZ",
            "y la sonda lo mide en la app viva: el renglón nombra lo que mide, sin abrir el código");

        string shortcuts = SourceText.CodeWithoutComments("FileFlow.App.Core/Services/EditorKeyboardShortcuts.cs");
        shortcuts.Should().Contain(
            "editor.DeleteSelectionCommand.Execute(null);",
            "Supr borra LA SELECCIÓN ENTERA por la orden del núcleo: los cables marcados —uno o VARIOS, que el " +
            "Ctrl añade— y los nodos elegidos, con lo que cuelga de ellos, en una sola operación de deshacer");
        code.Should().Contain(
            "hit.RightTapped += (_, e) =>",
            "el menú se abre al SOLTAR, que es el gesto del clic derecho");

        // 3b. Y sale DONDE ESTÁ EL PUNTERO. Anclado a la diana —cuyo rectángulo es TODO el cable, de un
        //     socket al otro— el menú aparecía en una esquina del cable: medido con el ratón inyectado,
        //     a **855 px** del cursor. La versión anterior lo abre en el puntero (su `ContextMenu` del host original),
        //     así que la posición explícita es la paridad, no un adorno.
        code.Should().Contain(
            "ShowWireMenuAt(connection, e.GetPosition(RootGrid));",
            "el menú recibe el punto del puntero en el MISMO espacio que el lienzo usa para todo lo demás");
        code.Should().Contain(
            "flyout.ShowAt(RootGrid, new FlyoutShowOptions",
            "y se muestra con la posición explícita sobre la raíz del lienzo");
        code.Should().Contain(
            "Position = canvasPoint,",
            "la posición es el punto recibido: sin ella, WinUI lo ancla al elemento pulsado (la esquina del cable)");
        code.Should().NotContain(
            "ShowAt(hit)",
            "anclar el menú a la DIANA es el defecto medido: su rectángulo es el cable entero y el menú sale en su esquina");

        // 4. Y la orden es la del NÚCLEO, con su rótulo del diccionario del host: la misma que cumple el menú
        //    del cable de la versión anterior, con su undo.
        code.Should().Contain(
            "Command = connection.DeleteCommand",
            "el host no borra nada por su cuenta: pide la orden del núcleo, y el grafo se entera");
        code.Should().Contain(
            "LocalizationManager.Instance.GetString(\"Uno_Connection_Delete\", \"Eliminar conexión\")",
            "el rótulo sale del diccionario del host (y es el texto con el que la versión anterior rotula el suyo)");

        // 5. La sonda mide las dos mitades en la app viva: una diana sin orden es un clic que no borra nada, y
        //    una orden sin diana es una capacidad invisible.
        code.Should().Contain(
            "internal (int Connections, int HitTargets, bool SameFigure, bool CoreOrder, string MenuText,",
            "la sonda de la selección del cable existe y devuelve la medida");
        CanvasSelfCheck().Should().Contain(
            "canvas.ProbeWireSelection()",
            "y la sonda del lienzo la corre en la app viva, que es donde la capa tiene geometría resuelta");
        CanvasSelfCheck().Should().Contain(
            "el cable que se ve se puede pulsar",
            "el renglón nombra lo que mide: se lee en el informe sin abrir el código");
        CanvasSelfCheck().Should().Contain(
            "el cable marcado SE VE marcado",
            "y la marca visible tiene su propio renglón: es la mitad que el usuario reportó como ausente");
        CanvasSelfCheck().Should().Contain(
            "el Supr de la tabla del núcleo borra el cable marcado",
            "como el borrado por atajo, con el undo que devuelve el grafo a su sitio");
        CanvasSelfCheck().Should().Contain(
            "pulsar reemplaza la selección",
            "y la REGLA de selección se mide en la app viva: sin Ctrl reemplazar es lo que el usuario pidió");
        CanvasSelfCheck().Should().Contain(
            "y con Ctrl se AÑADE a lo elegido",
            "y con Ctrl añadir es la otra mitad: sin su renglón, la regla sólo existiría en el fuente");
        CanvasSelfCheck().Should().Contain(
            "los cables marcados se borran de UNA vez y un solo deshacer los devuelve",
            "y el borrado en lote con UN undo es lo que hace útil el Ctrl sobre los cables");

        string mutation = File.ReadAllText(
            Path.Combine(TestRepositoryLocator.RepositoryRoot(), SelectionMutationPath));
        mutation.Should().Contain(
            $"\"FullyQualifiedName~{SelectionWitnessCase}\"",
            "esta guardia es el testigo de la mutación de la diana: quitarla la hace caer");

        string markMutation = File.ReadAllText(
            Path.Combine(TestRepositoryLocator.RepositoryRoot(), MarkMutationPath));
        markMutation.Should().Contain(
            $"\"FullyQualifiedName~{SelectionWitnessCase}\"",
            "y también es el testigo de la mutación del RESALTE: sin él la marca del núcleo dejaría de verse y el " +
            "caso caería nombrando el trazo que había que pintar");

        string placementMutation = File.ReadAllText(
            Path.Combine(TestRepositoryLocator.RepositoryRoot(), MenuPlacementMutationPath));
        placementMutation.Should().Contain(
            $"\"FullyQualifiedName~{SelectionWitnessCase}\"",
            "y de la del SITIO del menú: anclarlo a la diana lo devuelve a la esquina del cable, que es el defecto " +
            "que el usuario reportó");
    }

    [Fact]
    public void TheWireProbe_ShouldMeasureBothGestures_AndTheSelfCheckShouldRunIt()
    {
        string code = CanvasCode();

        code.Should().Contain(
            "internal (bool Before, bool After, bool Crowded, string Detail) ProbeWireTracking()",
            "sin sonda, el defecto sólo se ve con dedos de verdad: el usuario lo reportó así y la medida tiene " +
            "que quedar en la app viva, no en la memoria de nadie");

        code.Should().Contain(
            "CanvasTransform.TranslateX += 140;",
            "la sonda mueve el plano por los mismos mandos que el gesto del puntero");

        code.Should().Contain(
            "ZoomBy(1.25);",
            "y cambia el zoom por los mismos mandos de la rueda y de los botones: los dos gestos que el usuario " +
            "reportó se miden, no se suponen");

        code.Should().Contain(
            "private static bool TryFigureEnds(",
            "el extremo dibujado se lee del ÚLTIMO segmento de la figura, no del último punto de control: con " +
            "la figura de la versión anterior el cable no acaba en la Bézier");

        code.Should().Contain(
            "private static bool CrowdedShapeFitsTheHueco(out string detail)",
            "la segunda mitad del reporte del usuario («al mover un nodo la parte recta es demasiado grande y se " +
            "ve mal») se mide en la misma sonda: la FORMA del cable en el hueco estrecho, no sólo sus extremos");

        SelfCheck().Should().Contain(
            "la forma del cable cabe en el hueco estrecho (sin el rulo del 2)",
            "el renglón de la forma nombra lo que mide: el rulo con forma de «2» que aparece cuando el cuello no " +
            "cabe en el hueco que queda entre las dos anclas");

        SelfCheck().Should().Contain(
            "canvas.ProbeWireTracking()",
            "el selfcheck corre la sonda en la app viva, que es donde el plano tiene tamaño y las tarjetas están " +
            "materializadas");

        SelfCheck().Should().Contain(
            "el cable dibujado toca su socket con el plano sin mover",
            "el renglón del selfcheck nombra lo que mide: se lee en el informe sin abrir el código");

        SelfCheck().Should().Contain(
            "el cable sigue tocando su socket tras mover el plano y cambiar el zoom",
            "los dos gestos del reporte del usuario tienen su renglón propio");
    }
}
