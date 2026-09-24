using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.App.Services;
using FileFlow.Core.Engine;
using FileFlow.Core.Storage;
using FileFlow.Plugin.Archives;
using FileFlow.Sdk;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

/// <summary>
/// <b>Un archivo que no se pudo comprimir no deja un archivo a medias con la extensión del archivo prometido.</b>
///
/// <para>El nodo abre (y trunca) el destino <i>antes</i> de construir el escritor, así que una combinación que el
/// escritor rechaza dejaba en el disco un archivo de <b>cero bytes</b> llamado <c>.7z</c>: ni es un 7Z, ni es el
/// original, y ninguna fase posterior puede distinguirlo de un archivo bien hecho. Lo destapó el ejemplo 38 del
/// catálogo —7Z con la compresión por defecto del nodo, que el contenedor 7Z no admite— al ejecutarse de punta a
/// punta por primera vez (hito 204).</para>
///
/// <para>La segunda prueba es el <b>control</b>: la misma disposición con una compresión que el contenedor sí
/// admite tiene que entregar el archivo. Sin ella, una prueba que sólo mira que «no quede nada» pasaría también
/// si el nodo se hubiera roto por otra razón y no comprimiera nunca.</para>
///
/// <para><b>Dónde escribe el comprimido (hito 209)</b>: sin carpeta declarada, en la <b>salida del flujo</b> —la que
/// el flujo declara como suya y, si no declara ninguna, la que los ajustes dan por defecto—, y el log lo dice;
/// «junto al archivo que comprime» dejó de ser el respaldo y pasó a ser algo que se declara, <c>{CurrentDir}</c>. Las
/// cuatro pruebas de destino son las cuatro caras de esa regla: la declarada, la que no se declara, la plantilla que
/// el flujo declara como su salida (donde la salida global se expande y se ancla, en vez de dejar una ruta relativa
/// que acaba en la carpeta donde corre el proceso) y la de fábrica del parámetro.</para>
/// </summary>
public class ArchiveCompressorNodeTests
{
    [Fact]
    public async Task ACompressorAskedForAContainerItsWriterRejects_ShouldLeaveNoFileBehind()
    {
        string root = NewDirectory();
        string destination = NewDirectory();
        string input = Path.Combine(root, "nota.txt");
        await File.WriteAllTextAsync(input, "contenido que se quería guardar comprimido");

        try
        {
            var node = new ArchiveCompressorNode();
            node.Parameters["ArchiveFormat"] = "7Z";
            node.Parameters["DestinationFolder"] = destination;

            // La compresión se deja como viene de fábrica: es 'Deflate', y el contenedor 7Z sólo admite LZMA.
            node.Parameters["ArchiveName"] = "salida.7z";

            var context = new ProbeFlowContext { Storage = new PhysicalStorageService() };

            await node.ExecuteAsync("In", new FileItemContext(input), context, CancellationToken.None);

            context.EmittedPorts.Should().ContainSingle().Which.Should().Be("Error",
                "el nodo no puede decir que comprimió lo que el escritor rechazó");
            Directory.EnumerateFiles(destination).Should().BeEmpty(
                "un archivo de cero bytes con la extensión del archivo prometido aparenta una conversión que no ocurrió");
            context.Logs.Should().Contain(message => message.Contains("retirado por quedar vacío"),
                "y el log tiene que decir que se retiró, no callarse el rescate");
        }
        finally
        {
            Clean(root, destination);
        }
    }

    [Fact]
    public async Task ACompressorAskedForACombinationItsWriterAccepts_ShouldDeliverTheArchive()
    {
        string root = NewDirectory();
        string destination = NewDirectory();
        string input = Path.Combine(root, "nota.txt");
        await File.WriteAllTextAsync(input, "contenido que se quería guardar comprimido");

        try
        {
            var node = new ArchiveCompressorNode();
            node.Parameters["ArchiveFormat"] = "7Z";
            node.Parameters["CompressionType"] = "LZMA";
            node.Parameters["DestinationFolder"] = destination;
            node.Parameters["ArchiveName"] = "salida.7z";

            var context = new ProbeFlowContext { Storage = new PhysicalStorageService() };

            await node.ExecuteAsync("In", new FileItemContext(input), context, CancellationToken.None);

            context.EmittedPorts.Should().ContainSingle().Which.Should().Be("Out");
            string archive = Directory.EnumerateFiles(destination).Single();
            new FileInfo(archive).Length.Should().BeGreaterThan(0,
                "el 7Z que el contenedor admite tiene contenido: es el ejemplo 38 tal y como está declarado ahora");
        }
        finally
        {
            Clean(root, destination);
        }
    }

