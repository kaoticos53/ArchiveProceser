using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// Lectura del árbol de fuentes para las guardias que auditan el repositorio leyéndolo (puertos de los nodos,
/// contratos de estilo, inventarios). Existe para que esas guardias no se copien la enumeración: dos barridos
/// con criterios distintos sobre el mismo árbol dan veredictos distintos, y el que se olvide de excluir los
/// generados falla los días de compilación limpia.
///
/// <para>Los predicados se evalúan sobre la ruta <b>absoluta</b> normalizada, y solo después se relativiza: una
/// ruta relativa al repositorio empieza por el nombre del proyecto —<c>FileFlow.Plugin.Archives/…</c>— así que
/// filtrar por <c>/FileFlow.Plugin.</c> sobre ella no encuentra nada y deja el barrido vacío. Es el fallo
/// silencioso de esta clase de guardias: pasar en verde por no haber mirado.</para>
/// </summary>
public static class SourceTree
{
    /// <summary>Fuentes de los plugins del producto, que es donde viven los nodos.</summary>
    public static IEnumerable<(string File, string Source)> Plugins(string root) =>
        Files(root, p => p.Contains("/FileFlow.Plugin.", StringComparison.OrdinalIgnoreCase));

    /// <summary>Fuentes del proyecto de pruebas, sin la infraestructura compartida de <c>TestHelpers</c>.</summary>
    public static IEnumerable<(string File, string Source)> TestFiles(string root) =>
        Files(root,
            p => p.Contains("/FileFlow.Tests/", StringComparison.OrdinalIgnoreCase)
                 && !p.Contains("/TestHelpers/", StringComparison.OrdinalIgnoreCase));

    /// <summary>Todo el código del repositorio: rutas relativas normalizadas a <c>/</c> y sin generados.</summary>
    public static IEnumerable<(string File, string Source)> CSharpFiles(string root) =>
        Files(root, _ => true);

    private static IEnumerable<(string File, string Source)> Files(string root, Func<string, bool> matches)
    {
        IEnumerable<string> paths = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Select(p => p.Replace('\\', '/'))
            .Where(p => !p.Contains("/bin/", StringComparison.OrdinalIgnoreCase))
            .Where(p => !p.Contains("/obj/", StringComparison.OrdinalIgnoreCase))
            .Where(matches)
            .OrderBy(p => p, StringComparer.Ordinal);

        foreach (string path in paths)
        {
            yield return (Path.GetRelativePath(root, path).Replace('\\', '/'), File.ReadAllText(path));
        }
    }
}
