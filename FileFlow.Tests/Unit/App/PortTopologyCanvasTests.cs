using System.Linq;
using Avalonia;
using FileFlow.App.ViewModels;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.Logic;
using FileFlow.Plugin.Scripting;
using FileFlow.Plugin.Subflows;
using FileFlow.Sdk;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Reacción del lienzo a los cambios de puertos de un nodo. Los puertos de la tarjeta son una proyección
/// de los del nodo, y las conexiones apuntan a esa proyección: si el nodo cambia de puertos y el lienzo no
/// se entera, quedan cables colgando de puertos que ya no existen —aristas que el motor no vuelve a trazar
/// y que el usuario sólo ve al reabrir el flujo—.
///
/// Aquí se comprueba por dónde entra el cambio (el inspector, un caso de switch, un subflujo) y que la
/// reconstrucción conserva lo que sigue siendo válido: el puerto que sobrevive mantiene su instancia, y su
/// cable con ella.
/// </summary>
public class PortTopologyCanvasTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // La tarjeta reconstruye sus puertos cuando el nodo los cambia
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EditingAPortParameterInTheInspector_ShouldRebuildThePortsOfTheCard()
    {
        var editor = new EditorViewModel(new PluginLoader());
        var script = new CustomScriptNode();
        var scriptVm = AddNode(editor, script);

        scriptVm.OutputPorts.Select(port => port.Name).Should().Equal("Out");

        // El inspector escribe en el diccionario de parámetros, no en la propiedad del nodo.
        scriptVm.OnParameterValueChanged("OutputPorts", "Salida,Errores");

        scriptVm.OutputPorts.Select(port => port.Name).Should().Equal("Salida", "Errores");
        scriptVm.OutputPorts.Should().OnlyContain(port => port.Direction == PortDirection.Output);
    }

    [Fact]
    public void EditingAPortParameterOfASubflowBoundary_ShouldRebuildThePorts_WithoutAnyManualSync()
    {
        var editor = new EditorViewModel(new PluginLoader());
        var boundary = new SubflowInputNode();
        var boundaryVm = AddNode(editor, boundary);

        boundaryVm.OutputPorts.Select(port => port.Name).Should().Equal("In");

        boundaryVm.OnParameterValueChanged("PortNames", "In;Alternate");

        boundaryVm.OutputPorts.Select(port => port.Name).Should().Equal("In", "Alternate");
    }

    [Fact]
    public void ChangingAParameterThatDoesNotMovePorts_ShouldLeaveTheCardAlone()
    {
        var editor = new EditorViewModel(new PluginLoader());
        var script = new CustomScriptNode();
        var scriptVm = AddNode(editor, script);
        var portsBefore = scriptVm.OutputPorts.ToList();

        scriptVm.OnParameterValueChanged("TimeoutSeconds", 45);
        scriptVm.OnParameterValueChanged("OutputPorts", "Out");

        scriptVm.OutputPorts.Should().Equal(portsBefore, "los puertos no se recrean si la topología no cambia");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // El lienzo revalida los cables
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void WhenAPortSurvivesTheChange_ShouldKeepItsInstanceAndItsCable()
    {
        var editor = new EditorViewModel(new PluginLoader());
        var scriptVm = AddNode(editor, new CustomScriptNode());
        var sinkVm = AddNode(editor, new ThrottleDelayNode(), x: 300);

        var keptPort = scriptVm.OutputPorts.Single();
        editor.CreateConnection(keptPort, sinkVm.InputPorts.Single());
        editor.Connections.Should().HaveCount(1);

        scriptVm.OnParameterValueChanged("OutputPorts", "Out,Errores");

        scriptVm.OutputPorts.Select(port => port.Name).Should().Equal("Out", "Errores");
        scriptVm.OutputPorts[0].Should().BeSameAs(keptPort, "el puerto superviviente conserva su instancia");
        editor.Connections.Should().ContainSingle("el cable apunta a un puerto que sigue existiendo");
        sinkVm.InputPorts.Single().IsConnected.Should().BeTrue();
    }

    [Fact]
    public void WhenAPortDisappears_ShouldDropTheCableThatPointedToIt()
    {
        var editor = new EditorViewModel(new PluginLoader());
        var scriptVm = AddNode(editor, new CustomScriptNode());
        var sinkVm = AddNode(editor, new ThrottleDelayNode(), x: 300);

        editor.CreateConnection(scriptVm.OutputPorts.Single(), sinkVm.InputPorts.Single());
        editor.Connections.Should().HaveCount(1);

        // Renombrar el puerto equivale a quitarlo y crear otro: no hay forma de saber que es el mismo, y el
        // cable no puede colgar de un puerto inexistente.
        scriptVm.OnParameterValueChanged("OutputPorts", "Otro");

        editor.Connections.Should().BeEmpty();
        sinkVm.InputPorts.Single().IsConnected.Should().BeFalse();
    }

    [Fact]
    public void WhenASwitchCaseIsRemoved_ShouldDropOnlyThatCable()
    {
        var editor = new EditorViewModel(new PluginLoader());
        var switchVm = AddNode(editor, new SwitchCaseNode());
        var sinkVm = AddNode(editor, new ThrottleDelayNode(), x: 300);

        switchVm.AddSwitchCase();
        switchVm.OutputPorts.Select(port => port.Name).Should().Equal("Case 1", "Case 2", "Default");

        editor.CreateConnection(switchVm.OutputPorts[1], sinkVm.InputPorts.Single());
        editor.Connections.Should().HaveCount(1);

        var caseItem = switchVm.SwitchCases.Single(item => item.Name == "Case 2");
        switchVm.RemoveSwitchCase(caseItem);

        switchVm.OutputPorts.Select(port => port.Name).Should().Equal("Case 1", "Default");
        editor.Connections.Should().BeEmpty("el puerto del caso eliminado ya no existe");
    }

    [Fact]
    public void RenamingASwitchCase_ShouldKeepTheCable_BecauseThePortSurvivesItsRename()
    {
        var editor = new EditorViewModel(new PluginLoader());
        var switchVm = AddNode(editor, new SwitchCaseNode());
        var sinkVm = AddNode(editor, new ThrottleDelayNode(), x: 300);

        var casePort = switchVm.OutputPorts[0];
        editor.CreateConnection(casePort, sinkVm.InputPorts.Single());

        // El coordinador renombra el puerto en su sitio: la instancia no cambia y el cable sigue siendo suyo.
        switchVm.SwitchCases[0].Name = "Fotos";

        editor.Connections.Should().ContainSingle();
        editor.Connections[0].Source.Should().BeSameAs(casePort);
        casePort.Name.Should().Be("Fotos");
    }

    [Fact]
    public void RevalidatingOneNode_ShouldNotTouchTheCablesOfTheOthers()
    {
        var editor = new EditorViewModel(new PluginLoader());
        var scriptVm = AddNode(editor, new CustomScriptNode());
        var sinkVm = AddNode(editor, new ThrottleDelayNode(), x: 300);
        var untouchedSourceVm = AddNode(editor, new CustomScriptNode(), y: 200);
        var untouchedSinkVm = AddNode(editor, new ThrottleDelayNode(), x: 300, y: 200);

        editor.CreateConnection(scriptVm.OutputPorts.Single(), sinkVm.InputPorts.Single());
        editor.CreateConnection(untouchedSourceVm.OutputPorts.Single(), untouchedSinkVm.InputPorts.Single());
        editor.Connections.Should().HaveCount(2);

        scriptVm.OnParameterValueChanged("OutputPorts", "Otro");

        editor.Connections.Should().ContainSingle()
            .Which.Source.NodeOwner.Should().BeSameAs(untouchedSourceVm);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Ciclo de vida de la suscripción
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DisposingTheCard_ShouldStopFollowingTheNode()
    {
        var script = new CustomScriptNode();
        var scriptVm = AddNode(new EditorViewModel(new PluginLoader()), script);

        scriptVm.Dispose();

        script.Parameters["OutputPorts"] = "Otro";
        script.RefreshPortTopology();

        // Si la suscripción siguiera viva, este cambio habría reconstruido los puertos de la tarjeta: que
        // no lo haga es la prueba de que 'Cleanup' la suelta.
        scriptVm.OutputPorts.Select(port => port.Name).Should().Equal("Out");
    }

    private static NodeViewModel AddNode(EditorViewModel editor, IFlowNode node, int x = 0, int y = 0)
    {
        var nodeVm = new NodeViewModel(node, new Point(x, y)) { ParentEditor = editor };
        editor.Nodes.Add(nodeVm);
        return nodeVm;
    }
}