    [Fact]
    public async Task ACompressorWhoseDestinationIsItsOwnInput_ShouldRefuseInsteadOfTruncatingTheInput()
    {
        // Comprimir un archivo ya comprimido apuntando a su propia carpeta —lo que durante años fue el
        // comportamiento por omisión del nodo y hoy se declara con «{CurrentDir}»— y con el nombre de fábrica
        // «{FileNameWithoutExtension}.zip» apunta al propio archivo de entrada. El destino se abre para escribir (y
        // se trunca) antes de leer la entrada, así que el original del usuario quedaba vaciado: en el ejemplo 21, un
        // 'paquete.zip' de 148 bytes salía del flujo convertido en un comprimido de 22 bytes, sin que nada en el
        // resultado lo delatara. Un archivo no cabe dentro de sí mismo: el nodo tiene que pararse.
        string root = NewDirectory();
        string input = Path.Combine(root, "paquete.zip");
        byte[] original = [.. Enumerable.Range(0, 200).Select(i => (byte)(i % 251))];
        await File.WriteAllBytesAsync(input, original);

        try
        {
            var node = new ArchiveCompressorNode();
            node.Parameters["DestinationFolder"] = "{CurrentDir}";

            var context = new ProbeFlowContext { Storage = new PhysicalStorageService() };

            await node.ExecuteAsync("In", new FileItemContext(input), context, CancellationToken.None);

            context.EmittedPorts.Should().ContainSingle().Which.Should().Be("Error",
                "el nodo no puede decir que comprimió un archivo sobre sí mismo");
            File.ReadAllBytes(input).Should().Equal(original,
                "la entrada no se toca: el archivo del usuario no cabe dentro de sí mismo, pero tampoco se destruye");
            Directory.EnumerateFiles(root).Should().ContainSingle(
                "y no queda un comprimido a medias con el nombre del archivo prometido");
            context.Logs.Should().Contain(message => message.Contains("input file itself"),
                "el log tiene que decir por qué no se comprimió, no callarse");
        }
        finally
        {
            Clean(root);
        }
    }

    /// <summary>
    /// <b>Sin carpeta de destino declarada, el comprimido sale en la salida del flujo</b> —que es lo que el motor
    /// entrega en <c>GlobalOutputDir</c>: la carpeta que el flujo declara como suya y, cuando no declara ninguna, la
    /// salida por defecto de los ajustes— y el nodo lo <b>dice en el log</b>, porque dónde fue el archivo no se deduce
    /// del resultado. Es la regla del hito 209: hasta el 208 el comprimido salía junto al archivo —en el ejemplo 21,
    /// un <c>paquete.zip</c> convertido en un comprimido de 22 bytes— y los flujos guardados cuando eso pasaba no
    /// declaran carpeta, así que a ellos también les cambia el sitio: el log lo anuncia con el nombre de la carpeta.
    /// </summary>
    [Fact]
    public async Task ACompressorWithoutADestination_ShouldWriteInTheFlowsOutputFolder_AndSaySo()
    {
        string root = NewDirectory();
        string output = NewDirectory();
        string input = Path.Combine(root, "nota.txt");
        await File.WriteAllTextAsync(input, "contenido que se quería guardar comprimido");

        try
        {
            var node = new ArchiveCompressorNode();

            // Tal y como quedó guardado: la clave está y está vacía (el valor de fábrica de antes).
            node.Parameters["DestinationFolder"] = "";

            var item = new FileItemContext(input);
            item.Metadata["GlobalOutputDir"] = output;

            var context = new ProbeFlowContext { Storage = new PhysicalStorageService() };

            await node.ExecuteAsync("In", item, context, CancellationToken.None);

            context.EmittedPorts.Should().ContainSingle().Which.Should().Be("Out");

            string expected = Path.Combine(output, "nota.zip");
            context.EmittedItems.Single().CurrentPath.Should().Be(expected,
                "sin carpeta declarada el comprimido se escribe en la salida del flujo, no junto al archivo ni donde corra el proceso");
            File.Exists(expected).Should().BeTrue("y existe donde el nodo dice que lo dejó");
            Directory.EnumerateFiles(root).Should().ContainSingle(
                "junto al archivo ya no queda nada: el sitio viejo se declara, no se supone");
            context.Logs.Should().Contain(message => message.Contains("salida por omisión") && message.Contains(output),
                "el log tiene que decir dónde fue el comprimido, que es justo lo que el flujo no declaró");
        }
        finally
        {
            Clean(root, output);
        }
    }

