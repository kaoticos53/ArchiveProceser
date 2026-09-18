using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using FileFlow.App.Models;
using FileFlow.App.ViewModels;
using FileFlow.App.Views.Components;
using FileFlow.Core.Plugins;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Views;

[Collection("VisualSnapshots")]
public class NodeCardInteractiveControlsPointerTests
{
    private static NodeViewModel CreateTestNode()
    {
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Logic.SwitchCaseNode).Assembly);
        loader.ScanCurrentAppDomain();

        var editorVm = new EditorViewModel(loader);
        var node = editorVm.AddNode("SwitchCaseNode", new Point(100, 100));
        node.Should().NotBeNull();
        return node!;
    }

    [Fact]
    public void PointerPressed_OnComboBoxOrInteractiveChild_ShouldBeHandledByNodeCard()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var nodeVm = CreateTestNode();
            var cardView = new NodeCardView { DataContext = nodeVm };
            var comboBox = new ComboBox();
            cardView.Content = comboBox;

            var window = new Window { Content = cardView };
            window.Show();

            var pointer = new Pointer(1, PointerType.Mouse, true);
            var pointerProps = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonPressed);
            var pointerArgs = new PointerPressedEventArgs(comboBox, pointer, cardView, new Point(10, 10), 0, pointerProps, KeyModifiers.None)
            {
                RoutedEvent = InputElement.PointerPressedEvent
            };

            comboBox.RaiseEvent(pointerArgs);
            window.Close();

            pointerArgs.Handled.Should().BeTrue("el evento debe marcarse como manejado para que ItemContainer de Nodify no capture el puntero ni impida abrir la lista desplegable");
        });
    }

    [Fact]
    public void PointerPressed_OnAutoCompleteBox_ShouldBeHandledByNodeCard()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var nodeVm = CreateTestNode();
            var cardView = new NodeCardView { DataContext = nodeVm };
            var autoCompleteBox = new AutoCompleteBox();
            cardView.Content = autoCompleteBox;

            var window = new Window { Content = cardView };
            window.Show();

            var pointer = new Pointer(2, PointerType.Mouse, true);
            var pointerProps = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonPressed);
            var pointerArgs = new PointerPressedEventArgs(autoCompleteBox, pointer, cardView, new Point(10, 10), 0, pointerProps, KeyModifiers.None)
            {
                RoutedEvent = InputElement.PointerPressedEvent
            };

            autoCompleteBox.RaiseEvent(pointerArgs);
            window.Close();

            pointerArgs.Handled.Should().BeTrue("el evento sobre AutoCompleteBox debe marcarse como manejado");
        });
    }

    [Fact]
    public void PointerPressed_OnNonInteractiveCardSurface_ShouldNotBeHandled_AllowingNodifyDrag()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var nodeVm = CreateTestNode();
            var cardView = new NodeCardView { DataContext = nodeVm };

            var nonInteractiveBorder = new Border();
            cardView.Content = nonInteractiveBorder;

            var pointer = new Pointer(3, PointerType.Mouse, true);
            var pointerProps = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonPressed);
            var pointerArgs = new PointerPressedEventArgs(nonInteractiveBorder, pointer, cardView, new Point(10, 10), 0, pointerProps, KeyModifiers.None)
            {
                RoutedEvent = InputElement.PointerPressedEvent
            };

            nonInteractiveBorder.RaiseEvent(pointerArgs);

            pointerArgs.Handled.Should().BeFalse("el evento en superficie no interactiva no se marca como manejado para permitir el arrastre del nodo en Nodify");
        });
    }

    [Fact]
    public void PointerPressed_OnInteractiveControl_ShouldSelectAndBringNodeToFront()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var loader = new PluginLoader();
            loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Logic.SwitchCaseNode).Assembly);
            loader.ScanCurrentAppDomain();

            var editorVm = new EditorViewModel(loader);
            var node1 = editorVm.AddNode("SwitchCaseNode", new Point(50, 50))!;
            var node2 = editorVm.AddNode("SwitchCaseNode", new Point(100, 100))!;

            // node2 was added last, so it's currently at front
            editorVm.BringToFront(node2);
            node2.ZIndex.Should().BeGreaterThan(node1.ZIndex);

            var cardView1 = new NodeCardView { DataContext = node1 };
            var comboBox = new ComboBox();
            cardView1.Content = comboBox;

            var window = new Window { Content = cardView1 };
            window.Show();

            var pointer = new Pointer(4, PointerType.Mouse, true);
            var pointerProps = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonPressed);
            var pointerArgs = new PointerPressedEventArgs(comboBox, pointer, cardView1, new Point(10, 10), 0, pointerProps, KeyModifiers.None)
            {
                RoutedEvent = InputElement.PointerPressedEvent
            };

            comboBox.RaiseEvent(pointerArgs);
            window.Close();

            node1.IsSelected.Should().BeTrue("al hacer clic en un control interactivo el nodo debe quedar seleccionado");
            node1.ZIndex.Should().BeGreaterThan(node2.ZIndex, "el nodo debe pasar al frente por encima del otro nodo");
        });
    }

    [Theory]
    [InlineData("FileRelocatorNode", typeof(FileFlow.Plugin.FileSystem.FileRelocatorNode))]
    [InlineData("BestVersionSelectorNode", typeof(FileFlow.Plugin.Logic.BestVersionSelectorNode))]
    public void ExpandingNodeCard_WithFileVersionSelector_ShouldRenderWithoutException(string nodeTypeName, Type pluginAssemblyAnchor)
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var loader = new PluginLoader();
            loader.RegisterNodeTypesFromAssembly(pluginAssemblyAnchor.Assembly);
            loader.ScanCurrentAppDomain();

            var editorVm = new EditorViewModel(loader);
            var node = editorVm.AddNode(nodeTypeName, new Point(100, 100));
            node.Should().NotBeNull();
            node!.IsExpanded = true;

            var cardView = new NodeCardView { DataContext = node };
            var window = new Window { Content = cardView, Width = 600, Height = 600 };
            window.Show();

            // Force layout measure & arrange pass to trigger template loading
            window.Measure(new Size(600, 600));
            window.Arrange(new Rect(0, 0, 600, 600));

            node.Parameters.Should().NotBeEmpty();
            var versionParam = node.Parameters.FirstOrDefault(p => p.IsFileVersionSelector);
            versionParam.Should().NotBeNull("el nodo debe contener al menos un parámetro FileVersionSelector");
            versionParam!.AvailableVersionOptions.Should().NotBeEmpty("debe contener opciones de versiones de archivo");

            var comboBoxes = cardView.GetVisualDescendants().OfType<ComboBox>().ToList();
            var versionComboBox = comboBoxes.FirstOrDefault(cb => cb.ItemsSource == versionParam.AvailableVersionOptions);
            versionComboBox.Should().NotBeNull("debe existir un ComboBox vinculado a AvailableVersionOptions");

            var originalOption = versionParam.AvailableVersionOptions.FirstOrDefault(o => o.Tag == "Original");
            originalOption.Should().NotBeNull();

            versionComboBox!.SelectedItem = originalOption;

            versionParam.Value.Should().Be(originalOption!.Token, "al seleccionar una opción en el ComboBox, el valor del parámetro debe actualizarse");
            versionParam.SelectedVersionOption.Should().Be(originalOption);
            originalOption.IsSelected.Should().BeTrue("la opción seleccionada debe tener IsSelected = true");

            window.Close();
        });
    }
    [Fact]
    public void RightClick_OnNodeCard_ShouldMarkHandledAndOpenContextMenu()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var nodeVm = CreateTestNode();
            var cardView = new NodeCardView { DataContext = nodeVm };

            var nonInteractiveBorder = new Border();
            cardView.Content = nonInteractiveBorder;

            var window = new Window { Content = cardView };
            window.Show();

            // Simular clic derecho en superficie no interactiva de la tarjeta
            var pointer = new Pointer(5, PointerType.Mouse, true);
            var pointerProps = new PointerPointProperties(RawInputModifiers.RightMouseButton, PointerUpdateKind.RightButtonPressed);
            var pointerArgs = new PointerPressedEventArgs(nonInteractiveBorder, pointer, cardView, new Point(10, 10), 0, pointerProps, KeyModifiers.None)
            {
                RoutedEvent = InputElement.PointerPressedEvent
            };

            nonInteractiveBorder.RaiseEvent(pointerArgs);
            window.Close();

            // El clic derecho debe marcarse como manejado para evitar que NodifyEditor
            // inicie el paneo y bloquee el menú contextual del nodo.
            pointerArgs.Handled.Should().BeTrue("el clic derecho en un nodo debe marcar el evento como manejado para mostrar el menú contextual");
        });
    }
}
