using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Colección de las clases que ejercitan el <b>portapapeles de nodos del lienzo</b>
/// (<see cref="NodeClipboardServiceTests"/>, <see cref="NodeTitleCustomizationTests"/>,
/// <see cref="ClipboardDroppedConnectionsTests"/>, <see cref="DynamicPortsOnReloadTests"/> y
/// <see cref="LostConnectionTracesTests"/>), marcada como <b>no paralizable</b>.
///
/// <para>Lo que confina es el <b>portapapeles del proceso</b>: <c>NodeClipboardService.Copy</c> escribe el
/// paquete vía <c>HostUi.SetClipboardText</c> —el singleton <c>NullClipboardService.Instance</c> cuando no
/// hay host, que es lo que corre en pruebas— y <c>Paste</c> lo <b>lee primero del portapapeles global</b>,
/// dejando la copia en memoria sólo como respaldo. Es estado global de proceso exactamente como el
/// directorio de trabajo del banco de ejemplos: dos pruebas paralelas que copien y peguen a la vez pueden
/// acabar pegando el paquete de la vecina —medido en suite completa (2026-09-25):
/// <c>NodeTitleCustomizationTests</c> esperaba pegar 1 nodo y encontró 2, que es el paquete de la prueba
/// vecina <c>MultipleConnectedNodes</c> de <c>NodeClipboardServiceTests</c>—. Con
/// <c>DisableParallelization</c> las cinco clases corren en exclusividad y el portapapeles ya no puede
/// cambiar de dueño a mitad de una prueba.</para>
///
/// <para>El contrato está en <c>TestAssemblyParallelism.cs</c> y su guardia en
/// <c>TestCollectionContractGuardTests</c> (los patrones que lo delatan son las llamadas
/// <c>Copy</c>/<c>Paste</c>/<c>CanPaste</c>/<c>Duplicate</c> del servicio y los comandos
/// <c>PasteNodes</c>/<c>DuplicateSelectedNodes</c> del editor).</para>
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class NodeClipboardCollection
{
    public const string Name = "NodeClipboard";
}
