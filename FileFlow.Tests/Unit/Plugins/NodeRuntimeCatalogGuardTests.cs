using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FileFlow.App.Services;
using FileFlow.Core.Plugins;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

/// <summary>
/// Guardia de coherencia entre el <b>código fuente</b> y el <b>catálogo real de nodos</b>: exige que el
/// conjunto de clases de nodo declaradas en los plugins y el que el cargador descubre en tiempo de
/// ejecución sean exactamente el mismo, y que cada uno de ellos sea instanciable por su nombre.
///
/// Por qué existe: las otras guardias son estáticas (leen fuentes) y <c>ToolboxOrganizationTests</c> cuenta
/// el catálogo de runtime, pero nadie comparaba ambos conjuntos. La diferencia importa en los dos sentidos:
///
/// <list type="bullet">
/// <item><b>Declarado pero no descubierto (huérfano):</b> un nodo que nadie puede instanciar —una clase que
/// el cargador descarta, un ensamblado que no se carga— pasa desapercibido porque la app simplemente no lo
/// muestra en el Toolbox.</item>
/// <item><b>Descubierto pero no declarado:</b> un tipo que entra en el catálogo sin estar en las fuentes de
/// ningún plugin. Al escribir esta guardia aparecieron ocho, todos dobles de prueba del ensamblado
/// <c>FileFlow.Tests</c> (<c>FakeFlowNode</c>, <c>MockNode</c>, <c>DummyFailingNode</c>…): el cargador
/// descubre cualquier tipo concreto que implemente <c>IFlowNode</c> sin mirar el ensamblado, así que el
/// barrido del AppDomain los metía en el catálogo y lo inflaba de 70 a 78. Ese 78 estaba escrito como
/// «78 official nodes» en <c>ToolboxOrganizationTests</c>. La causa se corrigió en el cargador
/// (<c>PluginLoader.IsTestAssembly</c>) y el tercer test de este fichero la mantiene corregida.</item>
/// </list>
///
/// Usa <see cref="PluginRegistryHelper.CreateConfiguredLoader"/>, que es exactamente el camino que ejecuta
/// la app al arrancar (ensamblados incorporados + directorio de plugins + barrido del AppDomain), no un
/// cargador montado a mano para el test.
/// </summary>
// Registra recursos de los plugins en LocalizationManager (estado global de idioma del proceso): se
// serializa con la colección que ya confina ese estado.
[Collection("VisualSnapshots")]
public class NodeRuntimeCatalogGuardTests
{
    private const string DeclaredNodeSample = "FileFlow.Plugin.Logic.ThrottleDelayNode";

    /// <summary>El ensamblado que ejecuta estas pruebas: sus dobles no son nodos de producto.</summary>
    private static readonly Assembly TestAssembly = typeof(NodeRuntimeCatalogGuardTests).Assembly;

    /// <summary>Nodos de producto (los que declaran los plugins), sin los dobles del ensamblado de tests.</summary>
    private static System.Collections.Generic.List<Type> ProductNodes(PluginLoader loader) =>
        loader.UniqueNodeTypes.Where(type => type.Assembly != TestAssembly).ToList();

    [Fact]
    public void EveryDeclaredNode_ShouldBeRegisteredAtRuntime_AndNoneShouldBeOrphaned()
    {
        string root = TestRepositoryLocator.RepositoryRoot();

        var declared = PluginSourceLocator.NodeSourceFiles(root)
            .SelectMany(path => NodeArchitectureAnalyzer.ConcreteNodeTypeFullNames(File.ReadAllText(path)))
            .ToHashSet(StringComparer.Ordinal);

        PluginLoader loader = PluginRegistryHelper.CreateConfiguredLoader();

        var discovered = ProductNodes(loader)
            .Select(type => type.FullName ?? type.Name)
            .ToHashSet(StringComparer.Ordinal);

        // Un barrido vacío o parcial haría pasar la comparación sin comprobar nada.
        declared.Count.Should().BeGreaterThan(50, "los plugins declaran bastantes más de 50 nodos");
        declared.Should().Contain(DeclaredNodeSample, "el barrido debe estar leyendo los plugins de verdad");
        discovered.Should().NotBeEmpty("el cargador debe descubrir el catálogo de los plugins");

        // Se comparan las diferencias en lugar de los conjuntos completos: un BeEquivalentTo sobre 70
        // elementos trunca el mensaje («…39 more…») y el nodo infractor —lo único que importa para
        // arreglarlo— no llega a leerse.
        var orphans = declared.Except(discovered).OrderBy(name => name, StringComparer.Ordinal).ToList();
        var intruders = discovered.Except(declared).OrderBy(name => name, StringComparer.Ordinal).ToList();

        orphans.Should().BeEmpty(
            "cada nodo declarado en un plugin debe quedar registrado en el catálogo de runtime; " +
            $"huérfanos = [{string.Join(", ", orphans)}]");

        intruders.Should().BeEmpty(
            "el catálogo no puede contener tipos que ningún plugin declare; " +
            $"intrusos = [{string.Join(", ", intruders)}]");
    }

