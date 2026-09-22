using System.IO;
using System.Text.Json;
using FileFlow.Core.Engine;

namespace FileFlow.App.Services;

/// <summary>
/// Servicio de persistencia y serialización asíncrona de grafos de flujo en formato JSON.
/// </summary>
public class WorkflowStorageService : IWorkflowStorageService
{
    // Cómo se escribe y se lee un flujo lo dice Core, y se nombra en cada llamada
    // (`WorkflowGraph.SerializationOptions`) a propósito: un alias local con el mismo valor vuelve a ser un
    // sitio donde elegir, y este servicio ya tuvo las suyas —los nombres tal cual, sin política— mientras Core
    // escribía en camelCase, así que el mismo grafo guardado por los dos caminos daba dos textos distintos.

    /// <summary>
    /// Guarda el grafo declarando la versión que escribe, y <b>sin sobrescribir un flujo que esta versión no
    /// entiende</b>: si el archivo de destino lo escribió una versión posterior del formato, el guardado falla y
    /// lo dice, porque escribir encima perdería los campos que esa versión añadió sin que nadie se entere. La
    /// salida honesta es guardar en otra ruta.
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// El archivo de destino declara un formato posterior al que esta versión escribe.
    /// </exception>
    public async ValueTask SaveWorkflowAsync(string filePath, WorkflowGraph graph, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(graph);

        // La comprobación mira el <b>destino</b> y no el grafo que se está guardando: guardar siempre pregunta
        // la ruta, así que lo que hay que proteger es el archivo concreto al que se va a escribir —y eso cubre
        // también el caso de un archivo que cambió mientras la aplicación estaba abierta—.
        string? targetSchema = File.Exists(filePath) ? DeclaredSchemaOf(filePath) : null;
        if (WorkflowFormat.VersionOf(targetSchema) > WorkflowFormat.CurrentVersion)
        {
            throw new InvalidDataException(string.Format(
                "El archivo '{0}' lo escribió una versión más nueva de FileFlow (formato {1}); guárdalo en otra ruta para no perderlo.",
                filePath,
                targetSchema));
        }

        // El escritor declara la versión que escribe, por la misma puerta que el otro escritor: un archivo sin
        // versión se lee como anterior al versionado y se le aplican reparaciones pensadas para archivos que ya
        // no se producen.
        WorkflowFormat.DeclareCurrent(graph);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempPath = filePath + ".tmp_" + Guid.NewGuid().ToString("N");
        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, graph, WorkflowGraph.SerializationOptions, ct).ConfigureAwait(false);
            }
            File.Move(tempPath, filePath, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // Silencioso para no enmascarar excepciones previas
            }
        }
    }

    public async ValueTask<WorkflowGraph> LoadWorkflowAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"El archivo de flujo no existe: {filePath}", filePath);
        }

        await using var stream = File.OpenRead(filePath);
        var graph = await JsonSerializer.DeserializeAsync<WorkflowGraph>(stream, WorkflowGraph.SerializationOptions, ct).ConfigureAwait(false);

        return graph ?? throw new InvalidDataException("El archivo de flujo no contiene un grafo válido.");
    }

    public string SerializeGraph(WorkflowGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        WorkflowFormat.DeclareCurrent(graph);
        return JsonSerializer.Serialize(graph, WorkflowGraph.SerializationOptions);
    }

    public WorkflowGraph DeserializeGraph(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var graph = JsonSerializer.Deserialize<WorkflowGraph>(json, WorkflowGraph.SerializationOptions);
        return graph ?? throw new InvalidDataException("El contenido JSON no contiene un grafo válido.");
    }

    /// <summary>
    /// Valor de <c>schema</c> que declara el archivo de destino, leído del JSON sin interpretar el resto: un
    /// flujo de un formato posterior puede traer formas que este modelo no sabe enlazar, y ese es justo el
    /// archivo que no se puede sobrescribir.
    ///
    /// Un archivo ilegible o que no declara ninguna versión devuelve <c>null</c>: no se le puede atribuir una
    /// versión que no dice, y el guardado sigue como siempre —reparar un archivo corrupto no puede quedar
    /// bloqueado por una protección pensada para archivos vivos—.
    /// </summary>
    private static string? DeclaredSchemaOf(string filePath)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(filePath));
            return WorkflowFormat.DeclaredSchema(document.RootElement);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or NotSupportedException)
        {
            // No se puede leer lo que hay: no hay nada que proteger que se pueda leer.
            return null;
        }
    }
}
