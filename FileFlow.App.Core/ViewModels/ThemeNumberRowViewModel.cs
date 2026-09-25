using System;
using FileFlow.App.Services;
using FileFlow.App.Themes;

namespace FileFlow.App.ViewModels;

/// <summary>
/// Fila numérica del Theme Studio (radios, tamaños de fuente, densidad, sombras).
/// Expone el valor como <see cref="decimal"/> porque es el tipo que consume <c>NumericUpDown</c>.
/// </summary>
public sealed class ThemeNumberRowViewModel : ThemeSettingRowViewModel
{
    public ThemeNumberRowViewModel(ThemeSettingDescriptor descriptor, ThemeDefinition theme)
        : base(descriptor, theme)
    {
        OnThemeValueRead();
    }

    public decimal Minimum => (decimal)Descriptor.Minimum;

    public decimal Maximum => (decimal)Descriptor.Maximum;

    public decimal Step => (decimal)Descriptor.Step;

    public decimal Value
    {
        get
        {
            double current = Read<double>();
            return (decimal)Math.Round(current, 3);
        }
        set
        {
            double clamped = Math.Clamp((double)value, Descriptor.Minimum, Descriptor.Maximum);
            if (Math.Abs(clamped - Read<double>()) < 0.0001)
            {
                return;
            }

            Write(clamped);
        }
    }

    /// <summary>Valor numérico actual, sin la conversión a decimal (para diagnósticos y pruebas).</summary>
    public double NumericValue => Read<double>();

    protected override void OnThemeValueRead()
    {
        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(NumericValue));
    }
}
