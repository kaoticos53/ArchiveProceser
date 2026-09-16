using Xunit;

namespace FileFlow.Tests.Unit.AI;

/// <summary>
/// Colección de las pruebas que ejercitan el clúster de inferencia nativo, marcada como <b>no paralelizable</b>.
///
/// Los motores de ONNX Runtime y las librerías nativas del clúster de IA (vision, audio, embeddings) cargan y
/// liberan memoria no administrada dentro del proceso del host de pruebas. Ejecutadas en paralelo con el resto
/// de colecciones —y entre ellas—, la contención de hilos nativos y la carrera de carga/descarga de los
/// assemblies nativos terminan abortando el host (<c>0xC0000005</c>, <c>testhost</c> bloqueado), con el fallo
/// apareciendo en la prueba equivocada. En su propia colección serializada el clúster corre en serie y el resto
/// de la suite puede paralelizar a su alrededor sin riesgo.
///
/// Reúne dos grupos de estado global de proceso:
/// <list type="bullet">
///   <item>Los registros de sesiones de <c>ModelSessionRegistry</c>, <c>OnnxSessionManager</c> y
///   <c>AiPluginInitializer</c>: vaciar una caché mientras otra prueba consulta su cuenta dispara fallos cruzados.</item>
///   <item>Las sesiones nativas reales que <c>AiNodesTests</c> puede abrir si un modelo ya descargado existe en
///   disco: son el caso que históricamente aborta el host.</item>
/// </list>
///
/// Cualquier clase nueva que ejecute inferencia real, cree nodos del plugin de IA sobre modelos en disco o
/// vacíe los registros de sesiones debe declararse en esta colección.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class OnnxInferenceCollection
{
    public const string Name = "OnnxInference";
}
