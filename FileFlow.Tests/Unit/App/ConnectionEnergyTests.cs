using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using FileFlow.App.ViewModels;
using FileFlow.Core.Plugins;
using FileFlow.Sdk;
using FileFlow.Sdk.Services;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Flujo de energía animado en los cables durante la ejecución.
///
/// La capa de energía sólo es una animación: lo que hay que garantizar es que se enciende y se apaga en el
/// momento correcto. Un cable que se queda encendido miente sobre el estado del flujo, y uno que nunca se
/// enciende deja la animación reducida a decoración muerta, un fallo silencioso que ningún compilador ve.
/// </summary>
public class ConnectionEnergyTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Pulso de energía
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void PulseConnectionEnergy_ShouldLightTheWireAndIncrementTheGeneration()
    {
        var (editor, wire) = BuildEditorWithWire();
        int generationBefore = wire.LastDispatchedCount;

        editor.PulseConnectionEnergy(wire, durationMs: 10_000);

        wire.IsExecuting.Should().BeTrue("el cable está transportando datos");
        wire.LastDispatchedCount.Should().Be(generationBefore + 1, "cada pulso abre una nueva generación");

        editor.ClearConnectionEnergy();
    }

    [Fact]
    public void CompleteConnectionPulse_ShouldTurnOffTheWire_WhenTheGenerationMatches()
    {
        var (editor, wire) = BuildEditorWithWire();

        editor.PulseConnectionEnergy(wire, durationMs: 10_000);
        EditorViewModel.CompleteConnectionPulse(wire, wire.LastDispatchedCount);

        wire.IsExecuting.Should().BeFalse("el pulso que encendió el cable ya venció");
    }

    [Fact]
    public void CompleteConnectionPulse_ShouldNotTurnOffANewerPulse()
    {
        // Ráfaga: dos elementos llegan casi a la vez y el temporizador del primero vence en medio del
        // segundo. Si el apagado no comprobara la generación, el cable se apagaría con datos en tránsito.
        var (editor, wire) = BuildEditorWithWire();

        editor.PulseConnectionEnergy(wire, durationMs: 10_000);
        int staleGeneration = wire.LastDispatchedCount;
        editor.PulseConnectionEnergy(wire, durationMs: 10_000);

        EditorViewModel.CompleteConnectionPulse(wire, staleGeneration);

        wire.IsExecuting.Should().BeTrue("el segundo pulso sigue vivo");

        editor.ClearConnectionEnergy();
        wire.IsExecuting.Should().BeFalse();
    }

    [Fact]
    public async Task PulseConnectionEnergy_ShouldKeepTheWireLit_UntilTheClockReachesTheDuration()
    {
        // El vencimiento programado —no el apagado manual— es lo que hace que el cable vuelva solo a reposo.
        // Con el reloj del sistema, comprobarlo costaría la espera entera (y una espera real no prueba nada:
        // prueba que el tiempo pasa). Con el reloj inyectado, el paso del tiempo es el paso de la prueba.
        var clock = new ManualTimeProvider();
        var (editor, wire) = BuildEditorWithWire(clock);

        // El reloj también lleva el latido del vigilante de subflujos del propio editor, así que lo que se mide es
        // el incremento: el vencimiento del pulso tiene que quedar programado en el reloj inyectado, no en el del
        // sistema.
        int timersBeforePulse = clock.PendingTimerCount;

        Task expiry = editor.PulseConnectionEnergy(wire, durationMs: 900);

        wire.IsExecuting.Should().BeTrue("el pulso acaba de empezar");
        clock.PendingTimerCount.Should().Be(timersBeforePulse + 1, "el fin del pulso quedó programado en el reloj inyectado, no en el del sistema");

        clock.AdvanceBy(TimeSpan.FromMilliseconds(899));
        wire.IsExecuting.Should().BeTrue("un pulso de 900 ms no ha vencido a los 899");

        clock.AdvanceBy(TimeSpan.FromMilliseconds(1));
        await expiry.WaitAsync(TimeSpan.FromSeconds(5));

        // El estado final se sondea porque el apagado pasa por el hilo de UI; que el reloj inyectado sea el
        // correcto ya lo dice «PendingTimerCount» arriba, sin depender del tiempo real.
        await AsyncTestWaiter.WaitForAsync(
            () => !wire.IsExecuting,
            TimeSpan.FromSeconds(5),
            description: "el apagado del cable al vencer su pulso");
    }

    [Fact]
    public async Task PulseConnectionEnergy_ShouldNotLetTheStaleExpiryTurnOffANewerPulse()
    {
        // La ráfaga de verdad: los pulsos se solapan y el vencimiento del primero llega con el segundo en
        // marcha. Aquí se espera ese vencimiento obsoleto (en la aplicación nadie lo espera) para que la
        // comprobación no sea una carrera: si el apagado no mirara la generación, el cable se apagaría con
        // datos en tránsito.
        var clock = new ManualTimeProvider();
        var (editor, wire) = BuildEditorWithWire(clock);

        Task stale = editor.PulseConnectionEnergy(wire, durationMs: 900);

        clock.AdvanceBy(TimeSpan.FromMilliseconds(500));
        Task current = editor.PulseConnectionEnergy(wire, durationMs: 900);

        clock.AdvanceBy(TimeSpan.FromMilliseconds(400));
        await stale.WaitAsync(TimeSpan.FromSeconds(5));

        wire.IsExecuting.Should().BeTrue("el vencimiento del pulso viejo no puede apagar el nuevo");

        clock.AdvanceBy(TimeSpan.FromMilliseconds(500));
        await current.WaitAsync(TimeSpan.FromSeconds(5));

        await AsyncTestWaiter.WaitForAsync(
            () => !wire.IsExecuting,
            TimeSpan.FromSeconds(5),
            description: "el apagado del cable al vencer el último pulso");
    }

    [Fact]
    public void PulseConnectionEnergy_ShouldNotLightTheWire_WhenTheDurationIsNotPositive()
    {
        var (editor, wire) = BuildEditorWithWire();
        wire.IsExecuting = true;

        editor.PulseConnectionEnergy(wire, durationMs: 0);

        wire.IsExecuting.Should().BeFalse("un pulso sin duración no debe dejar el cable encendido");
    }

    [Fact]
    public void ClearConnectionEnergy_ShouldExtinguishEveryWire()
    {
        var (editor, first) = BuildEditorWithWire();
        var (_, second) = BuildEditorWithWire();

        editor.Connections.Add(second);
        editor.PulseConnectionEnergy(first, durationMs: 10_000);
        editor.PulseConnectionEnergy(second, durationMs: 10_000);

        editor.ClearConnectionEnergy();

        editor.Connections.Should().OnlyContain(c => !c.IsExecuting);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Despacho real: sólo se energiza el cable que transporta los datos
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void UpdateEdgeDispatched_ShouldPulseOnlyTheWiresOfThatOutput()
    {
        var editor = new EditorViewModel(new PluginLoader());
        var (sourceA, targetA) = AddConnectedNodes(editor);
        var (_, targetB) = AddConnectedNodes(editor);

        editor.Connections.Should().HaveCount(2);

        editor.UpdateEdgeDispatched(sourceA.Id, "out", count: 12);

        var wireA = editor.Connections.Single(c => c.Source.Name == "out" && c.Target.NodeOwner == targetA);
        var wireB = editor.Connections.Single(c => c.Target.NodeOwner == targetB);

        wireA.IsExecuting.Should().BeTrue();
        wireA.ItemCount.Should().Be(12);
        wireA.HasItems.Should().BeTrue();

        wireB.IsExecuting.Should().BeFalse("el otro cable no transporta estos datos");
        wireB.ItemCount.Should().Be(0);

        editor.ClearConnectionEnergy();
    }

    [Fact]
    public void UpdateEdgeDispatched_ShouldIgnoreUnknownOutputs()
    {
        var editor = new EditorViewModel(new PluginLoader());
        AddConnectedNodes(editor);

        editor.UpdateEdgeDispatched("nodo-inexistente", "out", count: 5);

        editor.Connections.Should().OnlyContain(c => !c.IsExecuting);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Un cable no puede existir sin puertos reales: la energía se prueba sobre el grafo de verdad, no
    /// sobre objetos inventados, para que el cable y su estado de conexión sean siempre coherentes.
    ///
    /// El despacho va en línea (<see cref="NullUiDispatcher"/>) porque el vencimiento del pulso entrega su
    /// apagado al hilo de la interfaz: sin eso, la prueba dependería de que exista una aplicación y de que su
    /// bucle atienda el despacho —y el reloj manual devuelve el control al hilo que avanza, no al de la
    /// interfaz—, que es justo la carrera que se quiere medir.
    /// </summary>
    private static (EditorViewModel Editor, ConnectionViewModel Wire) BuildEditorWithWire(TimeProvider? clock = null)
    {
        var editor = new EditorViewModel(new PluginLoader(), timeProvider: clock, uiDispatcher: NullUiDispatcher.Instance);
        AddConnectedNodes(editor);

        return (editor, editor.Connections.Single());
    }

    private static (NodeViewModel Source, NodeViewModel Target) AddConnectedNodes(EditorViewModel editor)
    {
        var source = new NodeViewModel(new FakeFlowNode("out", PortDirection.Output), new Point(0, 0));
        var target = new NodeViewModel(new FakeFlowNode("in", PortDirection.Input), new Point(200, 0));

        editor.Nodes.Add(source);
        editor.Nodes.Add(target);
        editor.CreateConnection(source.OutputPorts[0], target.InputPorts[0]);

        return (source, target);
    }

    private sealed class FakeFlowNode : IFlowNode
    {
        public FakeFlowNode(string portName, PortDirection direction)
        {
            var port = new NodePort(portName, typeof(FileItemContext), direction, portName);

            if (direction == PortDirection.Input) Inputs = [port];
            else Outputs = [port];
        }

        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name => "FakeFlowNode";
        public string Category => "Test";
        public string Description => "Nodo de prueba de cables";
        public IReadOnlyList<NodePort> Inputs { get; } = [];
        public IReadOnlyList<NodePort> Outputs { get; } = [];
        public Dictionary<string, object?> Parameters { get; } = [];

        public Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
