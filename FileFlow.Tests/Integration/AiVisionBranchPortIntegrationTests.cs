using System.Collections.Generic;
using System.IO;
using FileFlow.Plugin.AI;
using FileFlow.Tests.TestHelpers;
using FileFlow.Tests.Unit.AI;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Integration;

/// <summary>
/// Las ramas <c>Error</c> de los dos nodos de visión del plugin de IA, ejecutadas con el motor real: el ítem
/// tiene que llegar al puerto declarado cuando la imagen de entrada no existe.
///
/// <para><b>Por qué van aparte</b> y no junto al resto del contrato de ramas: el motor consulta la aceleración
/// del nodo al terminar cada nodo de IA (<c>IModelLifecycleNode.IsGpuAccelerated</c>, que resuelve la ruta del
/// modelo y mira el registro de sesiones), así que una clase que ejecute estos nodos toca estado global del
/// clúster ONNX y tiene que declarar la colección exclusiva. Ejecutarlos en paralelo con la colección
/// <c>OnnxInference</c> es justo la carrera que el suite aprendió a no tener.</para>
///
/// <para>La rama se dispara con una ruta inexistente, que estos nodos resuelven <b>antes</b> de tocar ningún
/// modelo: la prueba no descarga ni materializa sesiones, solo exige que el motor entregue el ítem por el puerto
/// que el nodo declara.</para>
/// </summary>
[Collection(OnnxInferenceCollection.Name)]
public class AiVisionBranchPortIntegrationTests
{
    /// <summary>
    /// Los nodos de visión cuya rama de error es «la imagen no está», con el nombre de su puerto de error.
    /// Los dos nombran igual su camino feliz y su rama, pero se declaran explícitamente para que añadir un nodo
    /// obligue a mirar sus puertos, no a heredar los del vecino.
    /// </summary>
    public static TheoryData<string, string> VisionNodesWhoseInputCanBeMissing => new()
    {
        { "ImageTypeClassifierNode", "Error" },
        { "MultimodalVisionLlmNode", "Error" }
    };

    [Theory]
    [MemberData(nameof(VisionNodesWhoseInputCanBeMissing))]
    public async Task AnAiVisionNodeThatCannotFindItsInput_ShouldLeaveByTheDeclaredErrorPort(
        string nodeTypeName,
        string errorPort)
    {
        await using var harness = await BranchPortHarness.CreateAsync();
        string missing = Path.Combine(harness.Root, "no_existe.png");

        var received = await harness.RunAsync(
            sourcePaths: [missing],
            nodeTypeName: nodeTypeName,
            nodeParameters: new Dictionary<string, object?>(),
            branchPort: errorPort,
            extraAssemblies: [typeof(ImageTypeClassifierNode).Assembly]);

        received.Should().ContainSingle($"{nodeTypeName} tiene que salir una vez por '{errorPort}'")
            .Which.Branch.Should().Be("Branch",
                $"{nodeTypeName} emite por '{errorPort}', que es el puerto que el cable declara hacia el espía");
        received[0].Item.CurrentPath.Should().Be(missing,
            "el ítem que sale por la rama es el que entró, con su ruta: el nodo no inventa una imagen que no tiene");
    }
}
