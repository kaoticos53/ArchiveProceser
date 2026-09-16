using System;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using FileFlow.App.Services;
using FileFlow.App.Themes;
using FileFlow.Sdk.Localization;

namespace FileFlow.App.ViewModels;

/// <summary>
/// Fila del editor del Theme Studio: enlaza un ajuste del catálogo con la propiedad real de
/// <see cref="ThemeDefinition"/> que le corresponde.
///
/// La lectura y escritura se hacen por reflexión sobre el nombre declarado en el catálogo, de modo que la
/// cobertura de ajustes (¿está todo expuesto?) se pueda comprobar automáticamente en lugar de mantener a
/// mano una lista de setters. Cada cambio avisa al view model del Studio, que regenera la vista previa.
/// </summary>
public abstract class ThemeSettingRowViewModel : ObservableObject
{
    private readonly PropertyInfo _property;

    protected ThemeSettingRowViewModel(ThemeSettingDescriptor descriptor, ThemeDefinition theme)
    {
        Descriptor = descriptor;
        Theme = theme;

        _property = typeof(ThemeDefinition).GetProperty(descriptor.Property)
            ?? throw new InvalidOperationException(
                $"El catálogo del Theme Studio declara '{descriptor.Property}', que no existe en ThemeDefinition.");
    }

    /// <summary>Tema en edición; las filas escriben directamente sobre él.</summary>
    protected ThemeDefinition Theme { get; }

    public ThemeSettingDescriptor Descriptor { get; }

    /// <summary>Nombre de la propiedad del tema que edita esta fila.</summary>
    public string Property => Descriptor.Property;

    /// <summary>Rótulo localizado de la fila.</summary>
    public string Label => LocalizationManager.Instance.GetString(Descriptor.LabelKey, Descriptor.Property);

    /// <summary>Se dispara con cada cambio de valor, para que el Studio refresque la vista previa.</summary>
    public event Action? ValueChanged;

    /// <summary>Valor actual del tema, para diagnósticos y pruebas.</summary>
    public object? CurrentValue => _property.GetValue(Theme);

    /// <summary>Lee el valor del tema convirtiéndolo al tipo que espera la fila.</summary>
    protected T? Read<T>() => _property.GetValue(Theme) is T value ? value : default;

    /// <summary>Vuelve a leer el valor desde el tema (al cambiar de tema seleccionado).</summary>
    public void RefreshFromTheme() => OnThemeValueRead();

    /// <summary>Relee el valor del tema y notifica a la vista (implementado por cada tipo de fila).</summary>
    protected abstract void OnThemeValueRead();

    /// <summary>Escribe el valor en el tema y notifica al Studio.</summary>
    protected void Write(object? value)
    {
        _property.SetValue(Theme, value);
        OnThemeValueRead();
        ValueChanged?.Invoke();
    }
}
