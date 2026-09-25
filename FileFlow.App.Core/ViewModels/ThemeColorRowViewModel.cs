using FileFlow.App.Services;
using FileFlow.App.Themes;

namespace FileFlow.App.ViewModels;

/// <summary>Fila de color del Theme Studio: se edita con el selector de color de la aplicación.</summary>
public sealed class ThemeColorRowViewModel : ThemeSettingRowViewModel
{
    public ThemeColorRowViewModel(ThemeSettingDescriptor descriptor, ThemeDefinition theme)
        : base(descriptor, theme)
    {
        OnThemeValueRead();
    }

    /// <summary>Color del token en formato hex. Es el valor que consume <c>ColorPickerButton</c>.</summary>
    public string SelectedColorHex
    {
        get => Read<string>() ?? string.Empty;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || value == SelectedColorHex)
            {
                return;
            }

            Write(value);
        }
    }

    protected override void OnThemeValueRead() => OnPropertyChanged(nameof(SelectedColorHex));
}
