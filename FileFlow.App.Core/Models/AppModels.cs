using System;
using System.Collections.Generic;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using Material.Icons;

using CommunityToolkit.Mvvm.ComponentModel;

namespace FileFlow.App.Models;

public record LogEntry(
    DateTime Timestamp,
    LogLevel Level,
    string Message
);

/// <summary>
/// Opción de un desplegable: un valor al que atarse (<see cref="Code">) y un texto que mostrar
/// (<see cref="DisplayName">).
///
/// Es un objeto y no una cadena porque un <c>ComboBox</c> necesita ambos: cuando el valor viaja en la
/// etiqueta visible del elemento (un <c>ComboBoxItem</c> con <c>Tag</c>) hay que enlazar el contenedor
/// consigo mismo, y ese enlace no resuelve —el desplegable queda <b>en blanco</b> (nada seleccionado) y
/// elegir una opción no llega nunca al view model, porque el control escribe <c>null</c> de vuelta.
/// </summary>
public sealed record SelectorOption(string Code, string DisplayName)
{
    public override string ToString() => DisplayName;
}

/// <summary>Idiomas que ofrece la aplicación. Es la lista de la que se alimentan todos los selectores.</summary>
/// <remarks>El nombre de cada idioma se escribe en el propio idioma (endónimo), como es costumbre en un
/// selector de idioma.</remarks>
public static class LanguageCatalog
{
    public static IReadOnlyList<SelectorOption> All { get; } =
    [
        new("es-ES", "Español"),
        new("en-US", "English")
    ];

    /// <summary>
    /// Traduce la cultura guardada a una opción de la lista, o <c>null</c> si el idioma ya no se ofrece.
    ///
    /// Un selector atado por valor necesita un elemento con ese valor exacto: con una preferencia escrita
    /// como «es» o con una cultura que ya no está en la lista, el desplegable aparecería en blanco (y el
    /// propio control devolvería <c>null</c> al view model). La comparación cae al idioma de dos letras, que
    /// es lo que expone <c>LocalizationManager.CurrentLanguage</c>.
    /// </summary>
    public static SelectorOption? Resolve(string? storedCode)
    {
        if (string.IsNullOrWhiteSpace(storedCode))
        {
            return null;
        }

        string code = storedCode.Trim();

        var exact = All.FirstOrDefault(o => string.Equals(o.Code, code, StringComparison.OrdinalIgnoreCase));
        if (exact != null)
        {
            return exact;
        }

        string twoLetter = code.Split('-')[0];
        return All.FirstOrDefault(o => o.Code.StartsWith(twoLetter + "-", StringComparison.OrdinalIgnoreCase));
    }
}

public record NodeToolboxItem(
    string Name,
    string Category,
    string Description,
    string TypeName,
    MaterialIconKind Icon = MaterialIconKind.Puzzle,
    bool IsFavorite = false,
    int UsageCount = 0,
    PipelineRole Role = PipelineRole.Transform,
    string[]? Tags = null,
    string SubCategory = "",
    string LocalizedRole = ""
)
{
    public MaterialIconKind FavoriteIcon => IsFavorite ? MaterialIconKind.Star : MaterialIconKind.StarOutline;

    public string RoleBadge => Role switch
    {
        PipelineRole.Source => LocalizationManager.Instance.GetString("Role_Source", "Source"),
        PipelineRole.Filter => LocalizationManager.Instance.GetString("Role_Filter", "Filter"),
        PipelineRole.Transform => LocalizationManager.Instance.GetString("Role_Transform", "Transform"),
        PipelineRole.Analyze => LocalizationManager.Instance.GetString("Role_Analyze", "Analyze"),
        PipelineRole.Sink => LocalizationManager.Instance.GetString("Role_Sink", "Sink"),
        PipelineRole.Control => LocalizationManager.Instance.GetString("Role_Control", "Control"),
        _ => Role.ToString()
    };
}

public record VariableItem(
    string Name,
    string Token,
    string Description,
    string Category = "General",
    string SampleValue = "",
    bool IsUpstream = false,
    string SourceNodeTitle = ""
);

public class VariableGroupItem(string groupName, bool isUpstream = false)
{
    public string GroupName { get; set; } = groupName;
    public bool IsUpstream { get; set; } = isUpstream;
    public List<VariableItem> Variables { get; } = [];
}

public partial class FileVersionOption : ObservableObject
{
    public string Tag { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public MaterialIconKind Icon { get; init; } = MaterialIconKind.FileDocument;
    public string Description { get; init; } = string.Empty;
    public bool IsUpstream { get; init; }
    public string SourceNodeTitle { get; init; } = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    public string ChipLabel => DisplayName;

    public FileVersionOption() { }

    public FileVersionOption(
        string tag,
        string token,
        string displayName,
        MaterialIconKind icon = MaterialIconKind.FileDocument,
        string description = "",
        bool IsUpstream = false,
        string SourceNodeTitle = "",
        bool isSelected = false)
    {
        Tag = tag;
        Token = token;
        DisplayName = displayName;
        Icon = icon;
        Description = description;
        this.IsUpstream = IsUpstream;
        this.SourceNodeTitle = SourceNodeTitle;
        _isSelected = isSelected;
    }
}
