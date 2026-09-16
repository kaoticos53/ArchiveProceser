using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Contrato de la capa de componentes del sistema de diseño.
/// Verifica, de forma estática y determinista (sin plataforma gráfica), que:
///   1. toda clase de estilo usada en las vistas existe realmente en algún selector del repositorio
///      (detecta erratas del tipo Classes="primry", que en Avalonia fallan de forma silenciosa);
///   2. la capa de estilos está registrada en App.axaml;
///   3. se registra DESPUÉS de FluentTheme, requisito para que gane la cascada (los estados hover/pressed
///      del tema base se declaran sobre las partes de plantilla y hay que sobrescribirlos).
/// </summary>
[Collection("ThemeTokens")]
public class UiStyleContractTests
{
    /// <summary>Clases aportadas por frameworks de terceros (FluentTheme, Nodify) no definidas en este repositorio.</summary>
    private static readonly HashSet<string> FrameworkClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "accent", // Button.accent de FluentTheme
    };

    private static readonly Regex ClassesAttributeRegex = new(@"Classes=""([^""]*)""", RegexOptions.Compiled);
    private static readonly Regex ClassSelectorRegex = new(@"\.([A-Za-z_][A-Za-z0-9_-]*)", RegexOptions.Compiled);

    private static IEnumerable<string> AllXamlFiles()
    {
        string root = TestRepositoryLocator.RepositoryRoot();

        return Directory.EnumerateFiles(root, "*.axaml", SearchOption.AllDirectories)
            .Select(p => p.Replace('\\', '/'))
            .Where(p => !p.Contains("/bin/", StringComparison.OrdinalIgnoreCase))
            .Where(p => !p.Contains("/obj/", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p, StringComparer.Ordinal);
    }

    /// <summary>Clases definidas: todo token tras un punto en cualquier atributo Selector del repositorio.</summary>
    private static HashSet<string> CollectDefinedClasses()
    {
        var defined = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string file in AllXamlFiles())
        {
            string text = File.ReadAllText(file);

            foreach (Match selector in Regex.Matches(text, @"Selector=""([^""]*)"""))
            {
                foreach (Match cls in ClassSelectorRegex.Matches(selector.Groups[1].Value))
                {
                    defined.Add(cls.Groups[1].Value);
                }
            }
        }

        return defined;
    }

    private static SortedDictionary<string, List<string>> CollectUsedClasses()
    {
        var used = new SortedDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (string file in AllXamlFiles())
        {
            string text = File.ReadAllText(file);
            string shortName = Path.GetFileName(file);

            foreach (Match match in ClassesAttributeRegex.Matches(text))
            {
                foreach (string token in match.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!used.TryGetValue(token, out var owners))
                    {
                        owners = [];
                        used[token] = owners;
                    }

                    if (!owners.Contains(shortName))
                    {
                        owners.Add(shortName);
                    }
                }
            }
        }

        return used;
    }

    [Fact]
    public void EveryUsedStyleClass_ShouldBeDefinedInSomeSelector()
    {
        var defined = CollectDefinedClasses();
        var used = CollectUsedClasses();
        used.Should().NotBeEmpty("las vistas deben consumir la capa de estilos");

        var undefined = used
            .Where(kvp => !defined.Contains(kvp.Key) && !FrameworkClasses.Contains(kvp.Key))
            .Select(kvp => $"{kvp.Key} (usada en {string.Join(", ", kvp.Value)})")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        undefined.Should().BeEmpty(
            "toda clase usada con Classes=... debe existir en algún Selector=\"...clase...\" del repositorio, " +
            "o estar declarada en FrameworkClasses si la aporta FluentTheme/Nodify. Sin definir: " + string.Join(" | ", undefined));
    }

    [Fact]
    public void StyleLayer_ShouldBeRegisteredInAppAxaml()
    {
        string app = Path.Combine(TestRepositoryLocator.RepositoryRoot(), "FileFlow.App/App.axaml");
        File.Exists(app).Should().BeTrue();

        string text = File.ReadAllText(app);

        foreach (string file in new[] { "Typography", "Surfaces", "Buttons", "Inputs", "Containers" })
        {
            text.Should().Contain($"avares://FileFlow.App/Styles/{file}.axaml",
                $"la capa de estilos '{file}' debe estar registrada en App.axaml");
        }
    }

    [Fact]
    public void StyleLayer_ShouldBeRegisteredAfterFluentTheme()
    {
        string app = Path.Combine(TestRepositoryLocator.RepositoryRoot(), "FileFlow.App/App.axaml");
        string text = File.ReadAllText(app);

        int themeIndex = text.IndexOf("<FluentTheme", StringComparison.Ordinal);
        int buttonIndex = text.IndexOf("avares://FileFlow.App/Styles/Buttons.axaml", StringComparison.Ordinal);

        themeIndex.Should().BeGreaterThanOrEqualTo(0, "App.axaml debe incluir FluentTheme");
        buttonIndex.Should().BeGreaterThan(themeIndex,
            "la capa de componentes debe registrarse después de FluentTheme para sobrescribir sus estados hover/pressed");
    }

    [Fact]
    public void ThemeResourceReferences_ShouldResolveToADeclaredKey()
    {
        // Las vistas de la consola asignan su ControlTheme por recurso (Theme="{DynamicResource SegmentTheme}").
        // Una errata ahí no rompe la compilación y el control se queda con la plantilla por defecto: es el mismo
        // modo de fallo silencioso que las clases inexistentes, así que se guarda igual.
        var themeReferenceRegex = new Regex(@"Theme=""\{DynamicResource[ ]+([A-Za-z0-9_.]+)[ ]*}""", RegexOptions.Compiled);
        var declaredKeys = new HashSet<string>(StringComparer.Ordinal);
        var references = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (string file in AllXamlFiles())
        {
            string text = File.ReadAllText(file);

            foreach (Match key in Regex.Matches(text, @"x:Key=""([^""]+)"""))
            {
                declaredKeys.Add(key.Groups[1].Value);
            }

            foreach (Match reference in themeReferenceRegex.Matches(text))
            {
                string referencedKey = reference.Groups[1].Value;
                if (!references.TryGetValue(referencedKey, out var owners))
                {
                    owners = [];
                    references[referencedKey] = owners;
                }

                owners.Add(Path.GetFileName(file));
            }
        }

        references.Should().NotBeEmpty("la capa de estilos se consume por recursos de plantilla");

        var unresolved = references
            .Where(kvp => !declaredKeys.Contains(kvp.Key))
            .Select(kvp => $"{kvp.Key} (referenciado en {string.Join(", ", kvp.Value)})")
            .ToList();

        unresolved.Should().BeEmpty(
            "todo Theme=\"{DynamicResource X}\" debe apuntar a una clave declarada con x:Key. Sin resolver: " + string.Join(" | ", unresolved));
    }

    [Fact]
    public void MigratedViews_ShouldNotDeclareInlineShapeOrColorLiterals()
    {
        string root = TestRepositoryLocator.RepositoryRoot();
        var shapeRegex = new Regex(@"(CornerRadius|FontSize)=""[0-9.,]+""", RegexOptions.Compiled);
        var hexRegex = new Regex(@"#[0-9A-Fa-f]{6,8}\b", RegexOptions.Compiled);

        foreach (string relative in new[] { "FileFlow.App/Views/ControlBarView.axaml", "FileFlow.App/Views/StatusBarView.axaml", "FileFlow.App/Views/LogView.axaml" })
        {
            string text = File.ReadAllText(Path.Combine(root, relative));

            hexRegex.Matches(text).Should().BeEmpty($"{relative} no debe contener colores literales");
            shapeRegex.Matches(text).Should().BeEmpty($"{relative} no debe contener radios ni tamaños de fuente literales");
        }
    }
}