    /// <summary>
    /// <b>La salida del flujo puede ser, a su vez, una plantilla</b>: el catálogo de ejemplos entero declara
    /// <c>{RelativeDir}</c>, y ahí «la carpeta de salida del flujo» significa la estructura del origen. Sin expandir y
    /// anclar ese valor, <c>{GlobalOutputDir}</c> valía la plantilla literal y el anclaje la combinaba consigo mismo
    /// (<c>{RelativeDir}\{RelativeDir}</c>), dejando una ruta relativa que acababa absolutizada contra el directorio
    /// de trabajo del proceso —medido: <c>bin/Debug/net10.0/{RelativeDir}/{RelativeDir}</c>—, que es la forma del
    /// defecto del hito 204. Aquí se fija el destino <b>por su valor</b> —la carpeta del archivo, dentro del
    /// origen—: el defecto deja el comprimido en otro sitio, y una carpeta llamada <c>{RelativeDir}</c> creada junto
    /// al proceso sobrevive a la corrección, así que preguntarle al disco por ella mediría lo que quedó de la
    /// última vez que algo falló, no lo que este nodo hizo.
    /// </summary>
    [Fact]
    public async Task AFlowWhoseOutputFolderIsATemplate_ShouldAnchorTheArchiveInARealFolder()
    {
        string root = NewDirectory();
        string source = Path.Combine(root, "Entrada");
        string subFolder = Path.Combine(source, "sub");
        Directory.CreateDirectory(subFolder);
        string input = Path.Combine(subFolder, "nota.txt");
        await File.WriteAllTextAsync(input, "contenido que se quería guardar comprimido");

        try
        {
            var node = new ArchiveCompressorNode();
            var item = new FileItemContext(input);
            item.Metadata["SourceRootPath"] = source;
            item.Metadata["GlobalOutputDir"] = "{RelativeDir}";

            var context = new ProbeFlowContext { Storage = new PhysicalStorageService() };

            await node.ExecuteAsync("In", item, context, CancellationToken.None);

            context.EmittedPorts.Should().ContainSingle().Which.Should().Be("Out");
            context.EmittedItems.Single().CurrentPath.Should().Be(Path.Combine(subFolder, "nota.zip"),
                "'{RelativeDir}' como salida del flujo significa la carpeta del archivo dentro del origen: una plantilla " +
                "que se queda sin expandir acaba dejando el comprimido en una carpeta con ese nombre junto al proceso");
        }
        finally
        {
            Clean(root);
        }
    }

