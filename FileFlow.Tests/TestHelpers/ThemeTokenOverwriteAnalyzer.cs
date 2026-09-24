using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// Analiza vistas y code-behind para detectar el patrón que tumbó el barrido de la splash: código que
/// <b>sobrescribe una propiedad que el tema posee</b> —una enlazada con <c>{DynamicResource}</c> en el XAML—
/// y luego <b>da por hecho su valor</b>.
///
/// <para><b>Por qué es frágil</b>: una propiedad enlazada a un token no es del código que la escribe.
/// <c>ThemeManager.ApplyResourceDictionary</c> reemplaza las entradas de <c>Application.Resources</c> al
/// aplicar un tema (arranque, Theme Studio, cambio de tema), Avalonia vuelve a evaluar el
/// <c>DynamicResource</c> y escribe encima el valor del token. Lo que el código había asignado desaparece sin
/// aviso: el muestrario de color volvía al acento del tema, y el barrido de la splash se encontró con un
/// <c>SolidColorBrush</c> donde esperaba un <c>LinearGradientBrush</c> y lanzó <c>InvalidCastException</c> en
/// su primer tick (hito 169).</para>
///
/// <para>El análisis es sintáctico (Roslyn + XDocument), no por expresiones regulares: la línea reportada es
/// la real y no hay falsos positivos por comentarios o cadenas.</para>
/// </summary>
public static class ThemeTokenOverwriteAnalyzer
{
    /// <summary>Castea un pincel de un control como si su tipo fuera una certeza.</summary>
    public const string RuleBlindBrushCast = "Pincel-casteado-a-ciegas";

    /// <summary>Escribe en código una propiedad que el XAML de la propia vista enlaza a un token del tema.</summary>
    public const string RuleTokenOverwrite = "Escritura-sobre-propiedad-del-tema";

