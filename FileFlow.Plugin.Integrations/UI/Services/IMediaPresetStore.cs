using System.Collections.Generic;

namespace FileFlow.Plugin.Integrations.UI.Services;

/// <summary>
/// El ALMACÉN de presets de medios: el catálogo que el nodo de transcodificación lee para convertir, y el que
/// el gestor edita.
///
/// <para><b>Para qué existe un contrato y no sólo la clase.</b> El gestor tiene una regla que no se puede
/// equivocar —qué se guarda, qué no se puede borrar, qué se restablece— y esa regla vive en su view model
/// portable. Con el almacén detrás de un contrato, la regla se puede ejercer en una prueba con un almacén de
/// mentira, sin escribir en el fichero de presets del usuario ni depender de lo que ya tenga guardado.</para>
///
/// <para>No es una frontera para cambiar de tecnología: el único almacén real sigue siendo
/// <see cref="MediaPresetManagerService"/>, que escribe el JSON que lee el motor de transcodificación.</para>
/// </summary>
public interface IMediaPresetStore
{
    /// <summary>El catálogo completo, en su orden.</summary>
    IReadOnlyList<MediaPreset> GetPresets();

    /// <summary>Los nombres del catálogo (lo que el desplegable del nodo ofrece).</summary>
    List<string> GetPresetNames();

    /// <summary>El preset con ese nombre, o null.</summary>
    MediaPreset? GetPresetByName(string name);

    /// <summary>Guarda el preset: lo reemplaza si ya existe (por id o por nombre) y si no lo añade.</summary>
    void SavePreset(MediaPreset preset);

    /// <summary>Borra el preset. Devuelve false cuando no existe o es uno del sistema.</summary>
    bool DeletePreset(string presetId);

    /// <summary>Restablece el catálogo a los presets del sistema.</summary>
    void ResetToDefaults();
}
