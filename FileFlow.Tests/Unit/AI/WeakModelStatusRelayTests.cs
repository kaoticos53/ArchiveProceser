using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using FileFlow.Plugin.AI;
using FileFlow.Plugin.AI.Inference;
using FileFlow.Sdk;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.AI;

/// <summary>
/// Verifica el relay débil que conecta los eventos estáticos de sesión con los nodos de IA.
///
/// Antes de este relay, cada constructor de nodo añadía una lambda
/// <c>() => ModelStatusChanged?.Invoke()</c> al evento estático <c>SessionStateChanged</c>: el delegado
/// multicas del evento estático capturaba al nodo y, como ni <see cref="IFlowNode"/> expone
/// <c>Dispose</c> ni el host libera nodos, cada nodo creado quedaba anclado al proceso para siempre.
/// Estas pruebas evitan que el patrón vuelva (guardia de fuente) y que el relay pierda su función
/// (reenvío vivo) o su limpieza (autodesuscripción tras GC, <c>Dispose</c> determinista).
///
/// Todas las pruebas que cuentan suscripciones estabilizan primero la línea base con
/// <see cref="StabilizeBaseline"/>: nodos de clases anteriores de esta colección exclusiva pueden seguir
/// pendientes de barrido, y sin esa purga inicial el conteo dependería del orden de ejecución.
/// </summary>
[Collection(OnnxInferenceCollection.Name)]
public class WeakModelStatusRelayTests
{
    [Fact]
    public void Subscribe_ShouldForwardSessionChanges_ToTheOwnersModelStatusChanged()
    {
        var node = new BackgroundRemoverNode();
        int fired = 0;
        node.ModelStatusChanged += () => fired++;

        try
        {
            OnnxSessionManager.ClearSessionCache();

            fired.Should().BeGreaterThan(0, "el cambio de sesión debe llegar al nodo vivo");
        }
        finally
        {
            OnnxSessionManager.ClearSessionCache();
        }
    }

    [Fact]
    public void CollectedNode_ShouldDrainItsSubscription_AfterGarbageCollection()
    {
        int baseline = StabilizeBaseline();

        CreateNodeAndDrop();

        WeakModelStatusRelay.LiveSubscriptionCount.Should().Be(
            baseline + 1,
            "el constructor del nodo deja exactamente una suscripción viva");

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // El disparo fuerza el barrido de la lista de invocación: las suscripciones con propietario
        // recolectado se dan de baja a sí mismas dentro de la propia invocación.
        OnnxSessionManager.ClearSessionCache();

        WeakModelStatusRelay.LiveSubscriptionCount.Should().Be(
            baseline,
            "el nodo recolectado debe dejar su suscripción drenada, no un delegado anclado para siempre");
    }

    [Fact]
    public void Dispose_ShouldDetachDeterminedly_EvenIfTheOwnerIsStillAlive()
    {
        int baseline = StabilizeBaseline();

        var owner = new object();
        int forwarded = 0;

        WeakModelStatusRelay.Subscription subscription = WeakModelStatusRelay.Subscribe(
            h => OnnxSessionManager.SessionStateChanged += h,
            h => OnnxSessionManager.SessionStateChanged -= h,
            owner,
            _ => forwarded++);

        WeakModelStatusRelay.LiveSubscriptionCount.Should().Be(baseline + 1);

        OnnxSessionManager.ClearSessionCache();
        forwarded.Should().BeGreaterThan(0, "con el propietario vivo el reenvío funciona");

        // Limpieza determinista: la vía prevista si el host algún día expone Dispose en IFlowNode.
        subscription.Dispose();

        WeakModelStatusRelay.LiveSubscriptionCount.Should().Be(baseline);

        int before = forwarded;
        OnnxSessionManager.ClearSessionCache();
        forwarded.Should().Be(before, "tras el Dispose el propietario ya no recibe avisos");
    }

