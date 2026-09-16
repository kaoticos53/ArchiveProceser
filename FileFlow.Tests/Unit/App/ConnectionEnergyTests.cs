using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using FileFlow.App.ViewModels;
using FileFlow.Core.Plugins;
using FileFlow.Sdk;
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
    /// </summary>
    private static (EditorViewModel Editor, ConnectionViewModel Wire) BuildEditorWithWire()
    {
        var editor = new EditorViewModel(new PluginLoader());
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
