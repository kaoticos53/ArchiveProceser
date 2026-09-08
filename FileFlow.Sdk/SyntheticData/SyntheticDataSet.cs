namespace FileFlow.Sdk.SyntheticData;

/// <summary>
/// Representa un conjunto de datos ficticios reutilizable y personalizable, compuesto por múltiples archivos
/// y carpetas con metadatos y simulación jerárquica de árbol.
/// </summary>
public sealed class SyntheticDataSet
{
    /// <summary>
    /// Identificador único del dataset.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Nombre visible del dataset (ej: 'Biblioteca Series 4K', 'Facturación Fiscal 2024').
    /// </summary>
    public string Name { get; set; } = "Nuevo Dataset";

    /// <summary>
    /// Descripción opcional del propósito o contenido del dataset.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Categoría a la que pertenece (ej: 'Películas', 'Series', 'Música', 'Fotos', 'Documentos', 'General', 'Personalizada').
    /// </summary>
    public string Category { get; set; } = "General";

    /// <summary>
    /// Indica si es un dataset incorporado por defecto (solo lectura/oficial) o creado por el usuario.
    /// </summary>
    public bool IsBuiltIn { get; set; } = false;

    /// <summary>
    /// Fecha de creación.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Fecha de última modificación.
    /// </summary>
    public DateTime LastModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Colección de archivos y carpetas definidos en este dataset.
    /// </summary>
    public List<SyntheticFileDefinition> Items { get; set; } = [];

    /// <summary>
    /// Cantidad total de archivos (excluyendo carpetas puras).
    /// </summary>
    public int TotalFiles => Items.Count(i => !i.IsDirectory);

    /// <summary>
    /// Cantidad total de carpetas definidas explícitamente.
    /// </summary>
    public int TotalDirectories => Items.Count(i => i.IsDirectory);

    /// <summary>
    /// Tamaño total acumulado en bytes.
    /// </summary>
    public long TotalSizeBytes => Items.Where(i => !i.IsDirectory).Sum(i => i.FileSizeBytes);

    public SyntheticDataSet() { }

    public SyntheticDataSet(string name, string category = "General", string description = "")
    {
        Name = name;
        Category = category;
        Description = description;
    }

    /// <summary>
    /// Clona profundamente este dataset. Por defecto conserva el Id e IsBuiltIn salvo que generateNewId sea true.
    /// </summary>
    public SyntheticDataSet Clone(string? newName = null, bool generateNewId = false)
    {
        var clone = new SyntheticDataSet
        {
            Id = generateNewId ? Guid.NewGuid().ToString("N") : Id,
            Name = newName ?? (generateNewId ? $"{Name} (Copia)" : Name),
            Description = Description,
            Category = Category,
            IsBuiltIn = generateNewId ? false : IsBuiltIn,
            CreatedAt = CreatedAt,
            LastModifiedAt = LastModifiedAt,
            Items = Items.Select(i => i.Clone()).ToList()
        };

        return clone;
    }
}