    [Fact]
    public void Source_ShouldNotAddOwnerCapturingLambdas_ToStaticSessionEvents()
    {
        // Guardia de fuente: el patrón 'X.SessionStateChanged += () => ...' es la fuga que el relay
        // elimina — la lambda captura al nodo y el evento estático la mantiene viva para siempre. Si un
        // nodo nuevo lo reintroduce, esto falla y señala el fichero y la línea.
        //
        // No delata las suscripciones legítimas: el relay suscribe con '+=' de un manejador nombrado
        // ('h => ... SessionStateChanged += h', que no empieza la línea por el tipo) y
        // AiPluginInitializer suscribe un grupo de métodos estático (no captura nada).
        string pluginRoot = Path.Combine(TestRepositoryLocator.RepositoryRoot(), "FileFlow.Plugin.AI");

        var offenders = Directory
            .GetFiles(pluginRoot, "*.cs", SearchOption.AllDirectories)
            .Select(file => (File: file, Text: File.ReadAllText(file)))
            .SelectMany(pair => Regex
                .Matches(
                    pair.Text,
                    @"(?m)^\s*(OnnxSessionManager|AudioInferenceEngine)\.SessionStateChanged\s*\+=\s*\(\)\s*=>")
                .Select(m => $"{pair.File}"))
            .ToList();

        offenders.Should().BeEmpty(
            "ningún nodo debe colgar una lambda propia de un evento estático: usa WeakModelStatusRelay.Subscribe");
    }

    [Fact]
    public void BuildingEveryAiNode_ShouldNotLeaveOrphanSubscriptions()
    {
        int baseline = StabilizeBaseline();

        int created = CreateAllAiNodesAndReturnCount();

        WeakModelStatusRelay.LiveSubscriptionCount.Should().Be(
            baseline + created,
            "cada nodo añade exactamente una suscripción viva");

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        SweepBothSessionEvents();

        WeakModelStatusRelay.LiveSubscriptionCount.Should().Be(
            baseline,
            "todos los nodos descartados deben drenar sus suscripciones en el primer barrido");
    }

    /// <summary>
    /// Purga los restos de clases anteriores de la colección (nodos aún no barridos) y devuelve el conteo
    /// estable de partida. Sólo fiable dentro de la colección exclusiva <c>OnnxInference</c>.
    /// </summary>
    private static int StabilizeBaseline()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        SweepBothSessionEvents();

        return WeakModelStatusRelay.LiveSubscriptionCount;
    }

    /// <summary>
    /// Fuerza el barrido de las suscripciones muertas disparando ambos eventos estáticos: cada relay sólo
    /// se desuscribe cuando <b>su</b> evento se invoca, y los nodos de audio escuchan el de
    /// <c>AudioInferenceEngine</c>, que <c>OnnxSessionManager.ClearSessionCache</c> no toca.
    /// </summary>
    private static void SweepBothSessionEvents()
    {
        OnnxSessionManager.ClearSessionCache();
        AudioInferenceEngine.ClearSessionCache();
    }

    /// <summary>Crea un nodo y deja que sea recolectable: el método sale y con él desaparece toda raíz local.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CreateNodeAndDrop()
    {
        _ = new BackgroundRemoverNode();
    }

    /// <summary>
    /// Instancia los 13 nodos de IA del plugin y devuelve cuántos fueron. El array muere al salir del
    /// método: así el GC puede recolectarlos sin raíces locales que distorsionen el conteo.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int CreateAllAiNodesAndReturnCount()
    {
        IFlowNode[] nodes =
        [
            new BackgroundRemoverNode(),
            new SuperResolutionUpscalerNode(),
            new ContentModerationFilterNode(),
            new ObjectDetectorNode(),
            new PromptObjectDetectorNode(),
            new FaceDetectorNode(),
            new SmartImageClassifierNode(),
            new VoiceActivityDetectorNode(),
            new TextToSpeechNode(),
            new LocalLlmProcessorNode(),
            new LocalAiTranslatorNode(),
            new PromptTransformerNode(),
            new PiiAnonymizerNode()
        ];

        return nodes.Length;
    }
}
