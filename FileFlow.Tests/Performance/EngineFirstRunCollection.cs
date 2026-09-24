using Xunit;

namespace FileFlow.Tests.Performance;

/// <summary>
/// Colección de la prueba que mide <b>cuánto tarda la primera ejecución de una sesión</b>, marcada como
/// <b>no paralizable</b>.
///
/// <para>Lo que esta colección confina no es un objeto global que alguien mute, sino un <b>recurso compartido
/// por todo el proceso</b>: el grupo de hilos y la CPU. La medida es una resta —la primera ejecución frente al
/// estado estable de la misma máquina— y cualquier colección que corra a la vez la ensucia en la dirección
/// contraria a la que se quiere medir: medido en una pasada con una clase vecina ejecutando su propio flujo,
/// la primera ejecución salió en 1 904 ms donde sola sale en 269 ms. Con <c>DisableParallelization</c> la
/// medición corre en exclusiva (ni siquiera las colecciones ajenas corren a la vez) y el número es el que dice
/// ser.</para>
///
/// <para>Es la única colección del suite cuyo motivo es <b>medir</b> y no aislar estado mutable: el grupo de
/// hilos no se toca desde las pruebas —lo declara el motor al arrancar una ejecución, que es lo que se viene a
/// comprobar— y la CPU no se puede aislar de ninguna otra manera que no ejecutando sola.</para>
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class EngineFirstRunCollection
{
    public const string Name = "EngineFirstRun";
}
