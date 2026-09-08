using FileFlow.Plugin.FileSystem.Services;
using FileFlow.Sdk.SyntheticData;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

public class SyntheticTreeDslParserTests
{
    [Fact]
    public void Parser_ShouldParseIndentedHierarchiesAndSizes()
    {
        string dsl = """
        Temporada 01/
            S01E01.mkv | size=1.5GB | res=1080p | codec=x265
            S01E02.mkv | size=1.4GB | res=1080p | codec=x265
        Temporada 02/
            S02E01.mkv | size=2GB | res=2160p
        documento_suelto.pdf | size=250KB | author=John Doe
        """;

        var items = SyntheticTreeDslParser.Parse(dsl);

        items.Should().HaveCount(6); // 2 carpetas + 4 archivos

        // Carpeta Temporada 01
        var t1Folder = items.First(i => i.FileName == "Temporada 01");
        t1Folder.IsDirectory.Should().BeTrue();

        // Archivos Temporada 01
        var ep1 = items.First(i => i.FileName == "S01E01.mkv");
        ep1.RelativePath.Should().Be("Temporada 01/S01E01.mkv");
        ep1.Directory.Should().Be("Temporada 01");
        ep1.FileSizeBytes.Should().Be((long)(1.5 * 1024 * 1024 * 1024));
        ep1.Metadata["res"]?.ToString().Should().Be("1080p");
        ep1.Metadata["codec"]?.ToString().Should().Be("x265");

        // Archivo en raíz
        var doc = items.First(i => i.FileName == "documento_suelto.pdf");
        doc.Directory.Should().BeEmpty();
        doc.FileSizeBytes.Should().Be(250 * 1024);
        doc.Metadata["author"]?.ToString().Should().Be("John Doe");
    }

    [Fact]
    public void Parser_ShouldParseSimulatedArchiveEntries()
    {
        string dsl = """
        Backups/
            paquete.zip | size=50MB [archive: manual.pdf | size=5MB; balance.xlsx | size=200KB; notas.txt]
        """;

        var items = SyntheticTreeDslParser.Parse(dsl);

        var zipItem = items.First(i => i.FileName == "paquete.zip");
        zipItem.IsArchive.Should().BeTrue();
        zipItem.FileSizeBytes.Should().Be(50L * 1024 * 1024);
        zipItem.SimulatedArchiveEntries.Should().HaveCount(3);

        zipItem.SimulatedArchiveEntries[0].InnerPath.Should().Be("manual.pdf");
        zipItem.SimulatedArchiveEntries[0].FileSizeBytes.Should().Be(5L * 1024 * 1024);

        zipItem.SimulatedArchiveEntries[1].InnerPath.Should().Be("balance.xlsx");
        zipItem.SimulatedArchiveEntries[1].FileSizeBytes.Should().Be(200L * 1024);

        zipItem.SimulatedArchiveEntries[2].InnerPath.Should().Be("notas.txt");
    }

    [Fact]
    public void Serializer_ShouldRoundTripWithParser()
    {
        var originalItems = new List<SyntheticFileDefinition>
        {
            new("Series/Breaking Bad/S01E01.mkv", 1024 * 1024 * 1024, false, new Dictionary<string, object?> { ["res"] = "1080p" }),
            new("Series/Breaking Bad/S01E02.mkv", 1024 * 1024 * 1024, false, new Dictionary<string, object?> { ["res"] = "1080p" }),
            new("Archivos/datos.zip", 5000000, false)
            {
                SimulatedArchiveEntries =
                [
                    new SyntheticArchiveEntryDefinition("doc1.txt", 1000),
                    new SyntheticArchiveEntryDefinition("doc2.pdf", 2000)
                ]
            }
        };

        string dsl = SyntheticTreeDslParser.Serialize(originalItems);
        dsl.Should().NotBeNullOrWhiteSpace();

        var reParsed = SyntheticTreeDslParser.Parse(dsl);
        reParsed.Should().Contain(i => i.FileName == "S01E01.mkv" && i.Metadata["res"] != null);
        reParsed.Should().Contain(i => i.FileName == "datos.zip" && i.SimulatedArchiveEntries.Count == 2);
    }
}