    /// <summary>
    /// <b>El nombre heredado del parámetro (<c>DestinationDirectory</c>) manda sobre el valor de fábrica.</b> No
    /// aparece en la ficha del nodo, así que un valor ahí sólo puede venir de un flujo guardado con el nombre viejo:
    /// uno que sí declaró dónde escribe. El valor de fábrica —que la instancia trae hoy puesto— no puede taparlo, o el
    /// cambio de valor por omisión movería en silencio la salida de esos flujos; con el valor moderno declarado de
    /// verdad, en cambio, manda el moderno.
    /// </summary>
    [Fact]
    public async Task ALegacyDestination_ShouldBeatTheFactoryDefault_ButNotADeclaredOne()
    {
        string root = NewDirectory();
        string legacy = NewDirectory();
        string declared = NewDirectory();
        string input = Path.Combine(root, "nota.txt");
        await File.WriteAllTextAsync(input, "contenido que se quería guardar comprimido");

        try
        {
            var firstRun = new ArchiveCompressorNode();
            firstRun.Parameters["DestinationDirectory"] = legacy;
            firstRun.Parameters["ArchiveName"] = "heredado.zip";

            var context = new ProbeFlowContext { Storage = new PhysicalStorageService() };
            await firstRun.ExecuteAsync("In", new FileItemContext(input), context, CancellationToken.None);

            File.Exists(Path.Combine(legacy, "heredado.zip")).Should().BeTrue(
                "un flujo guardado con el nombre viejo del parámetro conserva su carpeta: el valor de fábrica no lo tapa");

            var secondRun = new ArchiveCompressorNode();
            secondRun.Parameters["DestinationDirectory"] = legacy;
            secondRun.Parameters["DestinationFolder"] = declared;
            secondRun.Parameters["ArchiveName"] = "declarado.zip";

            var secondContext = new ProbeFlowContext { Storage = new PhysicalStorageService() };
            await secondRun.ExecuteAsync("In", new FileItemContext(input), secondContext, CancellationToken.None);

            File.Exists(Path.Combine(declared, "declarado.zip")).Should().BeTrue(
                "y con el nombre moderno declarado de verdad, manda el moderno");
        }
        finally
        {
            Clean(root, legacy, declared);
        }
    }

    /// <summary>
    /// El valor de fábrica del parámetro es la salida del flujo, y se ve en la ficha: es la mitad del hito 209 que el
    /// log no puede dar —el usuario tiene que poder leer, sin ejecutar, dónde va a acabar su archivo—. La instancia y
    /// el descriptor tienen que decir lo mismo, porque la ficha muestra el descriptor y el motor lee la instancia.
    /// </summary>
    [Fact]
    public void TheFactoryDefaultOfTheCompressor_ShouldBeTheOutputFolderOfTheFlow()
    {
        var node = new ArchiveCompressorNode();

        node.Parameters["DestinationFolder"].Should().Be("{GlobalOutputDir}",
            "un compresor recién puesto en el lienzo escribe en la salida del flujo sin que nadie toque nada");

        var descriptor = node.ParameterDescriptors.Single(d => d.Key == "DestinationFolder");
        descriptor.DefaultValue.Should().Be("{GlobalOutputDir}",
            "y la ficha del parámetro muestra el mismo valor que ejecuta el motor, no la clave cruda ni un vacío");
    }

    /// <summary>
    /// La aclaración del parámetro viaja como recurso —<c>Param_DestinationFolder_Help</c>— en los dos idiomas, y es
    /// la frase que cierra el hueco: dónde acaba el comprimido por omisión y cómo se pide el sitio de antes. Se lee
    /// del propio ensamblado del plugin, no de la interfaz, así que la prueba falla si alguien traduce a medias.
    /// </summary>
    [Fact]
    public void TheDestinationParameter_ShouldExplainWhereTheArchiveGoes_InBothLanguages()
    {
        var resources = new ResourceManager("FileFlow.Plugin.Archives.Resources.Strings", typeof(ArchiveCompressorNode).Assembly);

        string? helpEnglish = resources.GetString("Param_DestinationFolder_Help", new CultureInfo("en"));
        string? helpSpanish = resources.GetString("Param_DestinationFolder_Help", new CultureInfo("es"));

        resources.GetString("Param_DestinationFolder", new CultureInfo("es")).Should().Be("Carpeta de Destino",
            "el parámetro del destino también tiene nombre propio en la ficha, no la clave cruda");
        helpSpanish.Should().Contain("salida del flujo",
            "la ficha dice dónde va el comprimido por omisión, sin obligar a ejecutar para averiguarlo");
        helpSpanish.Should().Contain("{CurrentDir}",
            "y dice cómo pedir el sitio de antes, que hoy es una declaración y ya no un respaldo escondido");
        helpEnglish.Should().Contain("{CurrentDir}", "la misma frase tiene que estar en el idioma de casa");
        helpEnglish.Should().NotBe(helpSpanish, "una traducción copiada no es una traducción");
    }

