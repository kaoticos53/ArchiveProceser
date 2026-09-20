using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.App.Views.Components;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Views;

/// <summary>
/// Auditoría de <b>todos</b> los desplegables de la aplicación: cada uno debe mostrar su valor activo y, al
/// elegir, escribir en el modelo.
///
/// Existe porque el mismo defecto apareció en cuatro sitios distintos: un <c>ComboBox</c> atado por valor
/// <b>no pinta nada</b> si su valor no coincide con ningún elemento de la lista, y en ese caso el control
/// escribe <c>null</c> de vuelta al view model. En el menú dejaba «Tema visual» e «Idioma» en blanco; en el
/// Theme Studio dejaba la <b>tipografía de la interfaz</b> en blanco (las opciones del catálogo son familias
/// sueltas y el tema guarda la pila completa); y en el gestor de presets de media la categoría se sustituía en
/// silencio por «Video» al guardar.
///
/// Las guardias son de dos clases:
/// <list type="bullet">
///   <item><b>Barrido del catálogo</b>: para cada tipo de nodo del catálogo real se construye su nodo y se
///   exige que cada parámetro con desplegable ofrezca su valor actual entre las opciones. Es la forma de
///   cubrir ~150 nodos sin escribir una prueba por nodo.</item>
///   <item><b>Ventanas reales</b>: el Theme Studio y el gestor de presets se montan de verdad y se comprueba
///   que sus desplegables tienen una selección visible, que es lo que el usuario ve.</item>
/// </list>
///
/// Lo que <b>no</b> cubre: los desplegables de plugins cuyo valor es un enumerado tipado
/// (<c>AdvancedRenamerEditorWindow</c>: posiciones, tipos de mayúsculas, modos de normalización…). Éstos ya
/// usan el patrón correcto —el valor es un objeto y la lista contiene esos mismos objetos—, así que no pueden
/// quedarse en blanco con un valor ausente.
/// </summary>
[Collection(VisualSnapshotsCollection.Name)]
public class SelectorAuditTests : IClassFixture<SharedAppVisualFixture>
{
    private const string InheritedValue = "heredado-sin-opcion";

    private readonly SharedAppVisualFixture _shared;

    public SelectorAuditTests(SharedAppVisualFixture shared) => _shared = shared;

    [Fact]
    public void EveryNodeParameterDropdown_ShouldOfferItsCurrentValue()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var fixture = _shared.Frozen();
            fixture.EnsureFrozen();

