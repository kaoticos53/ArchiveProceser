using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using FileFlow.Sdk.Serialization;

namespace FileFlow.Core.Engine;

public class WorkflowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string NodeTypeName { get; set; } = string.Empty;
    public string? CustomTitle { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public bool HasBreakpoint { get; set; }
    public bool IsLoggingEnabled { get; set; } = true;
    public Dictionary<string, object?> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class WorkflowEdge
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SourceNodeId { get; set; } = string.Empty;
    public string SourcePortName { get; set; } = string.Empty;
    public string TargetNodeId { get; set; } = string.Empty;
    public string TargetPortName { get; set; } = string.Empty;
}

public class WorkflowAnnotation
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = "Nota";
    public string Content { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 240;
    public double Height { get; set; } = 180;
    public string Color { get; set; } = "#FEF08A";
}

public class WorkflowGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = "Grupo";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 400;
    public double Height { get; set; } = 300;
    public string Color { get; set; } = "#3B82F6";
    public List<string> NodeIds { get; set; } = [];
}

public class WorkflowGraph
{
    /// <summary>
    /// Versión del formato con la que está escrito el grafo. <c>null</c> significa «sin declarar» y no es
    /// lo mismo que un grafo recién construido en memoria: declara la versión el que <b>escribe</b> el
    /// archivo. Ver <see cref="WorkflowFormat"/>.
    /// </summary>
    public string? Schema { get; set; }

    public string Name { get; set; } = "Untitled Workflow";
    public string GlobalOutputDir { get; set; } = string.Empty;
    public string TemporaryDirectory { get; set; } = string.Empty;
    public List<WorkflowNode> Nodes { get; set; } = [];
    public List<WorkflowEdge> Edges { get; set; } = [];
    public List<WorkflowAnnotation> Annotations { get; set; } = [];
    public List<WorkflowGroup> Groups { get; set; } = [];
    public HashSet<string> BreakpointNodeIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> DisabledLoggingNodeIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Cómo se escribe y se lee un flujo. El lector es <b>tolerante</b> a propósito, y no por elegancia: un
    /// lector que exige el nombre exacto de la propiedad no falla, devuelve un grafo <b>vacío</b>, y la app
    /// guardaba los flujos con los nombres tal cual —no en camelCase—, así que ejecutar desde el CLI un flujo
    /// guardado desde la app era ejecutar nada. Los nombres se aceptan en cualquier caja y los valores se
    /// infieren igual que allí —texto, número, booleano— para que el mismo archivo dé el mismo grafo por los
    /// dos caminos.
    ///
    /// <para>
    /// Es la definición <b>única</b> del formato, y ahí está el cambio de la fase 3E: el lector tolerante hizo
    /// que los dos escritores —el servicio de guardado de la app y éste— se entendieran, y eso bastaba para
    /// leer, pero cada uno seguía escribiendo <b>su</b> dialecto (uno los nombres tal cual, el otro en
    /// camelCase), así que el mismo grafo guardado por los dos caminos daba dos textos distintos y el
    /// <c>schema</c> decía <c>v2</c> en los dos. Compartir la <b>instancia</b> y no una copia de los valores es
    /// lo que impide que vuelvan a separarse: no hay dos sitios donde elegir la política de nombres.
    /// </para>
    ///
    /// <para>
    /// Los nombres de <c>Parameters</c> no los toca la política de nombres —es de propiedades, y las claves de
    /// un diccionario llevan la suya aparte, que no se usa—, y eso importa: los parámetros se guardan tal y
    /// como los nombra el nodo, con sus mayúsculas. De una <b>propiedad</b> nula no se escribe nada, porque un
    /// campo ausente y uno nulo se leen igual y escribir los dos es escribir lo que nadie va a distinguir; un
    /// valor nulo <b>dentro</b> de un diccionario sí se escribe —la condición es de propiedades—, así que
    /// ningún parámetro se pierde por esta opción.
    /// </para>
    /// </summary>
    public static JsonSerializerOptions SerializationOptions { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new ObjectToInferredTypesConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

        // Los acentos y los símbolos se escriben tal cual, como en el resto del JSON del producto
        // (<see cref="JsonDefaults"/>): el valor por defecto de <c>System.Text.Json</c> los escapa —una ruta
        // con eñe se guarda como <c>\u00F1</c>— y un flujo es un archivo que el usuario abre y lee, con sus
        // rutas y sus títulos en su idioma. No cambia el documento, sólo cómo se escribe.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Serializa declarando la versión que escribe. No es un adorno: un archivo sin versión se lee como
    /// anterior al versionado y se le aplican reparaciones, así que omitir el campo haría que cada flujo
    /// guardado llevara puestas las suposiciones de un formato que ya no es el suyo.
    /// </summary>
    public string ToJson()
    {
        WorkflowFormat.DeclareCurrent(this);
        return JsonSerializer.Serialize(this, SerializationOptions);
    }

    public static WorkflowGraph FromJson(string json) =>
        JsonSerializer.Deserialize<WorkflowGraph>(json, SerializationOptions) ?? new WorkflowGraph();
}