    /// <summary>
    /// <b>El flujo guardado antes del cambio</b>: el que trae <c>"DestinationFolder": ""</c> —que es como quedaron
    /// todos los flujos guardados por la aplicación, porque el valor de fábrica era vacío— y no declara carpeta de
    /// salida propia. Al ejecutarlo con el motor de verdad, el comprimido acaba en la salida que el lanzador le da
    /// —lo que en la aplicación es la salida por defecto de los ajustes— y no junto al archivo. Es la migración
    /// entera: no se reescribe ningún archivo, la regla alcanza también a lo ya guardado, y la carpeta la dice el
    /// log.
    /// </summary>
    [Fact]
    public async Task AFlowSavedWithoutADestination_ShouldWriteInTheOutputFolderTheLauncherHands()
    {
        string root = NewDirectory();
        string output = Path.Combine(root, "Output");
        string inputFolder = Path.Combine(output, "Input", "sub");
        Directory.CreateDirectory(inputFolder);
        await File.WriteAllTextAsync(Path.Combine(inputFolder, "nota.txt"), "contenido que se quería guardar comprimido");

        try
        {
            var graph = new WorkflowStorageService().DeserializeGraph(SavedFlowWithoutADestination);
            var loader = PluginRegistryHelper.CreateConfiguredLoader();
            var executor = new WorkflowExecutor
            {
                EnableCheckpointing = false,
                GlobalOutputDir = output,
                TemporaryDirectory = Path.Combine(root, "tmp")
            };

            await executor.ExecuteAsync(graph, loader, CancellationToken.None);

            File.Exists(Path.Combine(output, "nota.zip")).Should().BeTrue(
                "un flujo guardado sin carpeta de destino escribe en la salida que los ajustes dan por defecto");
            File.Exists(Path.Combine(inputFolder, "nota.zip")).Should().BeFalse(
                "y ya no junto al archivo, que es donde lo dejaba el valor de fábrica vacío");
        }
        finally
        {
            Clean(root);
        }
    }

    /// <summary>
    /// Un compresor tal y como quedó guardado por la aplicación con el valor de fábrica de antes: la clave está, y
    /// está vacía. Es el material de la migración, no un flujo inventado: el escritor del producto guarda el
    /// parámetro siempre, así que «no declara carpeta» es esto.
    /// </summary>
    private const string SavedFlowWithoutADestination = """
    {
      "schema": "FileFlow.Workflow.v2",
      "name": "Empaquetado guardado sin destino",
      "globalOutputDir": "",
      "temporaryDirectory": "",
      "nodes": [
        {
          "id": "node-src",
          "nodeTypeName": "FolderSourceNode",
          "x": 100,
          "y": 150,
          "hasBreakpoint": false,
          "isLoggingEnabled": true,
          "parameters": {}
        },
        {
          "id": "node-zip",
          "nodeTypeName": "ArchiveCompressorNode",
          "x": 350,
          "y": 150,
          "hasBreakpoint": false,
          "isLoggingEnabled": true,
          "parameters": {
            "DestinationFolder": "",
            "ArchiveFormat": "ZIP",
            "CompressionType": "Deflate"
          }
        }
      ],
      "edges": [
        {
          "id": "e1",
          "sourceNodeId": "node-src",
          "sourcePortName": "Out",
          "targetNodeId": "node-zip",
          "targetPortName": "In"
        }
      ],
      "annotations": [],
      "groups": [],
      "breakpointNodeIds": [],
      "disabledLoggingNodeIds": []
    }
    """;

    private static string NewDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "FF_Compressor_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Clean(params string[] directories)
    {
        foreach (string directory in directories)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
