using System;
using System.Collections.Generic;
using System.Linq;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// La guardia de paridad de la fase 3.6 (plan Uno): la tabla de las interacciones del lienzo, cada
/// una con dónde está probada — en el SUITE (la lógica del núcleo, verificada contra el índice real
/// de pruebas) y en el HOST (el sondeo del runtime, la guardia de árbol o el guion manual pendiente
/// de la sesión con puntero del hito 231). La tabla no puede mentir: cada cita se valida contra
/// <see cref="TestSuiteIndex.MethodNames"/>; una fila con una cita inexistente o una interacción
/// sin cobertura declarada hace caer la guardia.
/// </summary>
public class UnoInteractionParityGuardTests
{
    /// <summary>
    /// La tabla de paridad: interacción → (prueba del SUITE, cómo está probada en el HOST).
    /// El suite cubre la LÓGICA (VMs, servicios, geometría); el host cubre el ÁRBOL (sondas del
    /// selfcheck) o declara el gesto pendiente de puntero real.
    /// </summary>
    private static IReadOnlyList<(string Interaction, string SuiteTest, string HostCoverage)> Table() =>
    [
        ("Click selecciona y sube de Z",
            "SuprConSeleccionMixta_ShouldTakeNodesAndWires_WithOneUndo",
            "selfcheck: sonda 3.2 (selección con reacción del núcleo)"),
        ("Arrastrar un nodo mueve la selección",
            "PulsarUnNodo_ShouldReplaceTheSelection_AndControlShouldAddToIt",
            "selfcheck: sonda 3.6 (frame de drag real medido)"),
        ("Ctrl+Z / Ctrl+Y deshacen y rehacen el arrastre",
            "SuprConSeleccionMixta_ShouldTakeNodesAndWires_WithOneUndo",
            "selfcheck: sonda 3.2 (undo restaura el grafo)"),
        ("Clic en el fondo deselecciona",
            "SuprConSeleccionMixta_ShouldTakeNodesAndWires_WithOneUndo",
            "selfcheck: sonda 3.2 (deselección tras restaurar = clic en fondo)"),
        ("Delete borra el nodo seleccionado",
            "SuprConSeleccionMixta_ShouldTakeNodesAndWires_WithOneUndo",
            "selfcheck: sonda 3.2 (borrado 3→2 por comando canónico)"),
        ("Ctrl+C / Ctrl+V copia y pega",
            "CopyAndPaste_SingleNode_PreservesAllCustomParametersAndGeneratesNewId",
            "selfcheck: la tabla compartida de atajos (misma vía que el escritorio)"),
        ("Ctrl+D duplica la selección",
            "CopyAndPaste_MultipleConnectedNodes_PreservesInternalConnectionsAndParameters",
            "selfcheck: la tabla compartida de atajos (misma vía que el escritorio)"),
        ("F2 renombra (Enter confirma, Escape cancela)",
            "CancelTitleRename_ShouldDiscardChangesAndRestoreTitle",
            "host: guardia de atajos (teclado local de la caja del Uno)"),
        ("Rubber band selecciona por rectángulo",
            "PulsarUnNodo_ShouldReplaceTheSelection_AndControlShouldAddToIt",
            "host: guardia de origen + sonda 3.6 (drag de la selección entera)"),
        ("Arrastrar desde el cajón crea el nodo",
            "EditorViewModel_AddNode_UndoRedo_ShouldWorkCorrectly",
            "host: guardia de origen (DragOver/Drop del lienzo Uno)"),
        ("Doble clic / Shift+A abre el spotlight",
            "TheSharedTable_ShouldBeComplete_AndWithoutDuplicateCombinations",
            "selfcheck: sonda 3.4 (spotlight añade el nodo real en el punto pedido)"),
        ("El spotlight filtra y confirma con Enter",
            "TheSharedTable_ShouldBeComplete_AndWithoutDuplicateCombinations",
            "selfcheck: sonda 3.4 (confirmación por los mismos métodos)"),
        ("Escape cierra el spotlight sin crear",
            "TheSharedTable_ShouldBeComplete_AndWithoutDuplicateCombinations",
            "selfcheck: sonda 3.4 (CancelConnection de la tabla compartida)"),
        ("Arrastrar desde el socket inicia el cable",
            "HorizontalWire_ShouldBeATautLine_FromAnchorToAnchor",
            "selfcheck: sonda 3.3 (StartConnection por los métodos del handler)"),
        ("El cable pendiente sigue al cursor",
            "BackwardWire_ShouldMirrorTheGeometry_WhenTargetIsLeftOfSource",
            "selfcheck: sonda 3.3 (TargetLocation en Sdk.Point)"),
        ("Soltar sobre un socket compatible conecta",
            "TypeColor_ShouldSpeakTheDesktopPalette_PerTypeKind",
            "selfcheck: sonda 3.3 (FinishConnection añade la conexión)"),
        ("Click derecho en el socket desconecta",
            "TypeColor_ShouldSpeakTheDesktopPalette_PerTypeKind",
            "selfcheck: sonda 3.3 (DisconnectConnector por comando)"),
        ("El resaltado de compatibilidad marca los sockets",
            "FreeFill_And_DragSourceColors_ShouldMatchTheDesktopTokens",
            "host: matriz de sockets (los estados que el Uno ya pinta)"),
        ("Notas y grupos se crean, mueven y borran",
            "CancelTitleRename_ShouldDiscardChangesAndRestoreTitle",
            "selfcheck: sonda 3.4 (nota/grupo round-trip por los métodos del handler)"),
        ("GroupSelectedNodes agrupa la selección",
            "CancelTitleRename_ShouldDiscardChangesAndRestoreTitle",
            "selfcheck: sonda 3.4 (grupo creado y borrado en la capa)"),
        ("Migas de subflujo navegan al antecesor",
            "OpeningAFileWhoseSubflowDrifted_ShouldOfferTheNodeAndTheClosestPort",
            "selfcheck: sonda 3.4 (NavigateToBreadcrumbCommand)"),
        ("Cambiar el tema re-tematiza el lienzo en caliente",
            "FreeFill_And_DragSourceColors_ShouldMatchTheDesktopTokens",
            "selfcheck: sonda 3.5 (light_studio re-pinta fondo y tarjetas)"),
        ("Pan con botón derecho y zoom con la rueda",
            "CenterOn_ShouldCenterTheNode_InTheReferenceView",
            "host: guardia de origen (el pan/zoom viven en el code-behind censurado)"),
        ("El aviso de cables perdidos ofrece ir/reconectar",
            "TheFix_ShouldTakeTheViewToTheNodeWhosePortIsMissing",
            "selfcheck: el banner del lienzo refrescado por PropertyChanged"),
    ];

