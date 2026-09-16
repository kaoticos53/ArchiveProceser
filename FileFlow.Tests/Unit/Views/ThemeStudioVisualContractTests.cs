using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using FileFlow.App.Services;
using Avalonia.Media;
using FileFlow.App.Themes;
using FileFlow.App.ViewModels;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Views;

/// <summary>
/// Contrato visual del Theme Studio.
///
/// El Studio es la superficie donde el usuario comprueba que el tema «sirve»: si un binding muere, una fila
/// desaparece o la previsualización deja de seguir al editor, el usuario no se entera hasta que un ajuste no
/// hace nada. Estos tests cubren las tres formas de fallar sin romper la compilación: rutas de binding
/// inexistentes (bindings por reflexión), estilos en línea que ignoran el tema y una previsualización
/// desconectada del diccionario de tokens en edición.
/// </summary>
[Collection(VisualSnapshotsCollection.Name)]
public class ThemeStudioVisualContractTests
{
    private const string WindowRelativePath = "FileFlow.App/Views/Components/ThemeCustomizerWindow.axaml";
    private const string CodeBehindRelativePath = "FileFlow.App/Views/Components/ThemeCustomizerWindow.axaml.cs";

    private static readonly Regex BindingPathRegex = new(
        @"\{Binding\s+!?([A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)\s*[,}]",
        RegexOptions.Compiled);

    private static readonly Regex DataTemplateContextRegex = new(
        @"<DataTemplate\b[^>]*DataType=""(?:[A-Za-z]+:)?([A-Za-z0-9_]+)""",
        RegexOptions.Compiled);

    private static readonly Regex ClosedDataTemplateRegex = new(@"</DataTemplate>", RegexOptions.Compiled);

    /// <summary>Contextos de datos que el Studio declara: la ventana y cada tipo de fila.</summary>
    private static readonly Dictionary<string, Type> KnownDataContexts = new(StringComparer.Ordinal)
    {
        [nameof(ThemeCustomizerViewModel)] = typeof(ThemeCustomizerViewModel),
        [nameof(ThemeSettingSectionViewModel)] = typeof(ThemeSettingSectionViewModel),
        [nameof(ThemeColorRowViewModel)] = typeof(ThemeColorRowViewModel),
        [nameof(ThemeNumberRowViewModel)] = typeof(ThemeNumberRowViewModel),
        [nameof(ThemeChoiceRowViewModel)] = typeof(ThemeChoiceRowViewModel),
        [nameof(ThemeDefinition)] = typeof(ThemeDefinition)
    };

    // ─────────────────────────────────────────────────────────────────────────────
    // Bindings
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EveryStudioBinding_ShouldResolveAgainstTheViewModelOfItsTemplate()
    {
        string window = ReadRepositoryFile(WindowRelativePath);
        var unresolved = new List<string>();
        var unknownContexts = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (line, path, contextName) in ExtractBindingsWithContext(window, unknownContexts))
        {
            if (KnownDataContexts.TryGetValue(contextName, out var context) && ResolvesPath(context, path))
            {
                continue;
            }

            unresolved.Add($"{Path.GetFileName(WindowRelativePath)}:{line} '{path}' sobre {contextName}");
        }

        unknownContexts.Should().BeEmpty(
            "toda plantilla del Studio debe declarar un DataType conocido para poder validar sus bindings. " +
            "Desconocidos: " + string.Join(", ", unknownContexts));

