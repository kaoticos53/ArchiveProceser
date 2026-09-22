using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.Examples;

/// <summary>
/// Ejemplo completo de nodo personalizado para FileFlow Studio, en su forma vigente: hereda de
/// <see cref="FlowNodeBase"/>, que ya aporta la identidad, el diccionario de parámetros, los puertos y los
/// ayudantes de ejecución. El nodo, por tanto, no declara <c>Id</c>, <c>Parameters</c>, <c>Inputs</c> ni
/// <c>Outputs</c>: los especializa.
///
/// Demuestra lo que necesita un nodo real:
/// <list type="bullet">
///   <item>Metadatos traducibles (<c>Name</c>, <c>Category</c>, <c>Description</c>) con <c>override</c>, y el
///   atributo <see cref="NodeDefinitionAttribute"/> con su rol y sus palabras clave para el buscador.</item>
///   <item>Dos puertos de entrada y tres de salida asignados en el constructor: la vía de los puertos fijos,
///   que es la de la mayoría de los nodos del proyecto.</item>
///   <item>Parámetros con valores por defecto en el constructor y su esquema declarado en
///   <see cref="FlowNodeBase.ParameterDescriptors"/>, que es lo que ordena y describe los campos del
///   inspector.</item>
///   <item>Lecturas con <c>GetParameter&lt;T&gt;</c> y emisión con los ayudantes <c>Log</c> y
///   <c>EmitAsync</c> de la base, sin tocar el diccionario de parámetros ni el contexto a mano.</item>
/// </list>
/// </summary>
[NodeDefinition("SampleMultiPortNode_Name", "General", "SampleMultiPortNode_Desc", PipelineRole.Filter,
    "ejemplo", "filtro", "tamano", "extension", "multipuerto", "sample", "filter")]
public sealed class SampleMultiPortNode : FlowNodeBase
{
    /// <summary>Nombre visible del nodo (traducible y refrescable en caliente desde el propio SDK).</summary>
    public override string Name =>
        LocalizationManager.Instance.GetString("SampleMultiPortNode_Name", "Filtro Multi-Puerto de Archivos");

    /// <summary>Categoría del nodo dentro del catálogo lateral.</summary>
    public override string Category => "General";

    /// <summary>Descripción que se muestra en la tarjeta informativa del lienzo.</summary>
    public override string Description => LocalizationManager.Instance.GetString(
        "SampleMultiPortNode_Desc",
        "Clasifica los elementos recibidos por sus dos entradas según su tamaño y extensión hacia tres puertos de salida.");

    public SampleMultiPortNode()
    {
        // Puertos fijos: se declaran una vez, en el constructor. Un nodo cuyos puertos dependan de los
        // parámetros sobrescribe BuildInputPorts()/BuildOutputPorts() y anuncia el cambio (ver la guía).
        Inputs =
        [
            new NodePort("MainInput", typeof(FileItemContext), PortDirection.Input, "Entrada Principal"),
            new NodePort("SecondaryInput", typeof(FileItemContext), PortDirection.Input, "Entrada Secundaria")
        ];

        Outputs =
        [
            new NodePort("Approved", typeof(FileItemContext), PortDirection.Output, "Aprobados"),
            new NodePort("Rejected", typeof(FileItemContext), PortDirection.Output, "Rechazados"),
            new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
        ];

        Parameters["TargetExtension"] = ".zip";
        Parameters["MaxFileSizeMB"] = 25;
        Parameters["StrictValidation"] = true;
        Parameters["OutputDirectoryPath"] = string.Empty;
    }

    /// <summary>
    /// Esquema de los parámetros: orden en el inspector, editor adecuado para cada uno, límites y ayuda.
    ///
    /// Es opcional —sin descriptores, la interfaz crea los campos a partir del diccionario de parámetros—,
    /// pero es lo que permite ordenarlos, acotarlos y explicarlos en lugar de mostrarlos en el orden en que
    /// se escribieron.
    /// </summary>
    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("TargetExtension", ParameterEditorType.Text, DefaultValue: ".zip", DisplayOrder: 1,
            HelpText: "Extensión que se considera válida, con el punto incluido."),

        new("MaxFileSizeMB", ParameterEditorType.Number, DefaultValue: 25, DisplayOrder: 2,
            Min: 0, Max: 10240, Step: 1,
            HelpText: "Tamaño máximo admitido, en megabytes."),

        new("StrictValidation", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 3,
            HelpText: "Si está activo, un archivo que exceda el tamaño máximo se rechaza aunque su extensión coincida."),

        new("OutputDirectoryPath", ParameterEditorType.FilePath, DefaultValue: "", DisplayOrder: 4,
            HelpText: "Carpeta de destino de los archivos aprobados. El nodo no escribe nada: es un dato para los nodos posteriores del flujo.")
    ];

    /// <summary>
    /// Lógica del nodo. El motor la invoca una vez por cada elemento que llega a uno de sus puertos de
    /// entrada, y <paramref name="inputPortName"/> dice por cuál entró.
    /// </summary>
    public override async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Lectura tipada: entiende el valor de la interfaz, el JsonElement de un perfil guardado y los
        // números embebidos en texto, y fuera de rango devuelve el valor por defecto en lugar de desbordar.
        string targetExtension = GetParameter("TargetExtension", ".zip");
        int maxFileSizeMb = GetParameter("MaxFileSizeMB", 25);
        bool strictValidation = GetParameter("StrictValidation", true);

        string path = item.CurrentPath;

        if (string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path)))
        {
            Log(context, $"[SampleMultiPort] La ruta '{path}' no existe.", LogLevel.Warning, item);
            item.AddLog($"SampleMultiPortNode: ruta '{path}' no válida.");

            // El puerto de error permite manejar el fallo dentro del flujo en lugar de abortar el procesamiento.
            await EmitAsync(context, item, "Error");
            return;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool extensionMatches = Path.GetExtension(path).Equals(targetExtension, StringComparison.OrdinalIgnoreCase);
            bool sizeMatches = item.FileSizeBytes <= maxFileSizeMb * 1024L * 1024L;

            if (extensionMatches && (sizeMatches || !strictValidation))
            {
                item.Metadata["ProcessedBy"] = nameof(SampleMultiPortNode);
                item.Metadata["InputSourcePort"] = inputPortName;

                Log(context, $"[SampleMultiPort] '{path}' aprobado (entró por '{inputPortName}').", LogLevel.Information, item);
                await EmitAsync(context, item, "Approved");
                return;
            }

            string reason = extensionMatches ? "excede el tamaño máximo" : "extensión no admitida";
            item.Metadata["RejectionReason"] = reason;
            item.AddLog($"SampleMultiPortNode clasificó el elemento como rechazado ({reason}).");

            Log(context, $"[SampleMultiPort] '{path}' rechazado: {reason}.", LogLevel.Information, item);
            await EmitAsync(context, item, "Rejected");
        }
        catch (OperationCanceledException)
        {
            Log(context, "[SampleMultiPort] Ejecución cancelada por el usuario.", LogLevel.Warning, item);
            throw;
        }
        catch (Exception ex)
        {
            // Un error inesperado no debe tumbar el flujo: se registra y el elemento sale por 'Error'.
            Log(context, $"[SampleMultiPort] Error al procesar '{path}': {ex.Message}", LogLevel.Error, item);
            item.AddLog($"SampleMultiPortNode falló con error: {ex.Message}");
            await EmitAsync(context, item, "Error");
        }
    }
}
