using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using FileFlow.App.Services;
using FileFlow.App.Themes;
using FileFlow.Tests.TestHelpers;
using FileFlow.Tests.Unit.Views;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Guardias de la propagación del tema activo (Fase 0 del rediseño visual).
/// Antes de esta corrección, ThemeManager sólo inyectaba brushes en Application.Resources y
/// la variante de FluentTheme permanecía fija en Dark, de modo que en los temas claros los
/// controles internos (ComboBox, ScrollBar, DataGrid, ContextMenu, Popup, TabControl)
/// seguían pintándose oscuros y las ventanas abiertas no se re-tematizaban.
/// <para>
/// Nota de diseño de los tests: se validan la decisión de variante, los eventos del ThemeManager y los
/// diccionarios de tokens generados (todo determinista y sin plataforma gráfica). La escritura real de
/// <c>Application.RequestedThemeVariant</c> y la reaplicación a ventanas abiertas dependen del ciclo de vida
/// de la aplicación, por lo que se protegen además con una guardia de código fuente que impide
/// que la publicación se elimine silenciosamente en el futuro.
/// </para>
/// <para>
/// <b>Colección exclusiva</b>: esta clase aplica temas al <c>ThemeManager</c> (proceso entero), así que no
/// puede correr en paralelo con las capturas headless —éstas están renderizando el tema activo—. Estaba en
/// la colección paralela <c>ThemeTokens</c> y dejaba el tema en un preset claro a mitad de una captura, de
/// modo que la comparación contra la línea base fallaba en otra prueba y sólo a veces.
/// La guardia <c>TestCollectionContractGuardTests</c> vigila ahora esta regla.
/// </para>
/// </summary>
[Collection(VisualSnapshotsCollection.Name)]
public class ThemeVariantPropagationTests
{
    private static readonly string[] DarkThemeIds = ["dark_fluent", "cyber_neon", "midnight_oled", "nord_slate", "dracula_purple", "emerald_forest"];
    private static readonly string[] LightThemeIds = ["light_studio", "pastel_spring"];

    #region Decisión de variante

    [Fact]
    public void ResolveThemeVariant_ShouldMapDarknessToFluentVariant()
    {
        WindowThemeHelper.ResolveThemeVariant(true).Should().Be(ThemeVariant.Dark);
        WindowThemeHelper.ResolveThemeVariant(false).Should().Be(ThemeVariant.Light);
    }

    [Fact]
    public void SetLightThemes_ShouldResolveAsNonDark()
    {
        foreach (var theme in BuiltInThemesCatalog.GetThemes().Where(t => !t.IsDark))
        {
            ThemeManager.Instance.SetTheme(theme);

            ThemeManager.Instance.IsCurrentThemeDark.Should().BeFalse($"'{theme.Id}' es un tema claro");
            WindowThemeHelper.ResolveThemeVariant(ThemeManager.Instance.IsCurrentThemeDark).Should().Be(ThemeVariant.Light);

            theme.Id.Should().BeOneOf(LightThemeIds);
        }
    }

    [Fact]
    public void SetDarkThemes_ShouldResolveAsDark()
    {
        foreach (var theme in BuiltInThemesCatalog.GetThemes().Where(t => t.IsDark))
        {
            ThemeManager.Instance.SetTheme(theme);

            ThemeManager.Instance.IsCurrentThemeDark.Should().BeTrue($"'{theme.Id}' es un tema oscuro");
            WindowThemeHelper.ResolveThemeVariant(ThemeManager.Instance.IsCurrentThemeDark).Should().Be(ThemeVariant.Dark);

            theme.Id.Should().BeOneOf(DarkThemeIds);
        }
    }

    [Fact]
    public void SetTheme_ShouldNotifySubscribersWithResolvedTheme()
    {
        var received = new List<AppTheme>();
        void Handler(AppTheme theme) => received.Add(theme);

        ThemeManager.Instance.ThemeChanged += Handler;
        try
        {
            ThemeManager.Instance.SetTheme(AppTheme.Light);
            ThemeManager.Instance.SetTheme(AppTheme.Cyber);
        }
        finally
        {
            ThemeManager.Instance.ThemeChanged -= Handler;
            ThemeManager.Instance.SetTheme(AppTheme.Dark);
        }

        received.Should().Equal(AppTheme.Light, AppTheme.Cyber);
    }

