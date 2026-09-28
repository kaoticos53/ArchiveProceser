namespace FileFlow.Sdk;

/// <summary>
/// Interfaz opcional que implementan los nodos cuya configuración se edita en una <b>superficie modal</b>
/// propia (el Diseñador de Datasets Sintéticos, por ejemplo).
///
/// <para><b>Para qué existe.</b> Un nodo sabe qué superficie pide y qué contiene —su modelo de datos, su
/// lógica de plantilla, su almacén—, pero <b>no</b> sabe con qué toolkit se pinta: el nodo vive en un
/// ensamblado de plugin y lo consumen hosts distintos. Cuando el nodo construía la ventana él mismo
/// (<see cref="INodeCustomActionProvider.ExecuteCustomAction"/>), el resultado era una ventana que sólo el
/// host de ese toolkit podía montar: en cualquier otro host la entrada del menú quedaba en un no-op.</para>
///
/// <para><b>El contrato.</b> El nodo declara <b>qué</b> diálogo quiere —su clave de
/// <see cref="FileFlow.Sdk.Services.DialogKeys"/>, la misma para todos los hosts— y <b>qué contiene</b> (un
/// view model portable, sin tipos de UI). Cada host lo sirve a su manera: el que tiene el toolkit del plugin
/// monta la ventana del plugin; el que no, pinta su propia vista sobre el MISMO view model. La lógica no se
/// duplica: se declara una vez, donde vive.</para>
/// </summary>
public interface INodeDialogSurfaceProvider
{
    /// <summary>
    /// La clave canónica (<see cref="FileFlow.Sdk.Services.DialogKeys"/>) con la que los hosts sirven esta
    /// superficie. Todos los hosts la leen de aquí, así que la identidad del diálogo no depende del host.
    /// </summary>
    string DialogKey { get; }

    /// <summary>
    /// La acción personalizada (<see cref="INodeCustomActionProvider"/>) que esta superficie <b>sustituye</b> en
    /// los hosts que no pueden montar la ventana del nodo, si el nodo también la ofrece por ese camino.
    ///
    /// <para><b>Para qué existe.</b> La misma puerta del producto se alcanza por dos botones: el de la fila
    /// del parámetro (p. ej. el «🎬» de un preset) y el de la tarjeta del nodo (su acción personalizada). Los
    /// dos llaman a la MISMA acción por nombre (<c>ManageMediaPresets</c>), y ese nombre es el único hilo que
    /// une los dos botones con la superficie declarada: sin él, un host no puede saber qué botón de la tarjeta
    /// corresponde a esta superficie, y el otro botón seguiría abriendo la ventana del toolkit.</para>
    /// </summary>
    /// <returns>El identificador de la acción que sustituye, o <c>null</c> si el nodo no ofrece esa acción.</returns>
    string? ReplacesCustomActionId => null;

    /// <summary>
    /// Construye la carga útil de la superficie: el view model portable que el host pintará. Devolver
    /// <c>null</c> es una declaración explícita de que la superficie no se puede construir ahora (y quien la
    /// pidió debe decirlo con su motivo, no quedarse en silencio).
    /// </summary>
    /// <param name="context">
    /// El contexto de apertura, si lo hay (el mismo objeto que recibe
    /// <see cref="INodeCustomActionProvider.ExecuteCustomAction"/>).
    /// </param>
    object? CreateDialogPayload(object? context = null);
}
