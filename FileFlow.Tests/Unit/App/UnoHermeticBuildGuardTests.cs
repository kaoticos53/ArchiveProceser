using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// La guardia del HOST ÚNICO y de la ausencia total de Avalonia.
///
/// <para><b>Qué protege.</b> (1) La solución única (<c>FileFlow.slnx</c>) trae el host Uno, su grafo entero y
/// el suite, y ya no existe ninguna solución de escritorio ni proyecto <c>FileFlow.App</c>; (2) el andamiaje
/// del «sabor doble» de UI (la constante que leían los nodos y la bandera que la elegía) ha desaparecido de
/// <c>Directory.Build.props</c>; (3) ningún plugin referencia paquetes de Avalonia; (4) <b>ningún fichero que
/// el producto compile menciona Avalonia</b> —ni en código, ni en comentarios, ni en XAML—; (5) cada nodo cuya
/// superficie el host no puede montar <b>declara la frontera</b> en vez de construirla a ciegas; (6) los textos
/// de esa frontera están en los dos diccionarios del plugin, y el nombre de la superficie que citan también; y
/// (7) el lanzador compila con <c>dotnet</c> sobre el proyecto del host, sin depender del nombre de una
/// solución para elegir el producto.</para>
///
/// <para><b>Por qué una guardia y no sólo el build.</b> El build prueba el árbol de HOY en la máquina de quien
/// lo corre; lo que hay que sostener es que el día que alguien reintroduzca una referencia o un <c>using</c> de
/// Avalonia, la suite lo cace sin depender de haber compilado la solución.</para>
/// </summary>
public class UnoHermeticBuildGuardTests
{
    private const string Solution = "FileFlow.slnx";
    private const string FlavourProps = "Directory.Build.props";
    private const string UnoHostProject = "FileFlow.App.Uno/FileFlow.App.Uno.csproj";
    private const string Launcher = "run-uno.ps1";
    private const string FrontierContract = "FileFlow.Sdk/Services/UnavailableSurface.cs";

    /// <summary>Los cinco plugins que traen superficies nativas propias (y declaran la frontera).</summary>
    private static readonly string[] PluginsWithNativeSurfaces =
    [
        "FileFlow.Plugin.AI",
        "FileFlow.Plugin.Archives",
        "FileFlow.Plugin.FileSystem",
        "FileFlow.Plugin.Integrations",
        "FileFlow.Plugin.Scripting",
    ];

    /// <summary>
    /// Los nodos cuya superficie el host no puede montar, con la clave del diccionario que da su nombre. La
    /// declaran por los diálogos de quien los abre (hito 268) en vez de construir una ventana a ciegas.
    /// </summary>
    private static readonly (string File, string NameKey, string Window)[] UnavailableSurfaceNodes =
    [
        ("FileFlow.Plugin.Archives/SmartUnpackNode.cs", "PasswordManager_WindowTitle", "PasswordManagerWindow"),
        ("FileFlow.Plugin.Archives/ArchiveFanOutNode.cs", "PasswordManager_WindowTitle", "PasswordManagerWindow"),
        ("FileFlow.Plugin.AI/Nodes/Vision/MultimodalVisionLlmNode.cs", "VlmConfig_WindowTitle", "MultimodalVlmConfigWindow"),
        ("FileFlow.Plugin.FileSystem/Nodes/Processing/AdvancedRenamerNode.cs", "AdvancedRenamer_WindowTitle", "AdvancedRenamerEditorWindow"),
        ("FileFlow.Plugin.FileSystem/Nodes/Sources/SyntheticDataSourceNode.cs", "DataSetDesigner_WindowTitle", "SyntheticDataSetDesignerWindow"),
        ("FileFlow.Plugin.Integrations/MediaTranscoderNode.cs", "PresetManager_WindowTitle", "MediaPresetManagerWindow"),
        ("FileFlow.Plugin.Scripting/CustomScriptNode.cs", "ScriptStudio_Title", "ScriptStudioWindow"),
    ];

    /// <summary>Extensiones que el producto compila y que, por tanto, no pueden mencionar Avalonia.</summary>
    private static readonly string[] CompiledExtensions = [".cs", ".xaml", ".csproj", ".axaml"];

