using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Services;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Ejecutar un lienzo sin nodos no hace nada, y hasta ahora lo contaba igual que una ejecución que sí hizo
/// trabajo: el motor no tiene nada que hacer con un grafo vacío —el validador no le encuentra defectos—, así
/// que terminaba con el mensaje verde de «flujo finalizado» y cero elementos procesados. Es el mismo silencio
/// que el CLI ya rechaza con un código de salida, y aquí el camino del fallo ya existía: un error en el motor
/// se convierte en un aviso en la consola y en un diálogo. Lo que faltaba era no tratarlo como un éxito.
/// </summary>
public class EmptyWorkflowExecutionTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // El resultado de la ejecución
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunningAWorkflowWithoutNodes_ShouldFailInsteadOfSucceeding()
    {
        var coordinator = CreateCoordinator(new EditorViewModel(CreateLoader()));

        var result = await coordinator.RunAsync(Options(), _ => { }, CancellationToken.None);

        result.Succeeded.Should().BeFalse("no hay nada que ejecutar, así que no puede ser un éxito");
        result.ErrorMessage.Should().Contain("ningún nodo");
        result.Cancelled.Should().BeFalse();
        coordinator.ActiveExecutor.Should().BeNull("ni siquiera llega a crearse el motor: no hay nada que ejecutar");
    }

    // La frontera —que un flujo **con** nodos y sin trabajo siga siendo un éxito— vivía fuera de este fichero
    // por una razón que ya no existe: una ejecución de verdad del coordinador terminaba esperando al despachador
    // de la interfaz, así que aquí se quedaba colgada en vez de fallar. El despachador se inyecta desde la fase
    // 3B, y esa mitad está fijada ahora en `WorkflowExecutionThroughTheAppTests`, junto al resto del camino de
    // ejecución de la aplicación.

    // ─────────────────────────────────────────────────────────────────────────────
    // El aviso, por donde lo recibe el usuario
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunningAWorkflowWithoutNodesFromTheApp_ShouldWarnAndNotReportSuccess()
    {
        var editor = new EditorViewModel(CreateLoader());
        var log = new LogViewModel(new InMemoryLogStore());
        var dialog = new RecordingDialogService();

        var controlBar = new ControlBarViewModel(
            editor,
            CreateLoader(),
            log,
            new NodeInspectorViewModel(editor, new FileDialogService(), log),
            new FileDialogService(),
            new WorkflowStorageService(),
            new InMemoryUserPreferencesService(),
            dialogService: dialog,
            uiDispatcher: NullUiDispatcher.Instance);

        await controlBar.ExecuteWorkflowAsync();
        log.FlushAllPendingLogs();

        log.Logs.Should().Contain(record => record.Level == LogLevel.Error && record.Message.Contains("ningún nodo"),
            "el aviso tiene que quedar registrado, no sólo aparecer un instante");
        dialog.ErrorMessages.Should().ContainSingle(message => message.Contains("ningún nodo"),
            "y tiene que llegar a la pantalla: un flujo que no se ejecutó no puede parecer ejecutado");
        log.Logs.Should().NotContain(record => record.Message.Contains(LocalizationManager.Instance["LogExecutionFinished"]),
            "lo que no puede pasar es que termine con el mensaje de éxito");
        controlBar.IsRunning.Should().BeFalse();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Utilidades
    // ─────────────────────────────────────────────────────────────────────────────

    private static WorkflowExecutionOptions Options() => new(
        IsDebug: false,
        IsDryRun: true,
        MaxParallelThreads: 2,
        WorkflowName: "Flujo sin nodos");

    /// <summary>
    /// El coordinador con el despachador en línea: el cierre de la ejecución —publicar el estado final de los
    /// modelos— se ejecuta en el hilo que llama en vez de esperar a un bucle que aquí nadie bombea.
    /// </summary>
    private static WorkflowExecutionCoordinator CreateCoordinator(EditorViewModel editor) =>
        new(editor, CreateLoader(), new LogViewModel(new InMemoryLogStore()),
            new NodeInspectorViewModel(editor, new FileDialogService(), new LogViewModel(new InMemoryLogStore())),
            uiDispatcher: NullUiDispatcher.Instance,
            userPreferencesService: new InMemoryUserPreferencesService(
                new UserPreferencesData { EnableCheckpointing = false }));

    private static PluginLoader CreateLoader()
    {
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
        return loader;
    }
}
