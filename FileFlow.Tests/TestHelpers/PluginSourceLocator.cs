using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// Localiza en disco el código fuente de los proyectos de plugin, para las guardias que auditan fuentes.
///
/// El alcance sale de <c>FileFlow.slnx</c>, no del prefijo de la carpeta: así un plugin nuevo queda bajo
/// las guardias en cuanto se añade a la solución, aunque se llame distinto. Cuando no hay solución a mano
/// —las guardias que plantan un árbol de plugin temporal— se cae al prefijo convencional.
/// </summary>
internal static class PluginSourceLocator
{
    /// <summary>Proyectos de la solución que no son plugins (no contienen nodos).</summary>
    public static readonly string[] NonPluginProjects =
    [
        "FileFlow.App",
        "FileFlow.Core",
        "FileFlow.Sdk",
        "FileFlow.Tests"
    ];

    /// <summary>El suite no es producción: es lo que audita a la producción.</summary>
    public const string TestsProject = "FileFlow.Tests";

    /// <summary>Nombres de los proyectos de plugin declarados en la solución.</summary>
    public static IReadOnlyList<string> PluginProjectNames(string root) =>
        SolutionProjectNames(root)
            .Where(name => !NonPluginProjects.Contains(name, StringComparer.Ordinal))
            .ToList();

    /// <summary>
    /// Proyectos de producción declarados en la solución: todo menos el suite.
    ///
    /// Es el alcance de las guardias que tienen que ver <b>todo</b> el código que se envía —el host, Core, el
    /// Sdk y los plugins—: se toma de la solución y no de una lista escrita a mano, de modo que un proyecto
    /// nuevo entra en el barrido al añadirlo a <c>FileFlow.slnx</c>.
    /// </summary>
    public static IReadOnlyList<string> ProductionProjectNames(string root) =>
        SolutionProjectNames(root)
            .Where(name => !string.Equals(name, TestsProject, StringComparison.Ordinal))
            .ToList();

    /// <summary>Los proyectos que declara la solución, en el orden en que aparecen.</summary>
    private static IReadOnlyList<string> SolutionProjectNames(string root)
    {
        string solutionPath = Path.Combine(root, "FileFlow.slnx");

        if (!File.Exists(solutionPath))
        {
            return Directory.EnumerateDirectories(root, "FileFlow.*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .OfType<string>()
                .ToList();
        }

        return XDocument.Load(solutionPath)
            .Descendants()
            .Attributes("Path")
            .Select(attribute => attribute.Value.Replace('\\', '/').Split('/')[0])
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Directorios existentes de los proyectos de plugin.</summary>
    public static IEnumerable<string> PluginDirectories(string root) =>
        PluginProjectNames(root)
            .Select(name => Path.Combine(root, name))
            .Where(Directory.Exists);

    /// <summary>Todos los .cs de los plugins, excluidos los artefactos de compilación.</summary>
    public static IEnumerable<string> NodeSourceFiles(string root) =>
        PluginDirectories(root)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            .Where(path => !IsBuildArtifact(path));

    public static bool IsBuildArtifact(string path)
    {
        string normalized = path.Replace('\\', '/');

        return normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase);
    }
}
