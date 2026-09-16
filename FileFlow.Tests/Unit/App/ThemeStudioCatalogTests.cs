using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Media;
using FileFlow.App.Services;
using FileFlow.App.Themes;
using FileFlow.App.ViewModels;
using FileFlow.Sdk.Localization;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Guardias del Theme Studio.
///
/// El editor se genera desde <see cref="ThemeSettingCatalog"/>, así que el riesgo real no es que un control
/// esté mal colocado, sino que **el catálogo se desincronice del tema**: una propiedad visual nueva sin
/// control (el usuario no puede personalizarla) o una fila que no cambia ningún token (un control decorativo
/// que parece funcionar y no hace nada). Ambas cosas se comprueban aquí.
/// </summary>
[Collection("ThemeTokens")]
public class ThemeStudioCatalogTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Cobertura del catálogo
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EveryVisualPropertyOfThemeDefinition_ShouldBeEditableOrExplicitlyExcluded()
    {
        var editable = ThemeSettingCatalog.Settings.Select(s => s.Property).ToHashSet(StringComparer.Ordinal);
        var documented = ThemeSettingCatalog.NotEditableYet.Keys.ToHashSet(StringComparer.Ordinal);

        var uncovered = typeof(ThemeDefinition)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .Select(p => p.Name)
            .Where(name => !editable.Contains(name) && !documented.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        uncovered.Should().BeEmpty(
            "toda propiedad visual del tema debe poder personalizarse en el Theme Studio o estar declarada en " +
            "ThemeSettingCatalog.NotEditableYet con su motivo. Sin cobertura: " + string.Join(", ", uncovered));
    }

    [Fact]
    public void NotEditableYet_ShouldNotListPropertiesThatAreAlreadyEditable()
    {
        var editable = ThemeSettingCatalog.Settings.Select(s => s.Property).ToHashSet(StringComparer.Ordinal);
        var duplicated = ThemeSettingCatalog.NotEditableYet.Keys.Where(editable.Contains).ToList();

        duplicated.Should().BeEmpty(
            "una propiedad no puede estar a la vez en el editor y en la lista de exclusiones: " + string.Join(", ", duplicated));
    }

    [Fact]
    public void EveryCatalogEntry_ShouldPointToARealProperty_WithoutDuplicates()
    {
        var failures = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var setting in ThemeSettingCatalog.Settings)
        {
            var property = typeof(ThemeDefinition).GetProperty(setting.Property);

            if (property == null)
            {
                failures.Add($"'{setting.Property}' no existe en ThemeDefinition");
                continue;
            }

            if (!seen.Add(setting.Property))
            {
                failures.Add($"'{setting.Property}' está declarada más de una vez");
            }

            if (!ThemeSettingCatalog.Sections.Contains(setting.SectionKey))
            {
                failures.Add($"'{setting.Property}' pertenece a la sección desconocida '{setting.SectionKey}'");
            }

            if (setting.Kind == ThemeSettingKind.Number)
            {
                if (setting.Maximum <= setting.Minimum)
                {
                    failures.Add($"'{setting.Property}' tiene un rango vacío ({setting.Minimum}..{setting.Maximum})");
                }

                if (setting.Step <= 0)
                {
                    failures.Add($"'{setting.Property}' tiene un paso no positivo ({setting.Step})");
                }
            }

            if (setting.Kind == ThemeSettingKind.Choice && (setting.Options == null || setting.Options.Count == 0))
            {
                failures.Add($"'{setting.Property}' es de elección y no publica opciones");
            }
        }

        failures.Should().BeEmpty("el catálogo del Theme Studio debe describir ajustes reales. Problemas: " + string.Join(" | ", failures));
    }

    [Fact]
    public void EverySection_ShouldHaveAtLeastOneSetting()
    {
        foreach (string section in ThemeSettingCatalog.Sections)
        {
            ThemeSettingCatalog.Settings.Should().Contain(
                s => s.SectionKey == section,
                $"la sección '{section}' se muestra en el editor, así que no puede quedar vacía");
        }

        ThemeSettingCatalog.Sections.Should().OnlyHaveUniqueItems("una sección repetida duplicaría sus filas en el editor");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Localización
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EveryCatalogLabelAndSection_ShouldExistInBothLanguages()
    {
        var english = ReadResxKeys("FileFlow.App/Resources/Strings.resx");
        var spanish = ReadResxKeys("FileFlow.App/Resources/Strings.es.resx");

        var keys = ThemeSettingCatalog.Sections
            .Concat(ThemeSettingCatalog.Settings.Select(s => s.LabelKey))
            .Distinct()
            .ToList();

        var missing = keys.Where(k => !english.Contains(k) || !spanish.Contains(k)).ToList();
        missing.Should().BeEmpty("las claves del editor deben existir en inglés y español: " + string.Join(", ", missing));

        // Y no deben quedar sin texto real: un rótulo vacío dejaría la fila sin nombre en la interfaz.
        foreach (string key in keys)
        {
            LocalizationManager.Instance.GetString(key, string.Empty).Should().NotBeNullOrWhiteSpace($"'{key}' debe tener texto");
        }
    }

    private static HashSet<string> ReadResxKeys(string relativePath)
    {
        string path = Path.Combine(TestRepositoryLocator.RepositoryRoot(), relativePath);
        File.Exists(path).Should().BeTrue($"debe existir el diccionario de recursos {relativePath}");

        return System.Text.RegularExpressions.Regex
            .Matches(File.ReadAllText(path), "name=\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Efecto real de cada ajuste
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Tokens que cada ajuste del catálogo mueve de verdad al generar el diccionario del tema.
    /// Si un ajuste no aparece aquí, editarlo en el Studio no tendría efecto visible.
    /// </summary>
    private static readonly Dictionary<string, string[]> TokensDrivenBy = new(StringComparer.Ordinal)
    {
        ["AppBackground"] = ["AppBackgroundBrush"],
        ["BgDark"] = ["BgDarkBrush"],
        ["BgEditor"] = ["BgEditorBrush"],
        ["BgSurface"] = ["BgSurfaceBrush", "OverlaySurfaceBrush"],
        ["BgCard"] = ["BgCardBrush"],
        ["BgHeader"] = ["BgHeaderBrush"],
        ["BgHover"] = ["BgHoverBrush"],
        ["AccentPrimary"] = ["AccentPrimaryBrush", "ElevGlowAccent"],
        ["AccentHover"] = ["AccentHoverBrush"],
        ["AccentGlow"] = ["AccentGlowBrush"],
        ["AccentSuccess"] = ["AccentSuccessBrush", "ElevGlowSuccess"],
        ["AccentWarning"] = ["AccentWarningBrush"],
        ["AccentError"] = ["AccentErrorBrush", "ElevGlowError"],
        ["AccentCyan"] = ["AccentCyanBrush"],
        ["AccentPurple"] = ["AccentPurpleBrush"],
        ["TextPrimary"] = ["TextPrimaryBrush"],
        ["TextSecondary"] = ["TextSecondaryBrush", "ChipBrush", "ChipStrongBrush", "TintFaintBrush"],
        ["TextMuted"] = ["TextMutedBrush"],
        ["TextOnAccent"] = ["TextOnAccentBrush"],
        ["BorderDark"] = ["BorderDarkBrush"],
        ["BorderSubtle"] = ["BorderSubtleBrush"],
        ["GridLine"] = ["GridLineBrush"],
        ["ScrollbarThumb"] = ["ScrollbarThumbBrush"],
        ["ScrollbarThumbHover"] = ["ScrollbarThumbHoverBrush"],
        ["WireColorStart"] = ["ConnectionWireBrush"],
        ["WireColorMid"] = ["ConnectionWireBrush"],
        ["WireColorEnd"] = ["ConnectionWireBrush"],
        ["FontFamily"] = ["AppFontFamily"],
        ["CodeFontFamily"] = ["CodeFontFamily"],
        ["BaseFontSize"] =
        [
            "AppFontSize", "FontSizeMicro", "FontSizeCaption", "FontSizeBodySm", "FontSizeBody",
            "FontSizeSubtitle", "FontSizeTitle", "FontSizeDisplay"
        ],
        ["CornerRadius"] = ["AppCornerRadius", "RadiusXs", "RadiusSm", "RadiusMd", "RadiusLg", "RadiusXl", "RadiusXxl"],
        ["SpacingUnit"] =
        [
            "Space1", "Space2", "Space3", "Space4", "Space5", "Space6", "Space7", "Space8", "Space9",
            "Pad1", "Pad4", "Pad6", "Pad9"
        ],
        ["NodeShadowBlur"] = ["Elev1", "Elev2", "Elev3", "Elev4", "ElevPanelLeft", "ElevGlowAccent"],
        ["NodeShadowOpacity"] = ["Elev1", "Elev2", "Elev3", "Elev4", "ElevPanelLeft"]
    };

    [Fact]
    public void EveryCatalogSetting_ShouldActuallyChangeSomethingInTheGeneratedTokens()
    {
        var failures = new List<string>();

        foreach (var setting in ThemeSettingCatalog.Settings)
        {
            if (!TokensDrivenBy.TryGetValue(setting.Property, out var expectedTokens))
            {
                failures.Add($"'{setting.Property}' no declara qué tokens mueve (TokensDrivenBy)");
                continue;
            }

            var baseline = new ThemeDefinition();
            var modified = baseline.Clone();

            if (!TryChange(setting, modified))
            {
                failures.Add($"'{setting.Property}' no se pudo modificar para la comprobación");
                continue;
            }

            var before = ThemeResourceApplier.BuildResourceDictionary(baseline);
            var after = ThemeResourceApplier.BuildResourceDictionary(modified);

            foreach (string token in expectedTokens)
            {
                if (Equivalent(before[token], after[token]))
                {
                    failures.Add($"'{setting.Property}' no cambia '{token}'");
                }
            }
        }

        failures.Should().BeEmpty(
            "cada control del Theme Studio debe tener un efecto real y visible sobre los tokens del tema. " +
            "Ajustes inertes: " + string.Join(" | ", failures));
    }

    [Fact]
    public void EveryScaleToken_ShouldMoveWhenItsDrivingSettingChanges()
    {
        // Comprobación independiente del catálogo: las escalas completas siguen a su ajuste base.
        var baselineScale = new ThemeDefinition { CornerRadius = 6, BaseFontSize = 12, SpacingUnit = 4, NodeShadowBlur = 24 };
        var changed = baselineScale.Clone();
        changed.CornerRadius = 14;
        changed.BaseFontSize = 18;
        changed.SpacingUnit = 7;
        changed.NodeShadowBlur = 40;

        var before = ThemeResourceApplier.BuildResourceDictionary(baselineScale);
        var after = ThemeResourceApplier.BuildResourceDictionary(changed);

        foreach (string token in new[] { "RadiusXs", "RadiusSm", "RadiusMd", "RadiusLg", "RadiusXl", "RadiusXxl" })
        {
            RadiusOf(before[token]).Should().NotBe(RadiusOf(after[token]), $"'{token}' debe seguir al radio base del tema");
        }

        foreach (string token in new[] { "FontSizeMicro", "FontSizeCaption", "FontSizeBodySm", "FontSizeBody", "FontSizeSubtitle", "FontSizeTitle", "FontSizeDisplay" })
        {
            ((double)before[token]!).Should().NotBe((double)after[token]!, $"'{token}' debe seguir al tamaño de fuente base del tema");
        }

        foreach (string token in new[] { "Space2", "Space5", "Space9" })
        {
            ((double)before[token]!).Should().NotBe((double)after[token]!, $"'{token}' debe seguir a la unidad de espaciado del tema");
        }

        PadOf(before["Pad4"]).Should().NotBe(PadOf(after["Pad4"]), "'Pad4' debe seguir a la unidad de espaciado del tema");

        foreach (string token in new[] { "Elev1", "Elev2", "Elev3", "Elev4" })
        {
            BlurOf(before[token]).Should().NotBe(BlurOf(after[token]), $"'{token}' debe seguir al desenfoque de sombra del tema");
        }
    }

    [Fact]
    public void EveryElevationToken_ShouldTraceBackToTheShadowSettings()
    {
        var flat = new ThemeDefinition { NodeShadowBlur = 24, NodeShadowOpacity = 0.05 };
        var deep = new ThemeDefinition { NodeShadowBlur = 48, NodeShadowOpacity = 1.0 };

        var flatTokens = ThemeResourceApplier.BuildResourceDictionary(flat);
        var deepTokens = ThemeResourceApplier.BuildResourceDictionary(deep);

        // La opacidad del tema debe oscurecer o aclarar TODA la escala de profundidad, no sólo la tarjeta.
        foreach (string token in new[] { "Elev1", "Elev2", "Elev3", "Elev4", "ElevPanelLeft" })
        {
            AlphaOf(flatTokens[token]).Should().BeLessThan(AlphaOf(deepTokens[token]), $"la opacidad del tema debe afectar a '{token}'");
        }

        BlurOf(deepTokens["Elev4"]).Should().BeGreaterThan(BlurOf(flatTokens["Elev4"]), "el desenfoque del tema debe afectar a la escala de elevación");
    }

    [Fact]
    public void GeneratedDictionary_ShouldExposeTheWholeDocumentedTokenSet()
    {
        var tokens = ThemeResourceApplier.BuildResourceDictionary(new ThemeDefinition());

        var expected = TokensDrivenBy.Values.SelectMany(v => v)
            .Concat(["RadiusPill", "ScrimBrush", "ScrimStrongBrush"])
            .Distinct()
            .ToList();

        var missing = expected.Where(token => !tokens.ContainsKey(token)).ToList();
        missing.Should().BeEmpty("faltan tokens en el diccionario generado: " + string.Join(", ", missing));
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Integración con el editor
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Editor_ShouldBuildARowForEveryCatalogSetting_AndKeepThePreviewInSync()
    {
        string storage = Path.Combine(Path.GetTempPath(), $"studio_catalog_{Guid.NewGuid():N}.json");

        try
        {
            var viewModel = new ThemeCustomizerViewModel(new CustomThemeService(storage));

            viewModel.Sections.SelectMany(s => s.Rows).Select(r => r.Property)
                .Should().BeEquivalentTo(ThemeSettingCatalog.Settings.Select(s => s.Property),
                    "el editor debe mostrar exactamente los ajustes del catálogo");

            // Cambiar un ajuste desde su fila debe regenerar los tokens de la vista previa.
            var radiusRow = viewModel.Sections.SelectMany(s => s.Rows)
                .OfType<ThemeNumberRowViewModel>()
                .First(r => r.Property == nameof(ThemeDefinition.CornerRadius));

            double before = RadiusOf(viewModel.LivePreviewResources["RadiusSm"]);

            radiusRow.Value += 6;

            RadiusOf(viewModel.LivePreviewResources["RadiusSm"])
                .Should().NotBe(before, "editar una fila debe refrescar los tokens de la vista previa");

            viewModel.EditingTheme.CornerRadius.Should().Be((double)radiusRow.Value, "la fila escribe sobre el tema en edición");
        }
        finally
        {
            File.Delete(storage);
        }
    }

    [Fact]
    public void LivePreviewResources_ShouldBeAStableInstance()
    {
        string storage = Path.Combine(Path.GetTempPath(), $"studio_stable_{Guid.NewGuid():N}.json");

        try
        {
            var viewModel = new ThemeCustomizerViewModel(new CustomThemeService(storage));
            var attached = viewModel.LivePreviewResources;

            viewModel.UpdateLivePreview();

            viewModel.LivePreviewResources.Should().BeSameAs(attached,
                "la vista previa engancha el diccionario una sola vez: si se reemplaza la instancia, los " +
                "DynamicResource del panel dejan de reflejar los cambios");

            attached.Should().ContainKey("AppBackgroundBrush");
        }
        finally
        {
            File.Delete(storage);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Utilidades
    // ─────────────────────────────────────────────────────────────────────────────

    private static bool TryChange(ThemeSettingDescriptor setting, ThemeDefinition theme)
    {
        var property = typeof(ThemeDefinition).GetProperty(setting.Property)!;

        switch (setting.Kind)
        {
            case ThemeSettingKind.Color:
                property.SetValue(theme, "#123456");
                return true;

            case ThemeSettingKind.Number:
                // Se empuja al extremo opuesto del rango para garantizar que el token cambie.
                double current = property.GetValue(theme) is double value ? value : 0;
                double alternative = setting.Maximum - current <= 0.001 ? setting.Minimum : setting.Maximum;
                property.SetValue(theme, alternative);
                return true;

            case ThemeSettingKind.Choice:
                var options = setting.Options!;
                string selected = property.GetValue(theme) as string ?? string.Empty;
                property.SetValue(theme, options.FirstOrDefault(o => !string.Equals(o, selected, StringComparison.Ordinal)) ?? options[0]);
                return true;

            default:
                return false;
        }
    }

    private static bool Equivalent(object? first, object? second) => (first, second) switch
    {
        (SolidColorBrush a, SolidColorBrush b) => a.Color == b.Color,
        (LinearGradientBrush a, LinearGradientBrush b) => GradientEquals(a, b),
        (BoxShadows a, BoxShadows b) => a.ToString() == b.ToString(),
        _ => Equals(first, second)
    };

    private static bool GradientEquals(LinearGradientBrush a, LinearGradientBrush b) =>
        a.GradientStops.Count == b.GradientStops.Count &&
        a.GradientStops.Zip(b.GradientStops).All(pair => pair.First.Color == pair.Second.Color && pair.First.Offset == pair.Second.Offset);

    private static double RadiusOf(object? value) => value is CornerRadius radius ? radius.TopLeft : double.NaN;

    private static Thickness PadOf(object? value) => value is Thickness thickness ? thickness : default;

    private static double BlurOf(object? value) => value is BoxShadows shadows && shadows.Count > 0 ? shadows[0].Blur : double.NaN;

    private static byte AlphaOf(object? value) => value is BoxShadows shadows && shadows.Count > 0 ? shadows[0].Color.A : (byte)0;
}
