using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Sdk;
using FileFlow.Sdk.Services;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// El formato se versiona desde 2E-P8, y esa versión se usaba sólo para <b>reparar</b>: nada impedía abrir un
/// flujo escrito por una versión posterior de la aplicación y guardar encima, perdiendo los campos que esa
/// versión añadió. Es la única pérdida de datos de la lista que el usuario no puede deshacer, y la protección
/// mira el <b>destino</b> —el archivo concreto al que se va a escribir— porque guardar siempre pregunta la ruta.
///
/// Estas pruebas fijan las dos mitades: que un archivo posterior <b>no</b> se sobrescriba, y que un archivo de
/// este formato o de uno anterior se siga guardando como siempre. Sin la segunda, la protección sería un
/// estorbo; sin la primera, no sería nada.
/// </summary>
public class NewerFormatProtectionTests
{
    private const string FutureSchema = "FileFlow.Workflow.v3";

    private readonly WorkflowStorageService _storage = new();

    // ─────────────────────────────────────────────────────────────────────────────
    // Lo que se protege
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SavingOverAWorkflowFromANewerFormat_ShouldFailAndLeaveTheFileUntouched()
    {
        string filePath = TempFile(FutureSchema);
        string original = File.ReadAllText(filePath);

        var saving = async () => await _storage.SaveWorkflowAsync(filePath, SampleGraph());

        await saving.Should().ThrowAsync<InvalidDataException>()
            .WithMessage($"*{FutureSchema}*");
        File.ReadAllText(filePath).Should().Be(original, "lo que no se puede perder es lo que el archivo ya tenía");
    }

    [Fact]
    public async Task SavingOverANewerFormatWorkflow_ShouldFailEvenIfThisVersionCannotReadItsBody()
    {
        // La versión se lee del JSON crudo justo por esto: un archivo posterior es el que puede traer formas que
        // este modelo no sabe enlazar, y hay que poder reconocerlo igual.
        string filePath = Path.Combine(NewTempDirectory(), "futuro.json");
        string original = $$"""
            {
              "schema": "{{FutureSchema}}",
              "nodes": { "esto": "no es una lista de nodos" }
            }
            """;
        await File.WriteAllTextAsync(filePath, original);

        var saving = async () => await _storage.SaveWorkflowAsync(filePath, SampleGraph());

        await saving.Should().ThrowAsync<InvalidDataException>()
            .WithMessage($"*{FutureSchema}*");
        File.ReadAllText(filePath).Should().Be(original);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Lo que no se protege, porque protegerlo sería un estorbo
    // ─────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(WorkflowFormat.CurrentSchema)]
    [InlineData("FileFlow.Workflow.v1")]
    [InlineData(null)]
    public async Task SavingOverAWorkflowOfThisFormatOrAnOlderOne_ShouldWork(string? declaredSchema)
    {
        // El caso `null` es un archivo anterior al versionado: existe y no declara formato. Tiene que existir de
        // verdad, o la prueba estaría comprobando un guardado en una ruta libre, que es justo el caso que no toca.
        string filePath = TempFile(declaredSchema);
        File.Exists(filePath).Should().BeTrue();

        await _storage.SaveWorkflowAsync(filePath, SampleGraph());

        var saved = await _storage.LoadWorkflowAsync(filePath);
        WorkflowFormat.VersionOf(saved).Should().Be(WorkflowFormat.CurrentVersion,
            "lo guardado declara el formato de esta versión");
    }

    [Fact]
    public async Task SavingOverAFileThatCannotBeRead_ShouldWork()
    {
        // Un archivo que no se puede interpretar no declara ninguna versión, así que no se le puede atribuir
        // una: bloquearlo sería impedir justo el guardado con el que el usuario lo repara.
        string filePath = Path.Combine(NewTempDirectory(), "roto.json");
        await File.WriteAllTextAsync(filePath, "{ esto no es json");

        await _storage.SaveWorkflowAsync(filePath, SampleGraph());

        WorkflowFormat.VersionOf(await _storage.LoadWorkflowAsync(filePath))
            .Should().Be(WorkflowFormat.CurrentVersion, "el archivo roto se pudo reemplazar por uno bueno");
    }

    [Fact]
    public async Task SavingToANewPath_ShouldWork()
    {
        string filePath = SubflowFixtures.TempFile();

        await _storage.SaveWorkflowAsync(filePath, SampleGraph());

        File.Exists(filePath).Should().BeTrue();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Y el aviso, donde el usuario lo lee
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task OpeningAWorkflowFromANewerFormat_ShouldWarnThatItCannotBeOverwritten()
    {
        string filePath = TempFile(FutureSchema);
        var log = new LogViewModel(new InMemoryLogStore());

        await OpenFile(filePath, log);

        log.Logs.Should().Contain(record =>
            record.Level == LogLevel.Warning && record.Message.Contains(FutureSchema),
            "el usuario tiene que enterarse al abrir, no cuando se lo rechacen al guardar");
    }

    [Fact]
    public async Task SavingFromTheAppOverAWorkflowFromANewerFormat_ShouldShowTheErrorAndNotWrite()
    {
        string filePath = TempFile(FutureSchema);
        string original = File.ReadAllText(filePath);
        var log = new LogViewModel(new InMemoryLogStore());
        var dialog = new RecordingDialogService();
        var controlBar = CreateControlBar(log, dialog, Mock.Of<IFileDialogService>(fileDialog =>
            fileDialog.ShowSaveFileDialog(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()) == filePath));

        await controlBar.SaveWorkflowAsync();
        log.FlushAllPendingLogs();

        dialog.ErrorMessages.Should().ContainSingle(message => message.Contains(FutureSchema),
            "el aviso tiene que explicar por qué no se guardó, con el formato en la mano");
        File.ReadAllText(filePath).Should().Be(original);
        log.Logs.Should().NotContain(record => record.Level == LogLevel.Information && record.Message.Contains("guardado"),
            "y no puede quedar registrado como guardado");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Utilidades
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Un flujo guardado de verdad que declara el formato indicado, o un archivo como los de antes de que el
    /// formato se versionara cuando no se le pasa ninguno.
    /// </summary>
    private static string TempFile(string? declaredSchema)
    {
        string filePath = Path.Combine(NewTempDirectory(), "flujo.json");

        if (declaredSchema == null)
        {
            File.WriteAllText(filePath, "{ \"name\": \"Antiguo\" }");
            return filePath;
        }

        var graph = SampleGraph();
        graph.Schema = declaredSchema;
        File.WriteAllText(filePath, new WorkflowStorageService().SerializeGraph(graph));
        return filePath;
    }

    private static string NewTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "FileFlowNewerFormat_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static WorkflowGraph SampleGraph() => new() { Name = "Flujo de prueba" };

    private static async Task OpenFile(string filePath, LogViewModel log)
    {
        var controlBar = CreateControlBar(log, new RecordingDialogService(),
            Mock.Of<IFileDialogService>(fileDialog =>
                fileDialog.ShowOpenFileDialog(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()) == filePath));

        await controlBar.LoadWorkflowAsync();
        log.FlushAllPendingLogs();
    }

    private static ControlBarViewModel CreateControlBar(LogViewModel log, IDialogService dialog, IFileDialogService fileDialog)
    {
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
        var editor = new EditorViewModel(loader);

        return new ControlBarViewModel(
            editor,
            loader,
            log,
            new NodeInspectorViewModel(editor, fileDialog, log),
            fileDialog,
            new WorkflowStorageService(),
            new InMemoryUserPreferencesService(),
            dialogService: dialog);
    }
}
