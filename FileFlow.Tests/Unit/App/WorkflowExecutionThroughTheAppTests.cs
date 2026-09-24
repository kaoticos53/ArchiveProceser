using System.IO;
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
/// La ejecución de un flujo <b>desde la aplicación</b>, de punta a punta: el camino que más usa el usuario y
/// el único que no se podía verificar.
///
/// Por qué no se podía: el cierre del coordinador publica el estado final de los modelos con un despacho
/// <b>esperado</b> al hilo de la interfaz, y ese despacho no vuelve nunca si nadie bombea el bucle del
/// despachador —cosa que el suite unitario no hace—. La ejecución se quedaba, por tanto, a medias y sin
/// poder comprobarse; lo único verificable era la guardia de un lienzo sin nodos, que termina antes de
/// llegar ahí. Con <see cref="NullUiDispatcher"/> el cierre se ejecuta en línea y el flujo entero se puede
/// correr en una prueba, que es lo mismo que el despachador de producción hace cuando ya está sobre su hilo.
///
/// Medido antes de tocar nada, porque la suposición contraria era razonable y falsa: de las cuatro
/// dependencias del hilo de la interfaz —publicar sin esperar, el cronómetro del lienzo, el despacho
/// esperado y el bucle de descarga— <b>sólo</b> el despacho esperado bloquea. El cronómetro de 30 FPS y las
/// publicaciones sin esperar funcionan desde cualquier hilo, porque no hacen nada hasta que el bucle los
/// atienda. Por eso el despachador es la única pieza que hizo falta inyectar además de las preferencias
/// —de las que se leen el directorio temporal, la limpieza de intermedios y la descarga de modelos al
/// terminar, y leerlas del proceso hacía que una prueba tocara la configuración real del usuario—.
/// </summary>
public class WorkflowExecutionThroughTheAppTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // El trabajo se hace, y se cuenta donde el usuario lo mira
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunningAWorkflowWithWorkToDo_ShouldDoTheWorkAndReportItOnTheCanvas()
    {
        string source = NewDirectory();
        string destination = NewDirectory();
        string output = NewDirectory();
        string temp = NewDirectory();
        File.WriteAllText(Path.Combine(source, "muestra.txt"), "contenido de prueba");

        try
        {
            var (editor, coordinator, log) = BuildRun(source, destination, output, temp);

            // El texto del mensaje de arranque se resuelve <b>una sola vez</b>, antes de la ejecución: el
            // coordinador lo resuelve por dentro al encolarlo, así que resolverlo aquí al afirmar era comparar
            // dos momentos de un global y la aserción podía fallar al azar si un registro de recursos ajeno
            // caía en medio (hito 179). Lo que cerró esa ventana es que el diccionario del host esté registrado
            // desde el arranque del suite (HostLocalization), no esta lectura; la lectura lo deja dicho.
            string startMessage = LocalizationManager.Instance["LogStartingExecution"];

            var result = await coordinator.RunAsync(Options(), _ => { }, CancellationToken.None);

            result.Succeeded.Should().BeTrue($"el flujo tiene que ejecutarse: {result.ErrorMessage}");
            result.Cancelled.Should().BeFalse();
            File.Exists(Path.Combine(destination, "muestra.txt"))
                .Should().BeTrue("el trabajo del flujo es mover el archivo de origen al destino");

            // Lo que hace que esta prueba valga: el lienzo recibió el estado de los nodos, y eso sólo ocurre
            // si el vaciado del cierre se ejecutó. Sin él los nodos se quedarían en «inactivo» para siempre.
            editor.Nodes.Should().OnlyContain(node => node.ExecutionStatus == NodeExecutionStatus.Completed,
                "el estado final de cada nodo tiene que llegar al lienzo: {0}",
                string.Join(", ", editor.Nodes.Select(node => $"{node.Title}={node.ExecutionStatus}")));

            // Sin vaciar nada desde aquí: la consola se lee tal como la dejó el cierre de la ejecución, que es
            // lo que esta prueba verifica. Vaciarla desde el test mediría al test, no al producto. (El latido de
            // la consola entrega el tick al hilo de la interfaz y en el suite headless el despacho desde otro
            // hilo se descarta —medido en el hito 179—, así que quien publica aquí es el cierre, que es
            // justamente el camino que interesa.)
            log.Logs.Should().Contain(record => record.Message == startMessage,
                "el registro de la ejecución tiene que salir por la consola sin que nadie lo empuje a mano");
            log.Logs.Should().NotContain(record => record.Level == LogLevel.Error,
                "un flujo que hizo su trabajo no deja errores en la consola");

            // Y el cierre termina de verdad: el motor se suelta en lugar de quedarse vivo tras la ejecución.
            coordinator.ActiveExecutor.Should().BeNull();
            coordinator.ActiveDebugSession.Should().BeNull();
        }
        finally
        {
            Delete(source, destination, output, temp);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // La frontera: no encontrar trabajo no es un fallo
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// La otra mitad de la guardia del lienzo vacío, que hasta ahora no se podía fijar aquí: un flujo
    /// <b>con</b> nodos que se ejecuta y no encuentra nada que hacer terminó bien, y tiene que seguir
    /// terminando bien. Confundir «sin nodos» con «sin trabajo» convertiría un origen vacío en un error.
    /// </summary>
    [Fact]
    public async Task RunningAWorkflowWhoseSourceIsEmpty_ShouldSucceedWithNothingDone()
    {
        string source = NewDirectory();
        string destination = NewDirectory();
        string output = NewDirectory();
        string temp = NewDirectory();

        try
        {
            var (_, coordinator, _) = BuildRun(source, destination, output, temp);

            var result = await coordinator.RunAsync(Options(), _ => { }, CancellationToken.None);

            result.Succeeded.Should().BeTrue("el flujo se ejecutó: encontró un origen vacío, que no es un fallo");
            result.ErrorMessage.Should().BeNull();
            result.Cancelled.Should().BeFalse();
            (Directory.Exists(destination) ? Directory.GetFiles(destination) : []).Should().BeEmpty(
                "no había nada que procesar, así que no puede haber nada en destino");
            coordinator.ActiveExecutor.Should().BeNull();
        }
        finally
        {
            Delete(source, destination, output, temp);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // El mismo camino, por donde lo recorre el botón
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// El camino completo de la barra de control —el que recorre el botón de ejecutar—, incluido el
    /// resultado que se publica en la interfaz. En simulación, que es como se recorre sin mover archivos
    /// del usuario: lo que se fija es que el trabajo se planifica y se cuenta, y que la ejecución queda
    /// limpia.
    /// </summary>
    [Fact]
    public async Task RunningTheWorkflowFromTheControlBar_ShouldReportThePlannedWorkAndFinishCleanly()
    {
        string source = NewDirectory();
        string destination = NewDirectory();
        string output = NewDirectory();
        string temp = NewDirectory();
        File.WriteAllText(Path.Combine(source, "muestra.txt"), "contenido de prueba");

        try
        {
            var loader = CreateLoader();
            var editor = new EditorViewModel(loader);
            editor.LoadFromGraphModel(Pipeline(source, destination, output, temp));

            var log = new LogViewModel(new InMemoryLogStore());
            var dialog = new RecordingDialogService();

            var controlBar = new ControlBarViewModel(
                editor,
                loader,
                log,
                new NodeInspectorViewModel(editor, new FileDialogService(), log),
                new FileDialogService(),
                new WorkflowStorageService(),
                new InMemoryUserPreferencesService(new UserPreferencesData { EnableCheckpointing = false }),
                dialogService: dialog,
                uiDispatcher: NullUiDispatcher.Instance)
            {
                IsDryRun = true
            };

            await controlBar.ExecuteWorkflowAsync();

            controlBar.IsRunning.Should().BeFalse("la ejecución terminó, así que el botón deja de estar ocupado");
            controlBar.HasVirtualFiles.Should().BeTrue(
                "la simulación tiene que dejar su resultado a la vista, no sólo un mensaje efímero");
            controlBar.VirtualFilesCount.Should().BeGreaterThan(0);
            dialog.ErrorMessages.Should().BeEmpty("un flujo que se ejecutó bien no puede mostrar un error");
            editor.Nodes.Should().OnlyContain(node => node.ExecutionStatus == NodeExecutionStatus.Completed,
                "el lienzo del usuario también se entera cuando la ejecución la lanza el botón");
        }
        finally
        {
            Delete(source, destination, output, temp);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Lo que va a hacer el flujo, dicho antes de lanzarlo
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// El diagnóstico compartido con el CLI, por donde lo recibe el usuario de la interfaz: la consola, antes
    /// de arrancar. Aquí el flujo sí se ejecuta —los avisos no bloquean— y el aviso cuenta lo que conviene
    /// saber: quedó un nodo sin conectar, así que el motor lo arrancará con un elemento vacío.
    /// </summary>
    [Fact]
    public async Task RunningAWorkflowWithANodeLeftUnconnected_ShouldSayItOnTheConsoleAndStillRun()
    {
        string source = NewDirectory();
        string destination = NewDirectory();
        string output = NewDirectory();
        string temp = NewDirectory();
        File.WriteAllText(Path.Combine(source, "muestra.txt"), "contenido de prueba");

        try
        {
            var graph = Pipeline(source, destination, output, temp);
            graph.Nodes.Add(new WorkflowNode
            {
                Id = "suelto",
                NodeTypeName = typeof(DestinationSinkNode).FullName!,
                X = 400,
                Parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["DestinationRoot"] = NewDirectory()
                }
            });

            var loader = CreateLoader();
            var editor = new EditorViewModel(loader);
            editor.LoadFromGraphModel(graph);

            var log = new LogViewModel(new InMemoryLogStore());
            var coordinator = new WorkflowExecutionCoordinator(
                editor,
                loader,
                log,
                new NodeInspectorViewModel(editor, new FileDialogService(), log),
                uiDispatcher: NullUiDispatcher.Instance,
                userPreferencesService: new InMemoryUserPreferencesService(
                    new UserPreferencesData { EnableCheckpointing = false }));

            var result = await coordinator.RunAsync(DryRunOptions(), _ => { }, CancellationToken.None);
            log.FlushAllPendingLogs();

            result.Succeeded.Should().BeTrue($"los avisos no bloquean la ejecución: {result.ErrorMessage}");

            log.Logs.Should().Contain(record => record.Message.Contains("🔎"),
                "el diagnóstico dice qué va a hacer el flujo antes de hacerlo");
            log.Logs.Should().Contain(record => record.Level == LogLevel.Warning && record.Message.Contains("elemento vacío"),
                "y dice lo que habría que arreglar: hay un nodo que nadie alimenta");
        }
        finally
        {
            Delete(source, destination, output, temp);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Utilidades
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Monta el flujo como lo monta abrir un archivo: por el cargador de plugins, que es quien crea las
    /// instancias reales de los nodos y quien une las aristas. Añadir los nodos al lienzo a mano probaría
    /// otra cosa —y un nodo de prueba sería descubrible por el propio producto—.
    /// </summary>
    // ─────────────────────────────────────────────────────────────────────────────
    // El latido visual: el lienzo se mueve <i>durante</i> la ejecución, no sólo al terminar
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// El cierre de la ejecución ya estaba cubierto (los estados finales llegan al lienzo), pero el fotograma
    /// periódico —el de los 33 ms que hace que la tarjeta cambie <i>mientras</i> el flujo corre— era una lambda
    /// capturada dentro de la ejecución: su camino no se ejecutaba nunca en el suite. Aquí se llama al mismo
    /// paso que el temporizador, sin motor de por medio.
    /// </summary>
    [Fact]
    public void TheVisualHeartbeat_ShouldPaintWhatTheEnginePublished_BeforeTheRunEnds()
    {
        string source = NewDirectory();
        string destination = NewDirectory();
        string output = NewDirectory();
        string temp = NewDirectory();

        try
        {
            var (editor, coordinator, _) = BuildRun(source, destination, output, temp);
            var origin = editor.Nodes.Single(node => node.Id == "origen");
            var cable = editor.Connections.Single();

            // Es lo que publica el motor: estado, progreso y el pulso de un cable al despachar ítems.
            coordinator.QueueNodeStatus("origen", NodeExecutionStatus.Running);
            coordinator.QueueNodeProgress("origen", 50, "mitad");
            coordinator.QueueEdgeDispatch(cable.Source.NodeOwner.Id, cable.Source.Name, 3);

            coordinator.FlushVisualFrame();

            origin.ExecutionStatus.Should().Be(NodeExecutionStatus.Running,
                "el estado encolado tiene que estar en el lienzo antes de que el flujo termine");
            origin.ProgressPercentage.Should().Be(50, "y su progreso, para que la tarjeta muestre por dónde va");
            origin.ProgressMessage.Should().Be("mitad");
            cable.ItemCount.Should().Be(3, "el pulso del cable cuenta los ítems que pasaron por ese puerto");
        }
        finally
        {
            Delete(source, destination, output, temp);
        }
    }

    /// <summary>
    /// Un nodo puede desaparecer del lienzo con la ejecución en marcha (el usuario lo borra, o se cierra el
    /// flujo). El fotograma resuelve por identificador y tiene que ignorarlo, no reventar en cada tick.
    /// </summary>
    [Fact]
    public void TheVisualHeartbeat_ShouldIgnore_UpdatesForNodesThatAreGone()
    {
        string source = NewDirectory();
        string destination = NewDirectory();
        string output = NewDirectory();
        string temp = NewDirectory();

        try
        {
            var (editor, coordinator, log) = BuildRun(source, destination, output, temp);
            var origin = editor.Nodes.Single(node => node.Id == "origen");

            coordinator.QueueNodeStatus("nodo-borrado", NodeExecutionStatus.Running);
            coordinator.QueueNodeProgress("nodo-borrado", 80, "fantasma");
            coordinator.QueueNodeStatus("origen", NodeExecutionStatus.Running);

            FluentActions.Invoking(coordinator.FlushVisualFrame).Should().NotThrow(
                "un nodo que ya no está se ignora; el latido no puede caer por eso");

            origin.ExecutionStatus.Should().Be(NodeExecutionStatus.Running,
                "y lo que sí está en el lienzo se pinta igual");
            log.StatusMessage.Should().NotBe("fantasma", "lo de un nodo que no existe no llega a ninguna parte");
        }
        finally
        {
            Delete(source, destination, output, temp);
        }
    }

    /// <summary>
    /// El temporizador vive durante toda la ejecución, así que late también antes de empezar y después de
    /// terminar: sin motor en marcha, el fotograma no tiene telemetría que empujar pero sí lo encolado.
    /// </summary>
    [Fact]
    public void TheVisualHeartbeat_ShouldRun_WithoutAnExecutionInFlight()
    {
        string source = NewDirectory();
        string destination = NewDirectory();
        string output = NewDirectory();
        string temp = NewDirectory();

        try
        {
            var (editor, coordinator, log) = BuildRun(source, destination, output, temp);
            coordinator.ActiveExecutor.Should().BeNull("esta prueba no arranca ninguna ejecución");
            string statusBefore = log.StatusMessage;

            coordinator.QueueNodeStatus("origen", NodeExecutionStatus.Idle);
            FluentActions.Invoking(coordinator.FlushVisualFrame).Should().NotThrow(
                "el primer latido puede caer con el motor aún sin crear");
            FluentActions.Invoking(coordinator.FlushVisualFrame).Should().NotThrow(
                "y el último, con el motor ya soltado");

            log.StatusMessage.Should().Be(statusBefore,
                "sin ejecución no hay telemetría que publicar: la barra no puede cambiar sola");
            editor.Nodes.Single(node => node.Id == "origen").ExecutionStatus.Should().Be(NodeExecutionStatus.Idle);
        }
        finally
        {
            Delete(source, destination, output, temp);
        }
    }

    private static (EditorViewModel Editor, WorkflowExecutionCoordinator Coordinator, LogViewModel Log) BuildRun(
        string source,
        string destination,
        string output,
        string temp)
    {
        var loader = CreateLoader();
        var editor = new EditorViewModel(loader);
        editor.LoadFromGraphModel(Pipeline(source, destination, output, temp));

        var log = new LogViewModel(new InMemoryLogStore());
        var coordinator = new WorkflowExecutionCoordinator(
            editor,
            loader,
            log,
            new NodeInspectorViewModel(editor, new FileDialogService(), log),
            uiDispatcher: NullUiDispatcher.Instance,
            userPreferencesService: new InMemoryUserPreferencesService(
                new UserPreferencesData { EnableCheckpointing = false }));

        return (editor, coordinator, log);
    }

    /// <summary>
    /// Carpeta de origen hacia carpeta de destino, con los directorios de salida y temporal fijados al de la
    /// prueba: sin ellos el coordinador tomaría los del usuario, que es justo lo que una prueba no debe tocar.
    /// </summary>
    private static WorkflowGraph Pipeline(string source, string destination, string output, string temp)
    {
        var graph = new WorkflowGraph
        {
            Name = "Flujo de prueba",
            GlobalOutputDir = output,
            TemporaryDirectory = temp
        };

        graph.Nodes.Add(new WorkflowNode
        {
            Id = "origen",
            NodeTypeName = typeof(FolderSourceNode).FullName!,
            X = 0,
            Parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["SourcePath"] = source,
                ["IncludeSubdirectories"] = false
            }
        });

        graph.Nodes.Add(new WorkflowNode
        {
            Id = "destino",
            NodeTypeName = typeof(DestinationSinkNode).FullName!,
            X = 200,
            Parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["DestinationRoot"] = destination
            }
        });

        graph.Edges.Add(new WorkflowEdge
        {
            SourceNodeId = "origen",
            SourcePortName = "Out",
            TargetNodeId = "destino",
            TargetPortName = "In"
        });

        return graph;
    }

    private static WorkflowExecutionOptions DryRunOptions() => Options() with { IsDryRun = true };

    private static WorkflowExecutionOptions Options() => new(
        IsDebug: false,
        IsDryRun: false,
        MaxParallelThreads: 2,
        WorkflowName: "Flujo de prueba",
        EnableCheckpointing: false);

    private static PluginLoader CreateLoader()
    {
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
        return loader;
    }

    private static string NewDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "FF_AppExec_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Delete(params string[] directories)
    {
        foreach (string directory in directories)
        {
            try
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
                // Limpieza de mejor esfuerzo: una carpeta temporal que no se puede borrar no invalida la prueba.
            }
        }
    }
}
