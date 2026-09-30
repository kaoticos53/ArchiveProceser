using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// El censo de nombres de <c>VisualStateGroup</c> y <c>VisualState</c> de los XAML del <b>host Uno</b>: los
/// nombres de estado de UN archivo comparten un espacio de nombres, así que repetir cualquiera de ellos —el
/// convencional <c>CommonStates</c>, un <c>Normal</c>, un <c>Checked</c>— <b>rompe la compilación de XAML</b>.
///
/// <para><b>Por qué una guardia y no una prueba de compilación</b>: el fallo es de los peores. El
/// <c>XamlCompiler</c> de Windows App SDK sale con código 1 y <b>sin ningún mensaje</b> — no imprime la
/// infracción, no la escribe en <c>output.json</c> y MSBuild sólo puede reportar
/// <c>MSB3073: el comando … salió con el código 1</c>, sin fichero ni línea. Medido a golpe de bisección al
/// arreglar el catálogo (hito 290):</para>
///
/// <list type="number">
/// <item>Dos plantillas en el mismo <c>&lt;UserControl.Resources&gt;</c> con su grupo <c>CommonStates</c> cada
/// una → <b>falla</b>.</item>
/// <item>Las mismas dos, con el grupo de la segunda renombrado (<c>GroupHeaderStates</c>) y sus estados
/// también renombrados → <b>compila</b>: no es «un grupo por diccionario», es el NOMBRE.</item>
/// <item>Grupos con nombres distintos y un <c>&lt;VisualState x:Name="Normal" /&gt;</c> repetido en los dos →
/// <b>falla</b>: los nombres de <c>VisualState</c> colisionan igual que los de grupo.</item>
/// <item>Un <c>x:Name</c> repetido de un elemento normal (<c>Border</c>, <c>Grid</c>, <c>ContentPresenter</c>)
/// entre dos plantillas del mismo diccionario, o entre el contenido y los recursos → <b>compila</b>: la
/// colisión es de los nombres de estado, no de todos los nombres.</item>
/// <item>Un bloque de estados en el CONTENIDO de la página repitiendo el <c>CommonStates</c> de una plantilla
/// de los recursos → <b>falla</b>: el espacio de nombres es el <b>archivo entero</b>, no el diccionario.</item>
/// </list>
///
/// <para><b>La consecuencia práctica</b>: dentro de un XAML sólo UNA máquina de estados puede usar los
/// nombres convencionales —y usarlos no es opcional, porque los controles de WinUI piden sus estados por
/// nombre (<c>GoToState("Checked")</c>)—. Un segundo <c>ControlTemplate</c> con estados convencionales en el
/// mismo archivo no es cuestión de estilo: no compila. La cura es extraer cada plantilla con estados a su
/// propio archivo (el chip del catálogo, desde el hito 291, vive en <c>Themes/ControlStyles.xaml</c>), que es
/// lo que le devuelve a la página su espacio de nombres.</para>
///
/// <para><b>La segunda regla muda (hito 291): los estados tienen que colgar DENTRO del raíz de la
/// plantilla</b>, no ser hermanos suyos dentro del <c>ControlTemplate</c>. Las dos formas compilan, pero en
/// Uno sólo la primera se aplica: unos <c>VisualStateGroups</c> hermanos del raíz se quedan declarados y
/// mudos —el chip del catálogo nació sin pintar su estado seleccionado por esto— y nada avisa.
/// <see cref="InspectTemplateStateGroupPlacement"/> lo vigila.</para>
///
/// <para><b>Por qué no hay mutación</b>: el andamiaje de mutaciones declara defectos que <b>compilan</b> y
/// pasan desapercibidos (un <c>NO-COMPILA</c> es un veredicto de fallo, no de éxito) — y este defecto no
/// compila. La guardia es, aquí, toda la red: no hay una prueba roja que pueda demostrar que muerde, sólo la
/// medida de la bisección que la motivó.</para>
/// </summary>
public static class UnoVisualStateNameScanner
{
    private const string KindGroup = "VisualStateGroup";
    private const string KindState = "VisualState";

