using FileFlow.App.ViewModels;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Subflows;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Cuando la definición de un subflujo no se puede leer —una ruta a medio escribir, un archivo que ya no
/// está, un JSON que el analizador rechaza— el contenedor conserva los puertos que ya había expuesto.
///
/// No es una comodidad: el lienzo revalida las conexiones contra los puertos vigentes, así que caer a los
/// genéricos <c>In</c>/<c>Out</c> no degrada nada, <b>borra</b> los cables conectados a los puertos propios
/// del subflujo. La memoria es del propio contenedor —expone lo que recuerda—, así que la siembra
/// materializar una definición que sí resuelve, y viaja en el archivo del flujo (ver
/// <c>WorkflowSaveLoadFidelityTests</c>). La regla que la acompaña: cuando una definición resuelve, lo que
/// dice manda, aunque diga que no expone ningún puerto propio.
/// </summary>
public class UnresolvedSubflowDefinitionTests
{
    private static readonly DateTime Stamp = new(2026, 5, 6, 7, 8, 9, DateTimeKind.Utc);

    [Fact]
    public void WhenTheDefinitionStopsResolving_ShouldKeepThePortsItReadLast()
    {
        string path = SubflowFixtures.TempFile();
        SubflowFixtures.WriteFile(path, SubflowFixtures.DefinitionJson("In;Alternate", "Out;Errores"), Stamp);

        var node = new SubflowNode { SubflowPath = path };
        SubflowPortResolver.Materialize(node);
        node.Inputs.Select(port => port.Name).Should().Equal("In", "Alternate");

        node.SubflowPath = $"no-existe-{Guid.NewGuid():N}.flow";
        SubflowPortResolver.Materialize(node);

        node.Inputs.Select(port => port.Name).Should().Equal(
            new[] { "In", "Alternate" },
            "sin definición que leer, el contenedor no puede perder los puertos que ya tenía");
    }

    [Fact]
    public void WhenTheDefinitionBecomesUnreadable_ShouldKeepThePortsItReadLast()
    {
        string path = SubflowFixtures.TempFile();
        SubflowFixtures.WriteFile(path, SubflowFixtures.DefinitionJson("In;Alternate", "Out;Errores"), Stamp);

        var node = new SubflowNode { SubflowPath = path };
        SubflowPortResolver.Materialize(node);
        node.Inputs.Select(port => port.Name).Should().Equal("In", "Alternate");

        // El archivo se corrompe, y cambia de marca: es una lectura nueva y de verdad, no la memorizada.
        SubflowFixtures.WriteFile(path, "{" + new string('x', 200), Stamp.AddMinutes(1));
        SubflowPortResolver.Materialize(node);

        node.Inputs.Select(port => port.Name).Should().Equal(
            new[] { "In", "Alternate" },
            "una definición ilegible tampoco es motivo para soltar los puertos del contenedor");
    }

    [Fact]
    public void AContainerWithoutAnyReadableDefinition_ShouldExposeTheGenericPorts()
    {
        var node = new SubflowNode { SubflowPath = $"no-existe-{Guid.NewGuid():N}.flow" };

        SubflowPortResolver.Materialize(node);

        node.Inputs.Select(port => port.Name).Should().Equal("In");
        node.Outputs.Select(port => port.Name).Should().Equal("Out");
    }

    [Fact]
    public void WhenTheDefinitionResolvesAgain_ShouldBelieveTheDefinition_EvenIfItDeclaresNoPorts()
    {
        string path = SubflowFixtures.TempFile();
        SubflowFixtures.WriteFile(path, SubflowFixtures.DefinitionJson("In;Alternate", "Out;Errores"), Stamp);

        var node = new SubflowNode { SubflowPath = path };
        SubflowPortResolver.Materialize(node);
        node.Inputs.Select(port => port.Name).Should().Equal("In", "Alternate");

        // La definición vuelve a resolverse y no declara frontera: los genéricos son la verdad de lo que
        // expone el subflujo, no una degradación, y no pueden quedar tapados por la memoria.
        SubflowFixtures.WriteFile(path, new WorkflowGraph().ToJson(), Stamp.AddMinutes(1));
        SubflowPortResolver.Materialize(node);

        node.Inputs.Select(port => port.Name).Should().Equal("In");
        node.Outputs.Select(port => port.Name).Should().Equal("Out");
    }

    [Fact]
    public void WhenTheDefinitionStopsResolving_TheContainerOnTheCanvas_ShouldKeepItsCables()
    {
        string path = SubflowFixtures.TempFile();
        SubflowFixtures.WriteFile(path, SubflowFixtures.DefinitionJson("In;Alternate", "Out;Errores"), Stamp);

        var editor = new EditorViewModel(new PluginLoader());
        var sourceVm = EditorFixtures.AddNode(editor, new FolderSourceNode());
        var containerVm = EditorFixtures.AddNode(editor, new SubflowNode { SubflowPath = path });

        containerVm.SyncSubflowPorts();
        editor.CreateConnection(
            sourceVm.OutputPorts.Single(),
            containerVm.InputPorts.Single(port => port.Name == "Alternate"));
        editor.Connections.Should().ContainSingle();

        // El usuario reescribe la ruta y la deja a medio escribir: mientras tanto no hay nada que leer.
        containerVm.OnParameterValueChanged("SubflowPath", $"ruta/a/medio-{Guid.NewGuid():N}.flow");

        containerVm.InputPorts.Select(port => port.Name).Should().Equal("In", "Alternate");
        editor.Connections.Should().ContainSingle("el cable apunta a un puerto que sigue existiendo");
        containerVm.InputPorts.Single(port => port.Name == "Alternate").IsConnected.Should().BeTrue();
    }
}
