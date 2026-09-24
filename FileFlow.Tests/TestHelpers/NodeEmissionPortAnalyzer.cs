using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// Analizador estático de los <b>nombres de puerto</b> de un nodo: compara los puertos que declara como salida
/// con los nombres por los que <b>emite</b> de verdad.
///
/// <para><b>Por qué es una regla del proyecto</b>: el motor empareja los cables por nombre exacto
/// (<c>{nodo}:{puerto}</c>) y, si el nombre emitido no tiene ninguna arista, da el ítem por terminado como si
/// fuera una hoja legítima del grafo —sin error, sin aviso y sin nodo descendente—. La interfaz tampoco puede
/// dibujar el cable que falta, porque sólo dibuja desde los puertos declarados. Le pasó al nodo Fan-Out, que
/// declaraba <c>Out</c> y emitía por <c>ItemOut</c>: el flujo de recompresión de cómics moría después del
/// desempaquetado y terminaba en verde (hito 188). El aviso nuevo del motor (<c>Log_UndeclaredOutputPort</c>) lo
/// cuenta <i>cuando la rama se ejecuta</i>; esta guardia es la otra mitad: señala la rama mal escrita aunque
/// nadie la recorra todavía, que es justo el caso de las ramas de error y de omitido.</para>
///
/// <para><b>Qué no puede juzgar</b>: un nodo cuyos puertos se calculan en código (<c>BuildOutputPorts</c> a
/// partir de un parámetro o de un subgrafo) declara nombres que no están en el texto. Esos nodos se
/// <b>aplazan</b> y la guardia los declara explícitamente, en lugar de fingir una comprobación que no se hizo.
/// Los nombres simbólicos (<c>WellKnownPorts.Out</c>) sí se resuelven: en este repositorio el nombre de la
/// constante es el nombre del puerto.</para>
/// </summary>
public static class NodeEmissionPortAnalyzer
{
    /// <summary>Una clase cualquiera con lista de bases, ya leída: el barrido decide si es un nodo.</summary>
    public sealed record NodeClassCandidate(
        string File,
        string Class,
        IReadOnlyList<string> BaseNames,
        IReadOnlyList<string> DeclaredOutputs,
        IReadOnlyList<string> EmittedPorts,
        bool PortsComputedInCode);

    /// <summary>Contrato de emisión de un nodo, leído de su código.</summary>
    public sealed record NodeEmissionReport(
        string File,
        string Class,
        string BaseClass,
        IReadOnlyList<string> DeclaredOutputs,
        IReadOnlyList<string> EmittedPorts,
        bool PortsComputedInCode)
    {
        /// <summary>Puertos por los que emite sin declararlos: el defecto que esta guardia persigue.</summary>
        public IReadOnlyList<string> Violations =>
            [.. EmittedPorts.Except(DeclaredOutputs, StringComparer.Ordinal)];
    }

    /// <summary>Una clase del árbol, con su fichero y, si aplica, la base que no se pudo resolver.</summary>
    public sealed record NodeClassRef(string Class, string File, string? UnresolvedBase = null)
    {
        /// <summary>Texto para el mensaje de la guardia.</summary>
        public string Describe() =>
            UnresolvedBase is null ? $"{Class} ({File})" : $"{Class} ({File}), base '{UnresolvedBase}'";
    }

    /// <summary>
    /// Resultado del barrido: los nodos juzgados, las infracciones, los nodos aplazados por calcular sus
    /// puertos y las clases que <b>emiten</b> sin que su base se resuelva dentro del árbol (el punto ciego).
    /// </summary>
    public sealed record SweepResult(
        IReadOnlyList<NodeEmissionReport> Reports,
        IReadOnlyList<string> Violations,
        IReadOnlyList<NodeClassRef> DeferredClasses,
        IReadOnlyList<NodeClassRef> UndecidedClasses)
    {
        /// <summary>Ficheros que el barrido alcanzó, juzgando o aplazando: la cobertura, medida.</summary>
        public IReadOnlyList<string> SweptFiles =>
            [.. Reports.Select(r => r.File).Concat(DeferredClasses.Select(d => d.File)).Distinct(StringComparer.Ordinal)];
    }

    private const string NodeBase = "FlowNodeBase";
    private const string NodeInterface = "IFlowNode";

    /// <summary>Cabecera de clase con su lista de bases: el cuerpo va hasta la siguiente cabecera del fichero.</summary>
    private static readonly Regex ClassHeaderRegex = new(
        @"(?:public|internal)\s+(?:sealed\s+|abstract\s+|static\s+|partial\s+)*class\s+(?<name>\w+)\s*(?:<[^>]*>)?\s*:\s*(?<bases>[^\n{]+)",
        RegexOptions.Compiled);