    #endregion

    #region Tokens generados por tema

    [Fact]
    public void EveryBuiltInTheme_ShouldGenerateMutedTextToken_DistinctFromSecondary()
    {
        var mutedColors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var theme in BuiltInThemesCatalog.GetThemes())
        {
            var generated = ThemeResourceApplier.BuildResourceDictionary(theme);

            generated.ContainsKey("TextMutedBrush").Should().BeTrue($"'{theme.Id}' debe publicar TextMutedBrush");

            var muted = generated["TextMutedBrush"].Should().BeOfType<SolidColorBrush>().Subject;

            muted.Color.Should().Be(Color.Parse(theme.TextMuted), $"'{theme.Id}' debe usar su TextMuted declarado");
            muted.Color.Should().NotBe(Color.Parse(theme.TextSecondary), $"'{theme.Id}' el atenuado no puede ser igual al secundario");

            mutedColors.Add($"{muted.Color.A:X2}{muted.Color.R:X2}{muted.Color.G:X2}{muted.Color.B:X2}");
        }

        mutedColors.Should().HaveCount(BuiltInThemesCatalog.GetThemes().Count,
            "cada preset debe definir su propio tono atenuado (no vale reutilizar el valor del tema oscuro)");
    }

    [Fact]
    public void EveryBuiltInTheme_ShouldGenerateTranslucentOverlayMatchingItsSurface()
    {
        foreach (var theme in BuiltInThemesCatalog.GetThemes())
        {
            var generated = ThemeResourceApplier.BuildResourceDictionary(theme);

            var overlay = generated["OverlaySurfaceBrush"].Should().BeOfType<SolidColorBrush>().Subject;
            var surface = generated["BgSurfaceBrush"].Should().BeOfType<SolidColorBrush>().Subject;

            overlay.Color.A.Should().BeInRange((byte)200, (byte)250, "el overlay del HUD debe ser translúcido pero legible");
            overlay.Color.R.Should().Be(surface.Color.R);
            overlay.Color.G.Should().Be(surface.Color.G);
            overlay.Color.B.Should().Be(surface.Color.B);
        }
    }

    [Fact]
    public void EveryBuiltInTheme_ShouldRespectDeclaredTypographyAndShapeTokens()
    {
        foreach (var theme in BuiltInThemesCatalog.GetThemes())
        {
            var generated = ThemeResourceApplier.BuildResourceDictionary(theme);

            generated["AppFontSize"].Should().Be(theme.BaseFontSize);
            ((CornerRadius)generated["AppCornerRadius"]!).TopLeft.Should().Be(theme.CornerRadius);
            ((FontFamily)generated["AppFontFamily"]!).Name.Should().Contain(theme.FontFamily.Split(',')[0].Trim());
        }
    }

    [Fact]
    public void ApplyThemeToOpenWindows_ShouldBeSafeWithoutDesktopLifetime()
    {
        // La guardia protege contra NRE si se invoca antes de que exista un ciclo de vida de escritorio.
        Action act = WindowThemeHelper.ApplyThemeToOpenWindows;

        act.Should().NotThrow();
    }

    #endregion

    #region Guardia de cableado en código fuente

    [Fact]
    public void ThemeManager_ShouldKeepPublishingVariantAndReapplyingToOpenWindows()
    {
        string path = Path.Combine(TestRepositoryLocator.RepositoryRoot(), "FileFlow.App/Services/ThemeManager.cs");
        File.Exists(path).Should().BeTrue();

        string source = File.ReadAllText(path);

        source.Should().Contain("Application.RequestedThemeVariant",
            "ThemeManager debe publicar la variante de FluentTheme al cambiar de tema (bug de Fase 0)");
        source.Should().Contain("WindowThemeHelper.ApplyThemeToOpenWindows()",
            "las ventanas ya abiertas deben re-tematizarse al cambiar de tema (bug de Fase 0)");
        source.Should().Contain("WindowThemeHelper.ResolveThemeVariant(",
            "la decisión claro/oscuro debe pasar por el punto único de traducción a ThemeVariant");
    }

    #endregion
}
