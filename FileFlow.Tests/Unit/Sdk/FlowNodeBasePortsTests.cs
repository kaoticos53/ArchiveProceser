using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FileFlow.Plugin.Logic;
using FileFlow.Plugin.Scripting;
using FileFlow.Plugin.Subflows;
using FileFlow.Sdk;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Sdk;

/// <summary>
/// Contrato de puertos de <see cref="FlowNodeBase"/>: los fijos se asignan en el constructor y los
/// dinámicos se calculan con los hooks <c>BuildInputPorts</c>/<c>BuildOutputPorts</c>, sin sobrescribir
/// las propiedades (que dejaron de ser <c>virtual</c>).
///
/// Los cinco nodos con puertos dinámicos —un switch con un puerto por caso, los dos nodos frontera de
/// subflujo, el subflujo contenedor y el nodo de script— se migraron a este contrato. Antes cada uno
/// sobrescribía la propiedad entera con su propio bloque get, y tres de ellos recalculaban lo mismo en
/// cada lectura con tres estilos distintos.
///
/// Los tests usan nodos REALES, nunca dobles: cualquier tipo concreto que implemente <c>IFlowNode</c> en
/// el ensamblado de tests lo descubre el cargador como un nodo del producto y rompe la guardia del
/// catálogo (el catálogo pasó de 78 a 80 nodos cuando se intentó con dobles).
/// </summary>
public class FlowNodeBasePortsTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Puertos fijos: la ruta que usan los otros ~60 nodos
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void StaticPorts_ShouldStillComeFromTheConstructor()
    {
        var node = new ThrottleDelayNode();

        node.Inputs.Should().ContainSingle();
        node.Inputs[0].Name.Should().Be("In");
        node.Inputs[0].Direction.Should().Be(PortDirection.Input);

        node.Outputs.Should().ContainSingle();
        node.Outputs[0].Name.Should().Be("Out");
        node.Outputs[0].Direction.Should().Be(PortDirection.Output);
    }

    [Fact]
    public void PortsShouldBeComputedByAProtectedHook_NotByOverridingTheProperty()
    {
        // El contrato: las propiedades no son puntos de extensión y el hook protegido sí lo es.
        // Ojo con el metadata: un miembro que implementa un miembro de interfaz se emite como
        // virtual+final, así que "sobrescribible" es virtual && !final, no sólo virtual.
        foreach (string propertyName in new[] { nameof(IFlowNode.Inputs), nameof(IFlowNode.Outputs) })
        {
            MethodInfo getter = typeof(FlowNodeBase).GetProperty(propertyName)!.GetGetMethod()!;

            (getter.IsVirtual && !getter.IsFinal)
                .Should().BeFalse($"{propertyName} no se sobrescribe: sus puertos se calculan con el hook");
        }

        foreach (string hook in new[] { "BuildInputPorts", "BuildOutputPorts" })
        {
            MethodInfo method = typeof(FlowNodeBase).GetMethod(hook, BindingFlags.Instance | BindingFlags.NonPublic)!;

            method.Should().NotBeNull();
            (method.IsVirtual && !method.IsFinal)
                .Should().BeTrue($"{hook} es el punto de extensión de los puertos dinámicos");
            method.IsFamily.Should().BeTrue($"{hook} sólo lo sobrescriben las clases derivadas");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Puertos calculados en cada lectura
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SwitchCaseNode_ShouldExposeOneOutputPerCase_RecomputedOnEveryRead()
    {
        var node = new SwitchCaseNode();

        node.Outputs.Select(port => port.Name).Should().Equal("Case 1", "Default");

        // Sin ninguna llamada de invalidación: la lectura siguiente ve el cambio.
        node.SetCases([new SwitchCaseRule("Imágenes", "jpg;png"), new SwitchCaseRule("Vídeo", "mp4")]);

        node.Outputs.Select(port => port.Name).Should().Equal("Imágenes", "Vídeo", "Default");
        node.Outputs.Should().OnlyContain(port => port.Direction == PortDirection.Output);
    }

    [Fact]
    public void SubflowInputNode_ShouldExposeOneOutputPerConfiguredName()
    {
        var node = new SubflowInputNode();

        node.Outputs.Select(port => port.Name).Should().Equal("In");

        node.Parameters["PortNames"] = "In;Alternate";

        node.Outputs.Select(port => port.Name).Should().Equal("In", "Alternate");
    }

    [Fact]
    public void SubflowOutputNode_ShouldExposeOneInputPerConfiguredName()
    {
        var node = new SubflowOutputNode();

        node.Inputs.Select(port => port.Name).Should().Equal("Out");

        node.Parameters["PortNames"] = "Out|Errors";

        node.Inputs.Select(port => port.Name).Should().Equal("Out", "Errors");
        node.Inputs.Should().OnlyContain(port => port.Direction == PortDirection.Input);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Puertos guardados y refrescados explícitamente
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SubflowNode_ShouldFollowItsDeclaredPorts_AndFallBackWhenThereAreNone()
    {
        var node = new SubflowNode();

        node.RefreshDynamicPorts(["Data", "Control"], ["Result"]);

        node.Inputs.Select(port => port.Name).Should().Equal("Data", "Control");
        node.Outputs.Select(port => port.Name).Should().Equal("Result");

        // Un subgrafo sin puertos declarados no puede quedarse sin ninguno: expone el genérico.
        node.RefreshDynamicPorts([], []);

        node.Inputs.Select(port => port.Name).Should().Equal("In");
        node.Outputs.Select(port => port.Name).Should().Equal("Out");
    }

    [Fact]
    public void CustomScriptNode_ShouldExposeThePortsConfiguredByTheUser()
    {
        var node = new CustomScriptNode();

        node.Inputs.Select(port => port.Name).Should().Equal("In");
        node.Outputs.Select(port => port.Name).Should().Equal("Out");

        node.Parameters["InputPorts"] = "Entrada, Extra";
        node.Parameters["OutputPorts"] = "Salida";
        node.SyncPortsFromParameters();

        node.Inputs.Select(port => port.Name).Should().Equal("Entrada", "Extra");
        node.Outputs.Select(port => port.Name).Should().Equal("Salida");

        // Un valor en blanco vuelve al puerto por defecto en lugar de dejar el nodo sin puertos.
        node.Parameters["InputPorts"] = "   ";
        node.SyncPortsFromParameters();

        node.Inputs.Select(port => port.Name).Should().Equal("In");
    }
}
