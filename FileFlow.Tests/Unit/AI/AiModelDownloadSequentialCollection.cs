using Xunit;

namespace FileFlow.Tests.Unit.AI;

/// <summary>
/// Definición de la colección <c>AiModelDownloadSequential</c>, marcada como <b>no paralelizable</b>.
///
/// Sus miembros (<c>AiModelManagerConfigTests</c>, <c>AiModelManagerViewModelTests</c>,
/// <c>AiModelUrlsConfigViewModelTests</c>) ejercitan descargas reales de modelos contra la red y escriben en
/// los directorios del perfil del usuario (<c>AppPaths</c>: modelos, configuración de URLs espejo). Antes de
/// esta definición era una colección implícita: sus clases se serializaban entre sí, pero corrían en
/// paralelo con todo el resto del suite. Como es una minoría de pruebas lentas y con efectos externos,
/// darles exclusividad total no cuesta apenas tiempo y elimina dos riesgos:
/// <list type="bullet">
///   <item>Que un modelo aparezca a mitad de prueba en el directorio de modelos mientras otra prueba
///   inspecciona el sistema de archivos.</item>
///   <item>Que la latencia de red degrade aserciones de tiempo de otras colecciones que corran a la vez.</item>
/// </list>
/// La inferencia nativa (cargar los modelos descargados en ONNX Runtime) vive en
/// <see cref="OnnxInferenceCollection"/>, no aquí.
/// </summary>
[CollectionDefinition("AiModelDownloadSequential", DisableParallelization = true)]
public sealed class AiModelDownloadSequentialCollection;
