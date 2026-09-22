using System.Collections.Generic;
using System.Linq;
using FileFlow.Plugin.Logic;
using FileFlow.Plugin.Scripting;
using FileFlow.Plugin.Subflows;
using FileFlow.Sdk;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Sdk;

/// <summary>
/// Contrato de notificación de la topología de puertos (<see cref="IPortTopologyNode"/>): el nodo anuncia
/// cuándo cambian sus puertos y calla cuando se le pide reevaluar sin que hayan cambiado.
///
/// Los puertos se calculan al leerlos y no hay ninguna invalidación observable, así que sin este anuncio el
/// editor sólo puede enterarse de un cambio de puertos por casualidad. Y el silencio importa tanto como el
/// aviso: el inspector escribe un parámetro por pulsación de tecla, de modo que un nodo que anunciara en
/// cada escritura reconstruiría el lienzo sin motivo.
///
/// Los tests usan nodos REALES, nunca dobles: cualquier tipo concreto que implemente <c>IFlowNode</c> en el
/// ensamblado de tests lo descubriría el cargador como nodo del producto.
/// </summary>
public class PortTopologyContractTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Nodos con puertos fijos: el caso de los ~60 nodos que no dependen de parámetros
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ANodeWithFixedPorts_ShouldStaySilent_BecauseItsTopologyNeverChanges()
    {
        var node = new ThrottleDelayNode();
        var announcements = new List<PortTopologyChangedEventArgs>();
        node.PortsChanged += (_, args) => announcements.Add(args);

        // La primera reevaluación fija la referencia: anuncia la topología que encuentra, porque desde este
        // lado todavía no se ha anunciado ninguna. A partir de ahí sólo habla si cambia de verdad.
        node.RefreshPortTopology();
        announcements.Should().ContainSingle().Subject.InputPortNames.Should().Equal("In");

        node.Parameters["DelayMilliseconds"] = 250;
        node.RefreshPortTopology();
        node.Parameters["Noise"] = "irrelevante";
        node.RefreshPortTopology();

        announcements.Should().ContainSingle(
            "sus puertos son siempre In/Out: editar un parámetro no debe reconstruir el lienzo de la tarjeta");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Nodos cuyos puertos se calculan al leerlos (lazy)
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SwitchCases_ShouldBeAnnounced_OnlyWhenThePortNamesChange()
    {
        var node = new SwitchCaseNode();
        var announcements = new List<PortTopologyChangedEventArgs>();
        node.PortsChanged += (_, args) => announcements.Add(args);

        node.SetCases([new SwitchCaseRule("Imágenes", "jpg;png"), new SwitchCaseRule("Vídeo", "mp4")]);

        var announcement = announcements.Should().ContainSingle().Subject;
        announcement.OutputPortNames.Should().Equal("Imágenes", "Vídeo", "Default");
        announcement.InputPortNames.Should().Equal("In");

        // Cambiar sólo los patrones no mueve ningún puerto: el caso del switch sigue llamándose igual.
        node.SetCases([new SwitchCaseRule("Imágenes", "tif"), new SwitchCaseRule("Vídeo", "mkv")]);

        announcements.Should().ContainSingle("un caso renombrado sí cambiaría la topología, pero un patrón no");
    }

    [Fact]
    public void SwitchCases_ShouldAnnounceTheRename_WhenACaseChangesItsName()
    {
        var node = new SwitchCaseNode();
        var announcements = new List<PortTopologyChangedEventArgs>();
        node.PortsChanged += (_, args) => announcements.Add(args);

        node.SetCases([new SwitchCaseRule("Imágenes", "jpg")]);
        node.SetCases([new SwitchCaseRule("Fotos", "jpg")]);

        announcements.Should().HaveCount(2);
        announcements[^1].OutputPortNames.Should().Equal("Fotos", "Default");
    }

    [Fact]
    public void SubflowInputNode_ShouldAnnounceTheConfiguredOutputNames()
    {
        var node = new SubflowInputNode();
        var announcements = new List<PortTopologyChangedEventArgs>();
        node.PortsChanged += (_, args) => announcements.Add(args);

        node.PortNames = "In;Alternate";

        announcements.Should().ContainSingle().Subject.OutputPortNames.Should().Equal("In", "Alternate");
        node.Outputs.Select(port => port.Name).Should().Equal("In", "Alternate");

        node.PortNames = "In;Alternate";

        announcements.Should().ContainSingle("asignar el mismo valor no es un cambio de topología");
    }

    [Fact]
    public void SubflowOutputNode_ShouldAnnounceTheConfiguredInputNames()
    {
        var node = new SubflowOutputNode();
        var announcements = new List<PortTopologyChangedEventArgs>();
        node.PortsChanged += (_, args) => announcements.Add(args);

        node.Parameters["PortNames"] = "Out|Errors";
        node.RefreshPortTopology();

        announcements.Should().ContainSingle().Subject.InputPortNames.Should().Equal("Out", "Errors");
        node.Inputs.Select(port => port.Name).Should().Equal("Out", "Errors");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Nodos cuyos puertos se materializan (eager): anunciar sin rederivar no sirve
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SubflowNode_ShouldAnnounceItsDeclaredPorts_BecauseTheyDoNotComeFromItsParameters()
    {
        var node = new SubflowNode();
        var announcements = new List<PortTopologyChangedEventArgs>();
        node.PortsChanged += (_, args) => announcements.Add(args);

        node.RefreshDynamicPorts(["Data", "Control"], ["Result"]);

        var announcement = announcements.Should().ContainSingle().Subject;
        announcement.InputPortNames.Should().Equal("Data", "Control");
        announcement.OutputPortNames.Should().Equal("Result");

        node.RefreshDynamicPorts(["Data", "Control"], ["Result"]);

        announcements.Should().ContainSingle("redescubrir los mismos puertos no cambia la topología");
    }

    [Fact]
    public void CustomScriptNode_ShouldRedriveThePortsFromItsParameters_BeforeAnnouncing()
    {
        var node = new CustomScriptNode();
        var announcements = new List<PortTopologyChangedEventArgs>();
        node.PortsChanged += (_, args) => announcements.Add(args);

        // Así escribe el inspector: en el diccionario, sin pasar por la propiedad. Anunciar sin volver a
        // derivar dejaría al editor con los puertos viejos, que es justo el fallo que se quiere evitar.
        node.Parameters["InputPorts"] = "Entrada";
        node.Parameters["OutputPorts"] = "Salida,Errores";
        node.RefreshPortTopology();

        var announcement = announcements.Should().ContainSingle().Subject;
        announcement.InputPortNames.Should().Equal("Entrada");
        announcement.OutputPortNames.Should().Equal("Salida", "Errores");

        node.RefreshPortTopology();

        announcements.Should().ContainSingle("reevaluar los mismos parámetros no vuelve a anunciar nada");
    }
}
