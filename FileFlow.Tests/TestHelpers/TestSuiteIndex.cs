using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// Índice de los <b>métodos de prueba</b> que existen en el suite, leído de las fuentes.
///
/// <para>Las guardias de este proyecto escriben su evidencia como el nombre del método que ejecuta el caso, no
/// como una descripción: una frase que suena bien puede describir una prueba que ya no está, y entonces la
/// guardia vigila el recuerdo de alguien en vez del suite. Con este índice, citar una prueba que no existe es
/// un fallo.</para>
///
/// <para>Lee <b>fuentes</b> (no reflexión) a propósito: es la misma clase de análisis que el resto de las
/// guardias —texto sobre el árbol— y no necesita cargar el ensamblado de pruebas desde sí mismo.</para>
///
/// <para>El mismo análisis devuelve el <b>bloque</b> de cada caso, que es lo que permite comprobar algo más
/// fuerte que «la prueba existe» —lo único que mira <see cref="MethodNames"/>—: que el caso <i>habla del nodo y
/// del puerto que dice cubrir</i>. Un bloque se corta en el final del cuerpo del propio caso —no en la
/// declaración siguiente, que atribuía a un caso la tabla de datos del de abajo— y se compone de:</para>
///
/// <list type="number">
/// <item><description>sus <b>atributos, firma y cuerpo</b>, con los comentarios retirados: un comentario que
/// nombre un puerto no es una prueba;</description></item>
/// <item><description>y las <b>tablas de datos que el caso nombra</b>. En este repositorio un caso con parámetros
/// declara sus datos en una tabla (<c>TheoryData&lt;…&gt; MisCasos =&gt; new() { { "Nodo", "Puerto" } }</c>) y la
/// cita con <c>[MemberData(nameof(MisCasos))]</c>, un atributo que no contiene ni el nombre del nodo ni el del
/// puerto. Sin esto, un caso con parámetros no podría citar nada de lo que afirma; con esto, la tabla entra
/// como parte del caso <i>que la usa</i>, y la tabla de un vecino no cuenta para nadie. (Se buscan las tablas de
/// <c>TheoryData</c>, que son las que existen en el suite: analizar cualquier miembro de clase pedía una
/// expresión regular con retroceso catastrófico, y esta guardia no puede colgar el suite.)</description></item>
/// </list>
/// </summary>
public static class TestSuiteIndex
{
    /// <summary>Un caso de prueba: su nombre, el fichero que lo declara y su bloque (sin comentarios).</summary>
    public sealed record TestBlock(string Name, string File, string Source);

    /// <summary>
    /// Todos los casos de prueba del proyecto, sin la infraestructura de <c>TestHelpers</c> (que no contiene
    /// casos).
    /// </summary>
    public static IReadOnlyList<TestBlock> Blocks(string repositoryRoot)
    {
        var blocks = new List<TestBlock>();

        foreach ((string file, string source) in SourceTree.TestFiles(repositoryRoot))
        {
            blocks.AddRange(BlocksOf(file, source));
        }

        return blocks;
    }

    /// <summary>
    /// Nombres de todos los métodos de prueba del proyecto. Es la vista mínima de <see cref="Blocks"/>, para las
    /// guardias que solo comprueban que una prueba citada existe.
    /// </summary>
    public static IReadOnlySet<string> MethodNames(string repositoryRoot)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (TestBlock block in Blocks(repositoryRoot))
        {
            names.Add(block.Name);
        }

