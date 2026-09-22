using System.IO;
using System.Text.RegularExpressions;
using FileFlow.App.Services;
using FileFlow.Core.Plugins;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

/// <summary>
/// Guardia del catálogo de nodos (<c>.agents/nodes_catalog.md</c>): el documento tiene que ser exactamente el
/// que produce <see cref="NodeCatalogDocument"/> desde el catálogo que descubre el cargador de la aplicación,
/// cada fila tiene que corresponder a un nodo descubierto y cada enlace tiene que apuntar al fichero que
/// declara ese nodo.
///
/// <para>
/// Por qué existe: el catálogo era el documento que más mentía del repositorio —49 nodos de 70, con nodos
/// inexistentes y enlaces a proyectos que no existen— y nada podía notarlo, porque era texto escrito a mano que
/// nadie comparaba con nada. Es el mismo trato que la forma del archivo de flujo: lo que se puede derivar del
/// código se deriva y se compara, y lo que se escribe a mano se queda sin guardia.
/// </para>
/// </summary>
// Instancia nodos y crea el cargador configurado (registra recursos de los plugins en LocalizationManager,
// estado global del proceso): se serializa con la colección que ya confina ese estado.
[Collection("VisualSnapshots")]
public class NodeCatalogGuardTests
{
    [Fact]
    public void TheCatalog_ShouldBeWhatTheLoaderDiscovers()
    {
        string path = NodeCatalogDocument.PathOf(TestRepositoryLocator.RepositoryRoot());
        string generated = NodeCatalogDocument.Generate();

        if (NodeCatalogDocument.UpdateRequested)
        {
            File.WriteAllText(path, generated);
            return;
        }

        if (!File.Exists(path))
        {
            File.WriteAllText(path, generated);

            throw new InvalidOperationException(
                $"No existía el catálogo de nodos: se acaba de crear en '{path}'. Revísalo y vuelve a ejecutar " +
                "la prueba; esta primera ejecución falla a propósito para que el documento no se bendiga solo.");
        }

        // Se compara con los saltos de línea normalizados: el documento se genera siempre con \n para que sea
        // el mismo en cualquier máquina, y un archivo escrito por una herramienta de Windows con \r\n describe
        // exactamente el mismo catálogo.
        string committed = File.ReadAllText(path).Replace("\r\n", "\n");

        if (committed != generated)
        {
            Assert.Fail(
                $"'{NodeCatalogDocument.RelativePath}' ya no es lo que descubre el cargador de la aplicación.\n" +
                $"{FirstDifference(committed, generated)}\n" +
                "Regéneralo y revisa el resultado:\n" +
                "    FILEFLOW_UPDATE_NODE_CATALOG=1 dotnet test --filter NodeCatalogGuardTests");
        }
    }

    [Fact]
    public void EveryCatalogRow_ShouldBeADiscoveredNodeWithItsDeclaringSource()
    {
        string root = TestRepositoryLocator.RepositoryRoot();
        var rows = CatalogRows(File.ReadAllText(NodeCatalogDocument.PathOf(root)));

        rows.Should().NotBeEmpty("el catálogo tiene que listar los nodos del producto");

        PluginLoader loader = PluginRegistryHelper.CreateConfiguredLoader();
        var discovered = NodeCatalogDocument.ProductNodeTypes(loader).ToDictionary(type => type.Name, type => type);

        var problems = new List<string>();

        foreach (var (name, link) in rows)
        {
            if (!discovered.ContainsKey(name))
            {
                problems.Add($"'{name}' no lo descubre el cargador: o el nodo desapareció o la fila es de otro tiempo");
                continue;
            }

            string sourcePath = Path.Combine(root, link.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(sourcePath))
            {
                problems.Add($"'{name}' enlaza a '{link}', que no existe");
                continue;
            }

            bool declaresIt = NodeArchitectureAnalyzer
                .ConcreteNodeTypeFullNames(File.ReadAllText(sourcePath))
                .Any(fullName => fullName.EndsWith($".{name}", StringComparison.Ordinal));

            if (!declaresIt)
            {
                problems.Add($"'{name}' enlaza a '{link}', que no declara ese nodo");
            }
        }

        var listed = rows.Select(row => row.Name).ToHashSet(StringComparer.Ordinal);
        var missing = discovered.Keys.Except(listed).OrderBy(name => name, StringComparer.Ordinal).ToList();

        if (missing.Count > 0)
        {
            problems.Add($"sin fila en el catálogo: {string.Join(", ", missing)}");
        }

        problems.Should().BeEmpty(string.Join("\n", problems));
    }

