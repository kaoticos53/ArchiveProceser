using FileFlow.Sdk.Localization;

namespace FileFlow.App.Services;

/// <summary>
/// Cómo se cuenta un cable que no se pudo reconstruir. Un solo sitio para las dos superficies que lo cuentan
/// —la consola al abrir un archivo, el aviso del lienzo al pegar—, porque el mismo problema dicho de dos
/// maneras distintas es dos problemas para quien lo lee.
///
/// El texto se construye con el servicio de localización y el idioma del catálogo, y cada frase lleva lo que
/// hace falta para volver a conectarlo: <b>quién</b> era cada extremo, <b>qué</b> puerto se nombraba y
/// <b>por qué</b> no se pudo.
/// </summary>
public static class DroppedConnectionText
{
    /// <summary>Los dos extremos de una conexión, tal y como se nombraban: <c>Origen(Puerto) → Destino(Puerto)</c>.</summary>
    public static string DescribeEndpoints(DroppedConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        return string.Format(
            "{0}({1}) → {2}({3})",
            connection.Source.NodeName, connection.Source.PortName,
            connection.Target.NodeName, connection.Target.PortName);
    }

    /// <summary>
    /// Por qué no se pudo reconstruir la conexión, <b>una frase por extremo</b>: una arista que falla por sus
    /// dos lados son dos nodos los que hay que arreglar, y contarlos como uno esconde la mitad del trabajo.
    /// </summary>
    public static IReadOnlyList<string> DescribeImpediments(ILocalizationService localization, DroppedConnection connection)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(connection);

        return [.. connection.Impediments.Select(impediment => DescribeImpediment(localization, impediment))];
    }

    /// <summary>Por qué un extremo no pudo conectarse, dicho para que se pueda arreglar.</summary>
    public static string DescribeImpediment(ILocalizationService localization, DroppedConnectionEnd impediment)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(impediment);

        return impediment.Problem switch
        {
            DroppedConnectionEndProblem.MissingNode => localization.GetFormattedString(
                "LogDroppedConnectionMissingNode",
                "el nodo '{0}' no está disponible (puede faltar el plugin que lo aporta)",
                impediment.NodeName),
            _ => localization.GetFormattedString(
                "LogDroppedConnectionMissingPort",
                "el nodo '{0}' ya no expone el puerto '{1}'",
                impediment.NodeName,
                impediment.PortName)
        };
    }

    /// <summary>
    /// El nodo al que un registro de la consola puede llevar a quien lo lea: el identificador y el nombre con
    /// el que el nodo existe <b>en el lienzo</b>, que es lo que el inspector necesita para abrir el nodo de la
    /// fila.
    ///
    /// De un extremo cuyo nodo <b>no</b> llegó a crearse se devuelve <c>null</c>, y no es un descuido: ese
    /// identificador no está en el lienzo, así que llevarlo en el registro sería mandar al inspector a un nodo
    /// que no es —o a ninguno—, y el nombre que sí se tiene lo dice ya la frase del motivo, que es donde se
    /// busca qué plugin falta.
    /// </summary>
    public static (string NodeId, string NodeName)? NodeToPointAt(DroppedConnectionEnd end) =>
        end.Problem == DroppedConnectionEndProblem.MissingNode ? null : (end.NodeId, end.NodeName);

    /// <summary>
    /// Una conexión perdida contada en una frase: los extremos, y por qué falló cada uno de los que fallaron.
    /// </summary>
    public static string Describe(ILocalizationService localization, DroppedConnection connection)
    {
        string endpoints = DescribeEndpoints(connection);
        var impediments = DescribeImpediments(localization, connection);

        return $"{endpoints}: {string.Join("; ", impediments)}";
    }
}