        return names;
    }

    /// <summary>
    /// Los casos de una fuente concreta, con su bloque. Es la mitad de <see cref="Blocks"/> que se puede probar
    /// sin tocar el árbol: el corte del bloque es una regla del analizador y se auto-testea con fuentes
    /// sintéticas.
    /// </summary>
    public static IReadOnlyList<TestBlock> BlocksOf(string file, string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        string code = SourceText.WithoutComments(source);
        var tables = TestDataTables(code);
        var blocks = new List<TestBlock>();

        foreach ((int start, bool isTest, string name, int signatureEnd) in Declarations(code))
        {
            if (!isTest) continue;

            // La signatura termina tras el paréntesis de cierre: hay que saltarla antes de buscar el cuerpo, o el
            // «final del bloque» sería la primera llave que aparezca dentro de un parámetro.
            int bodyEnd = BodyEnd(code, signatureEnd);
            string own = code[start..bodyEnd];
            var block = new StringBuilder(own);

            foreach ((string tableName, int tableStart, int tableEnd) in tables)
            {
                if (tableStart < bodyEnd && tableEnd > start) continue;   // es este mismo caso (u otro): no se duplica
                if (!Names(own, tableName)) continue;

                block.Append('\n').Append(code[tableStart..tableEnd]);
            }

            blocks.Add(new TestBlock(name, file, block.ToString()));
        }

        return blocks;
    }

    /// <summary>
    /// ¿Los atributos que preceden al método lo declaran como caso de prueba? Sin esto el índice incluiría
    /// cualquier <c>public void Dispose()</c> o <c>public Task ExecuteAsync()</c> de los dobles de prueba, y citar
    /// uno de esos nombres en el inventario pasaría por evidencia.
    /// </summary>
    private static bool IsTestAttribute(string attributes) =>
        TestAttributes.Any(attribute => attributes.Contains(attribute, StringComparison.Ordinal));

    private static readonly string[] TestAttributes = ["Fact", "Theory", "InlineData", "MemberData"];

    /// <summary>¿El texto nombra ese identificador como palabra completa?</summary>
    private static bool Names(string text, string identifier)
    {
        int index = text.IndexOf(identifier, StringComparison.Ordinal);

        while (index >= 0)
        {
            bool startsAWord = index == 0 || !IsWordCharacter(text[index - 1]);
            int after = index + identifier.Length;
            bool endsAWord = after >= text.Length || !IsWordCharacter(text[after]);

            if (startsAWord && endsAWord) return true;
            index = text.IndexOf(identifier, index + 1, StringComparison.Ordinal);
        }

        return false;
    }

    private static bool IsWordCharacter(char c) => char.IsLetterOrDigit(c) || c == '_';

    /// <summary>
    /// Recorre las <b>declaraciones de método</b> del código: la línea de atributos que las precede y el método
    /// que devuelve <c>Task</c> o <c>void</c>. Se busca la <b>declaración</b> (con paréntesis inmediato) y no una
    /// llamada, para que citar <c>Nombre(</c> en cualquier otro sitio del suite no cuente como que el caso
    /// existe.
    ///
    /// <para><b>Por qué a mano y no con una expresión regular</b>: la primera redacción era un patrón
    /// —<c>(?:\s*\[[^\]\r\n]*\]\s*)+public\s+…</c>— y sobre un fichero real del suite tardaba <b>103 s</b> en
    /// lugar de milisegundos: los <c>\s*</c> a los dos lados de una repetición hacen que un hueco de espacios se
    /// pueda repartir de infinitas maneras entre las repeticiones, y el motor prueba todas antes de fallar. El
    /// retroceso catastrófico no es un matiz de rendimiento en una guardia: es el suite entero colgado, que es
    /// lo que esta clase de análisis no puede permitirse. El recorrido de aquí es lineal y se auto-testea.</para>
    /// </summary>
    private static IEnumerable<(int Start, bool IsTest, string Name, int SignatureEnd)> Declarations(string code)
    {
        int i = 0;

        while (i < code.Length)
        {
            int at = code.IndexOf("public", i, StringComparison.Ordinal);
            if (at < 0) yield break;

            i = at + "public".Length;

            // «public» dentro de una palabra (una ruta, un nombre) no declara nada.
            if (at > 0 && IsWordCharacter(code[at - 1])) continue;

            int j = SkipSpace(code, i);
            if (Matches(code, j, "async")) j = SkipSpace(code, j + "async".Length);
            if (!Matches(code, j, "void") && !Matches(code, j, "Task")) continue;

            j = SkipSpace(code, j + 4);
            int nameStart = j;
            while (j < code.Length && IsWordCharacter(code[j])) j++;
            if (j == nameStart) continue;

            string name = code[nameStart..j];
            j = SkipSpace(code, j);
            if (j >= code.Length || code[j] != '(') continue;

            (int attributesStart, bool isTest) = AttributesBefore(code, at);
            yield return (attributesStart, isTest, name, MatchingDelimiter(code, j, ')') + 1);
        }
    }

    /// <summary>
    /// El bloque de atributos que precede a la declaración, y si alguno es de prueba. Los atributos se escriben
    /// uno por línea, así que se recorre hacia atrás una línea <c>[…]</c> cada vez —con la cuenta de corchetes
    /// para no partir un <c>[InlineData(new[] { 1 })]</c>— y se para en la primera línea que no lo sea.
    /// </summary>
    private static (int Start, bool IsTest) AttributesBefore(string code, int declarationStart)
    {
        int start = declarationStart;
        bool isTest = false;

        while (true)
        {
            int close = EndOfPreviousLineWithContent(code, start);
            if (close < 0 || code[close - 1] != ']') return (start, isTest);

            int open = MatchingBracketBackwards(code, close - 1);
            if (open < 0) return (start, isTest);

            // El atributo tiene que abrir su propia línea: si no, el corchete es un índice o un argumento.
            int attributeStart = open;
            while (attributeStart > 0 && IsHorizontalSpace(code[attributeStart - 1])) attributeStart--;
            if (attributeStart > 0 && code[attributeStart - 1] != '\n') return (start, isTest);

            isTest |= IsTestAttribute(code[(open + 1)..(close - 1)]);
            start = attributeStart;

            if (attributeStart == 0) return (start, isTest);
        }
    }

    /// <summary>
    /// Índice posterior al último carácter con contenido de la línea anterior <b>con contenido</b>, o <c>-1</c> si
    /// no hay ninguna. Las líneas en blanco se saltan a propósito: en C# una línea vacía no separa un atributo de
    /// su declaración, así que un <c>[Fact]</c> seguido de una línea en blanco sigue siendo el atributo del método.
    /// </summary>
    private static int EndOfPreviousLineWithContent(string code, int index)
    {
        int newline = index;
        while (newline > 0 && code[newline - 1] != '\n') newline--;

        while (newline > 0)
        {
            int i = newline - 1;
            while (i > 0 && IsHorizontalSpace(code[i])) i--;
            if (code[i] != '\n') return i + 1;

            newline = i;
        }

        return -1;
    }

    /// <summary>
    /// Corchete de apertura del atributo que cierra en la posición indicada, contando el anidamiento
    /// (<c>new[] { … }</c> dentro de un <c>[InlineData(…)]</c>) y sin cruzar el final de la línea: un atributo
    /// que no cierra en su línea no es un atributo.
    /// </summary>
    private static int MatchingBracketBackwards(string code, int close)
    {
        int depth = 0;

        for (int i = close; i >= 0; i--)
        {
            if (code[i] == '\n') return -1;

            if (code[i] == ']') depth++;
            else if (code[i] == '[' && --depth == 0) return i;
        }

        return -1;
    }

    /// <summary>
    /// ¿Es un carácter que una línea puede llevar delante del salto? El <c>\r</c> cuenta —y es la mitad del
    /// suite, que está en CRLF—: sin él, la línea anterior termina en <c>\r</c>, el retroceso no reconoce el
    /// corchete del atributo y el índice de pruebas se queda <b>vacío</b> en los ficheros de CRLF mientras sigue
    /// funcionando en los de LF. Ese es el fallo silencioso de esta clase de análisis: la guardia que se apoya en
    /// él pasa en verde sin haber mirado nada.
    /// </summary>
    private static bool IsHorizontalSpace(char c) => c is ' ' or '\t' or '\r';

    /// <summary>¿El texto empieza por esa palabra, como palabra completa?</summary>
    private static bool Matches(string code, int at, string word) =>
        at >= 0 && at + word.Length <= code.Length
        && code.AsSpan(at, word.Length).SequenceEqual(word)
        && (at + word.Length == code.Length || !IsWordCharacter(code[at + word.Length]));

    /// <summary>
    /// Una tabla de datos de las teorías: la clase, anclada al principio de línea, con el nombre en mayúscula
    /// inicial. Es la única clase de miembro que el bloque necesita, y buscarla con un patrón acotado —sin una
    /// clase de caracteres que contenga espacios y pueda retroceder— es lo que mantiene este análisis lineal.
    /// </summary>
    private static readonly Regex TestDataTableRegex = new(
        @"(?m)^[ \t]*(?:(?:public|internal|private|protected|static|readonly)[ \t]+)*TheoryData<[^\r\n]*?>[ \t]+(?<name>[A-Z]\w*)[ \t]*(?==>|\{|=)",
        RegexOptions.Compiled);

    private static IReadOnlyList<(string Name, int Start, int End)> TestDataTables(string code)
    {
        var tables = new List<(string, int, int)>();

        foreach (Match match in TestDataTableRegex.Matches(code))
        {
            int end = MemberEnd(code, match.Index + match.Length);
            if (end > match.Index)
            {
                tables.Add((match.Groups["name"].Value, match.Index, end));
            }
        }

        return tables;
    }

    /// <summary>Final del miembro cuya cabecera termina en la posición indicada.</summary>
    private static int MemberEnd(string text, int afterName)
    {
        int i = SkipSpace(text, afterName);

        if (i < text.Length && text[i] == '(')
        {
            i = SkipSpace(text, MatchingDelimiter(text, i, ')') + 1);
        }

        if (i + 1 < text.Length && text[i] == '=' && text[i + 1] == '>') return StatementEnd(text, i);
        if (i < text.Length && text[i] == '{') return MatchingDelimiter(text, i, '}') + 1;
        if (i < text.Length && text[i] == '=') return StatementEnd(text, i);

        return afterName;
    }

    /// <summary>
    /// Final del cuerpo del método cuya signatura termina en la posición indicada: por expresión (hasta el
    /// <c>;</c>) o por bloque.
    /// </summary>
    private static int BodyEnd(string text, int signatureEnd)
    {
        int i = SkipSpace(text, signatureEnd);

        if (i + 1 < text.Length && text[i] == '=' && text[i + 1] == '>') return StatementEnd(text, i);

        return i < text.Length && text[i] == '{' ? MatchingDelimiter(text, i, '}') + 1 : i;
    }

    /// <summary>Índice posterior al punto y coma que cierra el enunciado, fuera de literales.</summary>
    private static int StatementEnd(string text, int from)
    {
        var (position, _) = Scan(text, from, stopAtSemicolon: true);
        return position + 1;
    }

    /// <summary>
    /// Posición del delimitador que cierra el abierto, contando el anidamiento y <b>saltando los literales</b>:
    /// una llave dentro de una cadena no abre un bloque, y el <c>"{ }"</c> de una prueba desequilibraría la
    /// cuenta.
    /// </summary>
    private static int MatchingDelimiter(string text, int openerIndex, char close)
    {
        var (position, _) = Scan(text, openerIndex, close);
        return position;
    }

    /// <summary>
    /// Recorre el texto desde <paramref name="from"/> saltando literales. Devuelve la posición del delimitador de
    /// cierre indicado —o del punto y coma, si se busca el final de un enunciado— y la posición siguiente.
    /// </summary>
    private static (int Position, int End) Scan(string text, int from, char? close = null, bool stopAtSemicolon = false)
    {
        int depth = 0;
        bool inLiteral = false;
        char quote = '\0';

        for (int i = from; i < text.Length; i++)
        {
            char current = text[i];

            if (inLiteral)
            {
                if (current == '\\' && i + 1 < text.Length) i++;
                else if (current == quote) inLiteral = false;

                continue;
            }

            if (current is '"' or '\'')
            {
                inLiteral = true;
                quote = current;
                continue;
            }

            if (current is '{' or '(' or '[')
            {
                depth++;
            }
            else if (current is '}' or ')' or ']')
            {
                depth--;

                if (close == current && depth == 0) return (i, i + 1);
            }
            else if (current == ';' && stopAtSemicolon && depth == 0)
            {
                return (i, i + 1);
            }
        }

        return (Math.Max(from, text.Length - 1), text.Length);
    }

    private static int SkipSpace(string text, int from)
    {
        int i = from;
        while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
        return i;
    }
}
