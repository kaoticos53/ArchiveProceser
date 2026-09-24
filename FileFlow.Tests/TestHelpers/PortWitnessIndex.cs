using System;
using System.Collections.Generic;
using System.Linq;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// <b>Quién prueba qué puerto de qué nodo</b>, leído de las fuentes del suite.
///
/// <para><b>Por qué existe</b>: el inventario de puertos afirma cosas del estilo «este puerto lo cubre esta
/// prueba». Mientras la única comprobación fuera que la prueba <i>existe</i>, la afirmación se podía cumplir con
/// cualquier nombre del suite —y un nodo sin ninguna prueba podía quedar tapado citando la prueba del
/// vecino—. Este índice contesta tres preguntas por nodo, cada una comprobable sobre el texto:</para>
///
/// <list type="bullet">
/// <item><description><b>¿Alguien lo ejecuta?</b> Hay un caso cuyo bloque menciona el nodo y muestra una llamada
/// de ejecución (<c>ExecuteAsync</c>, <c>RunAsync</c>) o una construcción del propio nodo
/// (<c>XNode(</c>).</description></item>
/// <item><description><b>¿Algún caso nombra este puerto?</b> El bloque menciona el nodo y cita el nombre del
/// puerto como literal (<c>"Out"</c>, <c>"Error"</c>), que es la forma que tiene una prueba de decir por dónde
/// sale el ítem.</description></item>
/// <item><description><b>¿Qué casos hablan del nodo?</b> La lista completa, que es lo que el inventario cita
/// como testigo.</description></item>
/// </list>
///
/// <para><b>Qué no puede probar</b>: el texto no dice que la llamada sea <i>a este</i> nodo ni que el puerto
/// citado sea el que se recorre. Prueba que el caso habla del nodo y del puerto, que es bastante más de lo que
/// prueba un nombre de método suelto, y sigue dependiendo de que el inventario lo cite con honestidad. Por eso
/// las guardias distinguen los tres grados y no dan por igual una prueba que nombra el puerto y una que solo
/// ejecuta el nodo.</para>
/// </summary>
public static class PortWitnessIndex
{
    /// <summary>Los casos que hablan de un nodo, con las dos preguntas ya contestadas.</summary>
    public sealed class NodeWitnesses
    {
        /// <summary>
        /// Los casos que hablan de un nodo. Es público para que las guardias puedan fabricarse un índice con
        /// bloques de prueba sintéticos y auto-testear sus reglas sin depender del árbol real.
        /// </summary>
        public NodeWitnesses(string nodeClass, IReadOnlyList<TestSuiteIndex.TestBlock> mentioningTheNode)
        {
            NodeClass = nodeClass;
            MentioningTheNode = mentioningTheNode;
            ExercisingTest = mentioningTheNode.FirstOrDefault(IsAnExecution);
        }

        /// <summary>Clase del nodo, tal y como la nombra el inventario.</summary>
        public string NodeClass { get; }

        /// <summary>Casos cuyo bloque menciona el nodo, en orden determinista (fichero y nombre).</summary>
        public IReadOnlyList<TestSuiteIndex.TestBlock> MentioningTheNode { get; }

        /// <summary>El primer caso que lo ejecuta, o <c>null</c> si ninguno.</summary>
        public TestSuiteIndex.TestBlock? ExercisingTest { get; }

        /// <summary>¿Algún caso del suite ejecuta este nodo?</summary>
        public bool IsExercised => ExercisingTest is not null;

        /// <summary>
        /// El primer caso que <b>nombra</b> el puerto (y habla del nodo), prefiriendo uno que además lo ejecute.
        /// <c>null</c> si ningún caso lo nombra.
        /// </summary>
        public TestSuiteIndex.TestBlock? Citing(string port)
        {
            string literal = $"\"{port}\"";

            return MentioningTheNode.FirstOrDefault(b => b.Source.Contains(literal, StringComparison.Ordinal) && IsAnExecution(b))
                ?? MentioningTheNode.FirstOrDefault(b => b.Source.Contains(literal, StringComparison.Ordinal));
        }

        /// <summary>
        /// ¿El bloque muestra una llamada de ejecución? Es la señal más barata que distingue «un caso que habla
        /// del nodo» (una tabla de iconos, un catálogo) de «un caso que lo pone a trabajar».
        /// </summary>
        private bool IsAnExecution(TestSuiteIndex.TestBlock block) =>
            block.Source.Contains("ExecuteAsync", StringComparison.Ordinal)
            || block.Source.Contains("RunAsync(", StringComparison.Ordinal)
            || block.Source.Contains($"{NodeClass}(", StringComparison.Ordinal);

        /// <summary>Texto para el mensaje de la guardia cuando el nodo no lo ejecuta nadie.</summary>
        public string DescribeMentions() => MentioningTheNode.Count == 0
            ? "ninguna prueba lo menciona"
            : $"lo mencionan {MentioningTheNode.Count} caso(s) sin ejecutarlo: {string.Join(", ", MentioningTheNode.Select(b => b.Name).Take(3))}";
    }

    /// <summary>
    /// Construye el índice para las clases indicadas: un barrido del suite y una entrada por nodo, con los casos
    /// que lo mencionan. Las clases sin ninguna mención también aparecen (con la lista vacía), que es el dato que
    /// busca la guardia.
    /// </summary>
    public static IReadOnlyDictionary<string, NodeWitnesses> Build(string repositoryRoot, IEnumerable<string> nodeClasses)
    {
        ArgumentNullException.ThrowIfNull(nodeClasses);

        var wanted = new HashSet<string>(nodeClasses, StringComparer.Ordinal);
        var mentions = wanted.ToDictionary(c => c, _ => new List<TestSuiteIndex.TestBlock>(), StringComparer.Ordinal);

        foreach (TestSuiteIndex.TestBlock block in TestSuiteIndex.Blocks(repositoryRoot)
                     .OrderBy(b => b.File, StringComparer.Ordinal)
                     .ThenBy(b => b.Name, StringComparer.Ordinal))
        {
            foreach (string nodeClass in wanted)
            {
                if (block.Source.Contains(nodeClass, StringComparison.Ordinal))
                {
                    mentions[nodeClass].Add(block);
                }
            }
        }

        return mentions.ToDictionary(
            pair => pair.Key,
            pair => new NodeWitnesses(pair.Key, pair.Value),
            StringComparer.Ordinal);
    }
}
