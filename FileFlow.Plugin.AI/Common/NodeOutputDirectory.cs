using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Storage;

namespace FileFlow.Plugin.AI;

/// <summary>
/// <b>Dónde escribe un nodo de IA cuando el flujo no le da otra carpeta.</b> La regla, escrita una vez para los
/// cuatro nodos del plugin que la comparten (síntesis de voz, detección de voz, anonimizador y transcripción con
/// subtítulos), y con su contrato a la vista:
///
/// <list type="number">
/// <item>Con carpeta declarada —y distinta del valor de fábrica, que es la propia variable— manda lo declarado,
/// anclado por <see cref="ParameterHelper.ResolveOutputPath"/>: una ruta relativa cae dentro de lo que el flujo
/// declaró como salida y <b>nunca</b> donde corra el proceso.</item>
/// <item>Vacío —o el valor de fábrica— significa <b>la carpeta de salida del flujo</b>, que es lo que resuelve
/// <see cref="ParameterHelper.FlowOutputFolder"/>: la que el flujo declara y, si no declara ninguna, la de los
/// ajustes.</item>
/// <item>Sin ninguna de las dos, la carpeta del propio archivo, y sólo en último extremo el directorio temporal
/// del producto. El <b>directorio de trabajo del proceso</b> no aparece: leer la carpeta del flujo de la metadata
/// sin resolverla —lo que hacían estos nodos— dejaba el archivo donde corría la aplicación, y eso es la forma del
/// defecto del hito 204 (hitos 209 y 210).</item>
/// </list>
/// </summary>
internal static class NodeOutputDirectory
{
    /// <summary>
    /// Resuelve la carpeta de salida de un nodo que declara la suya en <paramref name="declaredDirectory"/> y cuyo
    /// valor de fábrica es <c>{GlobalOutputDir}</c>.
    /// </summary>
    internal static string For(string? declaredDirectory, FileItemContext item)
    {
        if (!string.IsNullOrWhiteSpace(declaredDirectory) &&
            !string.Equals(declaredDirectory, "{GlobalOutputDir}", StringComparison.OrdinalIgnoreCase))
        {
            return ParameterHelper.ResolveOutputPath(declaredDirectory, item);
        }

        string? flowFolder = ParameterHelper.FlowOutputFolder(item);
        if (!string.IsNullOrWhiteSpace(flowFolder))
        {
            return flowFolder;
        }

        // La carpeta del archivo, si el elemento trae una: `Path.GetDirectoryName` devuelve cadena VACÍA para un
        // nombre suelto, así que aquí no vale encadenar con `??` —el último escalón no llegaría a dispararse y el
        // nodo escribiría en la ruta vacía—.
        string? itemFolder = Path.GetDirectoryName(item.CurrentPath);
        return string.IsNullOrWhiteSpace(itemFolder) ? AppPaths.DefaultTempDirectory : itemFolder;
    }
}
