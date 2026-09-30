using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// El censo de enlaces de geometría del <b>host Uno</b> (fase 3.1 del plan, riesgo #1 del plan): cada enlace
/// XAML que mueva puntos entre un ViewModel del núcleo (<see cref="FileFlow.Sdk.Point"/>) y un control de
/// WinUI/Uno debe llevar el conversor del host (<c>UnoPointConverter.Instance</c>). El motor de enlaces de
/// WinUI no convierte entre tipos distintos de punto — la misma lección del hito 211, que ya ocurrió en el
/// host original— y un enlace sin conversor no falla: <b>no mueve nada</b>, y el síntoma es un lienzo vacío
/// en el producto en vez de un rojo en el árbol.
///
/// <para>La lógica vive en TestHelpers (como el contrato de colecciones) para poder auto-testearla con
/// snippets sintéticos: probar que la guardia detecta una infracción no debe exigir plantar ficheros
/// infractores en el árbol del host.</para>
/// </summary>
public static class UnoGeometryBindingScanner
{
    /// <summary>El conversor que el XAML del host Uno debe citar en cada enlace de geometría.</summary>
    public const string ConverterLiteral = "UnoPointConverter";

    /// <summary>
    /// Enlaces que mueven puntos: las propiedades de geometría que los ViewModels del grafo exponen en
    /// <see cref="FileFlow.Sdk.Point"/>. La lista es de propiedades, no de controles: un enlace nuevo sobre
    /// una de éstas cae en el censo esté donde esté (EditorCanvas, tarjeta, spotlight…).
    /// </summary>
    public static readonly string[] GeometryProperties =
    [
        "Location",
        "Anchor",
        "Source",
        "Target",
        "ViewportLocation",
        "TargetLocation",
        "SpotlightScreenPosition",
        "SpotlightCanvasPosition"
    ];

    /// <summary>Patrón de un enlace XAML de WinUI/Uno sobre cualquiera de esas propiedades del ViewModel.</summary>
    private static readonly Regex GeometryBinding = new(
        @"\{Binding\s+(?<path>" + string.Join("|", GeometryProperties.Select(p => Regex.Escape(p))) + @")\b[^}]*\}",
        RegexOptions.Compiled);

    /// <summary>
    /// Analiza un fichero XAML (o un fragmento) del host Uno y devuelve, por cada enlace de geometría, la
    /// línea donde está y si lleva el conversor. Los comentarios XML del XAML no se excluyen a propósito: un
    /// enlace comentado que reaparezca al descomentar sigue estando censado cuando lo hagan.
    /// </summary>
    public static IReadOnlyList<GeometryBindingScan> Scan(string xamlSource)
    {
        var findings = new List<GeometryBindingScan>();

        string[] lines = xamlSource.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            foreach (Match match in GeometryBinding.Matches(lines[i]))
            {
                string binding = match.Value;
                bool hasConverter = binding.Contains(ConverterLiteral, StringComparison.Ordinal);

                findings.Add(new GeometryBindingScan(
                    i + 1,
                    binding,
                    match.Groups["path"].Value,
                    hasConverter));
            }
        }

        return findings;
    }

    /// <summary>
    /// El censo de <b>proyecciones en code-behind</b> (el equivalente WinUI de los enlaces del 211): donde
    /// WinUI no puede enlazar (Setter con Binding no evalúa), el cruce de espacio de grafo a espacio de
    /// ventana lo hace el código — y debe ser <b>explícito</b>. Dos formas de cruzar en silencio:
    ///
    /// <list type="number">
    /// <item><b>Posicionar en el Canvas leyendo la Location cruda</b> del ViewModel — debe ir por la
    /// posición ya proyectada del adaptador (<c>NodeCardViewModel.Position</c>, que pasa por el conversor)
    /// o citar el conversor.</item>
    /// <item><b>Construir un punto del framework desde .X/.Y</b> sin pasar por la proyección — la
    /// matemática de cables en espacio de grafo (<c>Sdk.Point</c>) es legítima; lo que no lo es es el
    /// cruce a <c>Windows.Foundation.Point</c> hecho a mano en vez de por <c>UnoPointProjection</c>.</item>
    /// </list>
    /// </summary>
    public static IReadOnlyList<string> FindCodeBehindViolations(string csharpSource)
    {
        var violations = new List<string>();
        string[] lines = csharpSource.Replace("\r\n", "\n").Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.TrimStart().StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            // 1. Posicionar en el Canvas con la Location cruda del ViewModel.
            if (Regex.IsMatch(line, @"Canvas\.Set(Left|Top)\s*\(")
                && line.Contains("Location", StringComparison.Ordinal)
                && !line.Contains("Position", StringComparison.Ordinal)
                && !line.Contains(ConverterLiteral, StringComparison.Ordinal))
            {
                violations.Add($"Línea {i + 1}: posiciona en el Canvas leyendo la Location cruda — debe ir por la proyección (NodeCardViewModel.Position o {ConverterLiteral}).");
            }

            // 2. Cruzar a un punto del framework sin la proyección (hecho a mano con .X/.Y).
            if (line.Contains("new Windows.Foundation.Point", StringComparison.Ordinal)
                && Regex.IsMatch(line, @"\.\s*(X|Y)\b")
                && !line.Contains("UnoPointProjection", StringComparison.Ordinal)
                && !line.Contains("Position", StringComparison.Ordinal)
                && !line.Contains(ConverterLiteral, StringComparison.Ordinal))
            {
                violations.Add($"Línea {i + 1}: construye un punto del framework desde coordenadas crudas — el cruce debe pasar por UnoPointProjection o {ConverterLiteral}.");
            }
        }

        return violations;
    }

    /// <summary>
    /// Los XAML del host Uno (sin obj/bin), la superficie que la guardia barre. Los directorios de
    /// compilación se <b>podan</b> al recorrer (no se entra en ellos) en vez de filtrar la lista ya
    /// enumerada: <c>bin</c> lleva el runtime de WinUI entero —miles de ficheros por corrida de compilación—
    /// y recorrerlo es trabajo de E/S que ninguna guardia necesita. Con varias guardias leyendo el árbol en
    /// paralelo, ese recorrido gratuito competía por el disco con las pruebas de temporización del suite.
    /// </summary>
    public static IEnumerable<string> UnoXamlFiles(string repositoryRoot)
        => XamlFilesUnder(Path.Combine(repositoryRoot, "FileFlow.App.Uno"));

    /// <summary>Los <c>*.xaml</c> de un directorio y sus subdirectorios, sin pisar <c>bin</c> ni <c>obj</c>.</summary>
    public static IEnumerable<string> XamlFilesUnder(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var found = new List<string>();
        Collect(directory, found);
        return found;

        static void Collect(string current, List<string> found)
        {
            found.AddRange(Directory.EnumerateFiles(current, "*.xaml"));

            foreach (string child in Directory.EnumerateDirectories(current))
            {
                string name = Path.GetFileName(child);
                if (name.Equals("obj", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("bin", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Collect(child, found);
            }
        }
    }

    /// <summary>Un enlace de geometría encontrado: dónde está y si lleva el conversor.</summary>
    public sealed record GeometryBindingScan(int Line, string Binding, string Path, bool HasConverter);
}
