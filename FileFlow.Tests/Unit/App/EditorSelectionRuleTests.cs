using FileFlow.App.ViewModels;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using Point = FileFlow.Sdk.Point;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// La REGLA DE SELECCIÓN del lienzo, medida en el núcleo portable (donde vive y donde se puede ejercitar sin
/// el host): <b>pulsar REEMPLAZA</b> —lo que estuviera elegido se suelta, nodos y cables— y <b>Ctrl AÑADE</b> a
/// lo que ya estaba. La selección del lienzo es UNA, así que elegir un nodo suelta los cables y elegir un cable
/// suelta los nodos.
///
/// <para><b>Por qué estas pruebas existen</b>: el defecto que las pide es de gesto —el clic en una tarjeta
/// AÑADÍA a la selección en vez de reemplazarla, y el rectángulo nunca deseleccionaba—, y el host Uno (WinUI) no
/// se materializa en el suite. La decisión, sin embargo, es del núcleo, y ahí sí se mide: lo que el host hace es
/// leer el modificador y pedir esto.</para>
/// </summary>
public class EditorSelectionRuleTests
{
    private static (EditorViewModel Editor, NodeViewModel A, NodeViewModel B, NodeViewModel C) ChainOfThree()
    {
        var editor = new EditorViewModel(new PluginLoader());

        var a = new NodeViewModel(new FolderSourceNode(), new Point(0, 0)) { ParentEditor = editor };
        var b = new NodeViewModel(new DestinationSinkNode(), new Point(200, 0)) { ParentEditor = editor };
        var c = new NodeViewModel(new DestinationSinkNode(), new Point(400, 0)) { ParentEditor = editor };

        editor.Nodes.Add(a);
        editor.Nodes.Add(b);
        editor.Nodes.Add(c);

        editor.CreateConnection(a.OutputPorts.First(), b.InputPorts.First());
        editor.CreateConnection(b.OutputPorts.First(), c.InputPorts.First());

        return (editor, a, b, c);
    }

    [Fact]
    public void PulsarUnNodo_ShouldReplaceTheSelection_AndControlShouldAddToIt()
    {
        var (editor, a, b, _) = ChainOfThree();

        editor.SelectNode(a);
        editor.SelectNode(b);

        editor.Nodes.Count(n => n.IsSelected).Should().Be(
            1, "pulsar REEMPLAZA: el defecto era que el clic dejaba los dos elegidos y el Supr borraba de más");
        b.IsSelected.Should().BeTrue("lo elegido es lo último que se pulsó");

        editor.SelectNode(a, add: true);

        editor.Nodes.Count(n => n.IsSelected).Should().Be(
            2, "y con Ctrl se AÑADE: los dos quedan elegidos, que es lo que el usuario pide con el modificador");
        a.IsSelected.Should().BeTrue();
        b.IsSelected.Should().BeTrue();
    }

    [Fact]
    public void ElegirUnCable_ShouldReleaseTheNodes_AndControlShouldAddToTheMark()
    {
        var (editor, a, _, _) = ChainOfThree();
        var first = editor.Connections[0];
        var second = editor.Connections[1];

        editor.SelectNode(a);
        editor.SelectConnection(first);

        editor.Nodes.Should().OnlyContain(n => !n.IsSelected,
            "la selección del lienzo es UNA: elegir un cable suelta los nodos");
        editor.SelectedConnections.Should().ContainSingle("sin Ctrl, la marca reemplaza a la que hubiera");

        editor.SelectConnection(second, add: true);

        editor.SelectedConnections.Should().HaveCount(
            2, "con Ctrl la marca se SUMA: es lo que hace que Supr pueda llevarse varios cables de una vez");

        editor.SelectConnection(first);

        editor.SelectedConnections.Should().ContainSingle(
            "y sin Ctrl vuelve a reemplazar, aunque el cable ya estuviera marcado");
    }

    [Fact]
    public void SuprSobreLosCablesMarcados_ShouldDeleteThemAll_AndOneUndoShouldBringThemBack()
    {
        var (editor, _, _, _) = ChainOfThree();

        editor.SelectConnection(editor.Connections[0]);
        editor.SelectConnection(editor.Connections[1], add: true);
        editor.SelectedConnections.Should().HaveCount(2);

        editor.DeleteSelectedConnectionsCommand.Execute(null);

        editor.Connections.Should().BeEmpty("Supr borra LO ELEGIDO, y lo elegido son los dos cables marcados");
        editor.SelectedConnections.Should().BeEmpty(
            "el cable que sale del grafo no puede dejar su marca puesta: la invariante vive donde cambia el grafo");

        editor.UndoRedoService.Undo();

        editor.Connections.Should().HaveCount(
            2, "los borrados van en UNA transacción, así que un solo deshacer devuelve el conjunto entero");
    }

    [Fact]
    public void SuprConCablesMarcados_ShouldNotTouchTheUnmarkedOnes()
    {
        var (editor, _, _, _) = ChainOfThree();

        editor.SelectConnection(editor.Connections[1]);
        editor.DeleteSelectedConnectionsCommand.Execute(null);

        editor.Connections.Should().HaveCount(1, "sólo cae el cable marcado");
        editor.Connections.Should().Contain(editor.Connections[0]);
    }

