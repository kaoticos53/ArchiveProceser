using System;
using System.Collections.Generic;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

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
    string Icon = "🧩",
    bool IsFavorite = false,
    int UsageCount = 0,
    PipelineRole Role = PipelineRole.Transform,
    string[]? Tags = null,
    string SubCategory = "",
    string LocalizedRole = ""
)
{
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
    string Description
);

public class VariableGroupItem(string groupName)
{
    public string GroupName { get; set; } = groupName;
    public List<VariableItem> Variables { get; } = [];
}
