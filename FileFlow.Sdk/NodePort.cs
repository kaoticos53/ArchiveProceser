namespace FileFlow.Sdk;

public enum PortDirection
{
    Input,
    Output
}

/// <summary>
/// Un puerto de un nodo: por dónde le entran y le salen los elementos.
/// </summary>
/// <param name="IsFeedbackSignal">
/// <b>Este puerto no recibe elementos «de delante»: recibe el aviso de que una rama que el propio nodo
/// bifurcó ha terminado.</b> La arista que lo alimenta <i>cierra</i> la barrera, así que no es una
/// restricción de precedencia y el orden topológico no debe contarla como tal.
///
/// <para>Sin esta distinción un fork/join no se puede ni escribir: el nodo bifurca por <c>Fork1</c>, la rama
/// trabaja, y su resultado vuelve a la entrada del mismo nodo —que para Kahn es un ciclo, y el motor lo
/// rechazaba con «Graph contains a cycle (DAG violation)»—. Los nodos barrera quedaban inutilizables: sus
/// puertos existían, el editor los dibujaba y el catálogo los anunciaba, pero ningún flujo que los usara
/// llegaba a ejecutarse (destapado al ejecutar los ejemplos 22 y 30, hito 204).</para>
/// </param>
public record NodePort(
    string Name,
    Type DataType,
    PortDirection Direction,
    string DisplayName,
    string Description = "",
    bool IsFeedbackSignal = false
);
