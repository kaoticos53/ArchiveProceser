namespace FileFlow.Sdk.Renaming;

/// <summary>
/// Representa un patrón de expresión regular predefinido o guardado por el usuario con metadatos descriptivos.
/// </summary>
public sealed class RegexPatternItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string Pattern { get; set; } = string.Empty;
    public string Replacement { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SampleInput { get; set; } = string.Empty;
    public bool IsBuiltIn { get; set; } = false;

    public RegexPatternItem Clone()
    {
        return new RegexPatternItem
        {
            Id = this.Id,
            Name = this.Name,
            Category = this.Category,
            Pattern = this.Pattern,
            Replacement = this.Replacement,
            Description = this.Description,
            SampleInput = this.SampleInput,
            IsBuiltIn = this.IsBuiltIn
        };
    }
}
