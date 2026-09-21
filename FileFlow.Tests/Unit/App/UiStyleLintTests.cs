using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Lint de estilos inline con estrategia de trinquete (ratchet).
/// La auditoría de UI detectó 27 vistas con literales de color (<c>#HEX</c>) y valores inline de
/// <c>CornerRadius</c>/<c>FontSize</c>, causantes de la incoherencia visual y de la rotura en temas claros.
/// Este test NO exige la migración completa de golpe: fija el estado actual como línea base y falla
/// cuando un fichero EMPEORA (más literales que antes) o cuando una vista NUEVA introduce literales.
/// Las mejoras (menos literales) pasan siempre, de modo que la línea base se reduce al migrar a tokens.
///
/// La migración de la Fase 1 (radios, tipografía y sombras) bajó los literales de forma de ~370 a 15 y
/// dejó fuera de la línea base a todas las vistas que ya consumen exclusivamente tokens: sólo quedan los
/// radios ASIMÉTRICOS (<c>4,0,0,4</c>, <c>9,9,0,0</c>) y el <c>0</c> deliberado de las esquinas rectas, que no
/// son expresables con la escala simétrica del tema. El color inline sí se sigue vigilando aquí.
/// </summary>
[Collection("ThemeTokens")]
public class UiStyleLintTests
{
    /// <summary>Línea base: fichero AXAML -> (literales de color, radios asimétricos o rectos).</summary>
    private static readonly Dictionary<string, (int HexLiterals, int ShapeLiterals)> Baseline = new(StringComparer.OrdinalIgnoreCase)
    {
        // Sólo aparecen aquí las vistas que todavía conservan color inline o radios no simétricos: cada
        // fichero migrado desaparece de la lista, de modo que el trinquete no vuelva a subir.
        ["FileFlow.App/MainWindow.axaml"] = (1, 0),
        ["FileFlow.App/Preview/Controls/FilePreviewerControl.axaml"] = (1, 0),
        ["FileFlow.App/Preview/Controls/ImageCompareSliderControl.axaml"] = (3, 0),
        ["FileFlow.App/Preview/Views/FilePreviewerWindow.axaml"] = (10, 0),
        ["FileFlow.App/Themes/Templates/InspectorTemplates.axaml"] = (2, 0),
        ["FileFlow.App/Themes/Templates/NodeParameterTemplates.axaml"] = (0, 9),
        ["FileFlow.App/Views/AboutDialogWindow.axaml"] = (2, 0),
        ["FileFlow.App/Views/Components/AnnotationCardView.axaml"] = (19, 1),
        ["FileFlow.App/Views/Components/ColorPickerButton.axaml"] = (54, 0),
        ["FileFlow.App/Views/Components/GroupCardView.axaml"] = (15, 1),
        ["FileFlow.App/Views/Components/NodeCardView.axaml"] = (14, 3),
        ["FileFlow.App/Views/Components/WorkflowMetricsDashboardWindow.axaml"] = (0, 1),
        ["FileFlow.App/Views/NodeInspectorPanelView.axaml"] = (1, 0),
    };

    private static readonly Regex HexLiteralRegex = new(@"#[0-9A-Fa-f]{6,8}\b", RegexOptions.Compiled);
    private static readonly Regex ShapeLiteralRegex = new(@"(CornerRadius|FontSize)=""[0-9.,]+""", RegexOptions.Compiled);

    /// <summary>Tamaño de letra literal: no hay ningún caso legítimo, la escala tipográfica es del tema.</summary>
    private static readonly Regex FontSizeLiteralRegex = new(@"FontSize=""[0-9.]+""", RegexOptions.Compiled);

    /// <summary>Sombra escrita a mano: ignora la escala de elevación del tema (Elev1..Elev4 y los resplandores).</summary>
    private static readonly Regex HardCodedShadowRegex = new(@"BoxShadow[A-Za-z.]*=""[^""]*#", RegexOptions.Compiled);

    [Fact]
    public void NoXamlView_ShouldExceedItsInlineStyleBaseline()
    {
        var violations = new List<string>();
        var snapshot = new List<(string File, int Hex, int Shape)>();

        foreach (string file in EnumerateViewFiles())
        {
            string relative = Path.GetRelativePath(TestRepositoryLocator.RepositoryRoot(), file).Replace('\\', '/');
            string text = File.ReadAllText(file);

            int hex = HexLiteralRegex.Matches(text).Count;
            int shape = ShapeLiteralRegex.Matches(text).Count;
            snapshot.Add((relative, hex, shape));

            if (hex == 0 && shape == 0)
            {
                continue;
            }

            if (!Baseline.TryGetValue(relative, out var allowed))
            {
                violations.Add($"NUEVO fichero con estilos inline: {relative} ({hex} colores, {shape} literales de forma)");
                continue;
            }

            if (hex > allowed.HexLiterals)
            {
                violations.Add($"{relative}: colores inline {hex} > línea base {allowed.HexLiterals}");
            }

            if (shape > allowed.ShapeLiterals)
            {
                violations.Add($"{relative}: literales de forma/tipografía {shape} > línea base {allowed.ShapeLiterals}");
            }
        }

        violations.Should().BeEmpty(
            "usa tokens del tema ({DynamicResource ...}) y clases de estilo en lugar de literales, o reduce la línea base " +
            "tras migrar la vista. Incumplimientos: " + string.Join(" | ", violations) +
            Environment.NewLine + "Instantánea actual para actualizar la línea base:" + Environment.NewLine + BuildBaselineSnippet(snapshot));
    }

