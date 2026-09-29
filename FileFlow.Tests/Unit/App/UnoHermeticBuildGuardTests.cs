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
/// La guardia del SABOR DE UI y de la solución del host Uno (hito 268): el host que dibuja con WinUI se
/// compila con <c>dotnet build</c> sobre <c>FileFlow.Uno.slnx</c>, y ese grafo <b>no lleva una sola DLL de
/// Avalonia</b>.
///
/// <para><b>Qué protege.</b> (1) La solución del host Uno existe, trae su grafo entero y deja fuera al host de
/// escritorio y a sus pruebas; (2) el sabor lo elige el NOMBRE de la solución (no una bandera que alguien
/// tenga que recordar) y la constante que leen los nodos sale de ahí; (3) ningún plugin referencia el toolkit
/// del escritorio fuera de su condición; (4) ningún fichero que el sabor Uno compila menciona Avalonia fuera
/// de una región condicional —que es la medición que hace verdad «sin nada de Avalonia»—; (5) cada nodo cuya
/// ventana es del escritorio <b>declara la frontera</b> en vez de construirla a ciegas; (6) los textos de esa
/// frontera están en los dos diccionarios del plugin, y el nombre de la ventana que citan también (sin él, el
/// aviso saldría en el idioma equivocado); y (7) el lanzador compila con <c>dotnet</c> sobre esa solución y ya
/// no invoca MSBuild de Visual Studio.</para>
///
/// <para><b>Por qué una guardia y no sólo el build.</b> El build prueba el sabor de HOY en la máquina de quien
/// lo corre; lo que hay que sostener es que el día que alguien añada un <c>using Avalonia</c> a un fichero que
/// el host Uno compila —o un plugin nuevo con ventana— el sabor Uno deje de ser hermético <b>en la suite</b>,
/// sin depender de haber compilado la otra solución.</para>
/// </summary>
public class UnoHermeticBuildGuardTests
{
    private const string UnoSolution = "FileFlow.Uno.slnx";
    private const string DesktopSolution = "FileFlow.slnx";
    private const string FlavourProps = "Directory.Build.props";
    private const string UnoHostProject = "FileFlow.App.Uno/FileFlow.App.Uno.csproj";
    private const string Launcher = "run-uno.ps1";
    private const string FrontierContract = "FileFlow.Sdk/Services/DesktopOnlySurface.cs";
    private const string FlavourConstant = "FILEFLOW_NO_DESKTOP_TOOLKIT";

    /// <summary>Los cinco plugins que traen ventanas del toolkit del escritorio (y por eso lo referencian).</summary>
    private static readonly string[] PluginsWithDesktopWindows =
    [
        "FileFlow.Plugin.AI",
        "FileFlow.Plugin.Archives",
        "FileFlow.Plugin.FileSystem",
        "FileFlow.Plugin.Integrations",
        "FileFlow.Plugin.Scripting",
    ];