    [Fact]
    public void SuprConSeleccionMixta_ShouldTakeNodesAndWires_WithOneUndo()
    {
        var (editor, a, _, _) = ChainOfThree();
        var attached = editor.Connections[0];

        // Lo que deja un rectángulo: un nodo elegido y un cable marcado A LA VEZ (a mano también se consigue:
        // elegir el nodo y añadir el cable a la marca con Ctrl).
        editor.SelectNode(a);
        editor.SelectConnection(attached, add: true);
        int nodes = editor.Nodes.Count;
        int wires = editor.Connections.Count;

        editor.DeleteSelectionCommand.Execute(null);

        editor.Nodes.Should().HaveCount(nodes - 1, "Supr se lleva el nodo elegido");
        editor.Connections.Should().HaveCount(wires - 1, "y el cable marcado, que caía por ese nodo");
        editor.SelectedConnections.Should().BeEmpty("el cable que sale del grafo no deja su marca puesta");

        editor.UndoRedoService.Undo();

        editor.Nodes.Should().HaveCount(nodes, "y UN solo deshacer devuelve las dos cosas");
        editor.Connections.Should().HaveCount(wires);
    }

    [Fact]
    public void SuprConSeleccionMixta_ShouldTakeTheWireThatDoesNotHangFromTheNode()
    {
        var (editor, a, _, _) = ChainOfThree();
        var far = editor.Connections[1];

        // El caso que los dos caminos excluyentes no cubrían: el nodo elegido NO toca el cable marcado.
        editor.SelectNode(a);
        editor.SelectConnection(far, add: true);
        int nodes = editor.Nodes.Count;

        editor.DeleteSelectionCommand.Execute(null);

        editor.Nodes.Should().HaveCount(nodes - 1);
        editor.Connections.Should().BeEmpty(
            "el cable marcado cae aunque no cuelgue del nodo (y el del nodo se va con el nodo)");

        editor.UndoRedoService.Undo();

        editor.Nodes.Should().HaveCount(nodes);
        editor.Connections.Should().HaveCount(2, "un solo deshacer devuelve el nodo y los dos cables");
    }

    [Fact]
    public void ElNodoQueVuelveDelUndo_ShouldBeGovernedLikeTheRest()
    {
        var (editor, _, b, _) = ChainOfThree();
        editor.SelectNode(b);
        editor.DeleteSelectedNodesCommand.Execute(null);
        editor.UndoRedoService.Undo();
        editor.ClearSelection();

        // Se escribe la marca directamente, como hacen el lienzo y sus sondas: el aviso de la marca tiene que
        // estar suscrito igual en el nodo que vuelve del deshacer que en el que nunca se fue.
        b.IsSelected = true;

        editor.SelectedNode.Should().BeSameAs(
            b, "el nodo restaurado por el undo tiene que seguir gobernado por el núcleo");
    }

    [Fact]
    public void Rectangulo_ShouldSelectTheNodesAndMarkTheWiresInside()
    {
        var (editor, a, b, c) = ChainOfThree();
        var enclosed = editor.Connections[0];

        // Un rectángulo que encierra A y B y el cable que los une: las DOS cosas quedan elegidas a la vez
        // (el clic marca una; el rectángulo elige un área, y lo que cae dentro puede ser un nodo y un cable).
        editor.ApplyRubberSelection([a, b], [enclosed], add: false, [], []);

        a.IsSelected.Should().BeTrue();
        b.IsSelected.Should().BeTrue();
        c.IsSelected.Should().BeFalse("lo que queda fuera del rectángulo no está elegido");
        editor.SelectedConnections.Should().ContainSingle(
                "el cable con sus DOS anclas dentro entra en la marca, y el de fuera no")
            .Which.Should().BeSameAs(enclosed);
        editor.SelectedNode.Should().BeSameAs(b, "el nodo de referencia es el último elegido del grafo");
    }

    [Fact]
    public void RectanguloConCtrl_ShouldAddToTheSelection_WithoutReleasingTheRest()
    {
        var (editor, a, _, c) = ChainOfThree();
        var marked = editor.Connections[1];

        // Lo que el usuario tenía antes de empezar el rectángulo: A elegido (y el cable 2 marcado con Ctrl,
        // que es la única forma de tener las dos cosas: sin Ctrl elegir un cable suelta los nodos).
        editor.SelectNode(a);
        editor.SelectConnection(marked, add: true);

        // Ctrl+rectángulo sobre un área que sólo encierra a C: se AÑADE y lo de fuera no se suelta.
        editor.ApplyRubberSelection([c], [], add: true, [a], editor.SelectedConnections.ToList());

        a.IsSelected.Should().BeTrue("con Ctrl lo de fuera del rectángulo se queda");
        c.IsSelected.Should().BeTrue();
        editor.SelectedConnections.Should().ContainSingle("y los cables marcados de antes tampoco se sueltan")
            .Which.Should().BeSameAs(marked);

        // El mismo rectángulo SIN Ctrl reemplaza: manda el área, no lo de antes.
        editor.ApplyRubberSelection([c], [], add: false, [], []);

        a.IsSelected.Should().BeFalse("sin Ctrl el rectángulo reemplaza la selección entera");
        editor.SelectedConnections.Should().BeEmpty("y suelta también los cables marcados");
    }
}
