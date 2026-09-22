using System;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Sdk;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Clase base abstracta para los nodos de IA que consumen modelos de audio (Whisper, Silero VAD, Piper TTS).
///
/// <para>
/// Especializa <see cref="AiFlowNodeBase"/> en lo único que realmente difiere de visión y texto: el almacén
/// de sesiones. El audio no pasa por <c>OnnxSessionManager</c> sino por <see cref="AudioInferenceEngine"/>,
/// cuya caché (<c>AudioSessionCache</c>) fija opciones de CPU, comparte un lock de inferencia y expone su
/// propio evento. Sin este puente, un nodo de audio migrado dejaría de reaccionar a la carga y descarga de
/// sus modelos, y consultaría la caché equivocada al preguntar si el modelo está cargado.
/// </para>
///
/// <para>
/// El nodo concreto sólo declara su <see cref="AiFlowNodeBase.TaskType"/>, sus puertos, sus parámetros y su
/// <c>ExecuteAsync</c>: el ciclo de vida del modelo (<c>IsModelLoaded</c>, <c>ModelIdentifier</c>,
/// <c>PreloadModelAsync</c>, <c>UnloadModel</c>, <c>ModelStatusChanged</c>) queda heredado.
/// </para>
/// </summary>
public abstract class AudioAiFlowNodeBase : AiFlowNodeBase
{
    /// <summary>
    /// Engancha el relay débil al evento estático del motor de audio. Sigue siendo una única suscripción por
    /// nodo, igual que en la base: lo que cambia es la fuente, no el número.
    /// </summary>
    protected AudioAiFlowNodeBase()
        : base(
            h => AudioInferenceEngine.SessionStateChanged += h,
            h => AudioInferenceEngine.SessionStateChanged -= h)
    {
    }

    protected override bool IsSessionLoadedForModel(string modelPath)
        => AudioInferenceEngine.IsSessionLoaded(modelPath);

    protected override bool UnloadSessionForModel(string modelPath)
        => AudioInferenceEngine.UnloadSession(modelPath);

    /// <summary>
    /// Las sesiones de audio se crean siempre con opciones de CPU (ver <c>AudioSessionCache</c>): el motor
    /// no solicita DirectML para estos grafos, así que no hay aceleración que reportar.
    /// </summary>
    protected override bool IsGpuAcceleratedForModel(string modelPath) => false;

    /// <summary>
    /// Resolver la ruta del modelo es toda la precarga útil en audio: los motores materializan su sesión al
    /// inferir, y forzar aquí la carga del grafo duplicaría la memoria retenida en procesos con varios nodos
    /// de audio configurados.
    /// </summary>
    public override async Task PreloadModelAsync(CancellationToken cancellationToken = default)
    {
        await AiModelManager.ResolveModelPathAsync(
            ModelSelection,
            TaskType,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        RaiseModelStatusChanged();
    }
}
