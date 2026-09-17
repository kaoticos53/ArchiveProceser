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