    /// <summary>Un puerto declarado: literal o por constante de <c>WellKnownPorts</c>, seguido del tipo.</summary>
    private static readonly Regex DeclaredNameRegex = new(
        "\"(?<literal>[^\"]+)\"\\s*,\\s*typeof\\(|WellKnownPorts\\.(?<constant>\\w+)\\s*,\\s*typeof\\(",
        RegexOptions.Compiled);

    /// <summary>Un puerto emitido: literal o por constante de <c>WellKnownPorts</c>.</summary>
    private static readonly Regex EmittedNameRegex = new(
        "EmitAsync\\(\\s*(?:\"(?<literal>[^\"]+)\"|WellKnownPorts\\.(?<constant>\\w+))",
        RegexOptions.Compiled);

    private static readonly Regex OutputsAssignmentRegex = new(@"Outputs\s*=\s*(?<opener>[\[{])", RegexOptions.Compiled);
    private static readonly Regex OutputsAddRegex = new(@"Outputs\.Add(?:Range)?\s*(?<opener>\()", RegexOptions.Compiled);
    private static readonly Regex DynamicPortsRegex = new(@"BuildOutputPorts\s*\(", RegexOptions.Compiled);

    /// <summary>
    /// Analiza un fichero y devuelve una entrada por clase de nodo con su contrato de emisión.
    /// </summary>
    public static IReadOnlyList<NodeClassCandidate> Analyze(string source, string file)
    {
        ArgumentNullException.ThrowIfNull(source);

        string code = SourceText.WithoutComments(source);
        MatchCollection headers = ClassHeaderRegex.Matches(code);
        var candidates = new List<NodeClassCandidate>();

        for (int i = 0; i < headers.Count; i++)
        {
            Match header = headers[i];
            int bodyStart = header.Index + header.Length;
            int bodyEnd = i + 1 < headers.Count ? headers[i + 1].Index : code.Length;
            string body = code[bodyStart..bodyEnd];
            IReadOnlyList<string> baseNames = Bases.Split(header.Groups["bases"].Value);

            candidates.Add(new NodeClassCandidate(
                file,
                header.Groups["name"].Value,
                baseNames,
                DeclaredOutputs(body),
                EmittedPorts(body),
                PortsComputedInCode: DynamicPortsRegex.IsMatch(body)));
        }

        return candidates;
    }

    /// <summary>
    /// Barre el árbol de plugins completo: decide qué clases son nodos por su cadena de bases, resuelve los
    /// puertos que heredan, junta las infracciones y aparta las que no se pueden juzgar leyéndolas.
    ///
    /// <para>La pertenencia se resuelve con la cadena de bases y no con la lista de bases directas: los nodos
    /// de IA heredan de <c>AiFlowNodeBase</c>, que a su vez hereda de <c>FlowNodeBase</c>, y mirar sólo la base
    /// directa dejaba fuera a toda esa familia —una guardia que no ve a la mitad de los nodos no vigila nada—.</para>
    /// </summary>
    public static SweepResult Sweep(IEnumerable<(string File, string Source)> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        var candidates = files.SelectMany(f => Analyze(f.Source, f.File)).ToList();

        var declaredByClass = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var nextBaseByClass = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (NodeClassCandidate candidate in candidates)
        {
            if (!declaredByClass.TryGetValue(candidate.Class, out var declared))
            {
                declaredByClass[candidate.Class] = declared = new HashSet<string>(StringComparer.Ordinal);
            }

            declared.UnionWith(candidate.DeclaredOutputs);

            string? next = candidate.BaseNames.FirstOrDefault(IsClassLikeName);
            if (next != null) nextBaseByClass.TryAdd(candidate.Class, next);
        }

        var violations = new List<string>();
        var deferred = new List<NodeClassRef>();
        var undecided = new List<NodeClassRef>();
        var judged = new List<NodeEmissionReport>();

        foreach (NodeClassCandidate candidate in candidates)
        {
            IsNodeResult resolution = Resolve(candidate, nextBaseByClass, declaredByClass);

            if (!resolution.IsNode)
            {
                // Punto ciego: una clase que <b>parece</b> un nodo (declara puertos y emite) pero cuya base no se
                // resuelve dentro del árbol, así que no se puede saber qué declara ni juzgarla. No se calla.
                bool looksLikeANode = candidate.DeclaredOutputs.Count > 0 || candidate.PortsComputedInCode;
                if (looksLikeANode && candidate.EmittedPorts.Count > 0)
                {
                    undecided.Add(new NodeClassRef(candidate.Class, candidate.File, resolution.UnresolvedBase));
                }

                continue;
            }

            if (candidate.PortsComputedInCode)
            {
                deferred.Add(new NodeClassRef(candidate.Class, candidate.File));
                continue;
            }

            var report = new NodeEmissionReport(
                candidate.File,
                candidate.Class,
                resolution.UnresolvedBase ?? NodeBase,
                [.. resolution.InheritedOutputs.OrderBy(n => n, StringComparer.Ordinal)],
                candidate.EmittedPorts,
                PortsComputedInCode: false);

            judged.Add(report);

            foreach (string port in report.Violations)
            {
                violations.Add(
                    $"{report.Class} ({report.File}) emite por '{port}', que no declara. Declara: {Describe(report.DeclaredOutputs)}.");
            }
        }

        return new SweepResult(judged, violations, deferred, undecided);
    }

