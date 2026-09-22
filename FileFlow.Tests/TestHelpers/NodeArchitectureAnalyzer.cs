using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// Analiza ficheros de los plugins para detectar nodos que no siguen la arquitectura de
/// <c>FlowNodeBase</c>: o implementan <c>IFlowNode</c> a mano, o vuelven a declarar el ciclo de vida
/// (identidad, puertos, parámetros, metadatos) que la base ya aporta, o leen sus parámetros del
/// diccionario en lugar de con <c>GetParameter&lt;T&gt;</c>, o cambian sus puertos dinámicos sin anunciarlo
/// (el editor se quedaría con los puertos viejos y con cables apuntando a puertos inexistentes).
///
/// Una clase <c>partial</c> puede repartirse entre ficheros, así que el barrido acumula primero los
/// nombres de clase que son nodos —y los que anuncian su topología— en todo el proyecto y los pasa a
/// <see cref="Analyze(string, string, IReadOnlyCollection{string}, IReadOnlyCollection{string})()"/>: de otro modo, el fichero que sólo
/// aporta una propiedad no declara la herencia y su infracción quedaría invisible.
///
/// Por qué existe: tras migrar los 70 nodos del proyecto a la jerarquía, la forma de reintroducir el
/// boilerplate no es un error de compilación sino una advertencia de ocultación (<c>CS0108</c>) que sólo
/// es fatal en <c>FileFlow.Plugin.AI</c>. En los otros diez plugins el patrón antiguo compila sin ruido,
/// así que la guardia lo convierte en un fallo rojo con el fichero y la línea exactos.
///
/// Usa el árbol de sintaxis de Roslyn en lugar de expresiones regulares: la línea que se reporta es la
/// real de la declaración, y no hay falsos positivos por llaves, cadenas o comentarios.
/// </summary>
public static class NodeArchitectureAnalyzer
{
    /// <summary>Clases base que ya implementan el contrato de nodo y su ciclo de vida.</summary>
    public static readonly IReadOnlySet<string> NodeBaseTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "FlowNodeBase",
        "AiFlowNodeBase",
        "AudioAiFlowNodeBase"
    };

    /// <summary>Contrato del que debe venir la implementación de un nodo, nunca declarada a mano.</summary>
    public const string NodeContract = "IFlowNode";

    public const string RuleDirectImplementation = "Nodo-IFlowNode-a-mano";
    public const string RuleDuplicatedLifecycle = "Ciclo-de-vida-duplicado";
    public const string RuleManualParameterRead = "Lectura-de-parametro-a-mano";
    public const string RuleSilentPortTopology = "Topologia-de-puertos-silenciosa";

    /// <summary>
    /// Método con el que un nodo anuncia que su conjunto de puertos cambió. Es lo único que el editor
    /// escucha: sin la llamada, un nodo con puertos dinámicos cambia su topología en silencio y el lienzo
    /// sigue dibujando —y conservando— cables que apuntan a puertos que ya no existen.
    /// </summary>
    public const string PortTopologyNotificationMethod = "NotifyPortsChanged";

    /// <summary>Hooks con los que un nodo deriva sus puertos al leerlos.</summary>
    private static readonly HashSet<string> DynamicPortHooks = new(StringComparer.Ordinal)
    {
        "BuildInputPorts",
        "BuildOutputPorts"
    };

    /// <summary>Propiedades de puertos que sólo el constructor puede asignar sin derivar puertos dinámicos.</summary>
    private static readonly HashSet<string> PortProperties = new(StringComparer.Ordinal)
    {
        "Inputs",
        "Outputs"
    };

    /// <summary>
    /// Miembros que <c>FlowNodeBase</c> ya implementa. Un nodo sólo puede redefinirlos con
    /// <c>override</c> (puertos dinámicos) o, simplemente, no declararlos.
    /// </summary>
    private static readonly Dictionary<string, string> InheritedMembers = new(StringComparer.Ordinal)
    {
        ["Id"] = "FlowNodeBase ya genera y expone Id; un nodo no debe redeclararlo ni ocultarlo.",
        ["Parameters"] = "FlowNodeBase ya expone el diccionario Parameters; puebla las claves en el constructor.",
        ["Inputs"] = "Asigna los puertos de entrada en el constructor o sobrescribe BuildInputPorts() si dependen de los parámetros.",
        ["Outputs"] = "Asigna los puertos de salida en el constructor o sobrescribe BuildOutputPorts() si dependen de los parámetros.",
        ["Name"] = "La propiedad heredada debe declararse como 'override'.",
        ["Category"] = "La propiedad heredada debe declararse como 'override'.",
        ["Description"] = "La propiedad heredada debe declararse como 'override'.",
        ["MaxConcurrency"] = "La propiedad heredada debe declararse como 'override'.",
        ["ParameterDescriptors"] = "La propiedad heredada debe declararse como 'override'.",
        ["CustomActions"] = "La propiedad heredada debe declararse como 'override'.",
        ["ExecuteAsync"] = "El método heredado debe declararse como 'override'.",
        ["OnWorkflowCompletedAsync"] = "El método heredado debe declararse como 'override'."
    };

    /// <summary>
    /// Analiza un fichero fuente y devuelve las infracciones de arquitectura de nodo detectadas,
    /// cada una con su fichero y su línea.
    /// </summary>
    /// <param name="nodeClassNamesInProject">
    /// Nombres de clases que son nodos en OTROS ficheros del mismo proyecto. Sin ellos, una clase
    /// <c>partial</c> cuyo fichero no declara la herencia (el que sólo aporta la propiedad <c>Id</c>, por
    /// ejemplo) pasaría por no ser un nodo y su infracción quedaría invisible.
    /// </param>
    /// <param name="topologyAnnouncingClassesInProject">
    /// Nombres de clases que anuncian su topología en OTRO fichero del mismo proyecto. Es el mismo
    /// mecanismo que <paramref name="nodeClassNamesInProject"/> y existe por el mismo motivo: un nodo
    /// <c>partial</c> puede derivar sus puertos en un fichero y anunciar el cambio en el otro, y sin este
    /// contexto la guardia leería el segundo como un nodo mudo.
    /// </param>
    public static IReadOnlyList<NodeArchitectureViolation> Analyze(
        string relativePath,
        string source,
        IReadOnlyCollection<string>? nodeClassNamesInProject = null,
        IReadOnlyCollection<string>? topologyAnnouncingClassesInProject = null)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        ArgumentNullException.ThrowIfNull(source);

        var classes = ParseClasses(relativePath, source);

        if (classes.Count == 0)
        {
            return [];
        }

        var nodeClasses = CollectNodeClassNames(classes, nodeClassNamesInProject);
        var announcingClasses = new HashSet<string>(TopologyAnnouncingClassNames(source), StringComparer.Ordinal);

        if (topologyAnnouncingClassesInProject != null)
        {
            announcingClasses.UnionWith(topologyAnnouncingClassesInProject);
        }

        var violations = new List<NodeArchitectureViolation>();

        foreach (var cls in classes)
        {
            bool isNode = nodeClasses.Contains(cls.Identifier.Text);

            if (!isNode && GetBaseNames(cls).Contains(NodeContract, StringComparer.Ordinal))
            {
                violations.Add(new NodeArchitectureViolation(
                    relativePath,
                    LineOf(cls),
                    cls.Identifier.Text,
                    NodeContract,
                    RuleDirectImplementation,
                    $"El nodo implementa {NodeContract} directamente; hereda de una de las bases ({string.Join(", ", NodeBaseTypes)}) para no repetir identidad, puertos y parámetros."));
                continue;
            }

            if (!isNode)
            {
                continue;
            }

            // Lecturas de Parameters que esquivan el ayudante tipado. Se excluye "ContainsKey" (comprobar
            // presencia es legítimo para respaldos y parámetros heredados) y las escrituras por indizador
            // (equivalen a SetParameter).
            foreach (var invocation in cls.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.Text: "TryGetValue" } member ||
                    member.Expression is not IdentifierNameSyntax { Identifier.Text: "Parameters" })
                {
                    continue;
                }

                string key = invocation.ArgumentList.Arguments.Count > 0
                    ? invocation.ArgumentList.Arguments[0].Expression.ToString()
                    : "?";

                violations.Add(new NodeArchitectureViolation(
                    relativePath,
                    LineOf(invocation),
                    cls.Identifier.Text,
                    key,
                    RuleManualParameterRead,
                    "Lee el parámetro con GetParameter<T> en lugar de Parameters.TryGetValue + ParameterHelper: " +
                    "es equivalente (ambos delegan en ParameterValueConverter), entiende JsonElement y números " +
                    "embebidos en texto, y no desborda en silencio."));
            }

            // Un nodo con puertos dinámicos que nunca anuncia el cambio es una fuga silenciosa: el editor
            // sólo puede reaccionar si el nodo habla (ver IPortTopologyNode).
            var dynamicPortSite = FindDynamicPortSite(cls);

            if (dynamicPortSite is { } site && !announcingClasses.Contains(cls.Identifier.Text))
            {
                violations.Add(new NodeArchitectureViolation(
                    relativePath,
                    LineOf(site.Site),
                    cls.Identifier.Text,
                    site.Member,
                    RuleSilentPortTopology,
                    $"El nodo deriva puertos que no son fijos y nunca llama a {PortTopologyNotificationMethod}(), " +
                    "así que el editor no puede enterarse del cambio: quedan cables colgando de puertos que ya no " +
                    $"existen. Llama a {PortTopologyNotificationMethod}() donde cambien —o sobrescribe " +
                    "RefreshPortTopology() si materializas los puertos en lugar de calcularlos al leerlos—."));
            }

            foreach (var member in cls.Members)
            {
                string? memberName = member switch
                {
                    PropertyDeclarationSyntax property => property.Identifier.Text,
                    MethodDeclarationSyntax method => method.Identifier.Text,
                    _ => null
                };

                if (memberName is null || !InheritedMembers.TryGetValue(memberName, out string? reason))
                {
                    continue;
                }

                if (member.Modifiers.Any(SyntaxKind.OverrideKeyword))
                {
                    continue;
                }

                violations.Add(new NodeArchitectureViolation(
                    relativePath,
                    LineOf(member),
                    cls.Identifier.Text,
                    memberName,
                    RuleDuplicatedLifecycle,
                    reason));
            }
        }

        return violations;
    }

    /// <summary>
    /// Nombres de las clases de un fichero que anuncian su topología de puertos (contienen la llamada a
    /// <see cref="PortTopologyNotificationMethod}"/>). El barrido lo acumula por proyecto, igual que los
    /// nombres de los nodos, para que una clase <c>partial</c> no quede como si no anunciara nada.
    /// </summary>
    public static IReadOnlyCollection<string> TopologyAnnouncingClassNames(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return ParseClasses("virtual.cs", source)
            .Where(AnnouncesPortTopology)
            .Select(cls => cls.Identifier.Text)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Punto del nodo por el que se derivan puertos que no son fijos: un hook de puertos sobrescrito o una
    /// asignación de <c>Inputs</c>/<c>Outputs</c> fuera del constructor (asignarlos ahí es la vía de los
    /// puertos fijos, que son la mayoría de los nodos). Devuelve el nodo que lo declara, para poder reportar
    /// su línea exacta.
    /// </summary>
    private static (SyntaxNode Site, string Member)? FindDynamicPortSite(ClassDeclarationSyntax cls)
    {
        foreach (var member in cls.Members.OfType<MethodDeclarationSyntax>())
        {
            if (member.Modifiers.Any(SyntaxKind.OverrideKeyword) && DynamicPortHooks.Contains(member.Identifier.Text))
            {
                return (member, member.Identifier.Text);
            }
        }

        foreach (var assignment in cls.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            string? property = AssignedPortProperty(assignment);

            if (property is null || IsInsideConstructor(assignment))
            {
                continue;
            }

            return (assignment, property);
        }

        return null;
    }

    /// <summary>
    /// Nombre de la propiedad de puertos asignada (<c>Inputs</c>/<c>Outputs</c>), admitiendo la forma
    /// cualificada (<c>this.Inputs = …</c>). Cualquier otra asignación no es un cambio de topología.
    /// </summary>
    private static string? AssignedPortProperty(AssignmentExpressionSyntax assignment)
    {
        string? name = assignment.Left switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            MemberAccessExpressionSyntax member when member.Expression is ThisExpressionSyntax => member.Name.Identifier.Text,
            _ => null
        };

        return name is not null && PortProperties.Contains(name) ? name : null;
    }

    private static bool IsInsideConstructor(SyntaxNode node) =>
        node.Ancestors().OfType<MemberDeclarationSyntax>().FirstOrDefault() is ConstructorDeclarationSyntax;

    /// <summary>¿La clase anuncia su topología de puertos en alguna parte de su cuerpo?</summary>
    private static bool AnnouncesPortTopology(ClassDeclarationSyntax cls) =>
        cls.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(invocation => invocation.Expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.Text == PortTopologyNotificationMethod,
            MemberAccessExpressionSyntax member => member.Name.Identifier.Text == PortTopologyNotificationMethod,
            _ => false
        });

    /// <summary>
    /// Nombres de las clases de un fichero que derivan (directa o transitivamente) de una base de nodo.
    /// Es lo que el barrido acumula por proyecto antes de analizar cada fichero, para no dejar escapar
    /// una clase <c>partial</c>.
    /// </summary>
    public static IReadOnlyCollection<string> NodeClassNames(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return CollectNodeClassNames(ParseClasses("virtual.cs", source), null);
    }

    /// <summary>
    /// Nombres completos (<c>Namespace.Clase</c>, con <c>+</c> para las anidadas) de las clases que son
    /// nodos <b>concretos</b> de este fichero, en el mismo formato que <see cref="Type.FullName"/>: es lo
    /// que permite comparar el código fuente con lo que el cargador descubre en tiempo de ejecución.
    ///
    /// Se excluyen las bases abstractas (<c>AiFlowNodeBase</c>, <c>AudioAiFlowNodeBase</c>), porque el
    /// cargador tampoco las registra: no son nodos instanciables.
    /// </summary>
    public static IReadOnlyCollection<string> ConcreteNodeTypeFullNames(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var classes = ParseClasses("virtual.cs", source);
        var nodeClasses = CollectNodeClassNames(classes, null);

        return classes
            .Where(cls => nodeClasses.Contains(cls.Identifier.Text))
            .Where(cls => !cls.Modifiers.Any(SyntaxKind.AbstractKeyword))
            .Select(FullNameOf)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Nombre completo de una clase tal como lo compone <see cref="Type.FullName"/>.</summary>
    private static string FullNameOf(ClassDeclarationSyntax cls)
    {
        var containingTypes = new List<string>();
        string namespaceName = string.Empty;

        for (SyntaxNode? node = cls.Parent; node is not null; node = node.Parent)
        {
            switch (node)
            {
                case ClassDeclarationSyntax outer:
                    containingTypes.Insert(0, outer.Identifier.Text);
                    break;
                case BaseNamespaceDeclarationSyntax declaration:
                    namespaceName = declaration.Name.ToString();
                    break;
            }
        }

        containingTypes.Add(cls.Identifier.Text);
        string nestedName = string.Join("+", containingTypes);

        return string.IsNullOrEmpty(namespaceName) ? nestedName : $"{namespaceName}.{nestedName}";
    }

    private static List<ClassDeclarationSyntax> ParseClasses(string path, string source) =>
        CSharpSyntaxTree
            .ParseText(source, path: path)
            .GetCompilationUnitRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .ToList();

    /// <summary>
    /// Nombres de las clases del fichero que derivan (directa o transitivamente) de una base de nodo.
    /// El cierre de herencia resuelve bases intermedias declaradas en el mismo fichero.
    /// </summary>
    private static HashSet<string> CollectNodeClassNames(
        IReadOnlyList<ClassDeclarationSyntax> classes,
        IReadOnlyCollection<string>? nodeClassNamesInProject)
    {
        var basesByClass = classes
            .GroupBy(cls => cls.Identifier.Text, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => GetBaseNames(group.First()), StringComparer.Ordinal);

        var nodeClasses = nodeClassNamesInProject is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(nodeClassNamesInProject, StringComparer.Ordinal);

        bool IsNodeClass(string className, HashSet<string> visiting)
        {
            if (nodeClasses.Contains(className))
            {
                return true;
            }

            if (!basesByClass.TryGetValue(className, out var bases) || !visiting.Add(className))
            {
                return false;
            }

            bool isNode = bases.Any(NodeBaseTypes.Contains) || bases.Any(name => IsNodeClass(name, visiting));

            visiting.Remove(className);

            if (isNode)
            {
                nodeClasses.Add(className);
            }

            return isNode;
        }

        foreach (var cls in classes)
        {
            IsNodeClass(cls.Identifier.Text, new HashSet<string>(StringComparer.Ordinal));
        }

        return nodeClasses;
    }

    /// <summary>Nombres simples de la lista de bases de una clase, sin espacios de nombres ni genéricos.</summary>
    private static IReadOnlyList<string> GetBaseNames(ClassDeclarationSyntax cls) =>
        cls.BaseList is null
            ? []
            : cls.BaseList.Types.Select(type => RightmostIdentifier(type.Type)).ToList();

    private static string RightmostIdentifier(TypeSyntax type) => type switch
    {
        SimpleNameSyntax simple => simple.Identifier.Text,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
        _ => type.ToString()
    };

    private static int LineOf(SyntaxNode node) =>
        node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
}

/// <summary>
/// Infracción de arquitectura de nodo, con el fichero y la línea que la cometen.
/// </summary>
public sealed record NodeArchitectureViolation(
    string File,
    int Line,
    string ClassName,
    string Member,
    string Rule,
    string Reason)
{
    public override string ToString() =>
        $"{File}({Line}): [{Rule}] {ClassName}.{Member} — {Reason}";
}