    [Fact]
    public void EveryRegisteredNode_ShouldBeInstantiableByName()
    {
        // Estar en el catálogo no basta: el Toolbox crea el nodo con CreateNodeInstance(nombre), que usa
        // Activator.CreateInstance. Un nodo sin constructor sin parámetros —o abstracto por error— se
        // registraría y luego fallaría al arrastrarlo al lienzo.
        PluginLoader loader = PluginRegistryHelper.CreateConfiguredLoader();
        var nodes = ProductNodes(loader);

        nodes.Should().NotBeEmpty();

        foreach (Type type in nodes)
        {
            string fullName = type.FullName ?? type.Name;

            loader.CreateNodeInstance(fullName)
                .Should().NotBeNull($"el cargador debe poder instanciar {fullName} por su nombre completo")
                .And.BeAssignableTo(type);

            loader.CreateNodeInstance(type.Name)
                .Should().NotBeNull($"el catálogo también indexa {type.Name} por su nombre corto");
        }
    }

    [Fact]
    public void TheCatalog_ShouldBeComposedOnlyOfPluginAssemblies()
    {
        // El conjunto de ensamblados válidos sale de la solución, no de un prefijo de nombre, para que un
        // plugin nuevo (o renombrado) siga estando dentro y cualquier otro ensamblado quede fuera.
        var pluginAssemblies = PluginSourceLocator
            .PluginProjectNames(TestRepositoryLocator.RepositoryRoot())
            .ToHashSet(StringComparer.Ordinal);

        PluginLoader loader = PluginRegistryHelper.CreateConfiguredLoader();

        var foreign = ProductNodes(loader)
            .Where(type => !pluginAssemblies.Contains(type.Assembly.GetName().Name ?? string.Empty))
            .Select(type => $"{type.FullName} [{type.Assembly.GetName().Name}]")
            .ToList();

        foreign.Should().BeEmpty(
            "el catálogo del producto lo componen los plugins: ningún otro ensamblado debe aportar nodos. " +
            "El ensamblado de pruebas aportaba ocho dobles (FakeFlowNode, MockNode, DummyFailingNode…) que " +
            "inflaban el catálogo de 70 a 78 y eran lo que el Toolbox mostraba en el host de pruebas");
    }

    [Fact]
    public void TheCatalog_ShouldNotContainShortNameCollisions()
    {
        PluginLoader loader = PluginRegistryHelper.CreateConfiguredLoader();

        // El cargador indexa cada nodo por nombre completo Y por nombre corto, así que dos homónimos en
        // plugins distintos se pisan y el segundo queda inalcanzable desde el Toolbox.
        loader.UniqueNodeTypes
            .GroupBy(type => type.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key} → {string.Join(", ", group.Select(type => type.FullName))}")
            .Should().BeEmpty("el catálogo indexa también por nombre corto, y un homónimo dejaría un nodo huérfano");

        loader.DiscoveredNodesCount.Should().Be(
            loader.UniqueNodeTypes.Count,
            "el contador que ve la interfaz no puede incluir claves duplicadas");
    }

    [Fact]
    public void AbstractNodeBases_ShouldBeNodesInSource_ButNotRegistrable()
    {
        // Las bases abstractas derivan de FlowNodeBase (y la guardia de arquitectura las analiza), pero no
        // son instanciables, así que el cargador las descarta: la comparación de conjuntos debe excluirlas
        // por el lado de las fuentes, no por el de runtime.
        PluginLoader loader = PluginRegistryHelper.CreateConfiguredLoader();

        loader.UniqueNodeTypes
            .Select(type => type.FullName ?? type.Name)
            .Should().NotContain([
                "FileFlow.Plugin.AI.AiFlowNodeBase",
                "FileFlow.Plugin.AI.AudioAiFlowNodeBase"
            ]);

        NodeArchitectureAnalyzer
            .ConcreteNodeTypeFullNames(File.ReadAllText(Path.Combine(
                TestRepositoryLocator.RepositoryRoot(),
                "FileFlow.Plugin.AI/Common/AiFlowNodeBase.cs")))
            .Should().BeEmpty("una base abstracta no es un nodo registrable");
    }
}