    private readonly record struct IsNodeResult(
        bool IsNode,
        HashSet<string> InheritedOutputs,
        string? UnresolvedBase);

    /// <summary>Sube la cadena de bases hasta un nodo, reuniendo los puertos que declara cada eslabón.</summary>
    private static IsNodeResult Resolve(
        NodeClassCandidate candidate,
        Dictionary<string, string> nextBaseByClass,
        Dictionary<string, HashSet<string>> declaredByClass)
    {
        var inherited = new HashSet<string>(candidate.DeclaredOutputs, StringComparer.Ordinal);

        if (candidate.BaseNames.Contains(NodeInterface))
        {
            return new IsNodeResult(true, inherited, null);
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);
        string? current = nextBaseByClass.GetValueOrDefault(candidate.Class);

        while (current != null && visited.Add(current))
        {
            if (current is NodeBase or NodeInterface)
            {
                return new IsNodeResult(true, inherited, null);
            }

            if (!nextBaseByClass.TryGetValue(current, out string? next))
            {
                return new IsNodeResult(false, inherited, current);
            }

            if (declaredByClass.TryGetValue(current, out var declaredByBase)) inherited.UnionWith(declaredByBase);
            current = next;
        }

        return new IsNodeResult(false, inherited, current);
    }

    /// <summary>
    /// ¿El nombre puede ser una clase base? Las interfaces se descartan por convención de nombre, que es lo que
    /// permite quedarse con «el eslabón que aporta puertos» en una lista como
    /// <c>FlowNodeBase, INodeCustomActionProvider</c>.
    /// </summary>
    private static bool IsClassLikeName(string name) => name.Length > 0 && !(name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1]));

    private static IReadOnlyList<string> DeclaredOutputs(string body)
    {
        var declared = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match match in OutputsAssignmentRegex.Matches(body))
        {
            TakeNames(Region(body, match.Index + match.Length - 1, match.Groups["opener"].Value), declared);
        }

        foreach (Match match in OutputsAddRegex.Matches(body))
        {
            TakeNames(Region(body, match.Index + match.Length - 1, match.Groups["opener"].Value), declared);
        }

        // Los puertos calculados también pueden declarar nombres literales (el caso por defecto de un switch).
        foreach (Match match in DynamicPortsRegex.Matches(body))
        {
            TakeNames(MethodBody(body, match.Index + match.Length - 1), declared);
        }

        return [.. declared.OrderBy(n => n, StringComparer.Ordinal)];
    }

    private static IReadOnlyList<string> EmittedPorts(string body)
    {
        var emitted = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match match in EmittedNameRegex.Matches(body))
        {
            emitted.Add(PortName(match));
        }

        return [.. emitted.OrderBy(n => n, StringComparer.Ordinal)];
    }

    private static void TakeNames(string region, HashSet<string> into)
    {
        foreach (Match match in DeclaredNameRegex.Matches(region))
        {
            into.Add(PortName(match));
        }
    }

    private static string PortName(Match match) =>
        match.Groups["literal"].Success ? match.Groups["literal"].Value : match.Groups["constant"].Value;

    /// <summary>Desde el delimitador abierto hasta su cierre, contando anidamiento.</summary>
    private static string Region(string text, int openerIndex, string opener)
    {
        string closer = opener == "[" ? "]" : opener == "{" ? "}" : ")";
        int depth = 0;

        for (int i = openerIndex; i < text.Length; i++)
        {
            if (text[i] == opener[0]) depth++;
            else if (text[i] == closer[0] && --depth == 0) return text[(openerIndex + 1)..i];
        }

        return text[openerIndex..];
    }

    /// <summary>Cuerpo del método cuya signatura acaba en la posición indicada: por expresión o por bloque.</summary>
    private static string MethodBody(string text, int signatureEnd)
    {
        int i = signatureEnd;
        while (i < text.Length && char.IsWhiteSpace(text[i])) i++;

        if (i + 1 < text.Length && text[i] == '=' && text[i + 1] == '>')
        {
            int end = text.IndexOf(';', i);
            return end < 0 ? text[i..] : text[i..end];
        }

        return i < text.Length && text[i] == '{' ? Region(text, i, "{") : string.Empty;
    }

    private static string Describe(IReadOnlyList<string> ports) =>
        ports.Count == 0 ? "(ninguno)" : string.Join(", ", ports);

    /// <summary>Nombres de las bases de una clase, sin genéricos ni espacios.</summary>
    private static class Bases
    {
        public static IReadOnlyList<string> Split(string headerBases) =>
            headerBases
                .Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(name => name.Split('<')[0])
                .Where(name => name.Length > 0)
                .ToArray();
    }
}
