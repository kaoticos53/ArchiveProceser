using FileFlow.Sdk;

namespace FileFlow.Core.Engine;

/// <summary>
/// Materializa en una instancia de nodo recién configurada los puertos que <b>no</b> se deducen de su
/// constructor, es decir, los que dependen de la configuración que se acaba de volcar en sus parámetros.
///
/// <para>
/// Hay dos familias, y ambas importan al reconstruir un grafo:
/// <list type="bullet">
///   <item>los que el nodo <b>materializa</b> en una operación propia y por eso necesitan que se les pida
///   la reevaluación (<see cref="IPortTopologyNode.RefreshPortTopology"/>, como los puertos declarados de
///   un script o los casos de un switch);</item>
///   <item>los de un <b>subflujo contenedor</b>, que no salen de un parámetro sino de la definición del
///   subgrafo y se descubren con <see cref="SubflowPortResolver"/>.</item>
/// </list>
/// </para>
///
/// <para>
/// Vive en Core y no en la interfaz porque no es una preocupación de la interfaz: la pregunta «¿qué puertos
/// expone esta instancia recién configurada?» la hacen el cargador de un flujo, el portapapeles y el
/// diagnóstico previo a la ejecución, y los tres tienen que responderla <b>igual</b>.
/// </para>
///
/// <para>
/// Al cargar un flujo o pegar nodos, las conexiones se reconstruyen emparejando <b>nombres de puerto</b>.
/// Si los puertos todavía no existen, ese emparejamiento no encuentra nada y el cable se pierde sin dejar
/// rastro: el flujo reabierto parece completo y ya no lo está. Por eso este paso va antes de reconstruir
/// conexiones, no después.
/// </para>
/// </summary>
public static class DynamicPortMaterializer
{
    /// <summary>
    /// Deja los puertos del nodo al día con sus parámetros actuales. Es idempotente y seguro de llamar
    /// sobre cualquier nodo: los de puertos fijos no cambian nada.
    /// </summary>
    public static void Materialize(IFlowNode instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (instance is ISubflowNode subflowNode)
        {
            SubflowPortResolver.Materialize(subflowNode);
        }

        (instance as IPortTopologyNode)?.RefreshPortTopology();
    }
}
