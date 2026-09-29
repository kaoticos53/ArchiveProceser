using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Guardia del redibujado de cables del lienzo Uno: el lienzo tiene que <b>escuchar</b> la colección de
/// conexiones, no sólo la de nodos.
///
/// <para><b>Las dos mitades del control</b>: el dibujo de la capa de cables vive en
/// <c>EditorCanvasControl.Wires.cs</c> (hito 282) y el resto —pan/zoom, tarjetas y suscripciones— en
/// <c>EditorCanvasControl.xaml.cs</c>. Los censos de <c>DrawWires</c> leen la mitad de los cables; los de
/// las suscripciones, el fichero del control.</para>
///
/// <para><b>Por qué guarda la fuente y no el runtime</b>: el lienzo es WinUI (host Uno) y no se puede
/// materializar dentro de la sesión de pruebas; el defecto del hito 225 fue de suscripción —el lienzo
/// reconstruía cables sólo con <c>Nodes.CollectionChanged</c>, y como el importador añade todos los
/// nodos antes que las aristas, el último Rebuild corría con la colección de conexiones aún vacía y un
/// flujo cargado de disco quedaba sin cables—. El síntoma no es un fallo: es una app que corre sin
/// cables. La cura es una línea de suscripción en el setter de <c>Editor</c>, y esta guardia la exige
/// en el código: que el <c>Editor</c> vigente se desuscriba y se suscriba a AMBAS colecciones con
/// handlers con nombre. Un lint de texto simple se conformaría con la línea comentada (la lección del
/// 165): por eso el análisis retira los comentarios y exige las cuatro llamadas como código vivo.</para>
/// </summary>
public class UnoCanvasConnectionsGuardTests
{
    private const string CanvasPath = "FileFlow.App.Uno/Controls/EditorCanvasControl.xaml.cs";

    /// <summary>La otra mitad del mismo control: el dibujo de la capa de cables (hito 282).</summary>
    private const string WiresPath = "FileFlow.App.Uno/Controls/EditorCanvasControl.Wires.cs";

    private static string CanvasCode() => SourceText.CodeWithoutComments(CanvasPath);
    private static string WiresCode() => SourceText.CodeWithoutComments(WiresPath);

    [Fact]
    public void Canvas_ShouldSubscribeToConnectionsCollectionChanged()
    {
        CanvasCode().Should().Contain(
            "((INotifyCollectionChanged)_editor.Connections).CollectionChanged += OnConnectionsChanged;",
            "el importador añade los nodos antes que las aristas: sin escuchar Connections, un flujo " +
            "cargado de disco dibuja tarjetas pero cero cables (defecto del hito 225)");
    }

    [Fact]
    public void Canvas_ShouldUnsubscribeOldEditorFromConnections()
    {
        CanvasCode().Should().Contain(
            "((INotifyCollectionChanged)_editor.Connections).CollectionChanged -= OnConnectionsChanged;",
            "sin la desuscripción del editor saliente, cada reasignación del Editor acumula un lienzo " +
            "fantasma que redibuja cables sobre un árbol ya desmontado");
    }

    [Fact]
    public void Canvas_ShouldKeepTheNodesSubscriptionAlongsideConnections()
    {
        string code = CanvasCode();

        code.Should().Contain(
            "((INotifyCollectionChanged)_editor.Nodes).CollectionChanged += OnNodesChanged;",
            "la suscripción de Connections es COMPLEMENTARIA, no sustituta: el Rebuild de tarjetas sigue " +
            "necesitando el evento de Nodes (mutante que borra una de las dos suscripciones = impreciso)");

        code.Should().Contain(
            "((INotifyCollectionChanged)_editor.Nodes).CollectionChanged -= OnNodesChanged;",
            "la desuscripción de Nodes es parte del mismo contrato de vida: sin ella, reasignar el " +
            "Editor filtra la suscripción del anterior");
    }

    [Fact]
    public void Canvas_ShouldWireConnectionsEventToRedrawing()
    {
        CanvasCode().Should().Contain(
            "private void OnConnectionsChanged(object? sender, NotifyCollectionChangedEventArgs e) => DrawWires();",
            "el evento tiene que terminar en el redibujado de cables: una suscripción que no conecta con " +
            "DrawWires deja la cura en un no-op");
    }

    [Fact]
    public void Guard_ShouldReadCodeNotComments()
    {
        // La lección del 165: un lint que busca líneas en el archivo se conforma con la línea comentada
        // («// desactivado temporalmente»). La herramienta de la guardia retira comentarios antes de
        // buscar; esta auto-prueba fija ese comportamiento con el fragmento exacto que se vigila.
        string commented = SourceText.WithoutComments(
            "// ((INotifyCollectionChanged)_editor.Connections).CollectionChanged += OnConnectionsChanged;");

        commented.Should().NotContain(
            "CollectionChanged += OnConnectionsChanged",
            "la línea comentada no es código vivo: si el lint la contara, la guardia vigilaría el recuerdo de la suscripción");
    }

    [Fact]
    public void DrawWires_ShouldConsumeTheWrittenBackAnchors()
    {
        string code = WiresCode();

        // El write-back de la 3.3 manda: el cable nace del SOCKET REAL (ancla calculada del árbol) en
        // los dos extremos — el mutante `cable-con-anclas-estimadas` sustituye una de las dos llamadas
        // por null y una de las dos líneas cae.
        code.Should().Contain(
            "var source = AnchorOf(connection.Source)",
            "el cabo origen del cable nace del ancla real del socket (write-back del árbol), no de la " +
            "estimación del centro de tarjeta: es lo que cerró la brecha que el 226 midió (y171 vs y217)");

        code.Should().Contain(
            "var target = AnchorOf(connection.Target)",
            "el cabo destino del cable nace del ancla real del socket: el mutante que rompe UNA de las " +
            "dos mitades debe caer (la estimación Y+40 es sólo el respaldo si el árbol no materializó)");
    }

    [Fact]
    public void DrawWires_ShouldKeepTheEstimationAsFallbackOnly()
    {
        string code = WiresCode();

        // El caso hermano: el respaldo con la estimación Y+40 tiene que seguir ahí (para el árbol sin
        // materializar) PERO como coalescencia (??), no como camino único. El mutante que deja la
        // llamada a AnchorOf intacta (cambia la estimación, no el ancla) NO toca estas líneas: control.
        code.Should().Contain(
            "?? new Sdk.Point(",
            "la estimación Y+40 es respaldo por coalescencia: sin el ??, el respaldo sería camino único " +
            "y la brecha del 226 volvería en cuanto el write-back se ignorara");

        code.Should().Contain(
            "connection.Source.NodeOwner.Location.Y + 40",
            "el respaldo del cabo origen usa la estimación del centro de tarjeta (Y+40), que es lo " +
            "único disponible si el árbol aún no materializó el socket");

        code.Should().Contain(
            "connection.Target.NodeOwner.Location.Y + 40",
            "el respaldo del cabo destino usa la misma estimación: ambas mitades tienen su respaldo");
    }
}
