using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace FileFlow.Tests.TestHelpers;

/// <summary>Estado global de proceso que debe correr en una colección exclusiva.</summary>
public enum ExclusiveTestState
{
    /// <summary>Registro estático de sesiones de modelos (<c>ModelSessionRegistry</c>).</summary>
    ModelSessionRegistry,

    /// <summary>Gestor estático de sesiones ONNX (<c>OnnxSessionManager</c>).</summary>
    OnnxSessionManager,

    /// <summary>Singleton real de preferencias del usuario (no los dobles en memoria).</summary>
    RealUserPreferences,

    /// <summary>Sesión headless de Avalonia y su Dispatcher (helper, capturas, fixtures, UI).</summary>
    HeadlessUiSession,

    /// <summary>Tema activo del proceso y el diccionario de recursos de la aplicación (<c>ThemeManager</c>).</summary>
    ActiveTheme
}

/// <summary>
/// Analizador puro del <b>contrato de colecciones</b> del suite: dado el código de un fichero de test,
/// determina qué clases tocan un estado global de proceso que exige una colección exclusiva sin
/// declarar ninguna.
///
/// Es texto sobre el árbol del repositorio (como los lints de estilos), no análisis binario: suficiente
/// para la regla —"si mencionas el estado, declara la exclusividad"— y no necesita cargar ensamblados ni
/// depender de xUnit. La política que aplica es la documentada en el comentario de
/// <c>TestAssemblyParallelism.cs</c>, que es el mapa de referencia del suite.
///
/// Dos decisiones de diseño:
/// <list type="number">
///   <item><b>El análisis es por clase</b>, no por fichero: un fichero puede contener varias clases de
///   test con colecciones distintas, y el contrato es por clase. Cada segmento va desde la declaración de
///   una clase hasta la siguiente.</item>
///   <item><b>Declara cualquier colección exclusiva y basta</b>: <c>DisableParallelization = true</c>
///   ejecuta la colección en exclusividad total (mientras corre, no corre nada más), de modo que una
///   clase en <c>OnnxInference</c> que además mute las preferencias reales es correcta — todos los
///   estados que toca quedan serializados. Lo que el contrato no perdona es tocar el estado desde una
///   clase sin colección o en una colección paralela. La colección canónica de cada estado se usa para
///   orientar en el mensaje, no como única válida.</item>
/// </list>
///
/// La lógica vive en TestHelpers (y no dentro del propio guard) para poder auto-testearla con snippets
/// sintéticos: probar que la guardia detecta una infracción no debe exigir plantar ficheros infractores.
/// </summary>
public static class TestCollectionContractAnalyzer
{
    /// <summary>Todas las colecciones exclusivas del suite (con <c>DisableParallelization = true</c>).</summary>
    public static IReadOnlySet<string> ExclusiveCollections { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "VisualSnapshots",
        "OnnxInference",
        "AiModelDownloadSequential"
    };

