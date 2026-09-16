using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Media;
using FileFlow.App.Services;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Guardias del sistema de temas de FileFlow Studio.
/// Garantizan que todo token referenciado desde AXAML (host y plugins) exista en los 8 presets integrados,
/// en el diccionario de arranque y con el contraste mínimo de accesibilidad exigido, evitando la
/// regresión de recursos inexistentes (CardBgBrush, BgAppBrush, TextMutedBrush) detectada en la auditoría.
/// </summary>
[Collection("ThemeTokens")]
public class ThemeTokenCompletenessTests
{
    private static readonly Regex DynamicResourceRegex =
        new(@"\{DynamicResource\s+([A-Za-z0-9_.]+)\s*\}", RegexOptions.Compiled);

    private static readonly Regex ResourceKeyRegex =
        new(@"x:Key=""([^""]+)""", RegexOptions.Compiled);

    private static readonly Regex BrushColorRegex =
        new(@"Color=""(#[0-9A-Fa-f]{6,8})""", RegexOptions.Compiled);

    /// <summary>Tokens declarados explícitamente en el diccionario baseline de arranque.</summary>
    private const string BaselineThemeFile = "FileFlow.App/Themes/DarkTheme.axaml";

    /// <summary>Preset integrado que el baseline de arranque debe reflejar exactamente.</summary>
    private const string BaselinePresetId = "dark_fluent";

    #region Recopilación de tokens referenciados

    private static IEnumerable<string> EnumerateXamlFiles()
    {
        string root = TestRepositoryLocator.RepositoryRoot();
        return Directory.EnumerateFiles(root, "*.axaml", SearchOption.AllDirectories)
            .Select(p => p.Replace('\\', '/'))
            .Where(p => !p.Contains("/bin/", StringComparison.OrdinalIgnoreCase))
            .Where(p => !p.Contains("/obj/", StringComparison.OrdinalIgnoreCase))
            .Where(p => !p.EndsWith(BaselineThemeFile, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p, StringComparer.Ordinal);
    }

    /// <summary>
    /// Claves que la propia capa de estilos declara como recursos locales (x:Key en Styles/*.axaml),
    /// p. ej. el ControlTheme de los segmentos pastilla de los filtros de log.
    /// NO son tokens de tema: las posee Styles/*.axaml y ThemeResourceApplier no debe publicarlas,
    /// de modo que exigirles presencia en los 8 presets sería un falso positivo.
    /// </summary>
    private static HashSet<string> CollectStyleLayerLocalKeys()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        string stylesDir = Path.Combine(TestRepositoryLocator.RepositoryRoot(), "FileFlow.App/Styles");

        if (!Directory.Exists(stylesDir))
        {
            return keys;
        }

        foreach (string file in Directory.EnumerateFiles(stylesDir, "*.axaml"))
        {
            foreach (Match match in ResourceKeyRegex.Matches(File.ReadAllText(file)))
            {
                keys.Add(match.Groups[1].Value);
            }
        }

        return keys;
    }

    /// <summary>Tokens de tema realmente referenciados (excluye los recursos locales de la capa de estilos).</summary>
    private static SortedDictionary<string, List<string>> CollectReferencedTokens()
    {
        var tokens = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        var localKeys = CollectStyleLayerLocalKeys();

        foreach (string file in EnumerateXamlFiles())
        {
            string text = File.ReadAllText(file);
            foreach (Match match in DynamicResourceRegex.Matches(text))
            {
                string key = match.Groups[1].Value;
                if (localKeys.Contains(key))
                {
                    continue;
                }

                if (!tokens.TryGetValue(key, out var owners))
                {
                    owners = [];
                    tokens[key] = owners;
                }
                if (!owners.Contains(file))
                {
                    owners.Add(file);
                }
            }
        }

        return tokens;
    }

    #endregion

    [Fact]
    public void EveryTokenReferencedInXaml_ShouldBeDefinedInEveryBuiltInTheme()
    {
        var referenced = CollectReferencedTokens();
        referenced.Should().NotBeEmpty("el código AXAML debe consumir tokens del tema");

        var failings = new List<string>();
        foreach (var theme in BuiltInThemesCatalog.GetThemes())
        {
            var generated = ThemeResourceApplier.BuildResourceDictionary(theme);
            foreach (var (token, owners) in referenced)
            {
                if (!generated.ContainsKey(token))
                {
                    failings.Add($"[{theme.Id}] '{token}' (usado en {owners.Count} fichero(s), p.ej. {Path.GetFileName(owners[0])})");
                }
            }
        }

        failings.Should().BeEmpty(
            "todo token referenciado con DynamicResource debe existir en los 8 presets integrados. " +
            "Tokens ausentes: " + string.Join(" | ", failings));
    }

