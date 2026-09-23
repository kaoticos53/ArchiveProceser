using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FileFlow.Tests.TestHelpers;

/// <summary>Cómo se aplaza el trabajo en el tiempo.</summary>
public enum DeferredWorkKind
{
    /// <summary>Un reloj que late o vence solo: <c>DispatcherTimer</c>, <c>PeriodicTimer</c>, <c>Timer</c>, <c>CreateTimer</c>.</summary>
    Timer,

    /// <summary>Una espera explícita dentro de un flujo: <c>Task.Delay</c>.</summary>
    Delay
}

/// <summary>
/// Un sitio de producción que aplaza trabajo en el tiempo: dónde está, qué lo aplaza y si cuelga de un reloj
/// inyectado.
/// </summary>
public sealed record DeferredWorkSite
{
    public required string File { get; init; }

    public required int Line { get; init; }

    /// <summary>Miembro que lo contiene (método, constructor, función local, descriptor o campo).</summary>
    public required string Member { get; init; }

    public required DeferredWorkKind Kind { get; init; }

    /// <summary>Texto del constructo, para que el inventario diga <i>qué</i> se aplaza y no sólo dónde.</summary>
    public required string Construct { get; init; }

    /// <summary>El retardo cuelga de un <c>TimeProvider</c> en vez del reloj del sistema.</summary>
    public required bool UsesInjectedClock { get; init; }

    /// <summary>
    /// Identidad estable del sitio: fichero + miembro + tipo. Sólo lleva ordinal (<c>#1</c>, <c>#2</c>) cuando
    /// el mismo miembro aplaza trabajo más de una vez, y entonces lo lleva en todos sus sitios.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    public override string ToString() =>
        $"{Key}  →  {Construct} (línea {Line})" + (UsesInjectedClock ? "  [reloj inyectado]" : "");
}

/// <summary>Qué se ha decidido sobre un sitio del inventario.</summary>
public sealed record DeferredWorkDecision
{
    public required string Key { get; init; }

    /// <summary>Paso <b>público</b> que la prueba ejercita, cuando el sitio está cubierto.</summary>
    public string? Step { get; init; }

    /// <summary>Clase de test que nombra esa evidencia: la prueba de que el sitio no es invisible al suite.</summary>
    public string? EvidenceTest { get; init; }

    /// <summary>
    /// Texto que tiene que aparecer en esa clase de test. Por defecto es el nombre del paso —lo más fuerte: la
    /// prueba nombra exactamente lo que aplaza el trabajo—, pero un nodo que se ejecuta dentro de un flujo se
    /// cita por su <b>tipo</b>, que es lo que la prueba maneja.
    /// </summary>
    public string? Evidence { get; init; }

    /// <summary>Por qué este sitio no se prueba, cuando no se prueba.</summary>
    public string? Reason { get; init; }

    public bool IsExercised => Step is not null;

    public static DeferredWorkDecision Exercised(string key, string step, string evidenceTest, string? evidence = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(step);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceTest);

        return new DeferredWorkDecision
        {
            Key = key,
            Step = step,
            EvidenceTest = evidenceTest,
            Evidence = evidence ?? step
        };
    }

    public static DeferredWorkDecision RealTime(string key, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new DeferredWorkDecision { Key = key, Reason = reason };
    }

    public override string ToString() => IsExercised
        ? $"{Key}  →  cubierto por {EvidenceTest} ({Evidence})"
        : $"{Key}  →  tiempo real: {Reason}";
}