    /// <summary>
    /// Reglas: qué patrones de código delatan el uso de cada estado y qué colección es la canónica para él.
    /// </summary>
    public static IReadOnlyList<ExclusiveStateRule> Rules { get; } =
    [
        new(
            ExclusiveTestState.ModelSessionRegistry,
            CanonicalCollection: "OnnxInference",
            Reason: "ModelSessionRegistry es un registro estático de sesiones de modelos del proceso: " +
                    "notifica cambios y vacía sesiones globalmente, así que sólo puede tocarse bajo " +
                    "exclusividad (colección OnnxInference; ver TestAssemblyParallelism.cs).",
            Patterns: [new(@"\bModelSessionRegistry\s*\.", RegexOptions.Compiled)]),

        new(
            ExclusiveTestState.OnnxSessionManager,
            CanonicalCollection: "OnnxInference",
            Reason: "OnnxSessionManager es un gestor estático con caché de sesiones ONNX nativas del " +
                    "proceso: crear, consultar o descargar sesiones es estado compartido global, y sólo " +
                    "puede tocarse bajo exclusividad (colección OnnxInference; ver " +
                    "TestAssemblyParallelism.cs).",
            Patterns: [new(@"\bOnnxSessionManager\s*\.", RegexOptions.Compiled)]),

        new(
            ExclusiveTestState.RealUserPreferences,
            CanonicalCollection: "VisualSnapshots",
            Reason: "UserPreferencesService.Instance es el singleton real (escribe en el perfil del " +
                    "usuario y dispara PreferencesChanged globalmente): mutarlo o suscribirse a él exige " +
                    "exclusividad (colección VisualSnapshots, que serializa cultura, idioma, preferencias " +
                    "reales y la sesión headless; ver TestAssemblyParallelism.cs). Para un test aislado usa " +
                    "el double InMemoryUserPreferencesService de TestHelpers.",
            Patterns:
            [
                new(@"\bUserPreferencesService\s*\.\s*(Instance|Singleton)", RegexOptions.Compiled),
                new(@"\bnew\s+UserPreferencesService\s*\(", RegexOptions.Compiled)
            ]),

        new(
            ExclusiveTestState.HeadlessUiSession,
            CanonicalCollection: "VisualSnapshots",
            Reason: "La sesión headless de Avalonia es un bucle de mensajes único por proceso con " +
                    "afinidad de hilo: crear controles, despachar al Dispatcher o capturar fotogramas " +
                    "fuera de una colección exclusiva compite con otras colecciones en paralelo y produce " +
                    "fallos de afinidad en pruebas ajenas (colección VisualSnapshots; ver " +
                    "TestAssemblyParallelism.cs).",
            Patterns:
            [
                new(@"\bAvaloniaTestHelper\s*\.", RegexOptions.Compiled),
                new(@"\bVisualSnapshot\s*\.", RegexOptions.Compiled),
                new(@"\bAppVisualFixture\b", RegexOptions.Compiled),
                new(@"\bHeadlessUnitTestSession\b", RegexOptions.Compiled),
                new(@"\bDispatcher\.UIThread\b", RegexOptions.Compiled),
                new(@"\bApplication\.Current\b", RegexOptions.Compiled)
            ]),

        new(
            ExclusiveTestState.ActiveTheme,
            CanonicalCollection: "VisualSnapshots",
            Reason: "Aplicar un tema escribe estado global de proceso: la definición activa (ThemeManager) y el " +
                    "diccionario de recursos de la aplicación, que las capturas headless están renderizando. " +
                    "Hacerlo desde una colección paralela cambia el tema a mitad de una captura y la " +
                    "comparación falla en la prueba equivocada —y sólo a veces—; debe declararse la colección " +
                    "exclusiva que confina el tema (VisualSnapshots; ver TestAssemblyParallelism.cs), que es la " +
                    "misma que usan VisualSnapshot y las capturas para aplicarlo.",
            Patterns: [new(@"\bThemeManager\s*\.\s*Instance\s*\.\s*SetTheme\w*\s*\(", RegexOptions.Compiled)])
    ];

    /// <summary>Regla de un estado: patrones que lo delatan y colección canónica que lo confina.</summary>
    public sealed record ExclusiveStateRule(
        ExclusiveTestState State,
        string CanonicalCollection,
        string Reason,
        IReadOnlyList<Regex> Patterns);

    /// <summary>Una infracción del contrato: qué estado toca la clase, qué se espera y por qué.</summary>
    public sealed record ContractViolation(ExclusiveTestState State, string CanonicalCollection, string Reason);

    /// <summary>Un segmento de análisis: una clase de test con su colección declarada (o null) y su cuerpo.</summary>
    public sealed record ClassSegment(string ClassName, string? DeclaredCollection, string Source);

