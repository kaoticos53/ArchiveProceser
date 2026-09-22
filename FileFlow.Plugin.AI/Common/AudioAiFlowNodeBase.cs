using System.Threading;
using System.Threading.Tasks;
using FileFlow.Plugin.AI.Inference;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Clase base abstracta para los nodos de IA que consumen modelos de audio (Whisper, Silero VAD, Piper TTS).
///
/// <para>
/// La única diferencia real con visión y texto es el almacén que respalda el ciclo de vida: el audio no usa
/// el almacén por defecto sino <see cref="AudioSessionStore"/>, que fija opciones de CPU. Como ese almacén
/// ya publica sus cambios en el evento único del <see cref="OnnxSessionRegistry"/>, aquí basta con indicar
/// cuál es: la observación del estado, la consulta de carga y la descarga se heredan del nodo base.
/// </para>
///
/// <para>
/// El nodo concreto sólo declara su <see cref="AiFlowNodeBase.TaskType"/>, sus puertos, sus parámetros y su
/// <c>ExecuteAsync</c>: el ciclo de vida del modelo (<c>IsModelLoaded</c>, <c>ModelIdentifier</c>,
/// <c>UnloadModel</c>, <c>ModelStatusChanged</c>) queda heredado.
/// </para>
/// </summary>
public abstract class AudioAiFlowNodeBase : AiFlowNodeBase
{
    /// <summary>
    /// Las sesiones de audio se ejecutan siempre en CPU (ver <see cref="AudioSessionStore"/>), así que
    /// apuntar al almacén de audio basta para reportar tanto la carga del modelo como la ausencia de
    /// aceleración por hardware.
    /// </summary>
    protected override OnnxSessionStore SessionStore => AudioSessionStore.Instance;

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