    [Fact]
    public void TheSectionCounts_ShouldMatchTheRowsOfEachSection()
    {
        string catalog = File.ReadAllText(NodeCatalogDocument.PathOf(TestRepositoryLocator.RepositoryRoot()));

        var sections = SectionRows(catalog);

        sections.Should().NotBeEmpty("el catálogo agrupa los nodos por plugin");

        var problems = new List<string>();

        foreach (var (plugin, declaredCount, rows) in sections)
        {
            if (declaredCount != rows)
            {
                problems.Add($"'{plugin}' dice {declaredCount} nodos y lista {rows}");
            }
        }

        int total = sections.Sum(section => section.Rows);

        total.Should().Be(
            NodeCatalogDocument.ProductNodeTypes(PluginRegistryHelper.CreateConfiguredLoader()).Count,
            "la suma de las secciones tiene que ser el catálogo entero, no una parte");

        problems.Should().BeEmpty(string.Join("\n", problems));
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Lectura del documento
    // ─────────────────────────────────────────────────────────────────────────────

    private static readonly Regex RowPattern =
        new(@"^\| \*\*(?<node>[A-Za-z0-9_]+)\*\* \|.*\[`[^`]+`\]\(file:///(?<path>[^)]+)\) \|$", RegexOptions.Multiline);

    private static readonly Regex SectionPattern =
        new(@"^## (?<index>\d+)\. (?<plugin>\S+) \((?<count>\d+) nodos?\)$", RegexOptions.Multiline);

    /// <summary>Filas del catálogo: el nodo que nombran y el fichero al que enlazan.</summary>
    private static List<(string Name, string Link)> CatalogRows(string catalog) =>
        [.. RowPattern.Matches(Normalize(catalog))
            .Select(match => (match.Groups["node"].Value, match.Groups["path"].Value))];

    /// <summary>Secciones del catálogo: plugin, recuento declarado en el encabezado y filas que contiene.</summary>
    private static List<(string Plugin, int DeclaredCount, int Rows)> SectionRows(string catalog)
    {
        catalog = Normalize(catalog);

        var result = new List<(string, int, int)>();
        var headings = SectionPattern.Matches(catalog);

        for (int index = 0; index < headings.Count; index++)
        {
            int start = headings[index].Index;
            int end = index + 1 < headings.Count ? headings[index + 1].Index : catalog.Length;
            string body = catalog[start..end];

            result.Add((
                headings[index].Groups["plugin"].Value,
                int.Parse(headings[index].Groups["count"].Value),
                RowPattern.Matches(body).Count));
        }

        return result;
    }

    /// <summary>Saltos de línea normalizados: el documento es el mismo en cualquier máquina.</summary>
    private static string Normalize(string text) => text.Replace("\r\n", "\n");

    /// <summary>La primera línea en la que el documento y lo generado se separan, con su número de línea.</summary>
    private static string FirstDifference(string committed, string generated)
    {
        string[] expected = committed.Replace("\r\n", "\n").Split('\n');
        string[] actual = generated.Replace("\r\n", "\n").Split('\n');

        for (int line = 0; line < Math.Max(expected.Length, actual.Length); line++)
        {
            string left = line < expected.Length ? expected[line] : "<fin del archivo>";
            string right = line < actual.Length ? actual[line] : "<fin del archivo>";

            if (left != right)
            {
                return $"Primera diferencia en la línea {line + 1}:\n" +
                    $"  en el archivo: {left}\n" +
                    $"  lo que descubre el cargador: {right}";
            }
        }

        return "Los textos no coinciden, pero línea a línea son iguales (diferencia de fin de línea).";
    }
}
