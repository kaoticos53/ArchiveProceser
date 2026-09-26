using System.Collections.Generic;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Guardia de la capa de decoradores del lienzo Uno (notas y grupos, fase 3.4): la capa tiene que
/// <b>escuchar</b> <c>CanvasDecorators.CollectionChanged</c>, igual que las tarjetas escuchan Nodes.
///
/// <para><b>Por qué guarda la fuente y no el runtime</b>: el lienzo es WinUI (host Uno) y no se
/// materializa en la sesión de pruebas. El defecto del hito 230 lo cazó la propia sonda 3.4 en su
/// primera corrida: <c>AddAnnotation</c>/<c>AddGroup</c> del núcleo escribían <c>CanvasDecorators</c>
/// y la capa NO se reconstruía — las notas existían en el VM y jamás llegaban al árbol visual
/// (<c>noteRendered</c>/<c>groupRendered</c> en FALLO). La cura es la suscripción con handler con
/// nombre en el setter de <c>Editor</c>, y esta guardia la exige como código vivo (retira comentarios
/// antes de buscar: la lección del 165).</para>
/// </summary>
public class UnoCanvasDecoratorsGuardTests
{
    private const string CanvasPath = "FileFlow.App.Uno/Controls/EditorCanvasControl.xaml.cs";

    private static string CanvasCode() => SourceText.CodeWithoutComments(CanvasPath);

    [Fact]
    public void Canvas_ShouldSubscribeToDecoratorsCollectionChanged()
    {
        CanvasCode().Should().Contain(
            "((INotifyCollectionChanged)_editor.CanvasDecorators).CollectionChanged += OnDecoratorsChanged;",
            "sin esta suscripción, AddAnnotation/AddGroup del núcleo escriben el VM y la capa de " +
            "decoradores no se entera: notas y grupos jamás llegan al árbol (defecto cazado por la sonda 3.4)");
    }

    [Fact]
    public void Canvas_ShouldUnsubscribeOldEditorFromDecorators()
    {
        CanvasCode().Should().Contain(
            "((INotifyCollectionChanged)_editor.CanvasDecorators).CollectionChanged -= OnDecoratorsChanged;",
            "la simetría del contrato de vida (hito 225): sin la desuscripción del editor saliente, cada " +
            "reasignación del Editor deja una capa fantasma reconstruyendo decoradores sobre un VM viejo");
    }

    [Fact]
    public void Canvas_ShouldWireDecoratorsEventToRebuilding()
    {
        CanvasCode().Should().Contain(
            "private void OnDecoratorsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildDecorators();",
            "el evento tiene que terminar en la reconstrucción de la capa: una suscripción que no conecta " +
            "con RebuildDecorators dejaría la cura en un no-op");
    }

    [Fact]
    public void Canvas_ShouldKeepTheNodeAndWireSubscriptionsAlongsideDecorators()
    {
        // El caso hermano (el control del mutante decoradores-que-no-llegan-al-arbol): el mutante
        // invierte UNA línea (la suscripción de CanvasDecorators) y deja intactas las de Nodes y
        // Connections — el Rebuild de tarjetas y el redibujado de cables siguen cubiertos.
        string code = CanvasCode();

        code.Should().Contain(
            "((INotifyCollectionChanged)_editor.Nodes).CollectionChanged += OnNodesChanged;",
            "la suscripción de decoradores es COMPLEMENTARIA: el Rebuild de tarjetas sigue necesitando Nodes");

        code.Should().Contain(
            "((INotifyCollectionChanged)_editor.Connections).CollectionChanged += OnConnectionsChanged;",
            "el redibujado de cables sigue colgado de Connections: si un defecto tocara el bloque entero, " +
            "este control caería y el veredicto de la mutación sería IMPRECISA");
    }
}
