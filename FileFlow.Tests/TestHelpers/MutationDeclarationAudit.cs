using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// Auditoría de las <b>mutaciones declaradas</b> (<c>mutations/*.json</c>), las que ejecuta <c>mutate.ps1</c>.
///
/// <para>Una mutación es una afirmación sobre el suite —«esto lo detecta esta prueba»— y una afirmación así se
/// pudre en silencio: el producto cambia, el fragmento que se sustituía ya no está, la prueba se renombra y el
/// filtro deja de casar. El andamiaje lo detecta <i>cuando alguien ejecuta la mutación</i>; esta auditoría lo
/// detecta en el suite, que es donde se mira a diario. Las tres preguntas son:</para>
///
/// <list type="number">
/// <item><description><b>¿El fragmento sigue existiendo, el número de veces que se declara?</b> Un fragmento que
/// desaparece es una declaración obsoleta, y el andamiaje la rechaza sin medir nada.</description></item>
/// <item><description><b>¿El testigo nombra una prueba que existe?</b> Es la pregunta que más importa: un filtro
/// que no casa con ninguna prueba <i>no falla</i> —<c>dotnet test</c> sale con 0 cuando no hay coincidencias—, así
/// que un testigo renombrado se leería como «la mutación sobrevivió» y un control renombrado como «control
/// verde», que es un veredicto preciso sobre una medición vacía.</description></item>
/// <item><description><b>¿El fichero del id es el que se llama como el id?</b> Las definiciones se leen por
/// nombre: si el id y el fichero se separan, la mitad de las trazas apuntan a otro sitio.</description></item>
/// </list>
///
/// <para>Los fragmentos se declaran con <c>\n</c> y el producto está en CRLF, así que <b>todo se compara con los
/// terminadores normalizados</b>: es la misma regla que aplica el andamiaje antes de sustituir.</para>
/// </summary>
public static class MutationDeclarationAudit
{
    /// <summary>Una sustitución literal declarada, con las veces que tiene que aparecer.</summary>
    public sealed record Replacement(string Old, string New, int Count);

    /// <summary>Un fichero del producto que la mutación toca.</summary>
    public sealed record Edit(string File, IReadOnlyList<Replacement> Replacements);

    /// <summary>Un filtro de <c>dotnet test</c> que tiene que ponerse rojo (testigo) o seguir verde (control).</summary>
    public sealed record Filter(string Expression, string? Why);

    /// <summary>Una mutación declarada, tal y como la lee el andamiaje.</summary>
    public sealed record Declaration(
        string Id,
        string Origin,
        string Claim,
        IReadOnlyList<Edit> Edits,
        Filter? Witness,
        Filter? Control);

    /// <summary>El sufijo que reconoce este auditor: un nombre de prueba o de clase del suite.</summary>
    private const string FilterPrefix = "FullyQualifiedName~";

    private static readonly char[] _lineSeparators = ['\n'];

    /// <summary>
    /// Las mutaciones declaradas en <c>mutations/*.json</c>, en orden de fichero. Lee de forma tolerante —un campo
    /// que falta no lanza, se audita— para que el auditor pueda <i>decir</i> que falta en vez de reventar.
    /// </summary>
    public static IReadOnlyList<Declaration> Declarations(string repositoryRoot)
    {
        ArgumentNullException.ThrowIfNull(repositoryRoot);

        string directory = Path.Combine(repositoryRoot, "mutations");
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var declarations = new List<Declaration>();
        IEnumerable<string> files = Directory
            .EnumerateFiles(directory, "*.json")
            .OrderBy(path => path, StringComparer.Ordinal);

        foreach (string file in files)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            JsonElement root = document.RootElement;

            var edits = new List<Edit>();
            foreach (JsonElement edit in Many(root, "edits"))
            {
                var replacements = new List<Replacement>();
                foreach (JsonElement replacement in Many(edit, "replacements"))
                {
                    replacements.Add(new Replacement(
                        Text(replacement, "old") ?? string.Empty,
                        Text(replacement, "new") ?? string.Empty,
                        Number(replacement, "count") ?? 1));
                }

                edits.Add(new Edit(Text(edit, "file") ?? string.Empty, replacements));
            }

            declarations.Add(new Declaration(
                Text(root, "id") ?? Path.GetFileNameWithoutExtension(file),
                Path.GetFileName(file),
                Text(root, "claim") ?? string.Empty,
                edits,
                ReadFilter(root, "witness"),
                ReadFilter(root, "control")));
        }

