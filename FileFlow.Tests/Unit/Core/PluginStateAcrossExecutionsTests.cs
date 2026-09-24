using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Core.Storage;
using FileFlow.Plugin.Data;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Hashing;
using FileFlow.Plugin.Logic;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Core;

/// <summary>
/// <b>Lo que un nodo deja detrás: dos ejecuciones en el mismo proceso.</b>
///
/// <para><c>EngineStateAcrossExecutionsTests</c> barrió el estado del <b>motor</b>. Esto barre el de los
/// <b>nodos y plugins</b>: el índice de hashes que decide qué fichero es repetido, el búfer que acumula un lote,
/// el índice de una tabla de datos que se guarda en un estático. Son cachés propias del plugin, y el motor las
/// reutiliza porque <b>las ejecuciones no comparten nodo pero sí proceso</b>: cada nodo se instancia otra vez
/// (<c>Activator.CreateInstance</c> por validación), y lo que sobrevive a una ejecución es justo lo que vive en
/// un estático del plugin.</para>
///
/// <para>La forma de destaparlo es la del hito 200: <b>ejecutar dos veces</b>. Y como el estado puede estar en
/// dos sitios distintos, cada caso declara en qué se apoya —el índice del nodo o la caché estática del plugin—
/// y juzga el <b>resultado</b>, no el objeto: qué ficheros acaban clasificados, cuántos lotes salen, qué filas
/// se cruzan. Un índice que se hereda no rompe nada visible dentro del proceso: miente sobre los archivos.</para>
/// </summary>
public class PluginStateAcrossExecutionsTests
{
    private static PluginLoader CreateLoader()
    {
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(HashCalculatorNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(BatchBufferNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(DataLookupNode).Assembly);
        return loader;
    }

    [Fact]
    public async Task TheHashIndexOfOneRun_ShouldNotClassifyTheFilesOfTheNext()
    {
        // DeduplicationFilterNode lleva su propio índice de hashes vistos y lo reinicia cuando ve un
        // WorkflowExecutionId distinto. En el motor el nodo es nuevo en cada ejecución y el índice nace vacío,
        // así que lo que se juzga aquí es el resultado: si el índice sobreviviera, la segunda ejecución daría
        // por repetidos los ficheros que vio la primera.
        string sourceDir = TestDirectory("dedup-src");
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "uno.txt"), "contenido repetido");
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "dos.txt"), "contenido repetido");
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "tres.txt"), "contenido distinto");

        var loader = CreateLoader();

        string firstUnique = TestDirectory("dedup-unique-1");
        string firstDuplicates = TestDirectory("dedup-duplicate-1");
        await RunAsync(DeduplicationGraph(sourceDir, firstUnique, firstDuplicates), loader);

        FilesIn(firstUnique).Should().HaveCount(2, "dos de los tres ficheros traen contenido propio");
        FilesIn(firstDuplicates).Should().HaveCount(1, "el tercero repite el contenido de otro");

        string secondUnique = TestDirectory("dedup-unique-2");
        string secondDuplicates = TestDirectory("dedup-duplicate-2");
        await RunAsync(DeduplicationGraph(sourceDir, secondUnique, secondDuplicates), loader);

        FilesIn(secondUnique).Should().HaveCount(2,
            "la segunda ejecución vuelve a partir de un índice vacío: con el de la anterior, los dos ficheros " +
            "únicos de la primera vez contarían como repetidos");
        FilesIn(secondDuplicates).Should().HaveCount(1);
        FilesIn(secondUnique).Concat(FilesIn(secondDuplicates)).Should().BeEquivalentTo(["uno.txt", "dos.txt", "tres.txt"]);
    }

    [Fact]
    public async Task TheBatchBuffer_ShouldNotKeepPendingItemsForTheNextRun()
    {
        // BatchBufferNode acumula ítems hasta llenar el lote de dos, así que con un número impar de ficheros
        // siempre queda uno dentro al llegar el final... y sale entonces, porque el motor llama al gancho de fin
        // de flujo del nodo: no llenar el lote retrasa la entrega, no la cancela. Ese pendiente es además lo que
        // delataría una ejecución a la siguiente —cinco ficheros más el heredado suman seis y salen tres lotes—,
        // así que este caso juzga las dos cosas: cinco por ejecución, ni cuatro (el pendiente perdido) ni seis
        // (el pendiente heredado).
        string sourceDir = TestDirectory("buffer-src");
        for (int i = 0; i < 5; i++)
        {
            await File.WriteAllTextAsync(Path.Combine(sourceDir, $"item_{i}.txt"), "x");
        }

        var loader = CreateLoader();

        string firstDestination = TestDirectory("buffer-dst-1");
        await RunAsync(BufferGraph(sourceDir, firstDestination), loader);

        FilesIn(firstDestination).Should().HaveCount(5,
            "los lotes son de dos: cuatro ficheros salen al llenarse y el quinto, que quedó pendiente, al terminar la ejecución");

        string secondDestination = TestDirectory("buffer-dst-2");
        await RunAsync(BufferGraph(sourceDir, secondDestination), loader);

        FilesIn(secondDestination).Should().HaveCount(5,
            "la segunda ejecución entrega sus cinco ficheros y no los de la anterior: con el pendiente heredado, " +
            "los cinco más el heredado dan tres lotes de dos y salen seis");
        FilesIn(secondDestination).Should().BeEquivalentTo(FilesIn(firstDestination));
    }

    [Fact]
    public async Task TheLookupTableInMemory_ShouldBeTheOneOfThisRun()
    {
        // DataLookupTableLoader guarda el índice de cada tabla en un estático: sobrevive a la ejecución a
        // propósito —una tabla grande no se reparsea por ejecutar otra vez—, y por eso lo único que puede
        // decirle que la tabla ya no es la misma es la identidad del fichero.
        string tablePath = Path.Combine(TestDirectory("lookup-tables"), "tabla.csv");
        await File.WriteAllTextAsync(tablePath, "Id,Nombre\nLibro A,Alfa\n");

        string sourceDir = TestDirectory("lookup-src");
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "Libro A.csv"), "a");
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "Libro B.csv"), "b");

        var loader = CreateLoader();

        string firstMatched = TestDirectory("lookup-matched-1");
        string firstUnmatched = TestDirectory("lookup-unmatched-1");
        await RunAsync(LookupGraph(sourceDir, tablePath, firstMatched, firstUnmatched), loader);

        FilesIn(firstMatched).Should().BeEquivalentTo(["Libro A.csv"]);
        FilesIn(firstUnmatched).Should().BeEquivalentTo(["Libro B.csv"]);

        // La tabla se reescribe entre las dos ejecuciones: quien mande ahora es la fila de 'Libro B'.
        await File.WriteAllTextAsync(tablePath, "Id,Nombre,Nota\nLibro B,Beta,reescrita\n");

        string secondMatched = TestDirectory("lookup-matched-2");
        string secondUnmatched = TestDirectory("lookup-unmatched-2");
        await RunAsync(LookupGraph(sourceDir, tablePath, secondMatched, secondUnmatched), loader);

        FilesIn(secondMatched).Should().BeEquivalentTo(["Libro B.csv"],
            "la tabla cambió entre ejecuciones: el índice que quedó en el estático no puede contestar por ella");
        FilesIn(secondUnmatched).Should().BeEquivalentTo(["Libro A.csv"]);
    }

    [Fact]
    public async Task TheTableCache_ShouldNotAnswerWithRowsOfAFileThatChangedUnderTheSameTimestamp()
    {
        // El contrato de la caché, sin motor de por medio: sólo puede contestar con lo que sigue siendo el mismo
        // fichero. La fecha de escritura no basta —una copia con marcas de tiempo conservadas, o una edición en
        // el mismo tick del sistema de ficheros, deja la fecha intacta—, y el tamaño es lo que la delata.
        DataLookupTableLoader.ClearCache();

        string tablePath = Path.Combine(TestDirectory("tabla-fecha"), "clientes.csv");
        var storage = new PhysicalStorageService();
        DateTime timestamp = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        await File.WriteAllTextAsync(tablePath, "Id,Nombre\nA,Alfa\n");
        File.SetLastWriteTimeUtc(tablePath, timestamp);

        var firstRead = await DataLookupTableLoader.LoadLookupTableAsync(tablePath, "Id", CancellationToken.None, storage);
        firstRead["A"]["Nombre"].Should().Be("Alfa");

        // Reescritura de otro tamaño con la misma fecha: para un índice metadato a metadato, el mismo fichero.
        await File.WriteAllTextAsync(tablePath, "Id,Nombre,Nota\nA,Beta,reescrita\n");
        File.SetLastWriteTimeUtc(tablePath, timestamp);

        var secondRead = await DataLookupTableLoader.LoadLookupTableAsync(tablePath, "Id", CancellationToken.None, storage);

        secondRead["A"]["Nombre"].Should().Be("Beta",
            "el fichero cambió de contenido y de tamaño: la caché tenía que volver a leerlo aunque la fecha no " +
            "se haya movido");
    }

    [Fact]
    public async Task TheTableCache_ShouldNotGrowWithoutBoundAcrossRuns()
    {
        // La caché vive en un estático, así que cada tabla que cruza un flujo se queda en memoria hasta cerrar
        // el proceso. Sin tope, un lote de ejecuciones con tablas distintas —una por carpeta, una por idioma—
        // las retiene todas; el tope suelta las que nadie está usando.
        DataLookupTableLoader.ClearCache();

        string tablesDir = TestDirectory("tablas-muchas");
        var storage = new PhysicalStorageService();

        string firstTable = Path.Combine(tablesDir, "tabla_0.csv");
        await File.WriteAllTextAsync(firstTable, "Id,Nombre\nclave_0,valor_0\n");
        var firstIndex = await DataLookupTableLoader.LoadLookupTableAsync(firstTable, "Id", CancellationToken.None, storage);

        for (int i = 1; i < 24; i++)
        {
            string tablePath = Path.Combine(tablesDir, $"tabla_{i}.csv");
            await File.WriteAllTextAsync(tablePath, $"Id,Nombre\nclave_{i},valor_{i}\n");
            await DataLookupTableLoader.LoadLookupTableAsync(tablePath, "Id", CancellationToken.None, storage);
        }

        var firstTableAgain = await DataLookupTableLoader.LoadLookupTableAsync(firstTable, "Id", CancellationToken.None, storage);

        firstTableAgain.Should().NotBeSameAs(firstIndex,
            "la primera tabla es la que lleva más tiempo sin usarse: con veinticuatro tablas vivas tenía que " +
            "haber salido de la caché y volverse a leer");
        DataLookupTableLoader.CachedTableCount.Should().BeLessThan(24,
            "sin tope serían veinticuatro índices vivos hasta cerrar la aplicación");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Grafos: cada caso ejecuta su flujo dos veces, con destinos distintos por ejecución
    // para que el resultado de cada una se pueda leer entero.
    // ─────────────────────────────────────────────────────────────────────────────

    private static WorkflowGraph DeduplicationGraph(string sourceDir, string uniqueDir, string duplicateDir) => new()
    {
        Name = "estado-nodos-dedup-" + Guid.NewGuid().ToString("N"),
        Nodes =
        {
            new WorkflowNode
            {
                Id = "origen",
                NodeTypeName = "FolderSourceNode",
                Parameters = new Dictionary<string, object?> { ["SourcePath"] = sourceDir }
            },
            new WorkflowNode { Id = "hashes", NodeTypeName = "HashCalculatorNode" },
            new WorkflowNode { Id = "dedup", NodeTypeName = "DeduplicationFilterNode" },
            new WorkflowNode
            {
                Id = "unicos",
                NodeTypeName = "DestinationSinkNode",
                Parameters = new Dictionary<string, object?> { ["DestinationRoot"] = uniqueDir }
            },
            new WorkflowNode
            {
                Id = "repetidos",
                NodeTypeName = "DestinationSinkNode",
                Parameters = new Dictionary<string, object?> { ["DestinationRoot"] = duplicateDir }
            }
        },
        Edges =
        {
            new WorkflowEdge { SourceNodeId = "origen", SourcePortName = "Out", TargetNodeId = "hashes", TargetPortName = "In" },
            new WorkflowEdge { SourceNodeId = "hashes", SourcePortName = "Out", TargetNodeId = "dedup", TargetPortName = "In" },
            new WorkflowEdge { SourceNodeId = "dedup", SourcePortName = "Unique", TargetNodeId = "unicos", TargetPortName = "In" },
            new WorkflowEdge { SourceNodeId = "dedup", SourcePortName = "Duplicate", TargetNodeId = "repetidos", TargetPortName = "In" }
        }
    };

    private static WorkflowGraph BufferGraph(string sourceDir, string destinationDir) => new()
    {
        Name = "estado-nodos-buffer-" + Guid.NewGuid().ToString("N"),
        Nodes =
        {
            new WorkflowNode
            {
                Id = "origen",
                NodeTypeName = "FolderSourceNode",
                Parameters = new Dictionary<string, object?> { ["SourcePath"] = sourceDir }
            },
            new WorkflowNode
            {
                Id = "lotes",
                NodeTypeName = "BatchBufferNode",
                Parameters = new Dictionary<string, object?> { ["BatchSize"] = 2, ["MaxBatchSizeBytes"] = 0L }
            },
            new WorkflowNode
            {
                Id = "destino",
                NodeTypeName = "DestinationSinkNode",
                Parameters = new Dictionary<string, object?> { ["DestinationRoot"] = destinationDir }
            }
        },
        Edges =
        {
            new WorkflowEdge { SourceNodeId = "origen", SourcePortName = "Out", TargetNodeId = "lotes", TargetPortName = "ItemIn" },
            new WorkflowEdge { SourceNodeId = "lotes", SourcePortName = "ItemOut", TargetNodeId = "destino", TargetPortName = "In" }
        }
    };

    private static WorkflowGraph LookupGraph(string sourceDir, string tablePath, string matchedDir, string unmatchedDir) => new()
    {
        Name = "estado-nodos-lookup-" + Guid.NewGuid().ToString("N"),
        Nodes =
        {
            new WorkflowNode
            {
                Id = "origen",
                NodeTypeName = "FolderSourceNode",
                Parameters = new Dictionary<string, object?> { ["SourcePath"] = sourceDir }
            },
            new WorkflowNode
            {
                Id = "cruce",
                NodeTypeName = "DataLookupNode",
                Parameters = new Dictionary<string, object?>
                {
                    ["DataSourcePath"] = tablePath,
                    ["LookupKeyColumn"] = "Id",
                    ["MatchExpression"] = "{FileNameWithoutExtension}",
                    ["PrefixColumns"] = "Lookup_"
                }
            },
            new WorkflowNode
            {
                Id = "cruzados",
                NodeTypeName = "DestinationSinkNode",
                Parameters = new Dictionary<string, object?> { ["DestinationRoot"] = matchedDir }
            },
            new WorkflowNode
            {
                Id = "sinCruzar",
                NodeTypeName = "DestinationSinkNode",
                Parameters = new Dictionary<string, object?> { ["DestinationRoot"] = unmatchedDir }
            }
        },
        Edges =
        {
            new WorkflowEdge { SourceNodeId = "origen", SourcePortName = "Out", TargetNodeId = "cruce", TargetPortName = "In" },
            new WorkflowEdge { SourceNodeId = "cruce", SourcePortName = "Matched", TargetNodeId = "cruzados", TargetPortName = "In" },
            new WorkflowEdge { SourceNodeId = "cruce", SourcePortName = "Unmatched", TargetNodeId = "sinCruzar", TargetPortName = "In" }
        }
    };

    private static Task RunAsync(WorkflowGraph graph, PluginLoader loader) =>
        new WorkflowExecutor { EnableCheckpointing = false }.ExecuteAsync(graph, loader, CancellationToken.None);

    private static List<string> FilesIn(string directory) =>
        Directory.EnumerateFiles(directory).Select(path => Path.GetFileName(path)!).OrderBy(name => name, StringComparer.Ordinal).ToList();

    private static string TestDirectory(string label)
    {
        string path = Path.Combine(Path.GetTempPath(), $"FF_PluginState_{label}_{Guid.NewGuid().ToString("N")}");
        Directory.CreateDirectory(path);
        return path;
    }
}
