using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

/// <summary>
/// Colección de las clases que ejercitan el <b>proveedor de muestras del renombrador</b>
/// (<see cref="FileFlow.Plugin.FileSystem.UI.Services.RenamerSampleDataProvider"/>) y las mediciones de
/// <b>latencia con cronómetro</b>: <see cref="SyntheticDataSourceNodeTests"/> y
/// <see cref="FileFlow.Tests.Unit.App.AdvancedRenamerEditorViewModelTests"/>, marcada como
/// <b>no paralizable</b>. Confina dos cosas:
///
/// <para><b>1. El registro estático de muestras</b>: <c>RenamerSampleDataProvider</c> es una clase estática
/// del producto (plugin FileSystem) cuyo registro de muestras personalizadas es estado global del proceso;
/// el <c>Dispose</c> de las clases lo limpia, y una vecina paralela que registre muestras mientras otra
/// consulta mezclaría los catálogos.</para>
///
/// <para><b>2. La CPU que mide el cronómetro</b>: <c>SyntheticDataSourceNode_EmissionLatency_ShouldPaceEveryEmission</c>
/// afirma cotas <b>inferiores</b> de temporización con margen cero (los huecos entre emisiones deben durar al
/// menos los <c>EmissionDelayMs</c> declarados, medidos con <c>Stopwatch.GetElapsedTime</c>). Es la misma
/// tesis que <c>EngineFirstRunCollection</c>: no confina estado mutable, confina la CPU del proceso, que es
/// de todos — una colección vecina compitiendo por la CPU recorta el hueco medido por debajo del retardo que
/// el nodo promete. Medido en suite completa (2026-09-25): un hueco de 4,911 ms contra el umbral de 5 ms,
/// flake <c>EmissionLatency</c> que no se repite en solitario.</para>
///
/// <para>La colección existía desde el confinamiento del proveedor, pero <b>implícita</b>: sin clase de
/// definición, xUnit la paraleliza como cualquier otra. Esta definición le da la exclusividad que su
/// contenido ya pedía; los atributos <c>[Collection("RenamerSampleDataTests")]</c> de las clases miembro
/// no cambian.</para>
///
/// <para>El contrato está en <c>TestAssemblyParallelism.cs</c> y su guardia en
/// <c>TestCollectionContractGuardTests</c>: el patrón que delata la medición de cotas inferiores de
/// temporización es <c>Stopwatch.GetElapsedTime</c>.</para>
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RenamerSampleDataCollection
{
    public const string Name = "RenamerSampleDataTests";
}