    /// <summary>
    /// ¿Es un fichero de test sujeto al contrato? Cualquier .cs del proyecto de tests que no sea
    /// generado (<c>obj</c>/<c>bin</c>).
    /// </summary>
    public static bool IsTestFile(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        return normalized.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
            && normalized.Contains("Tests", StringComparison.OrdinalIgnoreCase)
            && !normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            && !normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>¿Es infraestructura de tests? Vive en <c>TestHelpers</c>: usa los estados en nombre de quien los declara.</summary>
    public static bool IsInfrastructure(string relativePath) =>
        relativePath.Replace('\\', '/').Contains("/TestHelpers/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Ficheros excluidos del barrido porque su contenido <b>es</b> el texto de prueba del propio
    /// analizador: los snippets sintéticos de los auto-tests contienen los patrones literalmente (un
    /// lint que se auto-testea necesariamente contiene infracciones de mentira en su fuente). Sin esta
    /// exclusión, la guardia se encontraría a sí misma y el barrido del árbol nunca pasaría.
    /// </summary>
    public static IReadOnlySet<string> SelfReferentialFiles { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "FileFlow.Tests/Unit/App/TestCollectionContractGuardTests.cs"
    };

    /// <summary>
    /// Parte un fichero en segmentos por clase: cada uno con el <c>[Collection]</c> que precede a su
    /// declaración (o null) y el código hasta la siguiente clase. Las clases sin atributo (las
    /// <c>CollectionDefinition</c>, los helpers locales) salen con colección null, que es también su
    /// semántica real en xUnit.
    /// </summary>
    public static IReadOnlyList<ClassSegment> SplitClasses(string source)
    {
        var matches = ClassDeclarationRegex.Matches(source);
        var segments = new List<ClassSegment>();

        for (int i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            int end = i + 1 < matches.Count ? matches[i + 1].Index : source.Length;
            string? declared = match.Groups["attr"].Success
                ? ResolveCollectionName(match.Groups["attr"].Value.Trim())
                : null;

            segments.Add(new ClassSegment(
                match.Groups["className"].Value,
                declared,
                source[match.Index..end]));
        }

        return segments;
    }

    /// <summary>
    /// Analiza un fichero de test clase a clase: devuelve las infracciones del contrato. Los ficheros de
    /// infraestructura (<c>TestHelpers</c>) y los que no son de test no se analizan.
    /// </summary>
    public static IReadOnlyList<ContractViolation> Analyze(string relativePath, string source)
    {
        var normalized = relativePath.Replace('\\', '/');

        if (!IsTestFile(relativePath) || IsInfrastructure(relativePath) || SelfReferentialFiles.Contains(normalized))
        {
            return [];
        }

        var violations = new List<ContractViolation>();

        foreach (var segment in SplitClasses(source))
        {
            bool isExclusive = segment.DeclaredCollection is { } name && ExclusiveCollections.Contains(name);

            foreach (var rule in Rules)
            {
                bool touchesState = rule.Patterns.Any(p => p.IsMatch(segment.Source));

                if (touchesState && !isExclusive)
                {
                    violations.Add(new ContractViolation(rule.State, rule.CanonicalCollection, rule.Reason));
                }
            }
        }

        return violations;
    }

    /// <summary>
    /// Normaliza el argumento del atributo: literal (<c>"Localization"</c>, comillas incluidas en el
    /// texto capturado) o referencia a la constante de la definición, con o sin cualificación de
    /// namespace (<c>VisualSnapshotsCollection.Name</c> o
    /// <c>FileFlow.Tests.Unit.Views.VisualSnapshotsCollection.Name</c>).
    /// </summary>
    private static string ResolveCollectionName(string attributeArgument)
    {
        var arg = attributeArgument.Trim();

        if (arg.Length >= 2 && arg.StartsWith('"') && arg.EndsWith('"'))
        {
            return arg[1..^1];
        }

        if (arg.EndsWith(".Name", StringComparison.Ordinal))
        {
            string definitionClass = arg[..^5].Split('.').Last();
            return CollectionDefinitionNames.TryGetValue(definitionClass, out var name)
                ? name
                : arg[..^5];
        }

        return arg;
    }

    /// <summary>Nombre de colección que define cada clase de definición del ensamblado.</summary>
    private static readonly Dictionary<string, string> CollectionDefinitionNames = new(StringComparer.Ordinal)
    {
        ["VisualSnapshotsCollection"] = "VisualSnapshots",
        ["OnnxInferenceCollection"] = "OnnxInference",
        ["AiModelDownloadSequentialCollection"] = "AiModelDownloadSequential"
    };

    /// <summary>
    /// Declaración de clase, con su <c>[Collection]</c> inmediatamente anterior si la hay. El grupo
    /// <c>attr</c> es opcional a propósito: así el regex también casa las clases sin atributo y el
    /// análisis las trata como «sin colección», que es su semántica real en xUnit.
    /// </summary>
    private static readonly Regex ClassDeclarationRegex = new(
        @"(?:\[Collection\(\s*(?<attr>(?:""[^""]+"")|(?:[A-Za-z_][A-Za-z0-9_.]*\.Name))\s*\)\]\s*(?://[^\r\n]*\s*|\[[^\]\r\n]*\]\s*)*)?" +
        @"(?:(?:public|internal|sealed|static|partial|abstract)\s+)*class\s+(?<className>\w+)",
        RegexOptions.Compiled);
}
