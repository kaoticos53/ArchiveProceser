using Xunit;

namespace FileFlow.Tests.Integration;

/// <summary>
/// Colección de las clases que ejercitan el contrato de puertos con <c>BranchPortHarness</c>.
///
/// <para>No es una colección <b>exclusiva</b> (<c>DisableParallelization</c>): estas pruebas no tocan estado global
/// de proceso y pueden correr al lado del resto del suite. Existe por otro motivo, más pequeño y más fácil de
/// olvidar: el espía del andamiaje guarda lo que recibe en un registro <b>estático</b>, así que dos clases que lo
/// compartan y corran a la vez se pisan —una limpia la cola de la otra y cada una ve ítems de la vecina—. En una
/// misma colección, xUnit no las solapa, y el aislamiento se mantiene sin serializar el suite entero.</para>
///
/// <para>Las pruebas de visión no están aquí: viven en la colección exclusiva <c>OnnxInference</c> porque el motor
/// consulta la aceleración del nodo al terminarlo, y una colección exclusiva no corre junto a ninguna otra, así
/// que tampoco puede pisar el espía.</para>
/// </summary>
[CollectionDefinition(Name)]
public sealed class BranchPortHarnessCollection
{
    public const string Name = "BranchPortHarness";
}
