using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using FileFlow.App;
using FileFlow.App.Models;
using FileFlow.App.Services;
using FileFlow.App.Themes;
using FileFlow.Sdk.Localization;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Views;

/// <summary>
/// Guardias de los <b>selectores del menú</b> (tema, idioma) y de los de ajustes (idioma, canal de
/// actualización).
///
/// Los cuatro desplegables compartían el mismo defecto: el valor viajaba en la etiqueta visible del
/// elemento (<c>&lt;ComboBoxItem Tag="es-ES"&gt;Español&lt;/ComboBoxItem&gt;</c>) y se enlazaba con
/// <c>SelectedValueBinding="{Binding Tag, RelativeSource={RelativeSource Self}}"</c>. Ese enlace no
/// resuelve: el campo aparecía <b>en blanco</b> (nada seleccionado, porque ningún elemento casaba con el
/// valor) y elegir una opción <b>no hacía nada</b>, porque el control escribía <c>null</c> de vuelta al view
/// model y el cambio moría en un <c>if (string.IsNullOrWhiteSpace(value)) return;</c>.
///
/// Las pruebas de aquí fijan las dos mitades del contrato: lo que se <b>ve</b> (el selector muestra el valor
/// activo, no un hueco) y lo que <b>ocurre</b> (elegir idioma cambia la cultura, persiste la preferencia y
/// refresca los textos en caliente). La última prueba es un lint del XAML: el idioma roto no puede volver a
/// escribirse en ninguna vista.
/// </summary>
[Collection(VisualSnapshotsCollection.Name)]
public class SelectorBindingGuardTests : IClassFixture<SharedAppVisualFixture>
{
    private const string Spanish = "es-ES";
    private const string English = "en-US";

    private readonly SharedAppVisualFixture _shared;

    public SelectorBindingGuardTests(SharedAppVisualFixture shared) => _shared = shared;

    [Fact]
    public void TheDrawerThemeSelector_ShouldShowTheAppliedTheme()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var fixture = _shared.Frozen();
            fixture.EnsureFrozen();

            var window = new MainWindow(fixture.Shell);
            window.Show();