    private static string Root() => TestRepositoryLocator.RepositoryRoot();

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(Root(), relativePath));

    private static string Code(string relativePath) => SourceText.CodeWithoutComments(relativePath);

    private static XDocument Project(string plugin) =>
        XDocument.Parse(Read(plugin + "/" + plugin + ".csproj"));

    /// <summary>El <c>Condition</c> del <c>ItemGroup</c> que contiene el item, o cadena vacía si no lo declara.</summary>
    private static string GroupCondition(XElement item) =>
        item.Parent?.Attribute("Condition")?.Value ?? string.Empty;

    // ─────────────────────────────────────────────────────────────────────────────
    // 1. La solución única
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheSingleSolution_ShouldCarryTheHostAndItsWholeGraph()
    {
        File.Exists(Path.Combine(Root(), Solution))
            .Should().BeTrue($"{Solution} es la solución canónica con la que se compila y se depura el producto");

        File.Exists(Path.Combine(Root(), "FileFlow.Uno.slnx"))
            .Should().BeFalse("la solución del host nació para elegir un sabor de UI que ya no existe: se consolida en una sola");

        var solution = XDocument.Parse(Read(Solution));
        var projects = solution.Descendants("Project")
            .Select(p => (string?)p.Attribute("Path") ?? string.Empty)
            .ToList();

        projects.Should().Contain(UnoHostProject, "la solución es del host: sin su proyecto no compila nada");

        // El host de escritorio (Avalonia) no existe y no puede viajar en el grafo.
        projects.Should().NotContain("FileFlow.App/FileFlow.App.csproj",
            "el host de escritorio con Avalonia fue eliminado: no cabe en el grafo");
        projects.Should().NotContain(p => p.Split('/')[0] == "FileFlow.App",
            "no queda ningún proyecto FileFlow.App: la capa portable es FileFlow.App.Core");

        // Y la otra dirección: todo lo que el host referencia tiene que estar en la solución.
        var declared = projects.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var references = XDocument.Parse(Read(UnoHostProject))
            .Descendants("ProjectReference")
            .Select(r => ((string?)r.Attribute("Include") ?? string.Empty).Replace('\\', '/'))
            .Select(p => p.StartsWith("../", StringComparison.Ordinal) ? p[3..] : p)
            .ToList();

        references.Should().NotBeEmpty("el host referencia su grafo: el censo no puede estar vacío");
        var missing = references.Where(r => !declared.Contains(r)).ToList();
        missing.Should().BeEmpty(
            "todo proyecto que el host referencia tiene que estar en la solución: un grafo declarado a medias "
            + "compila otra cosa de la que el script compila");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 2. Sin andamiaje de sabor doble
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheFlavourScaffolding_ShouldBeGone()
    {
        string props = Read(FlavourProps);

        props.Should().NotContain("FileFlowUnoHost",
            "la bandera del sabor ya no elige producto: hay un único host");
        props.Should().NotContain("FileFlowDesktopToolkit",
            "el inverso del sabor desaparece con él");
        props.Should().NotContain("FILEFLOW_NO_DESKTOP_TOOLKIT",
            "la constante que leían los nodos ya no tiene consumidor: se retira del build");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 3. Los plugins: sin paquetes de Avalonia
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void NoPluginThatDrawsAWindow_ShouldReferenceTheToolkit_OutsideItsCondition()
    {
        foreach (string plugin in PluginsWithNativeSurfaces)
        {
            var references = Project(plugin).Descendants("PackageReference")
                .Where(r => ((string?)r.Attribute("Include")) is { } name
                    && (name.StartsWith("Avalonia", StringComparison.Ordinal)
                        || name.StartsWith("Material.Icons.Avalonia", StringComparison.Ordinal)))
                .ToList();

            references.Should().BeEmpty(
                $"{plugin} no debe referenciar el toolkit de escritorio de Avalonia: el producto es portable");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 4. La medición del hermetismo: NINGÚN fichero menciona Avalonia
    // ─────────────────────────────────────────────────────────────────────────────

    private static Regex Glob(string pattern)
    {
        string normalized = pattern.Replace('\\', '/');
        string regex = Regex.Escape(normalized)
            .Replace(@"\*\*/", "(?:.*/)?")
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", "[^/]*");

        return new Regex("^" + regex + "$", RegexOptions.IgnoreCase);
    }

    [Fact]
    public void EveryFileTheProductCompiles_ShouldNotMentionAvalonia()
    {
        var offenders = new List<string>();
        int inspected = 0;

        foreach (string project in PluginsWithNativeSurfaces.Append("FileFlow.App.Core").Append("FileFlow.App.Uno")
                     .Append("FileFlow.Sdk").Append("FileFlow.Core"))
        {
            string directory = Path.Combine(Root(), project);
            foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                if (PluginSourceLocator.IsBuildArtifact(file))
                {
                    continue;
                }

                if (!CompiledExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                inspected++;
                if (File.ReadAllText(file).Contains("Avalonia", StringComparison.OrdinalIgnoreCase))
                {
                    offenders.Add(Path.GetRelativePath(Root(), file).Replace('\\', '/'));
                }
            }
        }

        inspected.Should().BeGreaterThan(100,
            "el barrido tiene que mirar de verdad el código del producto: un censo vacío pasaría esta guardia "
            + "sin haber leído nada");

        offenders.Should().BeEmpty(
            "Avalonia se eliminó por completo del producto: una mención en código, comentario o XAML mantiene "
            + "viva una dependencia que ya no existe");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 5. La frontera: el nodo la declara, no la finge
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EveryNodeWhoseSurfaceTheHostCannotMount_ShouldDeclareTheFrontier()
    {
        foreach (var (file, nameKey, window) in UnavailableSurfaceNodes)
        {
            string code = Code(file);
            string name = Path.GetFileName(file);

            code.Should().Contain("UnavailableSurface.Declare(",
                $"{name}: la frontera se DECLARA por los diálogos de quien lo abrió");
            code.Should().Contain("\"Plugin_SurfaceUnavailable_Message\"",
                $"{name}: el aviso sale del diccionario del plugin, no de un literal del código");
            code.Should().Contain("\"" + nameKey + "\"",
                $"{name}: el aviso nombra LA superficie que falta: sin el nombre, el usuario no sabe qué se perdió");
        }

        // Y la costura que recibe esa declaración existe, avisa por los diálogos y deja traza.
        string surface = Code(FrontierContract);
        surface.Should().Contain("ShowWarning(",
            "declarar la frontera sin avisar es la mitad del defecto: el usuario tiene que enterarse");
        surface.Should().Contain("Console.Error.WriteLine(",
            "y la traza queda fuera del proceso: un hueco que no se puede leer no se puede diagnosticar");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 6. Los textos de la frontera, en los dos idiomas
    // ─────────────────────────────────────────────────────────────────────────────

    private static Dictionary<string, string> Strings(string path)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var data in XDocument.Load(Path.Combine(Root(), path)).Root!.Elements("data"))
        {
            map[(string?)data.Attribute("name") ?? string.Empty] = data.Element("value")?.Value ?? string.Empty;
        }

        return map;
    }

    [Fact]
    public void TheFrontierTexts_ShouldBeInBothDictionaries_AndNameTheSurfacesTheyCite()
    {
        foreach (string plugin in PluginsWithNativeSurfaces)
        {
            var english = Strings($"{plugin}/Resources/Strings.resx");
            var spanish = Strings($"{plugin}/Resources/Strings.es.resx");

            foreach (string key in new[] { "Plugin_SurfaceUnavailable_Title", "Plugin_SurfaceUnavailable_Message" })
            {
                english.Should().ContainKey(key, $"{plugin}: la frontera se lee en los dos idiomas");
                spanish.Should().ContainKey(key, $"{plugin}: la frontera se lee en los dos idiomas");
            }

            english["Plugin_SurfaceUnavailable_Message"].Should().Contain("{0}",
                $"{plugin}: el mensaje lleva el nombre de la superficie que falta, o el usuario no sabe qué se perdió");
            spanish["Plugin_SurfaceUnavailable_Message"].Should().Contain("{0}");

            // El nombre que cita cada uno de SUS nodos también tiene que estar en los dos diccionarios: si
            // falta, el aviso sale con el texto de reserva incrustado —en un solo idioma, siempre—.
            foreach (var (file, nameKey, _) in UnavailableSurfaceNodes.Where(n => n.File.StartsWith(plugin + "/", StringComparison.Ordinal)))
            {
                english.Should().ContainKey(nameKey, $"{Path.GetFileName(file)} nombra la superficie por esta clave");
                spanish.Should().ContainKey(nameKey, $"{Path.GetFileName(file)} nombra la superficie por esta clave");
                Code(file).Should().Contain("\"" + nameKey + "\"", $"{Path.GetFileName(file)} la cita de verdad");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 7. El lanzador
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheLauncher_ShouldBuildWithDotnet_OnTheHostProject()
    {
        string launcher = Read(Launcher);

        launcher.Should().Contain("FileFlow.App.Uno.csproj",
            "el host se compila sobre su propio proyecto: no depende del nombre de una solución");
        launcher.Should().Contain("dotnet build",
            "y con `dotnet build` —sin MSBuild de Visual Studio: los targets de WinAppSDK ya corren sin él");

        launcher.Should().NotContain("MSBuild.exe",
            "si el lanzador volviera a MSBuild, el camino que esta solución abrió dejaría de recorrerse");
        launcher.Should().NotContain("FileFlow.Uno.slnx",
            "la solución de sabor ya no existe: el lanzador compila el proyecto directamente");
    }
}
