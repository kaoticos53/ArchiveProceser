using System;
using System.Collections.Generic;
using System.Linq;
using FileFlow.App.Services;
using FileFlow.App.Themes;

namespace FileFlow.App.ViewModels;

/// <summary>Fila de elección del Theme Studio (familias tipográficas de interfaz y de código).</summary>
public sealed class ThemeChoiceRowViewModel : ThemeSettingRowViewModel
{
    public ThemeChoiceRowViewModel(ThemeSettingDescriptor descriptor, ThemeDefinition theme)
        : base(descriptor, theme)
    {
        Options = WithCurrentValue(descriptor.Options ?? [], Read<string>());
        OnThemeValueRead();
    }

    public IReadOnlyList<string> Options { get; }

    /// <summary>
    /// Opciones del desplegable, con el valor actual del tema incluido si el catálogo no lo ofrece.
    ///
    /// El catálogo propone familias sueltas (<c>Segoe UI</c>, <c>Inter</c>…), pero un tema guarda la pila
    /// completa (<c>Segoe UI Variable Text, Segoe UI, sans-serif</c>). Sin añadirla, el desplegable aparecía
    /// <b>en blanco</b> porque ningún elemento casaba con el valor: el usuario no veía la tipografía activa y
    /// parecía que el tema no tuviera fuente. Se ofrece como primera opción, de modo que también se pueda
    /// volver a ella.
    /// </summary>
    private static IReadOnlyList<string> WithCurrentValue(IReadOnlyList<string> catalog, string? current)
    {
        if (string.IsNullOrWhiteSpace(current) ||
            catalog.Any(o => string.Equals(o, current, StringComparison.Ordinal)))
        {
            return catalog;
        }

        List<string> options = [current];
        options.AddRange(catalog);
        return options;
    }

    public string SelectedOption
    {
        get => Read<string>() ?? string.Empty;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || value == SelectedOption)
            {
                return;
            }

            Write(value);
        }
    }

    protected override void OnThemeValueRead() => OnPropertyChanged(nameof(SelectedOption));
}
