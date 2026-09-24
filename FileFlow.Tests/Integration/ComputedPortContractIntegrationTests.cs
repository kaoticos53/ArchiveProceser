using System.Collections.Generic;
using System.Linq;
using FileFlow.Plugin.Logic;
using FileFlow.Plugin.Subflows;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Integration;

/// <summary>
/// El contrato de los nodos cuyos puertos <b>no están en su código</b>: el switch que expone un puerto por caso
/// más <c>Default</c>, la entrada de subflujo que emite por el nombre configurado y el contenedor que expone los
/// puertos frontera de su subgrafo. Son los tres nodos que la auditoría de puertos <b>aplaza</b>, y un
/// aplazamiento sin prueba es un punto ciego con buena reputación: aquí se resuelve su topología en ejecución
/// (<see cref="DynamicPortResolver"/>, con el mismo materializador que usan el cargador de flujos y el
/// diagnóstico previo a la ejecución) y se ejecuta el nodo con el motor para exigir que el ítem salga por un
/// puerto <b>declarado</b>.
///
/// <para>La afirmación es doble y por eso vale: el puerto por el que llega el ítem al espía tiene que estar entre
/// los que el nodo declara (resueltos en ejecución, no deducidos del texto) y la arista sólo existe desde ese
/// nombre. Si el nodo emitiera por un nombre que no declara, el ítem no llegaría —y el motor dejaría su aviso—,
/// que es exactamente cómo se corta un flujo en silencio.</para>
/// </summary>
[Collection(BranchPortHarnessCollection.Name)]
public class ComputedPortContractIntegrationTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // SwitchCaseNode: un puerto por caso, más Default
    // ─────────────────────────────────────────────────────────────────────────────

    private const string TwoCases = """[{"Name":"Case 1","Pattern":"txt"},{"Name":"Case 2","Pattern":"png"}]""";

    [Fact]
    public async Task TheCaseThatMatches_ShouldLeaveByTheCasePortThatTheNodeDeclares()
    {
        await using var harness = await BranchPortHarness.CreateAsync();
        string file = harness.WriteFile("documento.txt", "contenido");

        DynamicPortResolver.DeclaredOutputsOf("SwitchCaseNode")
            .Should().Contain("Case 1", "el puerto del caso sale de `CasesJson`, así que declararlo es resolverlo");

        var received = await harness.RunAsync(
            sourcePaths: [file],
            nodeTypeName: "SwitchCaseNode",
            nodeParameters: new Dictionary<string, object?>
            {
                ["Expression"] = "{Ext}",
                ["CasesJson"] = TwoCases
            },
            branchPort: "Case 1",
            happyPort: "Default",
            extraAssemblies: [typeof(SwitchCaseNode).Assembly]);

        received.Should().ContainSingle("'.txt' coincide con el patrón del primer caso")
            .Which.Branch.Should().Be("Branch",
                "el ítem sale por 'Case 1', que es el puerto que el nodo declara para ese caso");
    }

    [Fact]
    public async Task AValueThatMatchesNoCase_ShouldLeaveByTheDeclaredDefaultPort()
    {
        await using var harness = await BranchPortHarness.CreateAsync();
        string file = harness.WriteFile("documento.bin", "contenido");

        var received = await harness.RunAsync(
            sourcePaths: [file],
            nodeTypeName: "SwitchCaseNode",
            nodeParameters: new Dictionary<string, object?>
            {
                ["Expression"] = "{Ext}",
                ["CasesJson"] = TwoCases
            },
            branchPort: "Default",
            happyPort: "Case 1",
            extraAssemblies: [typeof(SwitchCaseNode).Assembly]);

        received.Should().ContainSingle("'.bin' no coincide con ningún patrón: el switch tiene rama de reserva")
            .Which.Branch.Should().Be("Branch",
                "el valor sin caso sale por 'Default', el puerto que el nodo añade siempre a su topología");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // SubflowInputNode: emite por el nombre configurado
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AConfiguredPort_ShouldBeTheOneTheSubflowInputEmits()
    {
        await using var harness = await BranchPortHarness.CreateAsync();
        string file = harness.WriteFile("documento.txt", "contenido");

        // La entrada del contenedor no se llama 'In': sus puertos son los que el usuario escribió, así que el
        // nodo tiene que emitir por el primero de ellos en lugar del nombre genérico.
        string[] declared = [.. DynamicPortResolver.DeclaredOutputsOf("SubflowInputNode")];
        declared.Should().Equal(["Entrada", "Alterna"], "los dos nombres configurados, en su orden");

        var received = await harness.RunAsync(
            sourcePaths: [file],
            nodeTypeName: "SubflowInputNode",
            nodeParameters: new Dictionary<string, object?> { ["PortNames"] = "Entrada;Alterna" },
            branchPort: "Entrada",
            happyPort: "Alterna",
            extraAssemblies: [typeof(SubflowInputNode).Assembly]);

        received.Should().ContainSingle("el ítem entró por 'In', que ya no es un puerto suyo: sale por el primero")
            .Which.Branch.Should().Be("Branch");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // SubflowNode: expone la frontera de su subgrafo
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AContainerWithARenamedBoundary_ShouldEmitByTheDeclaredBoundaryPort()
    {
        await using var harness = await BranchPortHarness.CreateAsync();
        string file = harness.WriteFile("documento.txt", "contenido");

        // Frontera renombrada: el subgrafo recibe por 'In' y devuelve por 'Done', así que el contenedor expone
        // esos dos nombres en lugar de los genéricos y el flujo de dentro tiene que salir por el suyo.
        string definition = DynamicPortResolver.DefinitionJson("In", "Done");
        var container = DynamicPortResolver.Materialize(
            () => new SubflowNode(),
            new Dictionary<string, object?> { ["EmbedDefinition"] = true, ["SubflowDefinitionJson"] = definition });

        string[] inputs = [.. container.Inputs.Select(p => p.Name)];
        string[] outputs = [.. container.Outputs.Select(p => p.Name)];

        inputs.Should().Equal(["In"], "los puertos del contenedor son su frontera, no los genéricos");
        outputs.Should().Equal(["Done"], "y el cable del usuario apunta a ese nombre, no a 'Out'");

        var received = await harness.RunAsync(
            sourcePaths: [file],
            nodeTypeName: "SubflowNode",
            nodeParameters: new Dictionary<string, object?>
            {
                ["EmbedDefinition"] = true,
                ["SubflowDefinitionJson"] = definition,
                ["SubflowName"] = "Subflujo de la prueba"
            },
            branchPort: "Done",
            happyPort: null,
            extraAssemblies: [typeof(SubflowNode).Assembly]);

        received.Should().ContainSingle("el interior del subflujo llega a su frontera de salida renombrada")
            .Which.Branch.Should().Be("Branch",
                "el contenedor emite por 'Done' —el puerto que declara— y no por el genérico 'Out'");
    }
}
