using System;
using System.Threading;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Relay débil entre el evento único de estado de sesión del plugin
/// (<see cref="FileFlow.Plugin.AI.Inference.OnnxSessionRegistry.SessionStateChanged"/>, al que reexpiden los
/// almacenes de visión, audio y embeddings) y el evento de instancia
/// <see cref="IModelLifecycleNode.ModelStatusChanged"/> de cada nodo.
///
/// <para>
/// El patrón anterior — el constructor del nodo añade una lambda
/// <c>() => ModelStatusChanged?.Invoke()</c> al evento estático — es una fuga estructural: el delegado
/// multicas rooted en el evento estático captura el nodo a través de la lambda, y como el nodo no expone
/// ningún <c>Dispose</c> que el host invoque, la suscripción vive para siempre. En una sesión larga el
/// editor crea y descarta nodos constantemente (deshacer, rehacer, recargar plugins), y cada uno dejaba
/// un delegado anclado.
/// </para>
///
/// <para>
/// Este relay invierte la referencia: el nodo sólo es alcanzable desde el evento estático a través de una
/// <see cref="WeakReference"/>, de modo que el grafo nodo→suscripción queda recolectable en cuanto el
/// editor suelta el nodo. La desconexión real ocurre en el primer disparo posterior del evento (el barrido
/// ve <c>Target == null</c> y se da de baja a sí mismo dentro de la propia invocación, operación admitida
/// por los delegados multicas), o de forma determinista vía <see cref="Subscription.Dispose"/>.
/// </para>
///
/// <para>
/// Contrato de la lambda de reenvío: debe ser <c>static</c> (no capturar al propietario ni nada vivo) y
/// limitarse a llamar a un puente público del nodo que invoque su evento. Si capturara al propietario, la
/// referencia débil sería inútil: la lambda mantendría vivo al nodo exactamente igual que antes.
/// </para>
/// </summary>
public static class WeakModelStatusRelay
{
    private static int _liveSubscriptions;

    /// <summary>
    /// Suscripciones aún conectadas a su evento estático (nodos vivos + suscripciones de nodos ya
    /// recolectados pendientes del próximo barrido). Existe para verificar la autolimpieza en pruebas;
    /// el motor no debe tomar decisiones basadas en él.
    /// </summary>
    public static int LiveSubscriptionCount => Interlocked.CompareExchange(ref _liveSubscriptions, 0, 0);

    /// <summary>
    /// Suscribe el reenvío de un evento estático hacia el nodo propietario.
    ///
    /// <paramref name="sourceSubscribe"/>/<paramref name="sourceUnsubscribe"/> reciben el manejador del
    /// relay (nunca una lambda del nodo) y lo añaden/retiran del evento estático correspondiente.
    /// <paramref name="statusChanged"/> reconduce el aviso al evento de instancia del nodo vivo.
    /// </summary>
    public static Subscription Subscribe<TOwner>(
        Action<Action> sourceSubscribe,
        Action<Action> sourceUnsubscribe,
        TOwner owner,
        Action<TOwner> statusChanged)
        where TOwner : class
    {
        ArgumentNullException.ThrowIfNull(sourceSubscribe);
        ArgumentNullException.ThrowIfNull(sourceUnsubscribe);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(statusChanged);

        // Normaliza el reenvío a Action<object>. 'statusChanged' es una lambda estática por contrato, así
        // que esta envoltura sólo arrastra al delegado estático compartido: no mantiene vivo al propietario.
        return new Subscription(
            sourceSubscribe,
            sourceUnsubscribe,
            owner,
            target => statusChanged((TOwner)target));
    }

    private static void IncrementLiveCount() => Interlocked.Increment(ref _liveSubscriptions);

    private static void DecrementLiveCount() => Interlocked.Decrement(ref _liveSubscriptions);

    /// <summary>
    /// Una suscripción viva: conecta un evento estático con el nodo propietario mientras éste exista y se
    /// desconecta sola cuando el propietario desaparece. El nodo no necesita guardarla ni liberarla.
    /// </summary>
    public sealed class Subscription : IDisposable
    {
        private readonly Lock _gate = new();
        private readonly Action<Action> _sourceSubscribe;
        private readonly Action<Action> _sourceUnsubscribe;
        private readonly WeakReference _ownerRef;
        private readonly Action<object> _statusChanged;
        private readonly Action _handler;
        private bool _disposed;

        internal Subscription(
            Action<Action> sourceSubscribe,
            Action<Action> sourceUnsubscribe,
            object owner,
            Action<object> statusChanged)
        {
            _sourceSubscribe = sourceSubscribe;
            _sourceUnsubscribe = sourceUnsubscribe;
            _ownerRef = new WeakReference(owner);
            _statusChanged = statusChanged;
            _handler = Forward;

            // Si el attach lanza, la suscripción no llega a contar: el constructor del nodo propietario
            // propaga la excepción y no queda nada anclado.
            _sourceSubscribe(_handler);
            IncrementLiveCount();
        }

        /// <summary>Manejador conectado al evento estático. Es el único delegado rooted por el evento.</summary>
        private void Forward()
        {
            // Target no nulo devuelve una referencia fuerte local: el propietario no puede ser recolectado
            // entre la lectura y la invocación.
            object? owner = _ownerRef.Target;

            if (owner != null)
            {
                _statusChanged(owner);
                return;
            }

            // El propietario fue recolectado: la suscripción ya no sirve a nadie. Darse de baja aquí,
            // dentro de la propia invocación del evento, es seguro y cierra la fuga sin esperar a nada
            // externo (no hay Dispose en el ciclo de vida de los nodos).
            Dispose();
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
            }

            // '-=' de un delegado ausente es un no-op: es seguro aunque el barrido y una limpieza
            // determinista coincidan.
            _sourceUnsubscribe(_handler);
            DecrementLiveCount();
        }
    }
}