        unresolved.Should().BeEmpty(
            "una ruta de binding que no existe deja el control vacío en tiempo de ejecución sin fallar la " +
            "compilación. Rutas muertas: " + string.Join(" | ", unresolved));
    }

    [Fact]
    public void TheEditor_ShouldBeDrivenByTheCatalog_NotByHandWrittenColorBindings()
    {
        string window = ReadRepositoryFile(WindowRelativePath);

        window.Should().Contain("ItemsSource=\"{Binding Sections}\"", "el editor se genera desde el catálogo");
        window.Should().Contain("ItemsSource=\"{Binding Rows}\"", "cada sección proyecta sus filas");

        // Los bindings a propiedades del tema (p. ej. {Binding AccentPrimary}) son el patrón antiguo: se
        // quedaban muertos en silencio porque el DataContext de la ventana es el view model, no el tema.
        var themeProperties = typeof(ThemeDefinition).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Where(name => name is not ("Name" or "Description" or "IsBuiltIn" or "IsDark" or "Id"))
            .ToList();

        var handWritten = themeProperties
            .Where(property => window.Contains($"{{Binding {property}}}", StringComparison.Ordinal))
            .ToList();

        handWritten.Should().BeEmpty(
            "las filas del editor ya escriben sobre el tema: bindear una propiedad del tema desde la ventana " +
            "duplicaría el control y volvería a quedar muerto. Encontrados: " + string.Join(", ", handWritten));
    }

    [Fact]
    public void TheWindow_ShouldNotContainInlineShapeTypographyOrColorLiterals()
    {
        string window = ReadRepositoryFile(WindowRelativePath);

        Regex.Matches(window, @"FontSize=""[0-9.]+""").Should().BeEmpty(
            "el tamaño de letra del Studio debe salir de la escala tipográfica del tema (clases o FontSize* tokens)");

        Regex.Matches(window, @"CornerRadius=""[0-9.]+""").Should().BeEmpty(
            "la redondez del Studio debe salir de la escala de radios del tema (Radius*)");

        Regex.Matches(window, @"BoxShadow=""[^""]*#").Should().BeEmpty(
            "la profundidad del Studio debe salir de la escala de elevación del tema (Elev*)");

        Regex.Matches(window, @"=""#[0-9A-Fa-f]{6,8}""").Should().BeEmpty(
            "los colores del Studio deben salir de la paleta del tema, no de literales hex");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Previsualización en vivo
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ThePreview_ShouldRenderEveryCustomizableScale()
    {
        string window = ReadRepositoryFile(WindowRelativePath);
        int previewStart = window.IndexOf("PreviewHost", StringComparison.Ordinal);
        previewStart.Should().BeGreaterThanOrEqualTo(0, "la zona de previsualización debe existir y tener nombre");

        string preview = window[previewStart..];

        foreach (string token in new[]
                 {
                     "RadiusXs", "RadiusSm", "RadiusMd", "RadiusLg", "RadiusXl", "RadiusXxl", "RadiusPill",
                     "Space1", "Space4", "Space9",
                     "Elev1", "Elev2", "Elev3", "Elev4", "ElevGlowAccent"
                 })
        {
            preview.Should().Contain($"DynamicResource {token}}}",
                $"la previsualización debe demostrar el token '{token}'; si no, los ajustes de radios, densidad " +
                "y sombras no tienen dónde verse antes de aplicar el tema");
        }
    }

    [Fact]
    public void ThePreviewHost_ShouldReceiveTheLiveTokenDictionary()
    {
        string codeBehind = ReadRepositoryFile(CodeBehindRelativePath);

        codeBehind.Should().Contain("PreviewHost.Resources",
            "la previsualización sólo es «en vivo» si su subárbol resuelve los tokens del tema en edición");

        codeBehind.Should().Contain(nameof(ThemeCustomizerViewModel.LivePreviewResources),
            "el diccionario enganchado debe ser el que regenera el view model");
    }

    /// <summary>
    /// La previsualización no se prueba instanciando la ventana completa (arrastra los estilos globales de la
    /// aplicación), sino reproduciendo el mecanismo exacto del que depende: el host engancha el diccionario
    /// del tema en edición y sus descendientes resuelven ahí los tokens, siguiendo los cambios en caliente.
    /// </summary>
    [Fact]
    public void PreviewSubtree_ShouldResolveItsTokensFromTheDictionaryOfTheEditedTheme()
    {
        string storage = Path.Combine(Path.GetTempPath(), $"studio_visual_{Guid.NewGuid():N}.json");

        try
        {
            // Sin pasar por el Dispatcher: sólo se ejercita la resolución de recursos sobre controles
            // sueltos, así que el test no depende de que el bucle de mensajes de Avalonia esté bombeando
            // (y no puede bloquear al resto de la suite).
            AvaloniaTestHelper.EnsureInitialized();

            var viewModel = new ThemeCustomizerViewModel(new CustomThemeService(storage));

            var host = new Border();
            var descendant = new Border();
            host.Child = descendant;

            // Equivale a PreviewHost.Resources = viewModel.LivePreviewResources (ver el code-behind).
            host.Resources = viewModel.LivePreviewResources;

            descendant.TryFindResource("BgCardBrush", out var cardBrush).Should().BeTrue(
                "un descendiente de la previsualización debe resolver los tokens del tema en edición");

            ((SolidColorBrush)cardBrush!).Color.Should().Be(
                Color.Parse(viewModel.EditingTheme.BgCard),
                "el valor debe ser el del tema EN EDICIÓN, no el del tema activo de la aplicación");

            descendant.TryFindResource("Elev3", out var elevation).Should().BeTrue();
            elevation.Should().BeOfType<BoxShadows>();

            // Mover un ajuste desde su fila debe cambiar lo que ya resuelve la previsualización.
            var row = viewModel.Sections.SelectMany(s => s.Rows)
                .OfType<ThemeNumberRowViewModel>()
                .First(r => r.Property == nameof(ThemeDefinition.CornerRadius));

            row.Value += 8;

            descendant.TryFindResource("RadiusSm", out var radius).Should().BeTrue();
            ((CornerRadius)radius!).TopLeft.Should().Be(
                viewModel.EditingTheme.CornerRadius,
                "el diccionario enganchado se actualiza en el sitio: la previsualización sigue al editor sin reenganchar nada");
        }
        finally
        {
            File.Delete(storage);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────────

    private static string ReadRepositoryFile(string relativePath)
        => File.ReadAllText(Path.Combine(TestRepositoryLocator.RepositoryRoot(), relativePath));

    /// <summary>
    /// Recorre el XAML manteniendo la pila de plantillas abiertas: cada binding se asocia al DataType de la
    /// plantilla más interna en la que aparece, o al de la ventana si está fuera de cualquier plantilla.
    /// </summary>
    private static IEnumerable<(int Line, string Path, string Context)> ExtractBindingsWithContext(
        string xaml,
        HashSet<string> unknownContexts)
    {
        var stack = new Stack<string>();
        int line = 1;

        foreach (string rawLine in xaml.Replace("\r\n", "\n").Split('\n'))
        {
            foreach (Match opened in DataTemplateContextRegex.Matches(rawLine))
            {
                string context = opened.Groups[1].Value;
                stack.Push(context);

                if (!KnownDataContexts.ContainsKey(context))
                {
                    unknownContexts.Add(context);
                }
            }

            foreach (Match _ in ClosedDataTemplateRegex.Matches(rawLine))
            {
                if (stack.Count > 0) stack.Pop();
            }

            string contextForLine = stack.Count > 0 ? stack.Peek() : nameof(ThemeCustomizerViewModel);

            foreach (Match match in BindingPathRegex.Matches(rawLine))
            {
                yield return (line, match.Groups[1].Value, contextForLine);
            }

            line++;
        }
    }

    private static bool ResolvesPath(Type rootType, string path)
    {
        Type? current = rootType;

        foreach (string segment in path.Split('.'))
        {
            if (current is null)
            {
                return false;
            }

            var property = current.GetProperty(
                segment,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

            if (property is null)
            {
                return false;
            }

            current = property.PropertyType;
        }

        return true;
    }
}
