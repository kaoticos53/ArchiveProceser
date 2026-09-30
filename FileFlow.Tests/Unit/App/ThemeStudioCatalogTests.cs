using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
        var english = ReadResxKeys("FileFlow.App.Core/Resources/Strings.resx");
        var spanish = ReadResxKeys("FileFlow.App.Core/Resources/Strings.es.resx");

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

            // Los tokens los genera el host por el puente; la prueba instala uno propio que cuenta
            // cuántos tokens hay (la previsualización se alimenta del diccionario portable).
            FileFlow.App.Core.ThemeHostBridge.BuildResources = _ =>
                new Dictionary<string, object?> { ["RadiusSm"] = 12.0 };
            try
            {
                // Cambiar un ajuste desde su fila debe regenerar los tokens de la vista previa.
                var radiusRow = viewModel.Sections.SelectMany(s => s.Rows)
                    .OfType<ThemeNumberRowViewModel>()
                    .First(r => r.Property == nameof(ThemeDefinition.CornerRadius));

                radiusRow.Value += 6;

                viewModel.LivePreviewResources["RadiusSm"].Should().Be(12.0,
                    "editar una fila debe regenerar los tokens de la vista previa a través del puente");

                viewModel.EditingTheme.CornerRadius.Should().Be((double)radiusRow.Value, "la fila escribe sobre el tema en edición");
            }
            finally
            {
                FileFlow.App.Core.ThemeHostBridge.BuildResources = null;
            }
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

            // Con el puente instalado, el diccionario estable recibe los tokens del host.
            FileFlow.App.Core.ThemeHostBridge.BuildResources = _ =>
                new Dictionary<string, object?> { ["AppBackgroundBrush"] = "token-de-prueba" };
            try
            {
                viewModel.UpdateLivePreview();
                attached.Should().ContainKey("AppBackgroundBrush");
            }
            finally
            {
                FileFlow.App.Core.ThemeHostBridge.BuildResources = null;
            }
        }
        finally
        {
            File.Delete(storage);
        }
    }
}
