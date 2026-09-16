using System.Collections.Generic;
using FileFlow.App.Services;
using FileFlow.App.Themes;

namespace FileFlow.App.ViewModels;

/// <summary>Fila de elección del Theme Studio (familias tipográficas de interfaz y de código).</summary>
public sealed class ThemeChoiceRowViewModel : ThemeSettingRowViewModel
{
    public ThemeChoiceRowViewModel(ThemeSettingDescriptor descriptor, ThemeDefinition theme)
        : base(descriptor, theme)
    {
        Options = descriptor.Options ?? [];
        OnThemeValueRead();
    }

    public IReadOnlyList<string> Options { get; }

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
