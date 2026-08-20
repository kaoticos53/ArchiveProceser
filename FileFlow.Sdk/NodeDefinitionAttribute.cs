namespace FileFlow.Sdk;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class NodeDefinitionAttribute : Attribute
{
    public string Name { get; }
    public string Category { get; }
    public string Description { get; }

    public NodeDefinitionAttribute(string name, string category, string description)
    {
        Name = name;
        Category = category;
        Description = description;
    }
}
