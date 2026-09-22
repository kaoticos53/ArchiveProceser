using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Subflows;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Un contenedor de subflujo puede cambiar <b>por fuera</b> del lienzo que lo tiene abierto: se edita el
/// subflujo en otra pestaña, o en otro programa, y el archivo en disco deja de ser el que el contenedor
/// materializó. Hasta ahora el contenedor vivo se quedaba con la frontera vieja —los puertos que ya no
/// existen seguían en la tarjeta y los nuevos no aparecían— hasta que algo le preguntara: cambiar un
/// parámetro suyo, o reabrir el flujo. Ejecutar el flujo en ese estado usa una frontera que el subflujo ya no
/// declara.
///
/// Lo que se fija aquí es el refresco y, sobre todo, su frontera: los puertos que <b>siguen existiendo</b>
/// conservan sus cables, y lo que se pierde se cuenta en vez de desaparecer en silencio. La comprobación es
/// la <b>huella</b> del archivo —su fecha y su tamaño—, así que no lee su contenido, y de ahí salen las dos
/// mitades que se prueban al final: lo que la huella sí distingue, y el cambio que por definición no puede
/// distinguir.
/// </summary>
public class SubflowDefinitionOnDiskChangedTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // La detección y el refresco
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ChangingTheSubflowFileOnDisk_ShouldRefreshTheContainerPorts()
    {
        string path = SubflowFixtures.TempFile();

        try
        {
            SubflowFixtures.WriteFile(path, SubflowFixtures.DefinitionJson("In;Alternate", "Out;Errores"), DateTime.UtcNow);
            var (editor, _, container, _, _) = CanvasWithAContainerOnDisk(path);

            container.InputPorts.Select(port => port.Name).Should().Equal("In", "Alternate");

            // El subflujo gana una salida y pierde una entrada, sin que nadie toque este lienzo.
            SubflowFixtures.WriteFile(path, SubflowFixtures.DefinitionJson("In", "Out;Errores;Resultados"), DateTime.UtcNow.AddMinutes(1));

            var refreshed = editor.RefreshSubflowsChangedOnDisk();

            refreshed.Should().ContainSingle().Which.Should().BeSameAs(container,
                "el contenedor es el nodo cuyo origen cambió, no cualquier nodo del lienzo");
            container.InputPorts.Select(port => port.Name).Should().Equal("In");
            container.OutputPorts.Select(port => port.Name).Should().Equal("Out", "Errores", "Resultados");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PollingWithNothingChanged_ShouldLeaveTheGraphAlone()
    {
        string path = SubflowFixtures.TempFile();

        try
        {
            SubflowFixtures.WriteFile(path, SubflowFixtures.DefinitionJson("In;Alternate", "Out;Errores"), DateTime.UtcNow);
            var (editor, log, container, _, _) = CanvasWithAContainerOnDisk(path);

            var refreshed = editor.RefreshSubflowsChangedOnDisk();
            editor.RefreshSubflowsChangedOnDisk();

            refreshed.Should().BeEmpty("nada cambió en el disco, así que no hay nada que refrescar");
            container.InputPorts.Select(port => port.Name).Should().Equal("In", "Alternate");
            log.Logs.Should().BeEmpty("un latido sin novedad no puede escribir en la consola: son uno por segundo");
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Los cables: se conservan los que siguen existiendo
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void RefreshingTheContainer_ShouldKeepTheCablesWhosePortsStillExist()
    {
        string path = SubflowFixtures.TempFile();

        try
        {
            SubflowFixtures.WriteFile(path, SubflowFixtures.DefinitionJson("In;Alternate", "Out;Errores"), DateTime.UtcNow);
            // Un cable a la entrada que desaparece y otro a la salida que se queda: la prueba necesita las
            // dos mitades a la vez para que lo conservado no se pueda confundir con «no se refrescó nada».
            var (editor, _, container, source, sink) = CanvasWithAContainerOnDisk(path,
                withTheCableToTheVanishingPort: true, withTheCableToTheSurvivingPort: true);
            editor.Connections.Should().HaveCount(2);

            SubflowFixtures.WriteFile(path, SubflowFixtures.DefinitionJson("In", "Out;Errores"), DateTime.UtcNow.AddMinutes(1));

            editor.RefreshSubflowsChangedOnDisk();

            editor.Connections.Should().ContainSingle("el puerto 'Alternate' ya no existe; 'Errores' sí");
            editor.Connections[0].Source.NodeOwner.Should().BeSameAs(container);
            editor.Connections[0].Source.Name.Should().Be("Errores");
            editor.Connections[0].Target.NodeOwner.Should().BeSameAs(sink);
            container.OutputPorts.Should().Contain(port => port.Name == "Errores")
                .Which.Should().BeSameAs(editor.Connections[0].Source,
                    "el puerto que sobrevive conserva su instancia, y con ella el cable que cuelga de él");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AChangeThatOnlyAddsPorts_ShouldKeepEveryCableAndNotRaiseTheBanner()
    {
        string path = SubflowFixtures.TempFile();

        try
        {
            SubflowFixtures.WriteFile(path, SubflowFixtures.DefinitionJson("In;Alternate", "Out;Errores"), DateTime.UtcNow);
            var (editor, log, _, _, _) = CanvasWithAContainerOnDisk(path,
                withTheCableToTheVanishingPort: true, withTheCableToTheSurvivingPort: true);

            SubflowFixtures.WriteFile(path, SubflowFixtures.DefinitionJson("In;Alternate;Extra", "Out;Errores;Resultados"), DateTime.UtcNow.AddMinutes(1));

            editor.RefreshSubflowsChangedOnDisk();
            log.FlushAllPendingLogs();

            editor.Connections.Should().HaveCount(2, "el subflujo amplió su frontera sin quitar ningún puerto");
            editor.HasCanvasNotice.Should().BeFalse("no se perdió nada que leer en el lienzo");
            log.Logs.Should().Contain(record => record.Level == LogLevel.Information,
                "el refresco queda en la consola aunque no se haya roto nada");
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Lo que se pierde, contado
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ALostCable_ShouldBeCountedOnTheCanvasAndInTheConsole()
    {
        string path = SubflowFixtures.TempFile();

        try
        {
            SubflowFixtures.WriteFile(path, SubflowFixtures.DefinitionJson("In;Alternate", "Out;Errores"), DateTime.UtcNow);
            var (editor, log, container, source, _) = CanvasWithAContainerOnDisk(path,
                withTheCableToTheVanishingPort: true, withTheCableToTheSurvivingPort: false);

            SubflowFixtures.WriteFile(path, SubflowFixtures.DefinitionJson("In", "Out;Errores"), DateTime.UtcNow.AddMinutes(1));

            editor.RefreshSubflowsChangedOnDisk();
            log.FlushAllPendingLogs();

            editor.HasCanvasNotice.Should().BeTrue("el cambio se llevó un cable y el usuario no lo pidió");
            editor.CanvasNotice.Should().Contain(container.Title, "la cabecera dice de qué subflujo fue el cambio");

            // Y el detalle —qué puerto falta— vive en la fila que se puede arreglar.
            var fix = editor.CanvasNoticeFixes.Should().ContainSingle().Subject;
            fix.NodeTitle.Should().Be(container.Title);
            fix.MissingPortName.Should().Be("Alternate", "el aviso dice qué nodo y qué puerto hay que volver a conectar");

            log.Logs.Should().Contain(record => record.Level == LogLevel.Warning
                && record.Message.Contains("Alternate") && record.Message.Contains(container.Title),
                "la consola es el registro que sobrevive al cartel, y dice por qué se perdió");

            // Y el culpable es el extremo que dejó de exponer el puerto, no el que sigue exactamente igual:
            // culpar al origen mandaría a revisar un nodo que no tiene nada que arreglar.
            var sourceEnd = new DroppedConnectionEnd(
                source.Id, source.Title, source.OutputPorts.Single().Name, DroppedConnectionEndProblem.MissingPort);
            log.Logs.Single(record => record.Level == LogLevel.Warning).Message
                .Should().NotContain(DroppedConnectionText.DescribeImpediment(LocalizationManager.Instance, sourceEnd));
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Los límites, declarados
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AContainerWithAnEmbeddedDefinition_ShouldNeverFollowTheFile()
    {
        string path = SubflowFixtures.TempFile();

        try
        {
            SubflowFixtures.WriteFile(path, SubflowFixtures.DefinitionJson("In;Alternate", "Out;Errores"), DateTime.UtcNow);

            var loader = CreateLoader();
            var editor = new EditorViewModel(loader);
            var container = new SubflowNode
            {
                EmbedDefinition = true,
                SubflowDefinitionJson = SubflowFixtures.DefinitionJson("In", "Out"),
                SubflowPath = path
            };
            SubflowPortResolver.Materialize(container);
            var containerVm = EditorFixtures.AddNode(editor, container);

            SubflowFixtures.WriteFile(path, SubflowFixtures.DefinitionJson("Otra", "Cosa"), DateTime.UtcNow.AddMinutes(1));

            editor.RefreshSubflowsChangedOnDisk().Should().BeEmpty(
                "la definición va incrustada en el flujo: el archivo no la aporta y no puede cambiarla");
            containerVm.InputPorts.Select(port => port.Name).Should().Equal("In");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ASubflowThatMovedAway_ShouldKeepItsPortsAndItsCablesWithoutFloodingTheConsole()
    {
        string path = SubflowFixtures.TempFile();

        try
        {
            SubflowFixtures.WriteFile(path, SubflowFixtures.DefinitionJson("In;Alternate", "Out;Errores"), DateTime.UtcNow);
            var (editor, log, container, _, _) = CanvasWithAContainerOnDisk(path,
                withTheCableToTheVanishingPort: true, withTheCableToTheSurvivingPort: true);

            File.Delete(path);

            // Tres latidos: el resolutor no memoriza lo que no pudo leer, así que la pregunta sigue
            // respondiendo que sí —y así se ve que lo que no se repite es el aviso, no la comprobación—.
            container.HasSubflowDefinitionChanged().Should().BeTrue();
            editor.RefreshSubflowsChangedOnDisk();
            editor.RefreshSubflowsChangedOnDisk();
            editor.RefreshSubflowsChangedOnDisk();

            container.InputPorts.Select(port => port.Name).Should().Equal(
                ["In", "Alternate"], "el contenedor conserva los puertos que recordaba en vez de caer a los genéricos");
            editor.Connections.Should().HaveCount(2, "y con ellos sus cables");
            log.FlushAllPendingLogs();
            log.Logs.Should().BeEmpty("un aviso por segundo sobre un contenedor que no cambió de puertos es ruido");
            editor.HasCanvasNotice.Should().BeFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ARewrittenFileWithTheSameStampAndSize_ShouldBeInvisible()
    {
        string path = SubflowFixtures.TempFile();

        try
        {
            var stamp = DateTime.UtcNow;
            string original = SubflowFixtures.DefinitionJson("In;Alternate", "Out;Errores");
            SubflowFixtures.WriteFile(path, original, stamp);

            var (editor, _, container, _, _) = CanvasWithAContainerOnDisk(path);

            // Mismo tamaño y misma fecha de escritura, contenido distinto: es el cambio que la huella no puede
            // ver, y lo que se fija aquí es que el límite está donde dice el resolutor y no antes.
            string rewritten = SubflowFixtures.DefinitionJson("In;AlternatX", "Out;Errores");
            rewritten.Should().NotBe(original);
            SubflowFixtures.WriteFile(path, rewritten, stamp);
            new FileInfo(path).Length.Should().Be(original.Length, "la prueba necesita la colisión exacta de huella");

            editor.RefreshSubflowsChangedOnDisk().Should().BeEmpty();
            container.InputPorts.Select(port => port.Name).Should().Equal("In", "Alternate");
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Utilidades
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Lienzo con un contenedor atado a un archivo de subflujo, más un origen y un destino con los que
    /// colgarle cables a sus puertos. El contenedor arranca con la frontera <c>In;Alternate</c> /
    /// <c>Out;Errores</c> ya materializada, que es el estado en el que queda al abrir un flujo guardado.
    /// </summary>
    private static (EditorViewModel Editor, LogViewModel Log, NodeViewModel Container, NodeViewModel Source, NodeViewModel Sink)
        CanvasWithAContainerOnDisk(string definitionPath, bool withTheCableToTheVanishingPort = false, bool withTheCableToTheSurvivingPort = false)
    {
        var loader = CreateLoader();
        var log = new LogViewModel(new InMemoryLogStore());
        var editor = new EditorViewModel(loader, logViewModel: log);

        var container = new SubflowNode
        {
            EmbedDefinition = false,
            SubflowPath = definitionPath,
            SubflowName = "Contenedor"
        };
        SubflowPortResolver.Materialize(container);

        var source = EditorFixtures.AddNode(editor, new FolderSourceNode());
        var containerVm = EditorFixtures.AddNode(editor, container, x: 200);
        var sink = EditorFixtures.AddNode(editor, new DestinationSinkNode(), x: 400);

        if (withTheCableToTheVanishingPort)
        {
            editor.CreateConnection(source.OutputPorts.Single(), containerVm.InputPorts.Single(port => port.Name == "Alternate"));
        }

        if (withTheCableToTheSurvivingPort)
        {
            editor.CreateConnection(containerVm.OutputPorts.Single(port => port.Name == "Errores"), sink.InputPorts.Single());
        }

        log.FlushAllPendingLogs();

        return (editor, log, containerVm, source, sink);
    }

    private static PluginLoader CreateLoader()
    {
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(SubflowNode).Assembly);
        return loader;
    }
}
