using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileFlow.App.Services;
using FileFlow.Core.Engine;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// El <b>testigo</b> de una versión del formato: un archivo de flujo <b>de verdad</b>, guardado por el
/// escritor de esa versión y comprometido en el repositorio —uno por versión, en
/// <c>FileFlow.Tests/FormatBaselines</c>—.
///
/// <para>
/// Por qué hace falta: el registro de formas dice qué escribe cada versión, pero hasta ahora lo único que lo
/// atestiguaba era el propio registro, y reescribir a mano la fila de una versión entregada lo dejaba en
/// verde. Un archivo escrito por esa versión es lo que convierte la fila en una afirmación sobre algo que
/// existe: si la fila cambia y el archivo no, la prueba lo dice nombrando el archivo. Y hay una segunda
/// ganancia que no era el objetivo: el testigo es también la muestra con la que se mide la forma, así que
/// <b>sólo hay un flujo de referencia</b> —si fueran dos, un campo nuevo habría que añadirlo en los dos y no
/// habría forma de saber si se hizo en el que importa—.
/// </para>
///
/// <para>
/// La política de escritura es lo que distingue un testigo de una línea base visual. Una captura se regenera
/// cuando el aspecto cambia y eso es aceptar el cambio; un testigo <b>no se regenera nunca</b>, porque el
/// escritor que lo escribió ya no existe: reescribirlo con el de hoy lo convertiría en un archivo que miente
/// sobre su versión. Sólo hay una escritura legítima —la del testigo de la versión que aún no tiene uno, y
/// sólo mientras es la versión que se escribe—, y la prueba que la provoca <b>falla a propósito</b> para que
/// el archivo nuevo pase por revisión en vez de quedar bendecido solo.
/// </para>
/// </summary>
public static class FlowFormatWitness
{
    /// <summary>Dónde viven los testigos, junto a las líneas base visuales.</summary>
    public static readonly string DirectoryPath =
        Path.Combine(TestRepositoryLocator.RepositoryRoot(), "FileFlow.Tests", "FormatBaselines");

    private const string FilePrefix = "flow-format-v";

    /// <summary>Ruta del testigo de esa versión.</summary>
    public static string PathOf(int version) =>
        Path.Combine(DirectoryPath, $"{FilePrefix}{version}.json");

    /// <summary>
    /// Los archivos que hay en el directorio de testigos, con la versión que anuncia su <b>nombre</b> (el
    /// contenido se comprueba aparte, y tiene que decir lo mismo). Un archivo cuyo nombre no sea el de un
    /// testigo se devuelve con versión <c>0</c>: no es el testigo de nada.
    /// </summary>
    public static IReadOnlyList<(string Path, int Version)> Documents()
    {
        if (!Directory.Exists(DirectoryPath))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(DirectoryPath, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => (path, VersionOf(path)))
            .ToList();
    }

    /// <summary>Versiones que tienen testigo comprometido.</summary>
    public static IReadOnlyList<int> VersionsPresent() =>
        Documents().Where(document => document.Version > 0).Select(document => document.Version).ToList();

