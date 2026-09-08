using FileFlow.Sdk.SyntheticData;

namespace FileFlow.Plugin.FileSystem.Services;

/// <summary>
/// Contrato del servicio para la gestión, persistencia, importación y exportación de conjuntos de datos sintéticos.
/// </summary>
public interface ISyntheticDataSetStorageService
{
    /// <summary>
    /// Notifica cuando un dataset ha sido añadido, modificado, clonado o eliminado.
    /// </summary>
    event EventHandler? DataSetsChanged;

    /// <summary>
    /// Obtiene todos los datasets disponibles (tanto los incorporados por defecto como los personalizados del usuario).
    /// </summary>
    IReadOnlyList<SyntheticDataSet> GetAllDataSets();

    /// <summary>
    /// Busca un dataset por su identificador único.
    /// </summary>
    SyntheticDataSet? GetDataSetById(string id);

    /// <summary>
    /// Busca un dataset por su nombre legible.
    /// </summary>
    SyntheticDataSet? GetDataSetByName(string name);

    /// <summary>
    /// Guarda o actualiza un dataset en el almacenamiento del usuario.
    /// </summary>
    void SaveDataSet(SyntheticDataSet dataSet);

    /// <summary>
    /// Elimina un dataset personalizado del usuario. No permite eliminar datasets incorporados (BuiltIn).
    /// </summary>
    bool DeleteDataSet(string id);

    /// <summary>
    /// Clona un dataset existente creando una nueva copia editable por el usuario.
    /// </summary>
    SyntheticDataSet CloneDataSet(string sourceId, string newName);

    /// <summary>
    /// Exporta un dataset a una cadena en formato JSON formateado.
    /// </summary>
    string ExportDataSetToJson(SyntheticDataSet dataSet);

    /// <summary>
    /// Importa un dataset a partir de una cadena JSON y lo persiste opcionalmente.
    /// </summary>
    SyntheticDataSet ImportDataSetFromJson(string jsonContent, bool autoSave = true);
}