    [Fact]
    public void EveryTokenReferencedInXaml_ShouldBeDefinedInStartupBaseline()
    {
        var baselineKeys = ParseBaselineKeys();
        var referenced = CollectReferencedTokens();

        var missing = referenced.Keys.Where(k => !baselineKeys.Contains(k)).ToList();

        missing.Should().BeEmpty(
            "el diccionario de arranque (Themes/DarkTheme.axaml) resuelve los recursos antes de aplicar el tema guardado. " +
            "Faltan: " + string.Join(", ", missing));
    }

    [Fact]
    public void StartupBaseline_ShouldMirrorDefaultPreset_WithoutDrift()
    {
        var preset = BuiltInThemesCatalog.GetThemes()
            .FirstOrDefault(t => t.Id.Equals(BaselinePresetId, StringComparison.OrdinalIgnoreCase));

        preset.Should().NotBeNull($"el preset '{BaselinePresetId}' debe existir en builtin_themes.json");

        var generated = ThemeResourceApplier.BuildResourceDictionary(preset!);
        var baselineColorBrushes = ParseBaselineColorBrushes();
        var generatedKeys = generated.Keys.OfType<string>().ToHashSet(StringComparer.Ordinal);

        // 1. Paridad bidireccional de claves entre baseline y preset por defecto.
        var baselineKeys = ParseBaselineKeys();

        var missingInBaseline = generatedKeys.Where(k => !baselineKeys.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
        missingInBaseline.Should().BeEmpty(
            "el baseline debe declarar todas las claves que ThemeResourceApplier publica. Faltan: " + string.Join(", ", missingInBaseline));

        // La dirección inversa es la que provocaba el bug de TextMutedBrush: un token declarado en el
        // diccionario de arranque que el aplicador nunca reescribe queda con el valor del tema oscuro para siempre.
        var orphanInBaseline = baselineKeys.Where(k => !generatedKeys.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
        orphanInBaseline.Should().BeEmpty(
            "el baseline no puede declarar tokens que ThemeResourceApplier no publique (quedarían congelados al cambiar de tema). " +
            "Huérfanos: " + string.Join(", ", orphanInBaseline));

        // 2. Los brushes sólidos del preset deben coincidir exactamente con los declarados en el baseline.
        var mismatches = new List<string>();
        foreach (string key in generatedKeys.OrderBy(k => k, StringComparer.Ordinal))
        {
            if (generated[key] is not SolidColorBrush solid)
            {
                continue; // Los gradientes se comparan aparte (paso 3).
            }

            if (!baselineColorBrushes.TryGetValue(key, out var baselineColors) || baselineColors.Count != 1)
            {
                mismatches.Add($"{key}: el preset genera un color sólido pero el baseline no lo declara como tal");
                continue;
            }

            string actual = $"#{solid.Color.A:X2}{solid.Color.R:X2}{solid.Color.G:X2}{solid.Color.B:X2}";
            string expected = baselineColors[0];
            if (!ColorsEqual(expected, actual))
            {
                mismatches.Add($"{key}: baseline '{expected}' != preset '{actual}'");
            }
        }

        // 3. El gradiente del cable no puede divergir tampoco.
        if (generated["ConnectionWireBrush"] is LinearGradientBrush wire)

        {
            var baselineWire = ParseBaselineGradientStops("ConnectionWireBrush");
            baselineWire.Should().HaveCount(wire.GradientStops.Count,
                "el gradiente ConnectionWireBrush del baseline debe tener el mismo número de paradas que el preset");

            for (int i = 0; i < wire.GradientStops.Count; i++)
            {
                var stop = wire.GradientStops[i].Color;
                string actual = $"#{stop.A:X2}{stop.R:X2}{stop.G:X2}{stop.B:X2}";
                if (!ColorsEqual(baselineWire[i], actual))
                {
                    mismatches.Add($"ConnectionWireBrush[{i}]: baseline '{baselineWire[i]}' != preset '{actual}'");
                }
            }
        }

        mismatches.Should().BeEmpty(
            "el baseline de arranque debe ser un espejo exacto del preset por defecto. Divergencias: " + string.Join(" | ", mismatches));
    }

    [Fact]
    public void BuiltInThemes_TextMuted_ShouldMeetAaContrastOnSurfaces()
    {
        var failings = new List<string>();

        foreach (var theme in BuiltInThemesCatalog.GetThemes())
        {
            double muted = RelativeLuminance(Color.Parse(theme.TextMuted));

            foreach (var (label, hex) in new[] { ("BgSurface", theme.BgSurface), ("BgCard", theme.BgCard) })
            {
                double background = RelativeLuminance(Color.Parse(hex));
                double ratio = ContrastRatio(muted, background);

                if (ratio < 4.5)
                {
                    failings.Add($"[{theme.Id}] TextMuted {theme.TextMuted} sobre {label} {hex} = {ratio:F2}:1");
                }
            }
        }

        failings.Should().BeEmpty(
            "el texto atenuado debe cumplir WCAG AA (4.5:1) sobre las superficies donde se dibuja. " +
            "Incumplimientos: " + string.Join(" | ", failings));
    }

    #region Utilidades de parseo y contraste

    private static HashSet<string> ParseBaselineKeys()
    {
        string path = Path.Combine(TestRepositoryLocator.RepositoryRoot(), BaselineThemeFile);
        File.Exists(path).Should().BeTrue($"el diccionario baseline debe existir en {BaselineThemeFile}");

        return ResourceKeyRegex.Matches(File.ReadAllText(path))
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Devuelve, por clave, los colores declarados para brushes sólidos y las paradas de los gradientes.</summary>
    private static Dictionary<string, List<string>> ParseBaselineColorBrushes()
    {
        string path = Path.Combine(TestRepositoryLocator.RepositoryRoot(), BaselineThemeFile);
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        string? currentGradientKey = null;
        foreach (string rawLine in File.ReadAllLines(path))
        {
            string line = rawLine.Trim();

            if (line.StartsWith("<!--", StringComparison.Ordinal))
            {
                continue;
            }

            if (currentGradientKey != null)
            {
                if (line.Contains("</LinearGradientBrush>", StringComparison.Ordinal))
                {
                    currentGradientKey = null;
                    continue;
                }

                var stop = BrushColorRegex.Match(line);
                if (stop.Success)
                {
                    result[currentGradientKey].Add(stop.Groups[1].Value);
                }
                continue;
            }

            var key = ResourceKeyRegex.Match(line);
            if (!key.Success)
            {
                continue;
            }

            string tokenKey = key.Groups[1].Value;

            if (line.Contains("LinearGradientBrush", StringComparison.Ordinal))
            {
                currentGradientKey = tokenKey;
                result[tokenKey] = [];
                continue;
            }

            if (line.Contains("SolidColorBrush", StringComparison.Ordinal))
            {
                var color = BrushColorRegex.Match(line);
                if (color.Success)
                {
                    result[tokenKey] = [color.Groups[1].Value];
                }
            }
        }

        return result;
    }

    private static List<string> ParseBaselineGradientStops(string key)
    {
        var brushes = ParseBaselineColorBrushes();
        return brushes.TryGetValue(key, out var stops) ? stops : [];
    }

    /// <summary>Compara dos colores en formato hex. El canal alfa admite ±2 de tolerancia por redondeo de máscara.</summary>
    private static bool ColorsEqual(string expectedHex, string actualHex)
    {
        try
        {
            var expected = Color.Parse(expectedHex);
            var actual = Color.Parse(actualHex);
            return expected.R == actual.R
                && expected.G == actual.G
                && expected.B == actual.B
                && Math.Abs(expected.A - actual.A) <= 2;
        }
        catch
        {
            return false;
        }
    }

    private static double RelativeLuminance(Color color)
    {
        static double Channel(byte value)
        {
            double c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(color.R)) + (0.7152 * Channel(color.G)) + (0.0722 * Channel(color.B));
    }

    private static double ContrastRatio(double first, double second)
    {
        double lighter = Math.Max(first, second);
        double darker = Math.Min(first, second);
        return (lighter + 0.05) / (darker + 0.05);
    }

    #endregion
}
