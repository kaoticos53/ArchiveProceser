using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// Analiza ficheros fuente en busca de un <b>tercer dialecto</b> del archivo de flujo: cualquiera que
/// (des)serialice un <c>WorkflowGraph</c> con opciones propias —o sin declararlas, que es la misma cosa con
/// las opciones por defecto— en lugar de la definición única
/// (<c>WorkflowGraph.SerializationOptions</c>, ver <c>WorkflowFormat</c> y la fase 3E).
///
/// <para>
/// Por qué existe: hasta la fase 3E había <b>dos</b> escritores del mismo formato con dialectos distintos
/// —la app con los nombres del modelo tal cual y Core en camelCase, los dos declarando <c>v2</c>— y nada
/// fallaba, porque el lector tolerante acepta cualquier caja. Un formato con dos dialectos se lee; lo que no
/// se puede es saber qué se va a escribir. La forma de reintroducir el tercero no es un error de compilación:
/// es una línea que compila y guarda un archivo que sólo entiende su autor.
/// </para>
///
/// <para>
/// Qué mira: una llamada a <c>JsonSerializer.*</c> sobre un flujo cuya <b>condición de flujo se ve en la
/// línea</b> —el argumento de tipo <c>&lt;WorkflowGraph&gt;</c> o un argumento que es un identificador
/// declarado como <c>WorkflowGraph</c> en el mismo fichero— tiene que nombrar las opciones compartidas.
/// Deliberadamente no mira «este fichero habla de flujos y construye opciones»: en un fichero que lee un
/// flujo y escribe además un resumen —el caso del CLI— eso sería un falso positivo, y una guardia que
/// obliga a silenciarla deja de avisar.
/// </para>
///
/// <para>
/// Límite declarado: un alias de tipo inferido (<c>var copia = grafo;</c> y serializar la copia) escapa al
/// seguimiento de tipos, que es sintáctico. No hay camino que lo necesite —para escribir o leer un flujo
/// están <c>ToJson</c>/<c>FromJson</c>, que ya lo hacen bien—, y perseguirlo exigiría compilación semántica
/// para cubrir una forma que nadie escribe.
/// </para>
/// </summary>
public static class FlowSerializationAnalyzer
{
    /// <summary>Tipo que representa un flujo en memoria: lo que no se serializa con opciones propias.</summary>
    public const string FlowTypeName = "WorkflowGraph";

    /// <summary>Nombre del miembro con las opciones únicas del formato.</summary>
    public const string SharedOptionsMember = "SerializationOptions";

    /// <summary>
    /// Fichero en el que vive la definición. Se exceptúa porque es donde el formato se <b>define</b>: ahí las
    /// opciones compartidas se construyen y se usan, y pedirle a la definición que se cite a sí misma sería
    /// pedir que la guardia no pudiera leer su propio asunto.
    /// </summary>
    public const string CanonicalFile = "FileFlow.Core/Engine/WorkflowGraph.cs";

    public const string RuleOwnOptions = "Serializacion-de-flujo-con-opciones-propias";
    public const string RuleSharedOptionsMutation = "Opciones-compartidas-modificadas";

    /// <summary>Métodos de <c>JsonSerializer</c> que escriben o leen el formato.</summary>
    private static readonly HashSet<string> SerializerMethods = new(StringComparer.Ordinal)
    {
        "Serialize",
        "SerializeAsync",
        "Deserialize",
        "DeserializeAsync"
    };

    /// <summary>
    /// Analiza un fichero y devuelve las infracciones detectadas, cada una con su fichero y su línea.
    /// </summary>
    public static IReadOnlyList<FlowSerializationViolation> Analyze(string relativePath, string source)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        ArgumentNullException.ThrowIfNull(source);

        var root = CSharpSyntaxTree
            .ParseText(source, path: relativePath)
            .GetCompilationUnitRoot();

        var violations = new List<FlowSerializationViolation>();