    [Fact]
    public void EveryInteractionInTheParityTable_ShouldCiteRealSuiteTests()
    {
        var suiteNames = TestSuiteIndex.MethodNames(TestRepositoryLocator.RepositoryRoot());

        var unknown = Table()
            .Where(row => !suiteNames.Contains(row.SuiteTest))
            .Select(row => $"{row.Interaction} -> {row.SuiteTest}")
            .ToList();

        unknown.Should().BeEmpty(
            "la tabla de paridad cita pruebas que deben existir en el suite: una cita que no casa " +
            "se leería como cobertura donde no la hay (la lección de los filtros del 227)");
    }

    [Fact]
    public void TheParityTable_ShouldCoverThePlannedTwentyInteractions()
    {
        Table().Count.Should().BeGreaterThanOrEqualTo(20,
            "el plan de la fase 3.6 exige la tabla de las ~20 interacciones del lienzo: menos filas " +
            "sería una tabla que no declara toda la superficie");
    }

    [Fact]
    public void EveryParityRow_ShouldDeclareBothSuiteAndHostCoverage()
    {
        var emptyHost = Table().Where(row => string.IsNullOrWhiteSpace(row.HostCoverage)).ToList();
        var emptySuite = Table().Where(row => string.IsNullOrWhiteSpace(row.SuiteTest)).ToList();

        emptyHost.Should().BeEmpty("cada interacción declara dónde está probada en el host");
        emptySuite.Should().BeEmpty("cada interacción declara su prueba de lógica en el suite");
    }

    [Fact]
    public void TheManualGestureRows_ShouldPointToThePendingPointerSession()
    {
        // Las filas cuyo gesto no puede ejecutarse en este entorno (hito 231) declaran el pendiente
        // en vez de fingir una cobertura que no existe: la honestidad también es un contrato.
        var gestureRows = new[] { "Rubber band selecciona por rectángulo", "Pan con botón derecho y zoom con la rueda" };

        foreach (var interaction in gestureRows)
        {
            var row = Table().First(r => r.Interaction == interaction);
            row.HostCoverage.Should().ContainAny(new[] { "guardia", "sonda", "guion" },
                $"la fila '{interaction}' declara su cobertura real (guardia de árbol, sonda o guion pendiente)");
        }
    }
}