/// <summary>
/// Inventaría el <b>trabajo aplazado en el tiempo</b> del código de producción —temporizadores y esperas— para
/// que un sitio nuevo no pueda colarse sin que nadie decida qué se hace con él.
///
/// <para><b>Por qué existe</b>: la capa que sólo corre con la aplicación en marcha o después de que pase el
/// tiempo es la que peor envejece. El barrido de la splash murió en silencio durante varios hitos (hito 169) y
/// los cuatro latidos no se ejercitaban en ninguna prueba hasta el hito 173; los dos relojes con duración
/// semántica no tenían vencimiento probado hasta el 174. Cada uno se descubrió a mano. Esto es la lista, hecha
/// por código, para que el siguiente se descubra solo: el inventario se recalcula en cada ejecución del suite y
/// la guardia falla mientras un sitio no esté <i>ejercitado</i> o <i>explicado</i>.</para>
///
/// <para><b>Qué queda fuera, y por qué (regla explícita, no olvido)</b>:
/// <list type="bullet">
///   <item><c>Dispatcher.UIThread.Post</c>/<c>InvokeAsync</c>: es <i>marshalado de hilo</i>, no tiempo. En
///   headless el despachador existe y ese trabajo <b>sí</b> corre en las pruebas; meterlo aquí llenaría el
///   inventario de entradas sin riesgo y le quitaría filo a la guardia.</item>
///   <item><c>CancellationTokenSource.CancelAfter</c>: es una <i>fecha límite</i> de cancelación, no trabajo
///   programado —al vencer no se ejecuta nada, se despierta un token— y tiene su propia cobertura en los
///   caminos de cancelación.</item>
/// </list></para>
///
/// <para>El análisis es sintáctico (Roslyn), no por expresiones regulares: un <c>Task.Delay</c> comentado o
/// dentro de una cadena no es un sitio, y la línea que se reporta es la real. Fue la lección del hito 165 (un
/// <c>new SplashScreenWindow()</c> comentado colándose en un lint de texto).</para>
/// </summary>
public static class DeferredWorkInventoryAnalyzer
{
    /// <summary>Tipos cuyo constructor crea un reloj.</summary>
    public static readonly IReadOnlySet<string> TimerTypeNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "DispatcherTimer",
        "PeriodicTimer",
        "Timer",
        "ITimer"
    };

    /// <summary>Fábricas de reloj que no son un <c>new</c>: <c>TimeProvider.CreateTimer</c>.</summary>
    public const string CreateTimerMethod = "CreateTimer";

    /// <summary>La espera explícita.</summary>
    public const string DelayMethod = "Delay";

    /// <summary>Receptor de la espera: distingue <c>Task.Delay</c> de un <c>Delay</c> cualquiera.</summary>
    public const string TaskTypeName = "Task";

    /// <summary>Marca textual de un reloj inyectado (parámetro, campo o propiedad <c>TimeProvider</c>).</summary>
    public const string TimeProviderMarker = "timeprovider";

    /// <summary>Analiza el texto de un fichero y devuelve sus sitios, sin clave (la asigna <see cref="Inventory"/>).</summary>
    public static IReadOnlyList<DeferredWorkSite> AnalyzeSource(string source, string file)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(file);

        var tree = CSharpSyntaxTree.ParseText(source);
        SyntaxNode root = tree.GetRoot();
        var sites = new List<DeferredWorkSite>();

        foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            string typeName = LastIdentifier(creation.Type);

            if (!TimerTypeNames.Contains(typeName))
            {
                continue;
            }

            sites.Add(new DeferredWorkSite
            {
                File = file,
                Line = LineOf(tree, creation),
                Member = EnclosingMember(creation),
                Kind = DeferredWorkKind.Timer,
                Construct = $"new {typeName}(...)",
                UsesInjectedClock = false
            });
        }

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            string methodName = invocation.Expression switch
            {
                MemberAccessExpressionSyntax access => access.Name.Identifier.Text,
                IdentifierNameSyntax identifier => identifier.Identifier.Text,
                _ => string.Empty
            };

            if (methodName == CreateTimerMethod)
            {
                sites.Add(new DeferredWorkSite
                {
                    File = file,
                    Line = LineOf(tree, invocation),
                    Member = EnclosingMember(invocation),
                    Kind = DeferredWorkKind.Timer,
                    Construct = invocation.Expression.ToString(),
                    UsesInjectedClock = ContainsTimeProvider(ReceiverOf(invocation))
                });

                continue;
            }

            // El receptor de la espera distingue 'Task.Delay' de un 'Delay' cualquiera: sin comprobarlo, el
            // inventario se llenaría de coincidencias de nombre y la guardia perdería credibilidad.
            if (methodName != DelayMethod || invocation.Expression is not MemberAccessExpressionSyntax delayAccess ||
                LastIdentifierOfExpression(delayAccess.Expression) != TaskTypeName)
            {
                continue;
            }

            sites.Add(new DeferredWorkSite
            {
                File = file,
                Line = LineOf(tree, invocation),
                Member = EnclosingMember(invocation),
                Kind = DeferredWorkKind.Delay,
                Construct = $"Task.Delay({DelayArgument(invocation)})",
                UsesInjectedClock = HasInjectedClockArgument(invocation)
            });
        }

        return sites;
    }

    /// <summary>
    /// ¿El fichero declara un miembro <b>público</b> con ese nombre?
    ///
    /// Es la mitad estática del patrón del hito 173: el paso que ejecuta un temporizador tiene que ser
    /// alcanzable desde el suite —el mismo método que corre en el producto, no una copia que pueda divergir—.
    /// </summary>
    public static bool HasPublicMember(string source, string memberName)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberName);

        SyntaxNode root = CSharpSyntaxTree.ParseText(source).GetRoot();

        foreach (SyntaxNode node in root.DescendantNodes())
        {
            if (!IsPublic(node))
            {
                continue;
            }

            bool matches = node switch
            {
                MethodDeclarationSyntax method => method.Identifier.Text == memberName,
                PropertyDeclarationSyntax property => property.Identifier.Text == memberName,
                EventDeclarationSyntax @event => @event.Identifier.Text == memberName,
                FieldDeclarationSyntax field => field.Declaration.Variables.Any(variable => variable.Identifier.Text == memberName),
                _ => false
            };

            if (matches)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPublic(SyntaxNode node) => node switch
    {
        MethodDeclarationSyntax method => method.Modifiers.Any(SyntaxKind.PublicKeyword),
        PropertyDeclarationSyntax property => property.Modifiers.Any(SyntaxKind.PublicKeyword),
        EventDeclarationSyntax @event => @event.Modifiers.Any(SyntaxKind.PublicKeyword),
        FieldDeclarationSyntax field => field.Modifiers.Any(SyntaxKind.PublicKeyword),
        _ => false
    };

    /// <summary>Recalcula las claves de una lista de sitios: la identidad la fija el inventario, no el analizador.</summary>
    public static IReadOnlyList<DeferredWorkSite> Inventory(IEnumerable<DeferredWorkSite> sites)
    {
        ArgumentNullException.ThrowIfNull(sites);

        var keyed = new List<DeferredWorkSite>();

        foreach (var group in sites
            .GroupBy(site => (site.File, site.Member, site.Kind))
            .OrderBy(group => group.Key.File, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Member, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Kind))
        {
            var ordered = group.OrderBy(site => site.Line).ToList();

            for (int index = 0; index < ordered.Count; index++)
            {
                string suffix = ordered.Count == 1 ? string.Empty : $"#{index + 1}";
                string key = $"{ordered[index].File}::{ordered[index].Member}::{ordered[index].Kind}{suffix}";

                keyed.Add(ordered[index] with { Key = key });
            }
        }

        return keyed.OrderBy(site => site.Key, StringComparer.Ordinal).ToList();
    }

    /// <summary>Inventario completo del código de producción del repositorio (host, Core, Sdk y plugins).</summary>
    public static IReadOnlyList<DeferredWorkSite> ScanRepository(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        return Inventory(ProductionSourceFiles(root).SelectMany(path => AnalyzeSource(
            File.ReadAllText(path),
            Relative(root, path))));
    }

    /// <summary>Todos los .cs de producción: ni el suite, ni los artefactos de compilación.</summary>
    public static IEnumerable<string> ProductionSourceFiles(string root) =>
        PluginSourceLocator.ProductionProjectNames(root)
            .Select(name => Path.Combine(root, name))
            .Where(Directory.Exists)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            .Where(path => !PluginSourceLocator.IsBuildArtifact(path))
            .OrderBy(path => path, StringComparer.Ordinal);

    /// <summary>
    /// El miembro que contiene el sitio. Se resuelve por el ancestro más cercano y no por el nombre del
    /// fichero: en un constructor puede haber un temporizador y un inicializador de campo, y la clave tiene que
    /// distinguir dónde vive cada uno.
    /// </summary>
    private static string EnclosingMember(SyntaxNode node)
    {
        foreach (SyntaxNode ancestor in node.Ancestors())
        {
            switch (ancestor)
            {
                case MethodDeclarationSyntax method:
                    return method.Identifier.Text;
                case ConstructorDeclarationSyntax:
                    return ".ctor";
                case DestructorDeclarationSyntax:
                    return "~";
                case LocalFunctionStatementSyntax local:
                    return local.Identifier.Text;
                case AccessorDeclarationSyntax accessor:
                    return accessor.Keyword.Text;
                case PropertyDeclarationSyntax property:
                    return property.Identifier.Text;
                case FieldDeclarationSyntax field when field.Declaration.Variables.Count > 0:
                    return field.Declaration.Variables[0].Identifier.Text;
                default:
                    continue;
            }
        }

        return "<top-level>";
    }

    private static string LastIdentifier(TypeSyntax type) => type switch
    {
        QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
        _ => LastIdentifierOfExpression(type) ?? type.ToString()
    };

    private static string? LastIdentifierOfExpression(SyntaxNode? expression) => expression switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.Text,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
        IdentifierNameSyntax identifier => identifier.Identifier.Text,
        _ => null
    };

    private static string? ReceiverOf(InvocationExpressionSyntax invocation) =>
        invocation.Expression is MemberAccessExpressionSyntax access ? access.Expression.ToString() : null;

    private static bool ContainsTimeProvider(string? text) =>
        text is not null && text.Contains(TimeProviderMarker, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Un retardo cuelga de un reloj inyectado cuando lleva el <c>TimeProvider</c> como segundo argumento
    /// (<c>Task.Delay(delay, timeProvider)</c>, la sobrecarga que .NET 8 añadió para exactamente esto).
    /// </summary>
    private static bool HasInjectedClockArgument(InvocationExpressionSyntax invocation)
    {
        var arguments = invocation.ArgumentList.Arguments;

        return arguments.Count >= 2 &&
            arguments.Skip(1).Any(argument => ContainsTimeProvider(argument.ToString()));
    }

    /// <summary>El primer argumento es la duración; es lo que hay que leer para saber qué se está aplazando.</summary>
    private static string DelayArgument(InvocationExpressionSyntax invocation) =>
        invocation.ArgumentList.Arguments.Count > 0
            ? invocation.ArgumentList.Arguments[0].ToString()
            : string.Empty;

    private static int LineOf(SyntaxTree tree, SyntaxNode node) =>
        tree.GetLineSpan(node.Span).StartLinePosition.Line + 1;

    private static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');
}
