using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// Analizador puro de la capa de estilos para el <b>estado deshabilitado</b>: encuentra las reglas que atenúan
/// con <c>Opacity</c> la parte de plantilla que contiene la etiqueta.
///
/// <para><b>Por qué está mal y por qué es una regla del proyecto</b>: el tema base de Fluent ya atenúa el primer
/// plano del control deshabilitado en esa misma parte (<c>Foreground = ButtonForegroundDisabled</c>). Una
/// <c>Opacity</c> sobre la parte <i>multiplica</i> esa atenuación con la nuestra, porque dentro de la parte vive
/// también el texto. Medido con las líneas base del producto y con el tablero de estados: la etiqueta
/// deshabilitada quedaba en 2,13:1 sobre el tema oscuro y en <b>1,20:1</b> sobre el claro.</para>
///
/// <para>Lo que hay que hacer en su lugar es declarar la cara (un color propio del tema, como los tokens
/// <c>Accent*MutedBrush</c>) y el primer plano, de modo que quede <b>un solo</b> nivel de atenuación.</para>
///
/// <para>Sólo se señalan las reglas que atenúan una parte con <b>contenido</b> (<c>ContentPresenter</c>): la
/// opacidad de un borde o de una capa de fondo no toca la etiqueta, que es el caso legítimo de los campos de
/// texto y los desplegables.</para>
/// </summary>
public static class DisabledStateAnalyzer
{
    /// <summary>
    /// Apertura de una regla: <c>&lt;Style</c> seguido de espacio o de <c>&gt;</c>. El lookahead es lo que
    /// distingue la regla del elemento raíz <c>&lt;Styles&gt;</c> —que si no se colaba como una regla y atribuía
    /// el selector de la primera regla hija a un cuerpo que es el fichero entero—.
    /// </summary>
    private static readonly Regex OpeningRegex = new(@"<Style(?=[\s>/])", RegexOptions.Compiled);

    /// <summary>El selector de una regla, que va inmediatamente después de la apertura.</summary>
    private static readonly Regex SelectorRegex = new(
        "<Style\\s+Selector=\"(?<selector>[^\"]+)\"",
        RegexOptions.Compiled);

    /// <summary>Un <c>Opacity</c> declarado como propiedad dentro de la regla.</summary>
    private static readonly Regex OpacityRegex = new("Property=\"Opacity\"", RegexOptions.Compiled);

    /// <summary>
    /// Comentarios XML. Se retiran porque esta capa no es C# (el quitacomentarios del suite entiende <c>//</c> y
    /// <c>/* */</c>) y un comentario que <b>explica</b> el defecto no puede leerse como el defecto.
    /// </summary>
    private static readonly Regex XmlCommentRegex = new("<!--.*?-->", RegexOptions.Compiled | RegexOptions.Singleline);

    private const string CloseTag = "</Style>";

    /// <summary>
    /// Devuelve una entrada por regla infractora. <paramref name="file"/> sólo se usa en el mensaje.
    /// </summary>
    public static IReadOnlyList<string> Analyze(string xaml, string file)
    {
        ArgumentNullException.ThrowIfNull(xaml);

        var violations = new List<string>();
        string text = XmlCommentRegex.Replace(xaml, string.Empty);

        int index = 0;

        while (true)
        {
            int open = NextOpening(text, index);

            if (open < 0)
            {
                return violations;
            }

            // Las reglas anidadas se visitan igual: el recorrido avanza hasta la apertura, no hasta el final de
            // la regla que la contiene.
            index = open + "<Style".Length;

            int tagEnd = text.IndexOf('>', open);
            Match selector = SelectorRegex.Match(text, open);

            // El selector tiene que ser el de esta regla: si no cae dentro de su etiqueta de apertura, la regla
            // no lo declara y no hay nada que analizar.
            if (tagEnd < 0 || !selector.Success || selector.Index > tagEnd)
            {
                continue;
            }

            string value = selector.Groups["selector"].Value;

            if (!value.Contains(":disabled", StringComparison.Ordinal)
                || !value.Contains("ContentPresenter", StringComparison.Ordinal)
                || !OpacityRegex.IsMatch(Body(text, selector.Index + selector.Length)))
            {
                continue;
            }

            violations.Add(
                $"{file}: la regla '{value}' atenúa con Opacity la parte que contiene la etiqueta. El tema base ya " +
                "atenúa el primer plano deshabilitado en esa misma parte, así que las dos atenuaciones se " +
                "multiplican y el texto queda ilegible (medido: 1,20:1 en el tema claro). Declara la cara como un " +
                "color del tema (por ejemplo AccentPrimaryMutedBrush) y el primer plano con TextMutedBrush o " +
                "TextPrimaryBrush en lugar de atenuar la parte.");
        }
    }

    /// <summary>Índice de la siguiente apertura de regla a partir de <paramref name="from"/>, o -1.</summary>
    private static int NextOpening(string text, int from)
    {
        Match match = OpeningRegex.Match(text, from);
        return match.Success ? match.Index : -1;
    }

    /// <summary>
    /// Cuerpo de la regla cuyo selector termina en <paramref name="afterSelector"/>, contando las reglas
    /// anidadas para no confundir el cierre de una hija con el de su madre.
    /// </summary>
    private static string Body(string text, int afterSelector)
    {
        int start = text.IndexOf('>', afterSelector);

        if (start < 0)
        {
            return string.Empty;
        }

        int depth = 1;
        int cursor = start + 1;

        while (true)
        {
            int nested = NextOpening(text, cursor);
            int closes = text.IndexOf(CloseTag, cursor, StringComparison.Ordinal);

            if (closes < 0)
            {
                return text[(start + 1)..];
            }

            if (nested >= 0 && nested < closes)
            {
                depth++;
                cursor = nested + "<Style".Length;
                continue;
            }

            depth--;

            if (depth == 0)
            {
                return text[(start + 1)..closes];
            }

            cursor = closes + CloseTag.Length;
        }
    }
}