    /// <summary>
    /// Los nodos cuya ventana es del ESCRITORIO, con la clave del diccionario que da su nombre. Cuatro de
    /// ellos —el diseñador de datasets, el gestor de presets y los dos del gestor de contraseñas— además
    /// DECLARAN su superficie, así que en el host Uno se sirven por ahí (hito 278 para el de contraseñas): su
    /// rama del toolkit es defensa declarada, para el host que ignore las superficies declaradas.
    /// </summary>
    private static readonly (string File, string NameKey, string Window)[] DesktopOnlyNodes =
    [
        ("FileFlow.Plugin.Archives/SmartUnpackNode.cs", "PasswordManager_WindowTitle", "PasswordManagerWindow"),
        ("FileFlow.Plugin.Archives/ArchiveFanOutNode.cs", "PasswordManager_WindowTitle", "PasswordManagerWindow"),
        ("FileFlow.Plugin.AI/Nodes/Vision/MultimodalVisionLlmNode.cs", "VlmConfig_WindowTitle", "MultimodalVlmConfigWindow"),
        ("FileFlow.Plugin.FileSystem/Nodes/Processing/AdvancedRenamerNode.cs", "AdvancedRenamer_WindowTitle", "AdvancedRenamerEditorWindow"),
        ("FileFlow.Plugin.FileSystem/Nodes/Sources/SyntheticDataSourceNode.cs", "DataSetDesigner_WindowTitle", "SyntheticDataSetDesignerWindow"),
        ("FileFlow.Plugin.Integrations/MediaTranscoderNode.cs", "PresetManager_WindowTitle", "MediaPresetManagerWindow"),
        ("FileFlow.Plugin.Scripting/CustomScriptNode.cs", "ScriptStudio_Title", "ScriptStudioWindow"),
    ];

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
    // 1. La solución del host Uno
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheUnoSolution_ShouldCarryTheHostAndItsWholeGraph_WithoutTheDesktopHost()
    {
        File.Exists(Path.Combine(Root(), UnoSolution))
            .Should().BeTrue($"{UnoSolution} es la solución con la que se compila (y se depura) el host Uno");

        var solution = XDocument.Parse(Read(UnoSolution));
        var projects = solution.Descendants("Project")
            .Select(p => (string?)p.Attribute("Path") ?? string.Empty)
            .ToList();

        projects.Should().Contain(UnoHostProject, "la solución es del host Uno: sin su proyecto no compila nada");

        // El host de ESCRITORIO (Avalonia) y sus pruebas quedan fuera: son de la otra solución, y meterlas
        // aquí devolvería Avalonia al grafo que esta solución existe para dejar limpio.
        projects.Should().NotContain("FileFlow.App/FileFlow.App.csproj",
            "el host de escritorio (Avalonia) no viaja en la solución del host Uno");
        projects.Should().NotContain("FileFlow.Tests/FileFlow.Tests.csproj",
            "las pruebas montan ventanas de Avalonia: se compilan desde la solución del escritorio");
        // El censo se mira por DIRECTORIO y no por prefijo del nombre: `FileFlow.App.Core` empieza igual, y es
        // la capa PORTABLE que los dos hosts comparten —la que dejó de arrastrar Avalonia en el hito 267—. Lo
        // que no puede entrar es el host de escritorio.
        projects.Should().NotContain(p => p.Split('/')[0] == "FileFlow.App",
            "el host de escritorio es una aplicación Avalonia: en el grafo del host Uno no cabe");

        // Y la otra dirección: todo lo que el host Uno referencia tiene que estar en la solución, o Visual
        // Studio compilaría un grafo distinto del que compila el script.
        var declared = projects.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var references = XDocument.Parse(Read(UnoHostProject))
            .Descendants("ProjectReference")
            .Select(r => ((string?)r.Attribute("Include") ?? string.Empty).Replace('\\', '/'))
            .Select(p => p.StartsWith("../", StringComparison.Ordinal) ? p[3..] : p)
            .ToList();

        references.Should().NotBeEmpty("el host Uno referencia su grafo: el censo no puede estar vacío");
        var missing = references.Where(r => !declared.Contains(r)).ToList();
        missing.Should().BeEmpty(
            "todo proyecto que el host Uno referencia tiene que estar en SU solución: un grafo declarado a "
            + "medias es una solución que compila otra cosa de la que el script compila");

        // La solución del escritorio sigue siendo la suya (las dos existen, y ninguna sustituye a la otra).
        XDocument.Parse(Read(DesktopSolution)).Descendants("Project")
            .Select(p => (string?)p.Attribute("Path") ?? string.Empty)
            .Should().Contain("FileFlow.App/FileFlow.App.csproj",
                "la solución del escritorio no se toca: es la que compila la app con Avalonia");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 2. El sabor: lo elige el nombre de la solución
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheUiFlavour_ShouldBeChosenByTheSolutionName_AndDefineTheConstant()
    {
        string props = Read(FlavourProps);

        props.Should().Contain("$(SolutionFileName)",
            "el sabor tiene que salir del nombre de la solución: una bandera que hay que recordar se olvida, y "
            + "el que la olvida compila un grafo con el toolkit del escritorio sin enterarse");
        props.Should().Contain("'FileFlow.Uno.slnx'", "y la solución del host Uno es la que lo pide");
        props.Should().Contain("FileFlowUnoHost");
        props.Should().Contain(FlavourConstant, "la constante es lo que leen los nodos para no construir a ciegas");

        // El DEFECTO es el escritorio: compilar un proyecto suelto (o la solución del escritorio) no puede
        // cambiar de producto por sorpresa.
        Regex.IsMatch(props, @"<FileFlowDesktopToolkit Condition=""'\$\(FileFlowDesktopToolkit\)' == ''"">true</FileFlowDesktopToolkit>")
            .Should().BeTrue("sin contexto, el sabor por defecto es el de escritorio");

        Regex.IsMatch(props, @"<DefineConstants Condition=""'\$\(FileFlowDesktopToolkit\)' == 'false'"">")
            .Should().BeTrue("la constante se define SÓLO cuando falta el toolkit: es la condición de las regiones");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 3. Los plugins: el toolkit, condicionado
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void NoPluginThatDrawsAWindow_ShouldReferenceTheToolkit_OutsideItsCondition()
    {
        foreach (string plugin in PluginsWithDesktopWindows)
        {
            var references = Project(plugin).Descendants("PackageReference")
                .Where(r => ((string?)r.Attribute("Include")) is { } name
                    && (name.StartsWith("Avalonia", StringComparison.Ordinal)
                        || name.StartsWith("Material.Icons.Avalonia", StringComparison.Ordinal)))
                .ToList();

            references.Should().NotBeEmpty(
                $"{plugin} monta ventanas del escritorio: si ya no referencia el toolkit, esta guardia no está "
                + "mirando lo que cree");

            foreach (var reference in references)
            {
                GroupCondition(reference).Should().Contain("'$(FileFlowDesktopToolkit)' == 'true'",
                    $"{plugin} referencia {(string?)reference.Attribute("Include")} sin la condición del toolkit: "
                    + "esa referencia viaja al host Uno y le devuelve las DLL de Avalonia");
            }

            // Y el otro lado del sabor: lo que NO compila sin el toolkit tiene que estar quitado de verdad.
            var excluded = Project(plugin).Descendants("Compile")
                .Where(c => GroupCondition(c).Contains("'$(FileFlowDesktopToolkit)' != 'true'"))
                .Select(c => ((string?)c.Attribute("Remove") ?? string.Empty).Replace('\\', '/'))
                .ToList();

            excluded.Should().NotBeEmpty(
                $"{plugin} tiene ficheros que sólo existen para el escritorio (sus ventanas): el sabor Uno tiene "
                + "que declarar cuáles no compila, no dejarlo al azar de un using");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 4. La medición del hermetismo: ningún fichero que el sabor Uno compila menciona Avalonia
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Las líneas que MENCIONAN Avalonia sin estar dentro de una región condicional. La profundidad de
    /// preprocesador es lo que distingue «este fichero usa Avalonia» (menciones a nivel 0, que el sabor Uno
    /// compilaría) de «este fichero usa Avalonia cuando hay toolkit» (dentro de su <c>#if</c>).
    /// </summary>
    private static List<string> UnconditionalAvaloniaMentions(string plugin, string relativePath, string code)
    {
        var offenders = new List<string>();
        int depth = 0;
        int lineNumber = 0;

        // El texto llega SIN comentarios: una prosa que explique «esto lo monta el host Avalonia» no es una
        // mención de Avalonia en el código, y confundirlas convertiría la guardia en un lint de documentación.
        foreach (string line in code.Split('\n'))
        {
            lineNumber++;
            string trimmed = line.TrimStart();

            if (trimmed.StartsWith("#if", StringComparison.Ordinal)
                || trimmed.StartsWith("#elif", StringComparison.Ordinal))
            {
                depth++;
                continue;
            }

            if (trimmed.StartsWith("#endif", StringComparison.Ordinal))
            {
                depth = Math.Max(0, depth - 1);
                continue;
            }

            if (depth == 0 && line.Contains("Avalonia", StringComparison.OrdinalIgnoreCase))
            {
                offenders.Add($"{plugin}/{relativePath}:{lineNumber}");
            }
        }

        return offenders;
    }

    /// <summary>Un patrón de <c>Compile Remove</c> (<c>UI\Views\**\*.cs</c>) convertido a expresión regular.</summary>
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
    public void EveryFileTheUnoFlavourCompiles_ShouldNotMentionAvalonia_OutsideAConditionalRegion()
    {
        var offenders = new List<string>();
        int inspected = 0;

        // Los proyectos que el sabor Uno compila y que NO son del host (el host no menciona Avalonia: es
        // WinUI). El escritorio no entra: ahí el toolkit es justamente el que se usa.
        var projects = PluginsWithDesktopWindows.Append("FileFlow.App.Core");

        foreach (string plugin in projects)
        {
            var excluded = Project(plugin).Descendants("Compile")
                .Where(c => GroupCondition(c).Contains("'$(FileFlowDesktopToolkit)' != 'true'"))
                .Select(c => Glob(((string?)c.Attribute("Remove") ?? string.Empty)))
                .ToList();

            string directory = Path.Combine(Root(), plugin);
            foreach (string file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains("/obj/", StringComparison.Ordinal)
                    || file.Contains("\\obj\\", StringComparison.Ordinal)
                    || file.Contains("/bin/", StringComparison.Ordinal)
                    || file.Contains("\\bin\\", StringComparison.Ordinal))
                {
                    continue;
                }

                string relative = Path.GetRelativePath(directory, file).Replace('\\', '/');
                if (excluded.Any(g => g.IsMatch(relative)))
                {
                    continue; // este fichero NO lo compila el sabor Uno: puede tocar Avalonia todo lo que quiera
                }

                inspected++;
                offenders.AddRange(UnconditionalAvaloniaMentions(
                    plugin, relative, SourceText.WithoutComments(File.ReadAllText(file))));
            }
        }

        inspected.Should().BeGreaterThan(100,
            "el barrido tiene que mirar de verdad el código del producto: un censo vacío pasaría esta guardia "
            + "sin haber leído nada");

        offenders.Should().BeEmpty(
            "el sabor Uno compila estos ficheros, así que una mención de Avalonia fuera de una región condicional "
            + "les devuelve el toolkit al host: lo que sólo el escritorio puede montar va dentro de su #if");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 5. La frontera: el nodo la declara, no la finge
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EveryNodeWhoseWindowIsDesktopOnly_ShouldDeclareTheFrontier()
    {
        foreach (var (file, nameKey, window) in DesktopOnlyNodes)
        {
            string code = Code(file);
            string name = Path.GetFileName(file);

            int ifAt = code.IndexOf("#if " + FlavourConstant, StringComparison.Ordinal);
            int elseAt = code.IndexOf("#else", StringComparison.Ordinal);
            int windowAt = code.IndexOf("new " + window + "(", StringComparison.Ordinal);

            ifAt.Should().BeGreaterThan(-1, $"{name} tiene que tener su región sin toolkit");
            elseAt.Should().BeGreaterThan(ifAt, $"{name}: la región sin toolkit tiene su otra mitad");
            windowAt.Should().BeGreaterThan(elseAt,
                $"{name}: la ventana del toolkit se construye en la mitad CON toolkit, nunca antes de saber "
                + "cuál se está compilando");

            code.Should().Contain("DesktopOnlySurface.Declare(",
                $"{name}: sin toolkit no se construye la ventana y la frontera se DECLARA por los diálogos de "
                + "quien lo abrió —un botón que no hace nada y no avisa es el defecto, no la frontera—");
            code.Should().Contain("\"Plugin_DesktopOnly_Message\"",
                $"{name}: el aviso sale del diccionario del plugin, no de un literal del código");
            code.Should().Contain("\"" + nameKey + "\"",
                $"{name}: el aviso nombra LA ventana que falta: sin el nombre, el usuario no sabe qué se perdió");
            code.Should().Contain("#endif", $"{name}: la región tiene que cerrarse");
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
    public void TheFrontierTexts_ShouldBeInBothDictionaries_AndNameTheWindowsTheyCite()
    {
        foreach (string plugin in PluginsWithDesktopWindows)
        {
            var english = Strings($"{plugin}/Resources/Strings.resx");
            var spanish = Strings($"{plugin}/Resources/Strings.es.resx");

            foreach (string key in new[] { "Plugin_DesktopOnly_Title", "Plugin_DesktopOnly_Message" })
            {
                english.Should().ContainKey(key, $"{plugin}: la frontera se lee en los dos idiomas");
                spanish.Should().ContainKey(key, $"{plugin}: la frontera se lee en los dos idiomas");
            }

            english["Plugin_DesktopOnly_Message"].Should().Contain("{0}",
                $"{plugin}: el mensaje lleva el nombre de la ventana que falta, o el usuario no sabe qué se perdió");
            spanish["Plugin_DesktopOnly_Message"].Should().Contain("{0}");

            // El nombre que cita cada uno de SUS nodos también tiene que estar en los dos diccionarios: si
            // falta, el aviso sale con el texto de reserva incrustado —en un solo idioma, siempre—.
            foreach (var (file, nameKey, _) in DesktopOnlyNodes.Where(n => n.File.StartsWith(plugin + "/", StringComparison.Ordinal)))
            {
                english.Should().ContainKey(nameKey, $"{Path.GetFileName(file)} nombra la ventana por esta clave");
                spanish.Should().ContainKey(nameKey, $"{Path.GetFileName(file)} nombra la ventana por esta clave");
                Code(file).Should().Contain("\"" + nameKey + "\"", $"{Path.GetFileName(file)} la cita de verdad");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 7. El lanzador
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheLauncher_ShouldBuildWithDotnet_OnTheFlavourSolution()
    {
        string launcher = Read(Launcher);

        launcher.Should().Contain("FileFlow.Uno.slnx",
            "el host Uno se compila con SU solución: es la que elige el sabor sin Avalonia");
        launcher.Should().Contain("dotnet build",
            "y con `dotnet build` —sin MSBuild de Visual Studio: los targets de WinAppSDK ya corren sin él");
        launcher.Should().Contain("-p:FileFlowUnoHost=true",
            "el script no depende de adivinar el sabor por el nombre: lo dice explícitamente");

        launcher.Should().NotContain("MSBuild.exe",
            "si el lanzador volviera a MSBuild, el camino que esta solución abrió dejaría de recorrerse");
    }
}
