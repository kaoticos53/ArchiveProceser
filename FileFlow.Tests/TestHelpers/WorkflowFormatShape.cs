using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FileFlow.Core.Engine;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// La <b>forma</b> de un archivo de flujo: qué campos escribe y de qué tipo, como lista canónica y ordenada
/// de senderos (<c>nodes[].customTitle : String</c>, <c>breakpointNodeIds[] : String</c>).
///
/// <para>
/// Es la materia prima de la prueba que ata el formato a su versión: si el modelo del flujo gana o pierde un
/// campo, la forma del archivo cambia, y una forma distinta para la misma versión es un archivo que las
/// versiones anteriores no pueden leer ni reparar. La forma se lee del <b>texto escrito</b> —pasando por
/// <c>WorkflowGraph.ToJson</c>, el escritor único del formato—, no de la reflexión sobre el modelo: lo que se
/// comprueba es lo que queda en el archivo.
/// </para>
///
/// <para>
/// Los <b>diccionarios</b> son la excepción que hace utilizable la medida: las claves de <c>parameters</c> las
/// pone el usuario (y la definición incrustada de un subflujo es un formato dentro del formato), así que no son
/// campos de éste. El sendero que se declara diccionario se registra como una sola entrada
/// (<c>nodes[].parameters.{*} : datos</c>) y su contenido no se recorre, de modo que la forma no depende de los
/// datos que lleve un flujo concreto —hay una prueba que lo fija—.
/// </para>
/// </summary>
public static class WorkflowFormatShape
{
    /// <summary>Clave comodín: el diccionario tiene claves, pero las pone quien guarda, no el formato.</summary>
    public const string DataKeys = "{*}";

    /// <summary>Tipo que se anota a un diccionario: su contenido es dato, no campo.</summary>
    public const string DataKind = "datos";

    /// <summary>Forma del archivo que produce el escritor único para ese grafo.</summary>
    public static IReadOnlyList<string> Describe(WorkflowGraph graph, params string[] dictionaries)
    {
        ArgumentNullException.ThrowIfNull(graph);

        return Describe(graph.ToJson(), dictionaries);
    }

    /// <summary>
    /// Forma del archivo que hay en ese JSON. <paramref name="dictionaries"/> son los senderos cuyo objeto es
    /// un diccionario de claves puestas por el usuario, con la misma notación que la salida
    /// (<c>nodes[].parameters</c>).
    /// </summary>
    public static IReadOnlyList<string> Describe(string json, params string[] dictionaries)
    {
        ArgumentNullException.ThrowIfNull(json);

        var dataPaths = new HashSet<string>(dictionaries ?? [], StringComparer.Ordinal);
        var entries = new SortedSet<string>(StringComparer.Ordinal);

        using var document = JsonDocument.Parse(json);
        Walk(document.RootElement, string.Empty, entries, dataPaths);

        return entries.ToList();
    }

    /// <summary>
    /// Lo que una forma tiene de más y de menos respecto de otra, en ese orden: lo que apareció —un campo
    /// nuevo, o el mismo campo con otro tipo— y lo que desapareció. Según qué se le pase, es «la forma viva
    /// contra la registrada» o al revés, así que el orden de los argumentos es el de la frase.
    /// </summary>
    public static ShapeDifference Compare(IEnumerable<string> recorded, IEnumerable<string> live)
    {
        ArgumentNullException.ThrowIfNull(recorded);
        ArgumentNullException.ThrowIfNull(live);

        var recordedSet = new HashSet<string>(recorded, StringComparer.Ordinal);
        var liveSet = new HashSet<string>(live, StringComparer.Ordinal);

        return new ShapeDifference(
            Added: live.Where(entry => !recordedSet.Contains(entry)).ToList(),
            Removed: recorded.Where(entry => !liveSet.Contains(entry)).ToList());
    }

    /// <summary>
    /// La forma escrita como la fila que hay que pegar en el registro de versiones, para que el fallo diga
    /// cómo se arregla en vez de sólo que está mal.
    /// </summary>
    public static string AsRecordLiteral(IEnumerable<string> shape)
    {
        ArgumentNullException.ThrowIfNull(shape);

        return string.Join(
            Environment.NewLine,
            shape.Select(entry => "            \"" + entry.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\","));
    }

    /// <summary>
    /// Recorre el JSON anotando lo que el archivo dice de sí mismo. Un objeto no se anota —lo representan sus
    /// campos—, salvo que sea un diccionario (una entrada comodín) o esté vacío (que también es una forma de
    /// no escribir ningún campo, y hay que poder distinguirla de no escribir el objeto).
    /// </summary>
    private static void Walk(JsonElement element, string path, ISet<string> entries, IReadOnlySet<string> dataPaths)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (dataPaths.Contains(path))
                {
                    entries.Add($"{path}.{DataKeys} : {DataKind}");
                    return;
                }

                bool any = false;

                foreach (var property in element.EnumerateObject())
                {
                    any = true;
                    Walk(property.Value, Join(path, property.Name), entries, dataPaths);
                }

                if (!any)
                {
                    entries.Add($"{path} : (objeto vacío)");
                }

                break;

            case JsonValueKind.Array:
                bool hasItems = false;

                foreach (var item in element.EnumerateArray())
                {
                    hasItems = true;

                    // Un elemento que a su vez es compuesto se recorre con el mismo sufijo: la forma no se
                    // anota por elemento —todos los nodos son del mismo tipo—, sino por lo que el archivo
                    // escribe en cada uno.
                    if (item.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    {
                        Walk(item, path + "[]", entries, dataPaths);
                    }
                    else
                    {
                        entries.Add($"{path}[] : {Kind(item.ValueKind)}");
                    }
                }

                if (!hasItems)
                {
                    entries.Add($"{path}[] : (lista vacía)");
                }

                break;

            default:
                entries.Add($"{path} : {Kind(element.ValueKind)}");
                break;
        }
    }

    private static string Join(string path, string name) =>
        path.Length == 0 ? name : $"{path}.{name}";

    /// <summary>Tipo JSON del valor, con los dos booleanos y la ausencia de valor bajo el mismo nombre.</summary>
    private static string Kind(JsonValueKind kind) => kind switch
    {
        JsonValueKind.String => "String",
        JsonValueKind.Number => "Number",
        JsonValueKind.True or JsonValueKind.False => "Boolean",
        JsonValueKind.Object => "(objeto)",
        JsonValueKind.Array => "(lista)",
        _ => "null"
    };
}

/// <summary>Lo que una forma tiene de más (<see cref="Added"/>) y de menos (<see cref="Removed"/>).</summary>
public sealed record ShapeDifference(IReadOnlyList<string> Added, IReadOnlyList<string> Removed)
{
    public bool IsEmpty => Added.Count == 0 && Removed.Count == 0;

    /// <summary>La diferencia contada para un mensaje de fallo: primero lo que apareció, luego lo que se fue.</summary>
    public override string ToString()
    {
        if (IsEmpty)
        {
            return "sin diferencias";
        }

        var lines = new List<string>();

        if (Added.Count > 0)
        {
            lines.Add("Campos que el archivo escribe ahora y antes no:");
            lines.AddRange(Added.Select(entry => "  + " + entry));
        }

        if (Removed.Count > 0)
        {
            lines.Add("Campos que el archivo escribía antes y ya no:");
            lines.AddRange(Removed.Select(entry => "  - " + entry));
        }

        return string.Join(Environment.NewLine, lines);
    }
}