    /// <summary>
    /// El flujo que contienen todos los testigos: un flujo con algo de todo —varios nodos, un parámetro de
    /// cada tipo inferido y uno nulo, una arista, una nota, un grupo, un punto de interrupción y un nodo sin
    /// registro— para que el archivo escriba <b>todos</b> los campos del formato. Es el mismo grafo con el que
    /// se mide la forma, para que no haya dos muestras que mantener.
    /// </summary>
    public static WorkflowGraph Flow() => new()
    {
        // Sin `schema`: lo declara el escritor, que es lo que hace el producto. Un testigo no se fabrica
        // declarando su versión a mano —eso sería escribirlo con las instrucciones en vez de con el escritor—.
        Name = "Flujo de muestra del formato",
        GlobalOutputDir = @"C:\Salida",
        TemporaryDirectory = @"C:\Temporal",
        Nodes =
        [
            new WorkflowNode
            {
                Id = "nodo-1",
                NodeTypeName = "FileCopy",
                CustomTitle = "Copia los recibos",
                X = 120.5,
                Y = 80.25,
                HasBreakpoint = true,
                IsLoggingEnabled = true,
                Parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    // Los cuatro tipos que el lector infiere, y el nulo: un valor nulo dentro del diccionario
                    // sí se escribe —es una propiedad la que se omite cuando es nula, no una entrada—, y el
                    // testigo lo fija.
                    ["RutaOrigen"] = @"C:\Entrada",
                    ["Reintentos"] = 3,
                    ["Sobrescribir"] = true,
                    ["Etiqueta"] = null
                }
            },
            new WorkflowNode
            {
                Id = "nodo-2",
                NodeTypeName = "MoveFile",
                CustomTitle = "Mueve a Archivo",
                X = 420.5,
                Y = 80.25,
                HasBreakpoint = false,
                IsLoggingEnabled = false,
                Parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Destino"] = @"C:\Archivo"
                }
            }
        ],
        Edges =
        [
            new WorkflowEdge
            {
                Id = "arista-1",
                SourceNodeId = "nodo-1",
                SourcePortName = "Done",
                TargetNodeId = "nodo-2",
                TargetPortName = "In"
            }
        ],
        Annotations =
        [
            new WorkflowAnnotation
            {
                Id = "nota-1",
                Title = "Recuerda",
                Content = "Este flujo es el que se guarda como testigo de la versión del formato.",
                X = 60.5,
                Y = 220.75,
                Width = 280.5,
                Height = 160.25,
                Color = "#FEF08A"
            }
        ],
        Groups =
        [
            new WorkflowGroup
            {
                Id = "grupo-1",
                Title = "Entrada y archivo",
                X = 80.25,
                Y = 40.5,
                Width = 520.75,
                Height = 220.5,
                Color = "#3B82F6",
                NodeIds = ["nodo-1", "nodo-2"]
            }
        ],
        BreakpointNodeIds = ["nodo-1"],
        DisabledLoggingNodeIds = ["nodo-2"]
    };

    /// <summary>
    /// El texto que el producto escribe hoy para ese flujo, por el <b>camino de guardado de la aplicación</b>:
    /// el testigo tiene que ser lo que la app habría dejado en disco, no una serialización de conveniencia.
    /// </summary>
    public static string ProducedText() => new WorkflowStorageService().SerializeGraph(Flow());

    /// <summary>
    /// El texto del testigo de esa versión. Si no existe y la versión es la que se escribe, <b>se escribe
    /// ahora</b> y el método lanza: el archivo nuevo tiene que pasar por revisión humana —es un archivo que el
    /// producto abre— y la prueba que lo provocó vuelve a ejecutarse después.
    /// </summary>
    public static string Text(int version)
    {
        string path = PathOf(version);
        bool exists = File.Exists(path);

        switch (WriteDecision(version, exists, WorkflowFormat.CurrentVersion))
        {
            case WitnessWrite.Leave:
                return File.ReadAllText(path);

            case WitnessWrite.Create:
                System.IO.Directory.CreateDirectory(DirectoryPath);
                File.WriteAllText(path, ProducedText());
                throw new InvalidOperationException(
                    $"No existía el testigo de la v{version} y se acaba de escribir en '{path}' con el escritor " +
                    "de esta versión. Revísalo —es un archivo de flujo que el producto abre— y vuelve a ejecutar.");

            default:
                throw new InvalidOperationException(WhyNotWrite(version, exists, WorkflowFormat.CurrentVersion));
        }
    }

    /// <summary>
    /// Qué se puede hacer con el testigo de una versión: dejarlo como está, escribirlo porque no hay ninguno
    /// y es la versión que se escribe, o negarse. Las cuatro reglas, en orden:
    /// <list type="number">
    ///   <item>Una versión que no existe no tiene escritor: no hay testigo que fabricar.</item>
    ///   <item>Una versión que ya no es la actual está <b>entregada</b>: su testigo se conserva tal cual, exista
    ///   o falte —si falta, es un hueco que se explica, no un archivo que se rellena con el escritor de hoy—.</item>
    ///   <item>La versión actual sin testigo: se escribe, y la prueba falla para que se revise.</item>
    ///   <item>La versión actual con testigo: se lee. No hay regeneración: regenerar es aceptar el cambio, y
    ///   aceptar un cambio de lo que se escribe es subir la versión.</item>
    /// </list>
    /// </summary>
    public static WitnessWrite WriteDecision(int version, bool exists, int currentVersion)
    {
        if (version > currentVersion)
        {
            return WitnessWrite.Refuse;
        }

        if (version != currentVersion)
        {
            return WitnessWrite.Refuse;
        }

        return exists ? WitnessWrite.Leave : WitnessWrite.Create;
    }

    /// <summary>Por qué no se puede escribir el testigo de esa versión, dicho para quien lo intentó.</summary>
    public static string WhyNotWrite(int version, bool exists, int currentVersion)
    {
        if (version > currentVersion)
        {
            return $"No existe un escritor de la v{version} —la versión que se escribe es la v{currentVersion}—, " +
                "así que no hay forma de fabricar su testigo: un testigo lo escribe el escritor de su versión.";
        }

        return $"El testigo de la v{version} no se toca: la versión que se escribe es la v{currentVersion}, así " +
            $"que la v{version} está entregada. " +
            (exists
                ? "Reescribirlo con el escritor de hoy lo convertiría en un archivo que miente sobre su versión: " +
                    "lo que la v" + version + " escribió se conserva, y un cambio de lo que se escribe merece una " +
                    "versión nueva con su testigo."
                : "Y si falta, es un hueco que hay que explicar —buscándolo donde estuviera—, no un archivo que " +
                    "se rellena con el escritor de hoy, que escribiría otra cosa.");
    }

    /// <summary>Versión que anuncia el nombre del archivo; <c>0</c> si no se llama como un testigo.</summary>
    private static int VersionOf(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);

        return name.StartsWith(FilePrefix, StringComparison.Ordinal)
            && int.TryParse(name[FilePrefix.Length..], out int version)
                ? version
                : 0;
    }
}

/// <summary>Qué hacer con el archivo testigo de una versión del formato.</summary>
public enum WitnessWrite
{
    /// <summary>Leerlo y compararlo: es el testigo de una versión entregada, o el de la actual ya escrito.</summary>
    Leave,

    /// <summary>No había testigo de la versión que se escribe: se escribe, y la prueba falla para que se revise.</summary>
    Create,

    /// <summary>Ni se lee ni se escribe: no hay escritor de esa versión, o su testigo está congelado.</summary>
    Refuse
}
