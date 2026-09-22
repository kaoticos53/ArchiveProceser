using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Logic;
using FileFlow.Plugin.Subflows;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// El guardado y el reabrir de un flujo. Lo que se guarda no son sólo los parámetros que muestra el
/// inspector: el nodo lleva estado de <b>diseño</b> que no es configuración del usuario —la definición
/// incrustada de un subflujo, los casos de un switch, los puertos que expone un contenedor— y si ese
/// estado no viaja en el archivo, el flujo reabierto pierde lo que el usuario configuró. Las conexiones
/// son lo más visible: apuntan a puertos por nombre, así que un puerto que no vuelve es un cable que
/// desaparece sin dejar rastro.
///
/// Se guarda y se lee con el servicio real, en disco, y no con una serialización de conveniencia: así la
/// prueba pasa por donde pasa el producto, incluido el convertidor de tipos inferidos que devuelve los
/// parámetros ya convertidos en vez de como elementos JSON.
/// </summary>
public class WorkflowSaveLoadFidelityTests : IDisposable
{
    private static readonly DateTime Stamp = new(2026, 6, 7, 8, 9, 10, DateTimeKind.Utc);

    private readonly string _tempDirectory;
    private readonly WorkflowStorageService _storage = new();

    public WorkflowSaveLoadFidelityTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "FileFlow_SaveLoadTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
            catch
            {
                // Silencioso: un archivo temporal que no se puede borrar no debe ocultar el resultado.
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Subflujos
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SavingAContainerWithAnEmbeddedDefinition_ShouldPersistItAndThePortsItExposes()
    {
        var editor = new EditorViewModel(CreateLoader());
        var containerVm = EditorFixtures.AddNode(editor, EmbeddedContainer());
        containerVm.SyncSubflowPorts();
        containerVm.InputPorts.Select(port => port.Name).Should().Equal("In", "Alternate");

        var reopenedGraph = await SaveAndReload(editor);

        var saved = reopenedGraph.Nodes.Single();
        saved.Parameters.Should().ContainKey("SubflowDefinitionJson",
            "la definición incrustada es lo que permite que el subflujo siga existiendo al reabrir");
        saved.Parameters["SubflowDefinitionJson"]?.ToString().Should().Be(SubflowDefinitionJson());
        saved.Parameters["RememberedInputPorts"]?.ToString().Should().Be("In;Alternate");
        saved.Parameters["RememberedOutputPorts"]?.ToString().Should().Be("Out;Errores");

        var reopened = new EditorViewModel(CreateLoader());
        reopened.LoadFromGraphModel(reopenedGraph);

        var reopenedContainer = reopened.Nodes.Single(node => node.IsSubflowNode);
        reopenedContainer.InputPorts.Select(port => port.Name).Should().Equal("In", "Alternate");
        reopenedContainer.OutputPorts.Select(port => port.Name).Should().Equal("Out", "Errores");
    }

    [Fact]
    public async Task AWorkflowSavedWithASubflowThatDoesNotTravelWithIt_ShouldKeepItsPortsAndItsCables()
    {
        string subflowPath = SubflowFixtures.TempFile();
        SubflowFixtures.WriteFile(subflowPath, SubflowFixtures.DefinitionJson("In;Alternate", "Out;Errores"), Stamp);

        var editor = new EditorViewModel(CreateLoader());
        var sourceVm = EditorFixtures.AddNode(editor, new FolderSourceNode());
        var containerVm = EditorFixtures.AddNode(editor, new SubflowNode { SubflowPath = subflowPath }, x: 300);
        containerVm.SyncSubflowPorts();
        editor.CreateConnection(
            sourceVm.OutputPorts.Single(),
            containerVm.InputPorts.Single(port => port.Name == "Alternate"));

        var reopenedGraph = await SaveAndReload(editor);

        // El subflujo se queda atrás: el flujo se comparte sin él, o se mueve de sitio.
        File.Delete(subflowPath);

        var reopened = new EditorViewModel(CreateLoader());
        reopened.LoadFromGraphModel(reopenedGraph);

        var reopenedContainer = reopened.Nodes.Single(node => node.IsSubflowNode);
        reopenedContainer.InputPorts.Select(port => port.Name).Should().Equal(
            new[] { "In", "Alternate" },
            "el contenedor recuerda los puertos que exponía aunque su definición ya no esté");
        reopened.Connections.Should().ContainSingle("el cable apunta a un puerto que el contenedor recordaba");
        reopened.Connections[0].Target.Name.Should().Be("Alternate");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Casos de switch: el otro nodo con estado de diseño fuera del inspector
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SavingASwitchNode_ShouldPersistItsCasesAndBringThemBackOnReload()
    {
        var editor = new EditorViewModel(CreateLoader());
        var switchVm = EditorFixtures.AddNode(editor, new SwitchCaseNode());
        switchVm.AddSwitchCase();
        string[] configuredPorts = [.. switchVm.OutputPorts.Select(port => port.Name)];
        configuredPorts.Should().Equal("Case 1", "Case 2", "Default");

        var reopenedGraph = await SaveAndReload(editor);

        reopenedGraph.Nodes.Single().Parameters.Should().ContainKey("CasesJson",
            "los casos configurados no están en el inspector y tienen que viajar en el archivo");

        var reopened = new EditorViewModel(CreateLoader());
        reopened.LoadFromGraphModel(reopenedGraph);

        reopened.Nodes.Single().OutputPorts.Select(port => port.Name)
            .Should().Equal(configuredPorts, "los casos configurados vuelven con el nodo");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Control: lo que el inspector sí muestra se sigue guardando igual
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SavingANode_ShouldKeepTheParametersTypedInTheInspector()
    {
        var editor = new EditorViewModel(CreateLoader());
        var nodeVm = EditorFixtures.AddNode(editor, new FolderSourceNode());
        nodeVm.OnParameterValueChanged("ExtensionFilter", "*.jpg");

        var reopenedGraph = await SaveAndReload(editor);

        reopenedGraph.Nodes.Single().Parameters["ExtensionFilter"]?.ToString().Should().Be("*.jpg");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Utilidades
    // ─────────────────────────────────────────────────────────────────────────────

    private string WorkflowFilePath => Path.Combine(_tempDirectory, "flujo.flow");

    /// <summary>Guarda el lienzo en disco y devuelve el grafo tal y como vuelve del archivo.</summary>
    private async Task<WorkflowGraph> SaveAndReload(EditorViewModel editor)
    {
        await _storage.SaveWorkflowAsync(WorkflowFilePath, editor.ExportToGraphModel());
        return await _storage.LoadWorkflowAsync(WorkflowFilePath);
    }

    private static PluginLoader CreateLoader()
    {
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(SwitchCaseNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(SubflowNode).Assembly);
        return loader;
    }

    private static string SubflowDefinitionJson() =>
        SubflowFixtures.DefinitionJson("In;Alternate", "Out;Errores");

    /// <summary>Contenedor con la definición incrustada: el subflujo viaja dentro del flujo.</summary>
    private static SubflowNode EmbeddedContainer() => new()
    {
        EmbedDefinition = true,
        SubflowDefinitionJson = SubflowDefinitionJson(),
        SubflowName = "Contenedor"
    };
}
