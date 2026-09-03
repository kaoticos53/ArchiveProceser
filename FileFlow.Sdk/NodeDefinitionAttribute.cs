using System;

namespace FileFlow.Sdk;

/// <summary>
/// Metadatos declarativos para el descubrimiento y catalogación automática de nodos de flujo.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class NodeDefinitionAttribute : Attribute
{
    public string Name { get; }
    public string Category { get; }
    public string Description { get; }
    public string SubCategory { get; init; } = string.Empty;
    public string[] Tags { get; init; } = [];
    public PipelineRole Role { get; init; } = PipelineRole.Transform;

    public NodeDefinitionAttribute(string name, string category, string description)
    {
        Name = name;
        Category = category;
        Description = description;
    }

    public NodeDefinitionAttribute(
        string name,
        string category,
        string description,
        PipelineRole role,
        params string[] tags)
    {
        Name = name;
        Category = category;
        Description = description;
        Role = role;
        Tags = tags ?? [];
    }
}
