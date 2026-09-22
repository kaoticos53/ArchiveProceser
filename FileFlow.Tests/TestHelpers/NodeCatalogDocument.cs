using System.IO;
using System.Reflection;
using System.Text;
using FileFlow.App.Services;
using FileFlow.Core.Plugins;
using FileFlow.Sdk;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// El catálogo de nodos de <c>.agents/nodes_catalog.md</c>, <b>generado</b> desde el código: una fila por nodo
/// con su categoría, sus puertos, sus parámetros (clave y control) y el enlace al fichero que lo declara.
///
/// <para>
/// El documento no se escribe a mano porque escribirlo a mano es exactamente lo que lo dejó mintiendo: decía 49
/// nodos, repartidos en siete secciones, entre ellos varios que ya no existen (<c>ConditionalFilterNode</c>,
/// <c>ImageWatermarkNode</c>, <c>RoslynScriptNode</c>) y con enlaces a proyectos que tampoco
/// (<c>FileFlow.Plugin.Audio</c>). Nada de eso fallaba: era documentación, y la documentación no estaba
/// atada a nada. Ahora lo está: <c>NodeCatalogGuardTests</c> compara este texto con el que descubre el
/// cargador de la aplicación —el mismo camino que usa la app al arrancar— y falla si dejan de coincidir.
/// </para>
///
/// <para>
/// Todo lo que entra en el documento es <b>determinista</b>: el nombre de la clase tal y como se escribe en el
/// archivo del flujo, la categoría literal del <see cref="NodeDefinitionAttribute"/>, los nombres de puerto y
/// las claves y controles de los descriptores de parámetro. Los textos traducidos (<c>Name</c>, <c>Description</c>,
/// <c>HelpText</c>) quedan fuera a propósito: dependen del idioma del proceso, y un archivo comprometido que
/// cambie según la máquina que ejecute las pruebas no sirve como referencia.
/// </para>
/// </summary>
public static class NodeCatalogDocument
{
    /// <summary>Ruta del catálogo, relativa a la raíz del repositorio.</summary>
    public const string RelativePath = ".agents/nodes_catalog.md";

    private const string DocumentationPath = "docs/history/2026-09-13_nodes_catalog_full.md";

    /// <summary>¿Se pidió regenerar el catálogo? Mismo interruptor que las líneas base visuales.</summary>
    public static bool UpdateRequested =>
        Environment.GetEnvironmentVariable("FILEFLOW_UPDATE_NODE_CATALOG") is "1" or "true" or "TRUE";

    /// <summary>Ruta absoluta del catálogo.</summary>
    public static string PathOf(string repositoryRoot) =>
        Path.Combine(repositoryRoot, ".agents", "nodes_catalog.md");

