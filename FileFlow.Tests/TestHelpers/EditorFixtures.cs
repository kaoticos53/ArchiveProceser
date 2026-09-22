using Avalonia;
using FileFlow.App.ViewModels;
using FileFlow.Sdk;

namespace FileFlow.Tests.TestHelpers;

/// <summary>Utilidades compartidas por las pruebas que montan un lienzo.</summary>
public static class EditorFixtures
{
    /// <summary>
    /// Añade una instancia al lienzo y devuelve su tarjeta, ya con el editor como dueño: sin dueño, la
    /// tarjeta no revalidaría las conexiones al cambiar de puertos y la prueba no reproduciría el lienzo.
    /// </summary>
    public static NodeViewModel AddNode(EditorViewModel editor, IFlowNode node, double x = 0, double y = 0)
    {
        var nodeVm = new NodeViewModel(node, new Point(x, y)) { ParentEditor = editor };
        editor.Nodes.Add(nodeVm);
        return nodeVm;
    }
}