            var typeNames = fixture.Toolbox.CategoryGroups
                .SelectMany(group => group.Items)
                .Select(item => item.TypeName)
                // Sólo el catálogo real de plugins: el cargador de las pruebas registra además los nodos falsos
                // que declara el propio suite (p. ej. las clases anidadas de los tests de energía).
                .Where(name => name.StartsWith("FileFlow.Plugin.", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();

            typeNames.Should().HaveCountGreaterThan(40, "el catálogo real de nodos debe llegar al barrido");

            var outsideTheList = new List<string>();
            var withoutValue = new List<string>();
            int dropdowns = 0;

            try
            {
                foreach (string typeName in typeNames)
                {
                    var node = fixture.Editor.AddNode(typeName, new Point(0, 0));

                    if (node == null)
                    {
                        withoutValue.Add($"{typeName}: el editor no pudo construir el nodo del catálogo");
                        continue;
                    }

                    foreach (var parameter in node.Parameters)
                    {
                        if (parameter.Options.Count == 0)
                        {
                            continue;
                        }

                        dropdowns++;
                        string value = parameter.Value?.ToString() ?? string.Empty;

                        if (string.IsNullOrWhiteSpace(value))
                        {
                            withoutValue.Add($"{typeName}.{parameter.Key} (opciones: {string.Join("|", parameter.Options)})");
                        }
                        else if (!parameter.Options.Any(o => string.Equals(o, value, StringComparison.OrdinalIgnoreCase)))
                        {
                            outsideTheList.Add(
                                $"{typeName}.{parameter.Key}: valor '{value}' no está entre las opciones " +
                                $"[{string.Join("|", parameter.Options)}]");
                        }

                        // Y la otra mitad: un valor que llega después (flujo guardado con otras opciones, nodo
                        // pegado, deshacer) debe seguir siendo visible en el desplegable.
                        if (!parameter.IsEditableDropdown)
                        {
                            parameter.Value = InheritedValue;

                            if (!parameter.Options.Any(o => string.Equals(o, InheritedValue, StringComparison.Ordinal)))
                            {
                                outsideTheList.Add(
                                    $"{typeName}.{parameter.Key}: un valor heredado ('{InheritedValue}') no se añade " +
                                    $"a las opciones y el desplegable quedaría en blanco");
                            }

                            parameter.Value = value;
                        }
                    }
                }
            }
            finally
            {
                fixture.Editor.LoadFromGraphModel(new FileFlow.Core.Engine.WorkflowGraph());
            }

            dropdowns.Should().BeGreaterThan(10, "el barrido debe estar viendo los desplegables reales de los nodos");

            outsideTheList.Should().BeEmpty(
                "un desplegable que no ofrece su valor actual se pinta en blanco: el usuario no ve qué tiene " +
                "configurado y el control escribe null sobre el parámetro");

            withoutValue.Should().BeEmpty(
                "un desplegable con opciones y sin valor es un campo en blanco que el usuario debe adivinar");
        });
    }

    [Fact]
    public void EveryThemeChoiceRow_ShouldOfferItsCurrentValue()
    {
        var offenders = new List<string>();

        foreach (var theme in BuiltInThemesCatalog.GetThemes())
        {
            foreach (var row in ThemeRowsFor(theme))
            {
                if (!row.Options.Any(o => string.Equals(o, row.SelectedOption, StringComparison.Ordinal)))
                {
                    offenders.Add(
                        $"tema '{theme.Id}', fila '{row.Label}': valor '{row.SelectedOption}' no está entre las " +
                        $"opciones [{string.Join("|", row.Options)}]");
                }
            }
        }

        offenders.Should().BeEmpty(
            "las opciones del catálogo son familias sueltas y el tema guarda la pila completa: sin incluir el " +
            "valor actual, el desplegable de tipografía aparece en blanco");
    }

    [Fact]
    public void TheStudioChoiceSelectors_ShouldShowTheActiveValue()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            string storage = Path.Combine(Path.GetTempPath(), $"audit_themes_{Guid.NewGuid():N}.json");

            try
            {
                var viewModel = new ThemeCustomizerViewModel(new CustomThemeService(storage));
                var window = new ThemeCustomizerWindow { DataContext = viewModel };
                window.Show();

                try
                {
                    var selectors = window.GetLogicalDescendants().OfType<ComboBox>()
                        .Where(combo => combo.DataContext is ThemeChoiceRowViewModel)
                        .Distinct(SameControl.Instance)
                        .ToList();

                    selectors.Should().HaveCountGreaterThanOrEqualTo(2,
                        "el Theme Studio expone al menos la tipografía de interfaz y la monoespaciada");

                    foreach (var selector in selectors)
                    {
                        var row = (ThemeChoiceRowViewModel)selector.DataContext!;

                        selector.SelectedIndex.Should().BeGreaterThanOrEqualTo(0,
                            $"la fila '{row.Label}' debe mostrar el valor activo ('{row.SelectedOption}')");
                        selector.SelectedItem.Should().Be(row.SelectedOption);
                    }
                }
                finally
                {
                    VisualSnapshot.DetachTree(window);
                    window.Close();
                }
            }
            finally
            {
                File.Delete(storage);
            }
        });
    }

    [Fact]
    public void TheStudioChoiceSelector_ShouldWriteThePickedValueIntoTheTheme()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            string storage = Path.Combine(Path.GetTempPath(), $"audit_themes_{Guid.NewGuid():N}.json");

            try
            {
                var viewModel = new ThemeCustomizerViewModel(new CustomThemeService(storage));
                var window = new ThemeCustomizerWindow { DataContext = viewModel };
                window.Show();

                try
                {
                    var selector = window.GetLogicalDescendants().OfType<ComboBox>()
                        .Where(combo => combo.DataContext is ThemeChoiceRowViewModel)
                        .Distinct(SameControl.Instance)
                        .First(combo => ((ThemeChoiceRowViewModel)combo.DataContext!).Property == nameof(FileFlow.App.Themes.ThemeDefinition.FontFamily));

                    var row = (ThemeChoiceRowViewModel)selector.DataContext!;

                    int other = row.Options.ToList().FindIndex(o => !string.Equals(o, row.SelectedOption, StringComparison.Ordinal));
                    other.Should().BeGreaterThanOrEqualTo(0, "la fila debe ofrecer más de una tipografía");

                    selector.SelectedIndex = other;

                    row.SelectedOption.Should().Be(row.Options[other],
                        "elegir en el desplegable debe escribir la tipografía en el tema en edición");
                    viewModel.EditingTheme.FontFamily.Should().Be(row.Options[other],
                        "el cambio debe llegar al tema que se está editando");
                }
                finally
                {
                    VisualSnapshot.DetachTree(window);
                    window.Close();
                }
            }
            finally
            {
                File.Delete(storage);
            }
        });
    }

    [Fact]
    public void TheMediaPresetCategorySelector_ShouldShowThePresetCategory()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var window = ModalVisualFixture.Build(ModalSurface.MediaPresetManager);
            window.Show();

            try
            {
                var selector = window.GetLogicalDescendants().OfType<ComboBox>().Distinct(SameControl.Instance).Single();

                selector.SelectedIndex.Should().BeGreaterThanOrEqualTo(0,
                    "el gestor de presets selecciona el primer preset: su categoría debe verse en el desplegable, " +
                    "no un campo en blanco");
                (selector.SelectedItem as ComboBoxItem)?.Content?.ToString().Should().NotBeNullOrWhiteSpace();
            }
            finally
            {
                VisualSnapshot.DetachTree(window);
                window.Close();
            }
        });
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Ayudas
    // ─────────────────────────────────────────────────────────────────────────────

    private static IEnumerable<ThemeChoiceRowViewModel> ThemeRowsFor(FileFlow.App.Themes.ThemeDefinition theme)
    {
        var viewModel = new ThemeCustomizerViewModel(new CustomThemeService(
            Path.Combine(Path.GetTempPath(), $"audit_row_{Guid.NewGuid():N}.json")));

        viewModel.SelectedTheme = viewModel.AvailableThemes.FirstOrDefault(t => t.Id == theme.Id)
                                 ?? throw new InvalidOperationException($"El tema '{theme.Id}' no está en la lista del Studio.");

        return viewModel.Sections.SelectMany(section => section.Rows).OfType<ThemeChoiceRowViewModel>().ToList();
    }

    /// <summary>Compara controles por identidad: Avalonia puede listar el mismo control dos veces en el árbol lógico.</summary>
    private sealed class SameControl : IEqualityComparer<ComboBox>
    {
        public static readonly SameControl Instance = new();

        public bool Equals(ComboBox? x, ComboBox? y) => ReferenceEquals(x, y);

        public int GetHashCode(ComboBox obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