    /// <summary>El catálogo completo, tal y como se escribe en el archivo.</summary>
    public static string Generate()
    {
        string root = TestRepositoryLocator.RepositoryRoot();
        var sourceByNode = SourcePathByNodeType(root);

        PluginLoader loader = PluginRegistryHelper.CreateConfiguredLoader();

        var nodes = ProductNodeTypes(loader)
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToList();

        var missingSource = nodes.Where(type => !sourceByNode.ContainsKey(FullNameOf(type))).ToList();
        if (missingSource.Count > 0)
        {
            throw new InvalidOperationException(
                "Estos nodos no tienen fichero fuente localizable, así que el catálogo no puede enlazarlos: " +
                string.Join(", ", missingSource.Select(FullNameOf)));
        }

        // El salto de línea es explícito y no el del sistema: el documento es el mismo en Windows y en Linux,
        // así que la comparación con el archivo comprometido no depende de la máquina que ejecute las pruebas.
        var text = new StringBuilder();

        void Line(string content = "") => text.Append(content).Append('\n');

        Line("# Catálogo de Nodos de FileFlow Studio");
        Line();
        Line($"**{nodes.Count} nodos de producción**, generados desde el código. Este documento no se edita a mano:");
        Line("lo produce `NodeCatalogDocument` a partir del catálogo que descubre el cargador de la aplicación");
        Line("—el mismo camino que ejecuta la app al arrancar— y `NodeCatalogGuardTests` falla si deja de coincidir con él.");
        Line();
        Line("Para regenerarlo tras añadir, quitar o cambiar un nodo:");
        Line();
        Line("```bash");
        Line("FILEFLOW_UPDATE_NODE_CATALOG=1 dotnet test --filter NodeCatalogGuardTests");
        Line("```");
        Line();
        Line("> Especificación extendida, con descripción de cada parámetro:" +
            $" [`{Path.GetFileName(DocumentationPath)}`](file:///{DocumentationPath}).");
        Line();

        int section = 0;

        foreach (var plugin in nodes.GroupBy(type => type.Assembly.GetName().Name ?? "?", StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            section++;
            var pluginNodes = plugin.OrderBy(type => type.Name, StringComparer.Ordinal).ToList();

            Line("---");
            Line();
            Line($"## {section}. {plugin.Key} ({pluginNodes.Count} {(pluginNodes.Count == 1 ? "nodo" : "nodos")})");
            Line();
            Line("| Nodo | Categoría | Entradas | Salidas | Parámetros | Código fuente |");
            Line("| :--- | :--- | :--- | :--- | :--- | :--- |");

            foreach (Type type in pluginNodes)
            {
                Line(Row(type, sourceByNode[FullNameOf(type)]));
            }

            Line();
        }

        return text.ToString();
    }

    /// <summary>Nodos del producto: los que declaran los plugins, sin los dobles del ensamblado de pruebas.</summary>
    public static IReadOnlyList<Type> ProductNodeTypes(PluginLoader loader)
    {
        Assembly testAssembly = typeof(NodeCatalogDocument).Assembly;

        return
        [
            .. loader.UniqueNodeTypes
                .Where(type => type.Assembly != testAssembly)
                .DistinctBy(FullNameOf)
                .OrderBy(FullNameOf, StringComparer.Ordinal)
        ];
    }

    /// <summary>Nombre completo del nodo: la identidad con la que lo busca el cargador y lo escribe el archivo.</summary>
    public static string FullNameOf(Type type) => type.FullName ?? $"{type.Namespace}.{type.Name}";

    private static string Row(Type type, string sourcePath)
    {
        string category = type.GetCustomAttribute<NodeDefinitionAttribute>()?.Category ?? "—";

        if (InstanceOf(type) is not { } node)
        {
            return $"| **{type.Name}** | {category} | — | — | — | [`{Path.GetFileName(sourcePath)}`](file:///{sourcePath}) |";
        }

        string inputs = Ports(node.Inputs);
        string outputs = Ports(node.Outputs);
        string parameters = Parameters(node.ParameterDescriptors);

        return $"| **{type.Name}** | {category} | {inputs} | {outputs} | {parameters} | " +
            $"[`{Path.GetFileName(sourcePath)}`](file:///{sourcePath}) |";
    }

    /// <summary>Instancia el nodo por el camino del cargador; <c>null</c> si el tipo no se puede instanciar.</summary>
    private static IFlowNode? InstanceOf(Type type)
    {
        try
        {
            return Activator.CreateInstance(type) as IFlowNode;
        }
        catch (Exception)
        {
            // Un nodo que no se puede instanciar no es asunto de este documento: lo caza la guardia de
            // runtime. Aquí sólo se evita que el catálogo no se pueda ni generar por su culpa.
            return null;
        }
    }

    private static string Ports(IReadOnlyList<NodePort> ports) =>
        ports.Count == 0 ? "—" : string.Join(", ", ports.Select(port => $"`{port.Name}`"));

    private static string Parameters(IReadOnlyList<NodeParameterDescriptor> descriptors) =>
        descriptors.Count == 0
            ? "—"
            : string.Join(", ", descriptors.Select(d => $"`{d.Key}` ({d.EditorType})"));

    /// <summary>Fichero, relativo al repositorio, que declara cada tipo de nodo.</summary>
    private static Dictionary<string, string> SourcePathByNodeType(string repositoryRoot)
    {
        var sources = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (string path in PluginSourceLocator.NodeSourceFiles(repositoryRoot))
        {
            string relative = Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');

            foreach (string fullName in NodeArchitectureAnalyzer.ConcreteNodeTypeFullNames(File.ReadAllText(path)))
            {
                sources[fullName] = relative;
            }
        }

        return sources;
    }
}