    /// <summary>
    /// Tipos de pincel que el tema puede colocar donde el código esperaba otro: castear cualquiera de ellos
    /// sobre una propiedad de control es la suposición que hay que prohibir.
    /// </summary>
    public static readonly IReadOnlySet<string> BrushTypeNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "SolidColorBrush",
        "LinearGradientBrush",
        "RadialGradientBrush",
        "ConicGradientBrush",
        "GradientBrush",
        "ImageBrush",
        "ISolidColorBrush",
        "IBrush"
    };

    /// <summary>
    /// Propiedades de un control cuyo valor puede venir —y ser reemplazado— por el tema.
    /// </summary>
    public static readonly IReadOnlySet<string> ThemeOwnedProperties = new HashSet<string>(StringComparer.Ordinal)
    {
        "Foreground",
        "Background",
        "BorderBrush",
        "Fill",
        "Stroke",
        "BoxShadow",
        "CaretBrush"
    };

    /// <summary>Nombre de la lectura que prueba que el código no da por hecho el valor: comparación por identidad.</summary>
    public const string IdentityCheckMethod = "ReferenceEquals";

    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>
    /// Analiza un fichero de código: la única regla que depende solo del código es la del casteo a ciegas
    /// (la escritura sobre una propiedad del tema necesita el XAML de su vista, ver <see cref="AnalyzeView"/>).
    /// </summary>
    public static IReadOnlyList<ThemeTokenOverwriteViolation> AnalyzeCode(string source, string file)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(file);

        var violations = new List<ThemeTokenOverwriteViolation>();
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetRoot();

        foreach (var cast in root.DescendantNodes().OfType<CastExpressionSyntax>())
        {
            if (!IsBrushType(cast.Type))
            {
                continue;
            }

            string? touched = ThemeOwnedMemberTouched(cast.Expression);

            if (touched is null)
            {
                continue;
            }

            violations.Add(new ThemeTokenOverwriteViolation(
                file,
                LineOf(tree, cast),
                touched,
                RuleBlindBrushCast,
                $"castear '{cast.Type}' sobre '{touched}' da por hecho un tipo que el tema reescribe: " +
                $"comprueba con 'is'/'as' o compara la instancia con '{IdentityCheckMethod}' antes de usarla"));
        }

        return violations;
    }

    /// <summary>
    /// Analiza una vista completa: el code-behind escribe en una propiedad que el XAML de esa misma vista
    /// enlaza a un token y no hay ninguna lectura que compruebe el valor antes de depender de él.
    /// </summary>
    public static IReadOnlyList<ThemeTokenOverwriteViolation> AnalyzeView(
        string xamlSource,
        string codeBehindSource,
        string xamlFile,
        string codeFile)
    {
        ArgumentNullException.ThrowIfNull(xamlSource);
        ArgumentNullException.ThrowIfNull(codeBehindSource);

        var bound = TokenBoundMembers(xamlSource);

        if (bound.Count == 0)
        {
            return [];
        }

        var guarded = GuardedReads(codeBehindSource);
        var violations = new List<ThemeTokenOverwriteViolation>();

        foreach (var (element, property, line) in ThemeOwnedAssignments(codeBehindSource))
        {
            string key = $"{element}.{property}";

            if (!bound.Contains(key) || guarded.Contains(key))
            {
                continue;
            }

            violations.Add(new ThemeTokenOverwriteViolation(
                codeFile,
                line,
                key,
                RuleTokenOverwrite,
                $"'{key}' está enlazada a un token del tema en {xamlFile}, así que la asignación no es del " +
                "código: al republicarse el tema (arranque, Theme Studio, cambio de tema) el recurso se vuelve " +
                $"a evaluar por encima. Escribe el valor en una propiedad que el tema no posea, o compruébalo " +
                $"con '{IdentityCheckMethod}' antes de depender de él"));
        }

        return violations;
    }

    /// <summary>
    /// Miembros con nombre de elemento que el XAML enlaza a un token —<c>{DynamicResource …}</c>— en forma
    /// <c>Elemento.Propiedad</c>. Es el mapa de lo que el tema puede reescribir en esa vista.
    /// </summary>
    public static IReadOnlyCollection<string> TokenBoundMembers(string xamlSource)
    {
        var members = new HashSet<string>(StringComparer.Ordinal);

        XDocument document;

        try
        {
            document = XDocument.Parse(xamlSource);
        }
        catch (System.Xml.XmlException)
        {
            // Un XAML que no parsea no es asunto de esta guardia: la compilación y las guardias de estilo lo
            // delatan; aquí se devuelve un mapa vacío en lugar de reventar con una excepción distinta.
            return members;
        }

        foreach (var element in document.Descendants())
        {
            string? name = element.Attribute(XamlNamespace + "Name")?.Value;

            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            foreach (var attribute in element.Attributes())
            {
                // Sólo propiedades sin prefijo (Background, Foreground…): las adjuntas (Grid.Row) y las que
                // llevan prefijo de tipo quedan fuera de esta regla.
                if (attribute.IsNamespaceDeclaration || attribute.Name.Namespace != XNamespace.None)
                {
                    continue;
                }

                if (!attribute.Value.Contains("{DynamicResource", StringComparison.Ordinal))
                {
                    continue;
                }

                members.Add($"{name}.{attribute.Name.LocalName}");
            }
        }

        return members;
    }

    /// <summary>
    /// Miembros de propiedad del tema que el código lee <b>sin dar por hecho su valor</b>: comprobación de
    /// tipo (<c>is</c>/<c>as</c>) o comparación por identidad (<c>ReferenceEquals</c>). Es la forma de escribir
    /// sobre una propiedad del tema sin que la republicación del tema rompa nada.
    /// </summary>
    public static IReadOnlyCollection<string> GuardedReads(string codeBehindSource)
    {
        var guarded = new HashSet<string>(StringComparer.Ordinal);
        var root = CSharpSyntaxTree.ParseText(codeBehindSource).GetRoot();

        foreach (var access in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            if (!ThemeOwnedProperties.Contains(access.Name.Identifier.Text))
            {
                continue;
            }

            string key = $"{access.Expression}.{access.Name.Identifier.Text}";

            if (IsTypeCheck(access) || IsIdentityCheck(access))
            {
                guarded.Add(key);
            }
        }

        return guarded;
    }

    /// <summary>
    /// Asignaciones del código a propiedades que el tema posee, con nombre de elemento, propiedad y línea.
    /// </summary>
    public static IReadOnlyCollection<(string Element, string Property, int Line)> ThemeOwnedAssignments(
        string codeBehindSource)
    {
        var assignments = new List<(string, string, int)>();
        var tree = CSharpSyntaxTree.ParseText(codeBehindSource);

        foreach (var assignment in tree.GetRoot().DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (assignment.Left is not MemberAccessExpressionSyntax left ||
                left.Expression is not IdentifierNameSyntax element ||
                !ThemeOwnedProperties.Contains(left.Name.Identifier.Text))
            {
                continue;
            }

            assignments.Add((element.Identifier.Text, left.Name.Identifier.Text, LineOf(tree, assignment)));
        }

        return assignments;
    }

    private static bool IsBrushType(TypeSyntax type) =>
        BrushTypeNames.Contains(SimpleTypeName(type));

    private static string SimpleTypeName(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.Text,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
        AliasQualifiedNameSyntax alias => alias.Name.Identifier.Text,
        _ => type.ToString()
    };

    /// <summary>
    /// Propiedad del tema que el código toca dentro de una expresión: el casteo puede envolver paréntesis,
    /// supresiones de nulo (<c>!</c>) o llamadas, como en <c>((LinearGradientBrush)bar.Foreground!)</c>.
    /// </summary>
    private static string? ThemeOwnedMemberTouched(ExpressionSyntax expression) =>
        expression.DescendantNodesAndSelf()
            .OfType<MemberAccessExpressionSyntax>()
            .Select(access => access.Name.Identifier.Text)
            .FirstOrDefault(ThemeOwnedProperties.Contains);

    private static bool IsTypeCheck(MemberAccessExpressionSyntax access) =>
        access.Parent is IsPatternExpressionSyntax ||
        (access.Parent is BinaryExpressionSyntax binary &&
         (binary.IsKind(SyntaxKind.IsExpression) || binary.IsKind(SyntaxKind.AsExpression)));

    private static bool IsIdentityCheck(MemberAccessExpressionSyntax access) =>
        access.Ancestors().OfType<InvocationExpressionSyntax>().Any(invocation =>
            invocation.Expression is IdentifierNameSyntax method &&
            string.Equals(method.Identifier.Text, IdentityCheckMethod, StringComparison.Ordinal) &&
            invocation.ArgumentList.Arguments.Any(argument =>
                argument.Expression == access || argument.Expression.DescendantNodesAndSelf().Contains(access)));

    private static int LineOf(SyntaxTree tree, SyntaxNode node) =>
        tree.GetLineSpan(node.Span).StartLinePosition.Line + 1;
}

/// <summary>Una escritura o suposición sobre una propiedad que el tema posee, con su ubicación exacta.</summary>
public sealed record ThemeTokenOverwriteViolation(
    string File,
    int Line,
    string Member,
    string Rule,
    string Reason)
{
    public override string ToString() =>
        $"{File}({Line}): [{Rule}] {Member} — {Reason}";
}