    /// <summary>Los comentarios XML del XAML no declaran estados: un nombre citado en un comentario no cuenta.</summary>
    private static readonly Regex XmlComment = new(
        @"<!--.*?-->",
        RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>
    /// Un elemento de estado y sus atributos. El cuerpo del elemento no puede contener <c>&lt;</c> ni
    /// <c>&gt;</c> —en XAML se escriben escapados—, así que basta con cortar en el primer cierre. El
    /// <c>\b</c> deja fuera a <c>&lt;VisualStateManager…&gt;</c>, que es el contenedor y no un estado.
    /// </summary>
    private static readonly Regex StateElement = new(
        @"<(?<kind>VisualStateGroup|VisualState)\b(?<attrs>[^<>]*?)/?>",
        RegexOptions.Compiled);

    private static readonly Regex NameAttribute = new(
        @"\bx:Name\s*=\s*""(?<name>[^""]+)""",
        RegexOptions.Compiled);

    /// <summary>
    /// Los nombres de estado repetidos dentro de un mismo XAML. El primer uso se registra y cada repetición se
    /// reporta citando las dos líneas: la que ya existía y la que vuelve a usarlo.
    /// </summary>
    public static IReadOnlyList<VisualStateNameCollision> FindCollisions(string xamlSource)
    {
        var firstUse = new Dictionary<string, VisualStateName>(StringComparer.Ordinal);
        var collisions = new List<VisualStateNameCollision>();

        foreach (var found in DeclaredStates(xamlSource))
        {
            if (firstUse.TryGetValue(found.Name, out var previous))
            {
                collisions.Add(new VisualStateNameCollision(found, previous));
            }
            else
            {
                firstUse[found.Name] = found;
            }
        }

        return collisions;
    }

    /// <summary>
    /// Todos los nombres de estado que DECLARA un XAML, en orden de aparición. Existe para que la guardia
    /// pueda probar que miró algo: una guardia que sólo afirma «no hay colisiones» también pasa si el patrón
    /// dejó de encontrar estados, y ese verde mentiría.
    /// </summary>
    public static IReadOnlyList<VisualStateName> DeclaredStates(string xamlSource)
    {
        string source = XmlComment.Replace(xamlSource.Replace("\r\n", "\n"), string.Empty);
        var lineOf = LineIndex(source);
        var declared = new List<VisualStateName>();

        foreach (Match element in StateElement.Matches(source))
        {
            Match name = NameAttribute.Match(element.Groups["attrs"].Value);
            if (!name.Success)
            {
                continue;
            }

            declared.Add(new VisualStateName(
                name.Groups["name"].Value,
                element.Groups["kind"].Value,
                lineOf(element.Index)));
        }

        return declared;
    }

    /// <summary>
    /// La línea (1-based) de cada posición del texto, para poder citar dónde está la repetición: el
    /// <c>XamlCompiler</c> no la cita, así que la cita la pone la guardia.
    /// </summary>
    private static Func<int, int> LineIndex(string source)
    {
        var starts = new List<int> { 0 };
        for (int i = 0; i < source.Length; i++)
        {
            if (source[i] == '\n')
            {
                starts.Add(i + 1);
            }
        }

        int[] array = starts.ToArray();
        return position =>
        {
            int low = 0, high = array.Length - 1;
            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                if (array[mid] <= position)
                {
                    low = mid;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return low + 1;
        };
    }

    /// <summary>
    /// Inspecciona dónde cuelga cada <c>VisualStateManager.VisualStateGroups</c> de cada
    /// <c>ControlTemplate</c>: dentro del elemento raíz de la plantilla (la forma que Uno APLICA) o como
    /// hermano suyo dentro del <c>ControlTemplate</c> (la forma que compila pero queda MUDA).
    ///
    /// <para>Se parsea el XAML como XML (lo es) y se cuenta cada bloque: un <c>VisualStateManager.VisualStateGroups</c>
    /// que sea HIJO DIRECTO de un <c>ControlTemplate</c> es un hallazgo, y se cita su línea.</para>
    /// </summary>
    public static TemplateStateGroupPlacement InspectTemplateStateGroupPlacement(string xamlSource)
    {
        var doc = XDocument.Parse(xamlSource, LoadOptions.SetLineInfo);
        int insideRoot = 0;
        var siblings = new List<int>();

        foreach (var template in doc.Descendants().Where(e => e.Name.LocalName == "ControlTemplate"))
        {
            foreach (var child in template.Elements())
            {
                if (child.Name.LocalName == "VisualStateManager.VisualStateGroups")
                {
                    siblings.Add(((IXmlLineInfo)child).LineNumber);
                }
            }

            insideRoot += template.Descendants()
                .Count(e => e.Name.LocalName == "VisualStateManager.VisualStateGroups"
                            && e.Parent?.Name.LocalName != "ControlTemplate");
        }

        return new TemplateStateGroupPlacement(insideRoot, siblings);
    }

    /// <summary>Un nombre de estado con su tipo y su línea: la mitad de una colisión.</summary>
    public sealed record VisualStateName(string Name, string Kind, int Line)
    {
        /// <summary>«VisualStateGroup «CommonStates» (línea 12)» — la pieza que el compilador no imprime.</summary>
        public override string ToString() => $"{Kind} «{Name}» (línea {Line})";
    }

    /// <summary>Dónde cuelgan los grupos de estados de una plantilla: cuántos dentro del raíz (los que se aplican) y las líneas de los que son hermanos del raíz (los mudos).</summary>
    public sealed record TemplateStateGroupPlacement(int InsideRoot, IReadOnlyList<int> SiblingOfRoot);

    /// <summary>La repetición y el primer uso que la convierte en un fallo de compilación.</summary>
    public sealed record VisualStateNameCollision(VisualStateName Repeated, VisualStateName First)
    {
        /// <summary>«se repite «Normal» (VisualState, línea 24): ya estaba en la línea 6».</summary>
        public string Describe() => $"se repite {Repeated}: ya estaba en la línea {First.Line}";
    }
}