            try
            {
                var selector = SelectorWithItemsOf<ThemeDefinition>(window);
                string applied = ThemeManager.Instance.CurrentThemeId;

                selector.SelectedIndex.Should().BeGreaterThanOrEqualTo(0,
                    "el selector de temas del menú no puede aparecer en blanco: es el valor que el usuario cree " +
                    "estar usando");
                selector.SelectedValue.Should().Be(applied,
                    "debe mostrar el tema realmente aplicado ('Dark' guardado en preferencias antiguas no es " +
                    "ningún identificador del catálogo)");
                selector.SelectedItem.Should().BeOfType<ThemeDefinition>()
                    .Which.Id.Should().Be(applied);
                ((ThemeDefinition)selector.SelectedItem!).Name.Should().NotBeNullOrWhiteSpace(
                    "el elemento seleccionado necesita un nombre visible: sin él el campo se pinta vacío");
            }
            finally
            {
                VisualSnapshot.DetachTree(window);
                window.Close();
            }
        });
    }

    [Fact]
    public void TheDrawerLanguageSelector_ShouldShowTheActiveLanguage()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var fixture = _shared.Frozen();
            fixture.EnsureFrozen();

            var window = new MainWindow(fixture.Shell);
            window.Show();

            try
            {
                var selector = SelectorWithItemsOf<SelectorOption>(window);

                selector.SelectedIndex.Should().BeGreaterThanOrEqualTo(0,
                    "el selector de idioma del menú no puede aparecer en blanco " +
                    $"(VM='{fixture.ControlBar.SelectedLanguage}', SelectedValue='{selector.SelectedValue}', " +
                    $"opciones=[{string.Join(",", (selector.ItemsSource?.Cast<object>() ?? []).OfType<SelectorOption>().Select(o => $"{o.Code}='{o.DisplayName}'"))}])");
                selector.SelectedValue.Should().Be(fixture.ControlBar.SelectedLanguage);

                var selected = selector.SelectedItem.Should().BeOfType<SelectorOption>().Subject;
                selected.Code.Should().Be(fixture.ControlBar.SelectedLanguage);
                selected.DisplayName.Should().NotBeNullOrWhiteSpace();
                LanguageCatalog.Resolve(fixture.ControlBar.SelectedLanguage).Should().NotBeNull(
                    "el idioma activo debe existir en el catálogo que alimenta el selector");
            }
            finally
            {
                VisualSnapshot.DetachTree(window);
                window.Close();
            }
        });
    }

    [Fact]
    public void ChoosingAnotherLanguageInTheDrawer_ShouldApplyItPersistItAndRetranslateTheUi()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var fixture = _shared.Frozen();
            fixture.EnsureFrozen();

            var window = new MainWindow(fixture.Shell);
            window.Show();

            ComboBox? selector = null;

            try
            {
                selector = SelectorWithItemsOf<SelectorOption>(window);

                string headerInSpanish = LocalizationManager.Instance.GetString("Drawer_FlowManagement", string.Empty);
                int englishIndex = IndexOfCode(selector, English);

                englishIndex.Should().BeGreaterThanOrEqualTo(0,
                    $"'{English}' debe estar entre las opciones del selector de idioma");

                // Como si el usuario abriera el menú y eligiera inglés.
                selector.SelectedIndex = englishIndex;

                LocalizationManager.Instance.CurrentCulture.Name.Should().Be(English,
                    "elegir un idioma debe cambiar la cultura activa, no sólo un valor de la lista");
                fixture.ControlBar.SelectedLanguage.Should().Be(English);
                fixture.Preferences.Preferences.Language.Should().Be(English,
                    "la elección debe sobrevivir al reinicio");
                selector.SelectedItem.Should().BeOfType<SelectorOption>().Which.Code.Should().Be(English);

                // Y la interfaz se retraduce en caliente: el texto del propio panel que alberga el selector.
                string headerInEnglish = LocalizationManager.Instance.GetString("Drawer_FlowManagement", string.Empty);
                headerInEnglish.Should().NotBe(headerInSpanish,
                    "la clave usada como testigo debe tener textos distintos por idioma");

                window.GetLogicalDescendants().OfType<TextBlock>()
                    .Select(t => t.Text)
                    .Should().Contain(headerInEnglish,
                        "la interfaz debe reflejar el idioma elegido sin reiniciar");
            }
            finally
            {
                if (selector != null)
                {
                    int spanishIndex = IndexOfCode(selector, Spanish);
                    if (spanishIndex >= 0)
                    {
                        selector.SelectedIndex = spanishIndex;
                    }
                }

                LocalizationManager.Instance.SetCulture(Spanish);

                VisualSnapshot.DetachTree(window);
                window.Close();
            }
        });
    }

    [Fact]
    public void TheWorkflowSettingsSelectors_ShouldShowTheirStoredValues()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var window = ModalVisualFixture.Build(ModalSurface.WorkflowSettings);
            window.Show();

            try
            {
                var combos = Combos(window);

                AssertSelected(SelectorWithCode(combos, Spanish), "language");
                AssertSelected(SelectorWithCode(combos, "RenameIncremental"), "conflict strategy");
                AssertSelected(SelectorWithCode(combos, "Stable"), "update channel");
                AssertSelected(SelectorWithItemsOf<ThemeDefinition>(combos), "theme");
            }
            finally
            {
                VisualSnapshot.DetachTree(window);
                window.Close();
            }
        });
    }

    [Fact]
    public void StoredThemeIdentifiers_ShouldResolveToAnIdentifierTheSelectorsList()
    {
        // Las preferencias antiguas guardaban el nombre del enumerado: sin traducción, el selector no
        // encontraba su valor y salía en blanco (el tema sí se aplicaba, y por eso el fallo pasaba inadvertido).
        ThemeManager.ResolveThemeId("Dark").Should().Be(ThemeManager.DefaultThemeId);
        ThemeManager.ResolveThemeId("dark").Should().Be(ThemeManager.DefaultThemeId);
        ThemeManager.ResolveThemeId("Light").Should().Be("light_studio");
        ThemeManager.ResolveThemeId("pastel").Should().Be("pastel_spring");
        ThemeManager.ResolveThemeId("cyber").Should().Be("cyber_neon");
        ThemeManager.ResolveThemeId("system").Should().Be(ThemeManager.SystemThemeId);

        ThemeManager.ResolveThemeId(null).Should().BeNull();
        ThemeManager.ResolveThemeId("  ").Should().BeNull();
        ThemeManager.ResolveThemeId("theme-that-does-not-exist").Should().BeNull();

        // Y todo identificador que resuelve debe estar en el catálogo que alimenta el desplegable, que es lo
        // único que garantiza que el campo se pinte.
        var catalogIds = CustomThemeService.Instance.GetAllThemes().Select(t => t.Id).ToList();
        catalogIds.Should().Contain(ThemeManager.DefaultThemeId);

        foreach (var id in catalogIds)
        {
            ThemeManager.ResolveThemeId(id).Should().Be(id);
        }
    }

    [Fact]
    public void NoSelector_ShouldCarryItsValueInAComboBoxItemTag()
    {
        // Lint de XAML: el patrón que dejaba los campos en blanco no puede volver a escribirse en ninguna
        // vista del repositorio (incluidas las de los plugins).
        string root = TestRepositoryLocator.RepositoryRoot();

        var offenders = new List<string>();
        int combosSeen = 0;

        foreach (string file in Directory.EnumerateFiles(root, "*.axaml", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(root, file).Replace('\\', '/');

            if (relative.Contains("/obj/", StringComparison.Ordinal) ||
                relative.Contains("/bin/", StringComparison.Ordinal))
            {
                continue;
            }

            XDocument document;
            try
            {
                document = XDocument.Load(file);
            }
            catch (System.Xml.XmlException)
            {
                continue;
            }

            foreach (var combo in document.Descendants().Where(e => e.Name.LocalName == "ComboBox"))
            {
                combosSeen++;

                string? valueBinding = combo.Attribute("SelectedValueBinding")?.Value;
                if (valueBinding != null && valueBinding.Contains("RelativeSource", StringComparison.Ordinal))
                {
                    offenders.Add($"{relative}: SelectedValueBinding='{valueBinding}'");
                }

                bool carriesValueInTag = combo.Descendants()
                    .Any(e => e.Name.LocalName == "ComboBoxItem" && e.Attribute("Tag") != null);

                if (carriesValueInTag)
                {
                    offenders.Add($"{relative}: un ComboBoxItem lleva su valor en 'Tag'");
                }
            }
        }

        combosSeen.Should().BeGreaterThan(10, "el lint debe estar viendo los desplegables reales del repositorio");

        offenders.Should().BeEmpty(
            "el valor de un desplegable se enlaza con el dato del elemento (SelectedValueBinding=\"{Binding Code}\"), " +
            "nunca con la etiqueta visible de su contenedor: ese enlace no resuelve, deja el campo en blanco y " +
            "el control escribe null de vuelta al view model");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Ayudas
    // ─────────────────────────────────────────────────────────────────────────────

    private static void AssertSelected(ComboBox selector, string what)
    {
        selector.SelectedIndex.Should().BeGreaterThanOrEqualTo(0, $"el desplegable de {what} no puede estar en blanco");
        selector.SelectedItem.Should().NotBeNull($"el desplegable de {what} debe mostrar su valor activo");
    }

    private static ComboBox SelectorWithItemsOf<T>(Control root) => SelectorWithItemsOf<T>(Combos(root));

    private static ComboBox SelectorWithItemsOf<T>(IReadOnlyList<ComboBox> combos)
    {
        var matches = combos
            .Where(c => c.ItemsSource != null && c.ItemsSource.Cast<object>().Any(i => i is T))
            .ToList();

        matches.Should().ContainSingle($"debe haber un único desplegable alimentado con {typeof(T).Name}");
        return matches[0];
    }

    private static ComboBox SelectorWithCode(IReadOnlyList<ComboBox> combos, string code)
    {
        var matches = combos
            .Where(c => c.ItemsSource != null &&
                        c.ItemsSource.Cast<object>().OfType<SelectorOption>()
                            .Any(o => string.Equals(o.Code, code, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        matches.Should().ContainSingle(
            $"debe haber un único desplegable con la opción '{code}' " +
            $"(encontrados: {string.Join(" | ", matches.Select(c => $"#{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(c)}:" +
                $"[{string.Join(",", (c.ItemsSource?.Cast<object>() ?? []).OfType<SelectorOption>().Select(o => o.Code))}]"))})");
        return matches[0];
    }

    /// <summary>
    /// Desplegables del árbol lógico, sin repetidos: Avalonia puede listar el mismo control dos veces cuando
    /// el recorrido pasa por un contenedor de contenido y por su presentador.
    /// </summary>
    private static List<ComboBox> Combos(Control root) =>
        [.. root.GetLogicalDescendants().OfType<ComboBox>().Distinct(SameControl.Instance)];

    /// <summary>Compara controles por identidad (no por valor).</summary>
    private sealed class SameControl : IEqualityComparer<ComboBox>
    {
        public static readonly SameControl Instance = new();

        public bool Equals(ComboBox? x, ComboBox? y) => ReferenceEquals(x, y);

        public int GetHashCode(ComboBox obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }

    private static int IndexOfCode(ComboBox selector, string code)
    {
        var items = selector.ItemsSource?.Cast<object>().OfType<SelectorOption>().ToList();
        if (items == null)
        {
            return -1;
        }

        return items.FindIndex(o => string.Equals(o.Code, code, StringComparison.OrdinalIgnoreCase));
    }
}