        return declarations;
    }

    /// <summary>
    /// Los nombres que un filtro del suite puede citar: los <b>métodos de prueba</b> y las <b>clases de prueba</b>
    /// (un filtro del suite casa con cualquiera de los dos, y una mutación puede declarar como testigo una clase
    /// entera cuando el defecto rompe varios casos de la misma).
    /// </summary>
    public static IReadOnlySet<string> SuiteNames(string repositoryRoot)
    {
        var names = new HashSet<string>(TestSuiteIndex.MethodNames(repositoryRoot), StringComparer.Ordinal);

        foreach ((string _, string source) in SourceTree.TestFiles(repositoryRoot))
        {
            foreach (string type in DeclaredTypes(SourceText.WithoutComments(source)))
            {
                names.Add(type);
            }
        }

        return names;
    }

    /// <summary>
    /// Las infracciones de una declaración: fragmento que ya no está (o no las veces que se declara), fichero
    /// inexistente, testigo ausente, filtro que no nombra ninguna prueba del suite o id que no coincide con su
    /// fichero. Una lista vacía significa que todas las mutaciones declaradas siguen midiendo lo que dicen.
    /// </summary>
    public static IReadOnlyList<string> Audit(
        IEnumerable<Declaration> declarations,
        Func<string, string?> readFile,
        IReadOnlySet<string> suiteNames)
    {
        ArgumentNullException.ThrowIfNull(declarations);
        ArgumentNullException.ThrowIfNull(readFile);
        ArgumentNullException.ThrowIfNull(suiteNames);

        var infractions = new List<string>();

        foreach (Declaration declaration in declarations)
        {
            string expectedId = Path.GetFileNameWithoutExtension(declaration.Origin);
            if (!string.Equals(declaration.Id, expectedId, StringComparison.Ordinal))
            {
                infractions.Add($"'{declaration.Origin}': el id '{declaration.Id}' no coincide con el nombre del fichero.");
            }

            if (string.IsNullOrWhiteSpace(declaration.Claim))
            {
                infractions.Add($"'{declaration.Id}': no declara tesis (claim): una mutación sin tesis no dice qué defiende.");
            }

            if (declaration.Witness is null)
            {
                infractions.Add($"'{declaration.Id}': no declara testigo: sin testigo no hay nada que morder.");
            }

            foreach (Filter? filter in new[] { declaration.Witness, declaration.Control })
            {
                if (filter is null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(filter.Expression))
                {
                    infractions.Add($"'{declaration.Id}': un filtro está vacío.");
                    continue;
                }

                foreach (string term in filter.Expression.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!term.StartsWith(FilterPrefix, StringComparison.Ordinal))
                    {
                        infractions.Add($"'{declaration.Id}': el filtro '{term}' no es de la forma que este auditor sabe juzgar ('{FilterPrefix}Nombre').");
                        continue;
                    }

                    // El '~' del filtro es coincidencia PARCIAL (así lo aplica `dotnet test`), así que aquí no
                    // se exige el nombre exacto: se exige que case con algún caso o clase del suite. Un testigo
                    // citado por su principio —la declaracion de un metodo de teoria sin su sufijo— es legitimo.
                    string name = term[FilterPrefix.Length..].Trim();
                    if (!suiteNames.Any(entry => entry.Contains(name, StringComparison.Ordinal)))
                    {
                        infractions.Add($"'{declaration.Id}': el filtro '{term}' no casa con ninguna prueba del suite (¿se renombró la prueba?): sin prueba, un testigo se lee como superviviente y un control como verde.");
                    }
                }
            }

            foreach (Edit edit in declaration.Edits)
            {
                if (string.IsNullOrWhiteSpace(edit.File))
                {
                    infractions.Add($"'{declaration.Id}': una edición no declara fichero.");
                    continue;
                }

                string? content = readFile(edit.File);
                if (content is null)
                {
                    infractions.Add($"'{declaration.Id}': el fichero '{edit.File}' no existe.");
                    continue;
                }

                string source = Normalize(content);
                foreach (Replacement replacement in edit.Replacements)
                {
                    if (replacement.Count < 1)
                    {
                        infractions.Add($"'{declaration.Id}': en '{edit.File}' una sustitución declara aparecer {replacement.Count} vez/veces.");
                        continue;
                    }

                    int found = CountOccurrences(source, Normalize(replacement.Old));
                    if (found != replacement.Count)
                    {
                        infractions.Add($"'{declaration.Id}': en '{edit.File}' el fragmento aparece {found} vez/veces y la mutación declara {replacement.Count} (¿cambió el producto?).");
                    }
                }
            }
        }

        return infractions;
    }

    /// <summary>Compara con los terminadores normalizados: es la regla que aplica el andamiaje antes de sustituir.</summary>
    public static string Normalize(string text) => text.Replace("\r\n", "\n");

    // ── Cobertura: qué subsistemas del producto tienen mutación declarada y qué guardias la tienen ────────

    /// <summary>Etiqueta del suite: mutar sus ficheros no cubre ningún subsistema del producto.</summary>
    public const string TestInfrastructure = "FileFlow.Tests (infraestructura de pruebas)";

    /// <summary>Los proyectos del producto: los directorios <c>FileFlow.*</c> menos el del suite.</summary>
    public static IReadOnlyList<string> ProductProjects(string repositoryRoot)
    {
        ArgumentNullException.ThrowIfNull(repositoryRoot);

        return
        [
            .. Directory.EnumerateDirectories(repositoryRoot, "FileFlow.*")
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name) && !name.Equals("FileFlow.Tests", StringComparison.Ordinal))
                .Select(name => name!)
                .OrderBy(name => name, StringComparer.Ordinal)
        ];
    }

    /// <summary>Un subsistema y las mutaciones declaradas que tocan sus ficheros.</summary>
    public sealed record SubsystemCoverage(
        string Subsystem,
        bool IsProduct,
        IReadOnlyList<string> Mutations,
        IReadOnlyList<string> Files);

    /// <summary>
    /// La cobertura por subsistema: <b>una fila por proyecto del producto —con o sin mutaciones— más el suite</b>,
    /// que es lo que permite publicar los huecos en vez de solo lo cubierto. Se clasifica por el proyecto del
    /// fichero que la mutación toca: una mutación sobre la <i>declaración</i> de una guardia (el censo, el
    /// analizador) cuenta como suite, por honesto que sea el testigo que la muerde.
    /// </summary>
    public static IReadOnlyList<SubsystemCoverage> Coverage(
        IEnumerable<string> productProjects,
        IReadOnlyList<Declaration> declarations)
    {
        ArgumentNullException.ThrowIfNull(productProjects);
        ArgumentNullException.ThrowIfNull(declarations);

        var projects = productProjects.ToList();
        var mutations = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var files = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (string project in projects) { mutations[project] = []; files[project] = []; }
        mutations[TestInfrastructure] = [];
        files[TestInfrastructure] = [];

        foreach (Declaration declaration in declarations)
        {
            foreach (string file in declaration.Edits.Select(e => e.File).Distinct(StringComparer.Ordinal))
            {
                string project = file.Split('/')[0];
                string subsystem = projects.Contains(project) ? project : TestInfrastructure;

                if (!mutations[subsystem].Contains(declaration.Id, StringComparer.Ordinal)) mutations[subsystem].Add(declaration.Id);
                if (!files[subsystem].Contains(file, StringComparer.Ordinal)) files[subsystem].Add(file);
            }
        }

        var rows = new List<SubsystemCoverage>();
        foreach (string project in projects)
        {
            rows.Add(new SubsystemCoverage(project, IsProduct: true, mutations[project], files[project]));
        }
        rows.Add(new SubsystemCoverage(TestInfrastructure, IsProduct: false, mutations[TestInfrastructure], files[TestInfrastructure]));
        return rows;
    }

    /// <summary>Una guardia del repositorio y las mutaciones que la citan como testigo.</summary>
    public sealed record GuardCoverage(string File, IReadOnlyList<string> Mutations);

    /// <summary>
    /// Las <b>guardias que auditan el repositorio</b> —las pruebas que leen el árbol de fuentes, reconocidas por
    /// usar los ayudantes de auditoría— y qué mutaciones las citan como testigo. Es la otra mitad de la cobertura:
    /// una guardia sin ninguna mutación que la muerda está escrita, pero nadie ha demostrado que muerda.
    /// </summary>
    public static IReadOnlyList<GuardCoverage> RepositoryGuardCoverage(
        string repositoryRoot,
        IReadOnlyList<Declaration> declarations)
    {
        ArgumentNullException.ThrowIfNull(repositoryRoot);
        ArgumentNullException.ThrowIfNull(declarations);

        var namesByFile = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (TestSuiteIndex.TestBlock block in TestSuiteIndex.Blocks(repositoryRoot))
        {
            if (!namesByFile.TryGetValue(block.File, out List<string>? names))
            {
                namesByFile[block.File] = names = [];
            }
            names.Add(block.Name);
        }

        var witnesses = new List<(string Mutation, string Term)>();
        foreach (Declaration declaration in declarations)
        {
            if (declaration.Witness is null) continue;

            foreach (string term in declaration.Witness.Expression.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                witnesses.Add((declaration.Id, term.StartsWith(FilterPrefix, StringComparison.Ordinal) ? term[FilterPrefix.Length..].Trim() : term));
            }
        }

        var guards = new List<GuardCoverage>();
        foreach ((string file, string source) in SourceTree.TestFiles(repositoryRoot))
        {
            if (!_guardMarkers.Any(marker => source.Contains(marker, StringComparison.Ordinal))) continue;

            string fileName = Path.GetFileNameWithoutExtension(file);
            var citing = new List<string>();
            foreach ((string mutation, string term) in witnesses)
            {
                bool cites = fileName.Contains(term, StringComparison.Ordinal)
                    || (namesByFile.TryGetValue(file, out List<string>? names) && names.Any(name => name.Contains(term, StringComparison.Ordinal)));

                if (cites && !citing.Contains(mutation, StringComparer.Ordinal)) citing.Add(mutation);
            }

            guards.Add(new GuardCoverage(file, citing));
        }

        return guards;
    }

    /// <summary>Los ayudantes que delatan que una prueba audita el repositorio en vez de un comportamiento.</summary>
    private static readonly string[] _guardMarkers = ["SourceTree.", "TestRepositoryLocator.", "TestSuiteIndex."];

    /// <summary>
    /// El documento publicado de la cobertura (<c>mutations/COVERAGE.md</c>): qué declara cada mutación, los
    /// subsistemas del producto que <b>no</b> tienen ninguna y las guardias que nadie ha demostrado que muerdan.
    /// </summary>
    public static string RenderCoverage(string repositoryRoot, IReadOnlyList<Declaration> declarations)
    {
        ArgumentNullException.ThrowIfNull(repositoryRoot);
        ArgumentNullException.ThrowIfNull(declarations);

        var coverage = Coverage(ProductProjects(repositoryRoot), declarations);
        var product = coverage.Where(row => row.IsProduct).ToList();
        var covered = product.Where(row => row.Mutations.Count > 0).ToList();
        var gaps = product.Where(row => row.Mutations.Count == 0).ToList();
        var infrastructure = coverage.Single(row => !row.IsProduct);
        var guards = RepositoryGuardCoverage(repositoryRoot, declarations);
        var guardsWithout = guards.Where(guard => guard.Mutations.Count == 0).ToList();

        var text = new StringBuilder();
        text.AppendLine("# Cobertura de mutaciones");
        text.AppendLine();
        text.AppendLine("> Generado por `MutationDeclarationCoverageTests` a partir de `mutations/*.json` y del árbol de fuentes.");
        text.AppendLine("> **No se edita a mano.**");
        text.AppendLine("> Regenerar: `FILEFLOW_UPDATE_MUTATION_COVERAGE=1 dotnet test --filter MutationDeclarationCoverageTests`.");
        text.AppendLine();
        text.AppendLine($"Mutaciones declaradas: {declarations.Count}");
        text.AppendLine($"Subsistemas del producto con alguna mutación: {covered.Count} de {product.Count}");
        text.AppendLine($"Guardias que auditan el repositorio con mutación que las muerda: {guards.Count - guardsWithout.Count} de {guards.Count}");
        text.AppendLine();
        text.AppendLine("## Qué declara cada mutación");
        text.AppendLine();
        text.AppendLine("| Mutación | Fichero que muta | Testigo | Control |");
        text.AppendLine("| :--- | :--- | :--- | :--- |");
        foreach (Declaration declaration in declarations.OrderBy(d => d.Id, StringComparer.Ordinal))
        {
            string files = string.Join("<br>", declaration.Edits.Select(edit => $"`{edit.File}`"));
            text.AppendLine($"| `{declaration.Id}` | {files} | {Shorten(declaration.Witness)} | {Shorten(declaration.Control)} |");
        }
        text.AppendLine();
        text.AppendLine($"## Subsistemas del producto sin ninguna mutación declarada ({gaps.Count} de {product.Count})");
        text.AppendLine();
        text.AppendLine("Una mutación por comportamiento que importa; estos proyectos no tienen ninguna, así que ningún");
        text.AppendLine("defecto declarado demuestra que sus pruebas muerdan. Es la lista de trabajo, no un reproche.");
        text.AppendLine();
        if (gaps.Count == 0)
        {
            text.AppendLine("Ninguno: todos los proyectos del producto tienen al menos una mutación declarada.");
        }
        else
        {
            foreach (SubsystemCoverage gap in gaps) text.AppendLine($"- `{gap.Subsystem}`");
        }
        text.AppendLine();
        text.AppendLine($"## Guardias del repositorio sin ninguna mutación que las muerda ({guardsWithout.Count} de {guards.Count})");
        text.AppendLine();
        text.AppendLine("Las guardias que auditan el árbol (usan `SourceTree`, `TestRepositoryLocator` o `TestSuiteIndex`) y no").AppendLine("aparecen como testigo de ninguna mutación: están escritas, y nadie ha demostrado que muerdan.");
        text.AppendLine();
        foreach (GuardCoverage guard in guardsWithout)
        {
            text.AppendLine($"- `{guard.File}`");
        }
        if (guardsWithout.Count == 0) text.AppendLine("Ninguna: todas las guardias del repositorio son testigo de alguna mutación.");
        text.AppendLine();
        text.AppendLine("## Dónde muta cada declaración");
        text.AppendLine();
        foreach (SubsystemCoverage row in coverage.Where(r => r.Mutations.Count > 0))
        {
            text.AppendLine($"- **{row.Subsystem}**: {string.Join(", ", row.Mutations.Select(id => $"`{id}`"))}");
        }
        if (infrastructure.Mutations.Count > 0)
        {
            text.AppendLine();
            text.AppendLine($"Las que mutan la **declaración** de una guardia (el censo, el analizador) cuentan como infraestructura de pruebas, no como subsistema del producto: son {infrastructure.Mutations.Count}.");
        }

        return text.ToString();
    }

    private static string Shorten(Filter? filter) =>
        filter is null || string.IsNullOrWhiteSpace(filter.Expression)
            ? "—"
            : $"`{filter.Expression.Replace(FilterPrefix, string.Empty, StringComparison.Ordinal)}`";

    private static int CountOccurrences(string text, string fragment)
    {
        if (string.IsNullOrEmpty(fragment))
        {
            return 0;
        }

        int count = 0;
        int index = text.IndexOf(fragment, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = text.IndexOf(fragment, index + fragment.Length, StringComparison.Ordinal);
        }

        return count;
    }

    /// <summary>Los nombres de tipo declarados en una fuente, con un escáner lineal (nada de expresiones con retroceso).</summary>
    private static IEnumerable<string> DeclaredTypes(string code)
    {
        string[] keywords = ["class ", "record ", "struct "];

        foreach (string line in code.Split(_lineSeparators, StringSplitOptions.None))
        {
            string trimmed = line.TrimStart(' ', '\t');

            foreach (string keyword in keywords)
            {
                // Solo una declaración: la línea tiene que empezar por el modificador o por la palabra clave, no
                // por una instrucción que mencione un tipo ("var x = new Foo();" no declara nada).
                int start = trimmed.IndexOf(keyword, StringComparison.Ordinal);
                if (start < 0 || !IsDeclarationStart(trimmed[..start]))
                {
                    continue;
                }

                string rest = trimmed[(start + keyword.Length)..];
                int end = 0;
                while (end < rest.Length && (char.IsLetterOrDigit(rest[end]) || rest[end] == '_'))
                {
                    end++;
                }

                if (end > 0)
                {
                    yield return rest[..end];
                }
            }
        }
    }

    private static bool IsDeclarationStart(string prefix)
    {
        foreach (string word in prefix.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!word.Equals("public", StringComparison.Ordinal) &&
                !word.Equals("internal", StringComparison.Ordinal) &&
                !word.Equals("sealed", StringComparison.Ordinal) &&
                !word.Equals("abstract", StringComparison.Ordinal) &&
                !word.Equals("static", StringComparison.Ordinal) &&
                !word.Equals("partial", StringComparison.Ordinal) &&
                !word.Equals("file", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static Filter? ReadFilter(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement element) || element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new Filter(Text(element, "filter") ?? string.Empty, Text(element, "why"));
    }

    private static IEnumerable<JsonElement> Many(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement element))
        {
            return [];
        }

        return element.ValueKind switch
        {
            JsonValueKind.Array => element.EnumerateArray().ToArray(),
            JsonValueKind.Object => [element],
            _ => [],
        };
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;
}
