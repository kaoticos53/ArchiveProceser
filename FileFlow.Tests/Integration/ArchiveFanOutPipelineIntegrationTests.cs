using System.IO;
using System.IO.Compression;
using System.Linq;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.Archives;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Images;
using FileFlow.Sdk;
using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace FileFlow.Tests.Integration;

/// <summary>
/// El flujo de recompresión de cómics, ejecutado de verdad por el motor: carpeta origen → desempaquetado
/// Fan-Out → optimizador de imágenes → empaquetado Fan-In.
///
/// <para>Existe porque la parte del medio del flujo se caía sin decirlo: el Fan-Out emitía cada elemento
/// extraído por el puerto <c>ItemOut</c>, que <b>no declaraba</b> (declaraba <c>Out</c>), así que no había
/// arista que recogiera esos ítems y el motor los daba por terminados. Los nodos de aguas abajo no llegaban a
/// ejecutarse y en la consola sólo aparecían los logs del origen y del desempaquetador. Las pruebas de
/// Fan-Out y Fan-In que ya existían no podían verlo: cada una llama a un nodo con un contexto simulado que
/// acepta el nombre de puerto que se le pida. Esta prueba usa el grafo y el cableado reales.</para>
/// </summary>
public class ArchiveFanOutPipelineIntegrationTests
{
    [Fact]
    public async Task EndToEnd_FolderSourceToFanOutToImageOptimizerToFanIn_ShouldRunEveryNodeAndRepackTheArchive()
    {
        // Arrange: un cómic con una página, la misma forma que un .cbz real
        string root = Path.Combine(Path.GetTempPath(), "FF_FanOut_E2E_" + Guid.NewGuid().ToString("N"));
        string source = Path.Combine(root, "Comics");
        string temp = Path.Combine(root, "temp");
        string temp2 = Path.Combine(root, "temp2");
        string destination = Path.Combine(root, "Salida");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(temp);

        try
        {
            string pagePath = Path.Combine(root, "pagina01.png");
            using (var page = new Image<Rgba32>(16, 16)) page.Save(pagePath, new PngEncoder());

            string archivePath = Path.Combine(source, "comic.cbz");
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(pagePath, "pagina01.png");
            }

            var loader = new PluginLoader();
            loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
            loader.RegisterNodeTypesFromAssembly(typeof(ArchiveFanOutNode).Assembly);
            loader.RegisterNodeTypesFromAssembly(typeof(ImageOptimizerNode).Assembly);

            var graph = new WorkflowGraph { Name = "Recompresión de cómics", GlobalOutputDir = destination };
            graph.Nodes.Add(new WorkflowNode
            {
                Id = "origen",
                NodeTypeName = "FolderSourceNode",
                Parameters = new Dictionary<string, object?>
                {
                    ["SourcePath"] = source,
                    ["ExtensionFilter"] = "cbz, cbr",
                    ["Recursive"] = "True",
                    ["EmitMode"] = "FilesOnly",
                    ["MaxRecursionDepth"] = "-1"
                }
            });
            graph.Nodes.Add(new WorkflowNode
            {
                Id = "desempaquetar",
                NodeTypeName = "ArchiveFanOutNode",
                Parameters = new Dictionary<string, object?>
                {
                    ["OutputDirectory"] = temp + Path.DirectorySeparatorChar,
                    ["ArchiveFormat"] = "Auto",
                    ["PreserveDirectoryStructure"] = "True",
                    ["FilterPattern"] = "*.*"
                }
            });
            graph.Nodes.Add(new WorkflowNode
            {
                Id = "optimizar",
                NodeTypeName = "ImageOptimizerNode",
                Parameters = new Dictionary<string, object?>
                {
                    ["Width"] = "",
                    ["Height"] = "100%",
                    ["TargetFormat"] = "WebP",
                    ["Quality"] = "80",
                    ["OnlyDownscale"] = "True",
                    ["OutputDirectory"] = temp2 + Path.DirectorySeparatorChar,
                    ["KeepOriginalIfLarger"] = "True",
                    ["ReplaceOriginalInPlace"] = "False",
                    ["PassThroughNonImages"] = "False"
                }
            });
            graph.Nodes.Add(new WorkflowNode
            {
                Id = "empaquetar",
                NodeTypeName = "ArchiveFanInNode",
                Parameters = new Dictionary<string, object?>
                {
                    ["DestinationFolder"] = @"{GlobalOutputDir}\{RelativePath}",
                    ["ArchiveName"] = "{Archive:OriginalArchiveFileName}",
                    ["ArchiveFormat"] = "Auto",
                    ["CompressionType"] = "Deflate",
                    ["CleanWorkingFolder"] = "True",
                    ["TimeoutSeconds"] = "100"
                }
            });

            void Connect(string from, string toPort) => graph.Edges.Add(new WorkflowEdge
            {
                SourceNodeId = from,
                SourcePortName = "Out",
                TargetNodeId = toPort,
                TargetPortName = "In"
            });

            Connect("origen", "desempaquetar");
            Connect("desempaquetar", "optimizar");
            Connect("optimizar", "empaquetar");

            var executor = new WorkflowExecutor { GlobalOutputDir = destination, TemporaryDirectory = temp };
            var completedNodes = new System.Collections.Concurrent.ConcurrentDictionary<string, bool>();
            executor.NodeStatusChanged += (nodeId, status) =>
            {
                if (status == NodeExecutionStatus.Completed) completedNodes[nodeId] = true;
            };

            // Act
            await executor.ExecuteAsync(graph, loader, cancellationToken: CancellationToken.None);

            // Assert: los cuatro nodos se ejecutan y el cómic vuelve a estar empaquetado en destino
            completedNodes.Keys.Should().BeEquivalentTo(["origen", "desempaquetar", "optimizar", "empaquetar"],
                "un nodo de aguas abajo que no se ejecuta es exactamente el corte que este flujo tenía");

            string[] repacked = Directory.GetFiles(destination, "*.cbz", SearchOption.AllDirectories);
            repacked.Should().ContainSingle();
            Path.GetFileName(repacked[0]).Should().Be("comic.cbz",
                "el Fan-In conserva el nombre del archivo original");

            using var result = ZipFile.OpenRead(repacked[0]);
            result.Entries.Select(e => e.FullName).Should().ContainSingle()
                .Which.Should().Be("pagina01.webp", "la página pasó por el optimizador antes de reempaquetarse");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