        // Mutar la instancia compartida no es un dialecto por fichero: es cambiarle el formato a todo el
        // proceso —y, si ya se usó, ni eso, porque JsonSerializerOptions se congela al primer uso—. Se mira
        // en todos los ficheros, incluida la definición, donde un `??=` distraído sería el mismo error.
        foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (IsSharedOptionsAccess(assignment.Left))
            {
                violations.Add(new FlowSerializationViolation(
                    relativePath,
                    LineOf(assignment),
                    assignment.Left.ToString(),
                    RuleSharedOptionsMutation,
                    MutationReason));
            }
        }

        // Y mutarla por una llamada —`…SerializationOptions.Converters.Add(…)`— es lo mismo con otras letras.
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is MemberAccessExpressionSyntax member && IsSharedOptionsAccess(member.Expression))
            {
                violations.Add(new FlowSerializationViolation(
                    relativePath,
                    LineOf(invocation),
                    invocation.Expression.ToString(),
                    RuleSharedOptionsMutation,
                    MutationReason));
            }
        }

        if (Normalize(relativePath).EndsWith(CanonicalFile, StringComparison.OrdinalIgnoreCase))
        {
            return violations;
        }

        var flowIdentifiers = FlowTypedIdentifiers(root);

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (!TryGetSerializerMethod(invocation.Expression, out string? method))
            {
                continue;
            }

            if (!SerializesAFlow(invocation, flowIdentifiers))
            {
                continue;
            }

            if (invocation.ArgumentList.Arguments.Any(argument => IsSharedOptionsAccess(argument.Expression)))
            {
                continue;
            }

            violations.Add(new FlowSerializationViolation(
                relativePath,
                LineOf(invocation),
                method!,
                RuleOwnOptions,
                $"Un {FlowTypeName} se escribe y se lee con las opciones del formato " +
                $"({FlowTypeName}.{SharedOptionsMember}) —o, mejor, con {FlowTypeName}.ToJson()/FromJson()—: con " +
                "otras opciones —o con las de por defecto, que es no decir ninguna— se guarda un dialecto que " +
                "sólo entiende quien lo escribió."));
        }

        return violations;
    }

    /// <summary>
    /// Identificadores que en este fichero son un flujo: parámetros, campos, propiedades y locales
    /// declarados con ese tipo. Es lo que permite reconocer <c>JsonSerializer.SerializeAsync(flujo, …)</c>,
    /// que no lleva argumento de tipo que mirar.
    /// </summary>
    private static HashSet<string> FlowTypedIdentifiers(CompilationUnitSyntax root)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var parameter in root.DescendantNodes().OfType<ParameterSyntax>())
        {
            if (IsFlowType(parameter.Type))
            {
                names.Add(parameter.Identifier.Text);
            }
        }

        foreach (var field in root.DescendantNodes().OfType<FieldDeclarationSyntax>())
        {
            if (!IsFlowType(field.Declaration.Type))
            {
                continue;
            }

            foreach (var variable in field.Declaration.Variables)
            {
                names.Add(variable.Identifier.Text);
            }
        }

        foreach (var property in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
        {
            if (IsFlowType(property.Type))
            {
                names.Add(property.Identifier.Text);
            }
        }

        foreach (var local in root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
        {
            if (!IsFlowType(local.Declaration.Type))
            {
                continue;
            }

            foreach (var variable in local.Declaration.Variables)
            {
                names.Add(variable.Identifier.Text);
            }
        }

        return names;
    }

    /// <summary>¿La llamada escribe o lee un flujo?</summary>
    private static bool SerializesAFlow(
        InvocationExpressionSyntax invocation,
        IReadOnlySet<string> flowIdentifiers)
    {
        // `<WorkflowGraph>`: la forma explícita, y la única posible al leer.
        if (invocation.Expression is MemberAccessExpressionSyntax { Name: GenericNameSyntax generic } &&
            generic.TypeArgumentList.Arguments.Any(argument => RightmostIdentifier(argument) == FlowTypeName))
        {
            return true;
        }

        // Un argumento que en este fichero está declarado como flujo. Se mira el identificador más a la
        // izquierda de cada argumento, que es el que lleva el tipo: `grafo` y `this.grafo` son el mismo.
        return invocation.ArgumentList.Arguments.Any(argument =>
            LeftmostIdentifier(argument.Expression) is { } name && flowIdentifiers.Contains(name));
    }

    /// <summary>
    /// ¿La expresión nombra las opciones compartidas, o algo suyo? Cubre las tres formas de tocarlas:
    /// pasarlas (<c>WorkflowGraph.SerializationOptions</c>), asignarles un miembro
    /// (<c>…SerializationOptions.WriteIndented = …</c>) y cambiarlas por llamada
    /// (<c>…SerializationOptions.Converters.Add(…)</c>), que es lo que hay que ver cuando el acceso no es el
    /// de fuera de la expresión sino el de dentro.
    /// </summary>
    /// <remarks>
    /// Mira la <b>espina</b> de la expresión —lo que se accede a lo que se accede— y no cualquier acceso de
    /// dentro: si mirara todo el árbol, la propia llamada que <b>pasa</b> las opciones sería una mutación, o
    /// una llamada cualquiera sobre una expresión que las lleva como argumento lo sería.
    /// </remarks>
    private static bool IsSharedOptionsAccess(ExpressionSyntax expression)
    {
        for (ExpressionSyntax? current = expression; current is not null;)
        {
            switch (current)
            {
                case MemberAccessExpressionSyntax access
                    when access.Name.Identifier.Text == SharedOptionsMember
                      && RightmostIdentifierOfExpression(access.Expression) == FlowTypeName:
                    return true;

                case MemberAccessExpressionSyntax access:
                    current = access.Expression;
                    break;

                case InvocationExpressionSyntax invocation:
                    current = invocation.Expression;
                    break;

                default:
                    return false;
            }
        }

        return false;
    }

    /// <summary>Por qué no se tocan las opciones compartidas, dicho igual en las dos formas de tocarlas.</summary>
    private const string MutationReason =
        "Las opciones del formato son una sola instancia compartida y no se tocan: cambiarlas aquí cambia lo " +
        "que se escribe en todas partes —y después del primer uso ni se puede, porque JsonSerializerOptions se " +
        "congela—.";

    /// <summary>
    /// Nombre del método de <c>JsonSerializer</c> si la expresión es una de sus llamadas; <c>null</c> en
    /// cualquier otro caso. Se admite la forma cualificada (<c>System.Text.Json.JsonSerializer.Serialize</c>).
    /// </summary>
    private static bool TryGetSerializerMethod(ExpressionSyntax expression, out string? method)
    {
        method = null;

        if (expression is not MemberAccessExpressionSyntax member)
        {
            return false;
        }

        string? name = member.Name switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            GenericNameSyntax generic => generic.Identifier.Text,
            _ => null
        };

        if (name is null || !SerializerMethods.Contains(name))
        {
            return false;
        }

        if (RightmostIdentifierOfExpression(member.Expression) != "JsonSerializer")
        {
            return false;
        }

        method = name;
        return true;
    }

    /// <summary>¿El tipo escrito es el del flujo?</summary>
    private static bool IsFlowType(TypeSyntax? type) =>
        type is not null && RightmostIdentifierOfType(type) == FlowTypeName;

    private static string? RightmostIdentifierOfType(TypeSyntax type) => type switch
    {
        SimpleNameSyntax simple => simple.Identifier.Text,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
        NullableTypeSyntax nullable => RightmostIdentifierOfType(nullable.ElementType),
        _ => type.ToString().Split('.', '<', '>', '?')[^1].Trim()
    };

    /// <summary>Identificador más a la derecha de un tipo escrito con genéricos (<c>Task&lt;WorkflowGraph&gt;</c>).</summary>
    private static string RightmostIdentifier(TypeSyntax type) => type switch
    {
        // SimpleNameSyntax cubre ya el nombre con genéricos (GenericNameSyntax deriva de él): el identificador
        // de `Task<WorkflowGraph>` es `Task`, que es lo que hay que mirar para el tipo del argumento.
        SimpleNameSyntax simple => simple.Identifier.Text,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
        _ => type.ToString()
    };

    /// <summary>Identificador más a la derecha de una expresión (<c>FileFlow.Core.WorkflowGraph</c> → <c>WorkflowGraph</c>).</summary>
    private static string? RightmostIdentifierOfExpression(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.Text,
        MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
        _ => null
    };

    /// <summary>
    /// Identificador más a la <b>izquierda</b> de una expresión: el que lleva el nombre de la variable, sea
    /// <c>grafo</c>, <c>this.grafo</c> o <c>_almacen.GrafoActual</c>. Devuelve <c>null</c> si la expresión no
    /// empieza por un nombre, que es lo que ocurre con las llamadas y las construcciones.
    /// </summary>
    private static string? LeftmostIdentifier(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.Text,
        ThisExpressionSyntax => "this",
        MemberAccessExpressionSyntax member => LeftmostIdentifier(member.Expression),
        _ => null
    };

    private static int LineOf(SyntaxNode node) =>
        node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    private static string Normalize(string path) => path.Replace('\\', '/');
}

/// <summary>
/// Infracción de la regla del formato único, con el fichero y la línea que la cometen.
/// </summary>
public sealed record FlowSerializationViolation(
    string File,
    int Line,
    string Member,
    string Rule,
    string Reason)
{
    public override string ToString() =>
        $"{File}({Line}): [{Rule}] {Member} — {Reason}";
}
