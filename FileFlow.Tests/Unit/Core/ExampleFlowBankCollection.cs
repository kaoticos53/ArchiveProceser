using Xunit;

namespace FileFlow.Tests.Unit.Core;

/// <summary>
/// Colección del <b>banco de los 40 ejemplos</b> (<see cref="ExampleFlowsEndToEndTests"/>), marcada como
/// <b>no paralizable</b>.
///
/// <para>Lo que confina es el <b>directorio de trabajo del proceso</b>: el banco lo apunta a una sala limpia suya
/// mientras ejecuta los flujos, para poder decir de quién es un archivo que aparezca ahí. Es estado global —lo
/// comparte todo el proceso, incluidas las pruebas que corren a la vez—, así que la exclusividad no es un
/// adorno: sin ella, una colección vecina que resolviera una ruta relativa caería dentro de la sala del banco y
/// el archivo se le atribuiría al ejemplo que estuviera corriendo en ese momento.</para>
///
/// <para>El contrato está en <c>TestAssemblyParallelism.cs</c> y su guardia en
/// <c>TestCollectionContractGuardTests</c> (el patrón que la delata es <c>Directory.SetCurrentDirectory</c>).</para>
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ExampleFlowBankCollection
{
    public const string Name = "ExampleFlowBank";
}