    /// <summary>
    /// Cero tolerancia en las dos magnitudes que hacen visible el tema en toda la interfaz: la tipografía y la
    /// profundidad. Un tamaño de letra o una sombra literales no siguen al tema, así que rompen el ajuste del
    /// Theme Studio en esa vista (y en los temas claros dejan de leerse).
    /// </summary>
    [Fact]
    public void NoView_ShouldUseLiteralFontSizeOrHandWrittenShadow()
    {
        var violations = new List<string>();

        foreach (string file in EnumerateViewFiles())
        {
            string relative = Path.GetRelativePath(TestRepositoryLocator.RepositoryRoot(), file).Replace('\\', '/');
            string text = File.ReadAllText(file);

            foreach (Match match in FontSizeLiteralRegex.Matches(text))
            {
                violations.Add($"{relative}: '{match.Value}' (usa la escala FontSizeMicro..FontSizeDisplay o una clase tipográfica)");
            }

            foreach (Match match in HardCodedShadowRegex.Matches(text))
            {
                if (match.Value.Contains('#'))
                {
                    violations.Add($"{relative}: '{match.Value}' (usa la escala Elev1..Elev4, ElevGlow* o ElevPanelLeft)");
                }
            }
        }

        violations.Should().BeEmpty(
            "el tamaño de letra y la profundidad se ajustan desde el Theme Studio: deben salir de los tokens del " +
            "tema en TODAS las vistas. Incumplimientos: " + string.Join(" | ", violations));
    }

    [Fact]
    public void StudioWindow_ShouldNotNeedABaselineEntry()
    {
        // La ventana del Theme Studio es la referencia de lo que se espera del resto: cero literales.
        var baselineKeys = Baseline.Keys.Select(k => k.Replace('\\', '/')).ToList();

        baselineKeys.Should().NotContain("FileFlow.App/Views/Components/ThemeCustomizerWindow.axaml",
            "el Theme Studio no debe volver a la lista de vistas con estilos en línea");
    }

    [Fact]
    public void Baseline_ShouldOnlyReferenceExistingViewFiles()
    {
        var existing = EnumerateViewFiles()
            .Select(f => Path.GetRelativePath(TestRepositoryLocator.RepositoryRoot(), f).Replace('\\', '/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var stale = Baseline.Keys.Where(k => !existing.Contains(k)).ToList();

        stale.Should().BeEmpty(
            "la línea base no debe contener vistas eliminadas o renombradas: " + string.Join(", ", stale));
    }

    [Fact]
    public void DesignSystemFiles_ShouldBeExcluded_ButTemplatesShouldBeLinted()
    {
        // Los literales sólo son legítimos en la capa de diseño (diccionarios de tokens y estilos de
        // componente), pero las plantillas de tema sí son vistas y deben auditarse.
        string tokenDictionary = Path.Combine(TestRepositoryLocator.RepositoryRoot(), "FileFlow.App/Themes/DarkTheme.axaml");
        File.Exists(tokenDictionary).Should().BeTrue();
        File.Exists(Path.Combine(TestRepositoryLocator.RepositoryRoot(), "FileFlow.App/Styles/Buttons.axaml")).Should().BeTrue();

        var linted = EnumerateViewFiles().Select(f => Path.GetRelativePath(TestRepositoryLocator.RepositoryRoot(), f).Replace('\\', '/')).ToList();

        linted.Should().NotContain("FileFlow.App/Themes/DarkTheme.axaml");
        linted.Should().NotContain("FileFlow.App/Styles/Buttons.axaml");
        linted.Should().Contain("FileFlow.App/Themes/Templates/NodeParameterTemplates.axaml");
        linted.Should().Contain("FileFlow.App/Themes/Templates/InspectorTemplates.axaml");
    }

    /// <summary>Ficheros auditables: todo AXAML de FileFlow.App salvo la capa de diseño (Themes/ y Styles/).</summary>
    private static readonly Regex DesignSystemFileRegex = new(@"/(Themes|Styles)/[^/]+\.axaml$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static IEnumerable<string> EnumerateViewFiles()
    {
        string root = TestRepositoryLocator.RepositoryRoot();

        return Directory.EnumerateFiles(Path.Combine(root, "FileFlow.App"), "*.axaml", SearchOption.AllDirectories)
            .Select(p => p.Replace('\\', '/'))
            .Where(p => !p.Contains("/bin/", StringComparison.OrdinalIgnoreCase))
            .Where(p => !p.Contains("/obj/", StringComparison.OrdinalIgnoreCase))
            .Where(p => !DesignSystemFileRegex.IsMatch(p))
            .OrderBy(p => p, StringComparer.Ordinal);
    }

    private static string BuildBaselineSnippet(IEnumerable<(string File, int Hex, int Shape)> snapshot)
    {
        var builder = new StringBuilder();
        foreach (var (file, hex, shape) in snapshot.Where(s => s.Hex > 0 || s.Shape > 0).OrderBy(s => s.File, StringComparer.Ordinal))
        {
            builder.AppendLine($"        [\"{file}\"] = ({hex}, {shape}),");
        }

        return builder.ToString();
    }
}
