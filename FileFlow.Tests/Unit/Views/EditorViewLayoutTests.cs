using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using FileFlow.App.Converters;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.App.Views;
using FileFlow.App.Views.Components;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Views;

[Collection(VisualSnapshotsCollection.Name)]
public class EditorViewLayoutTests

{
    private PluginLoader CreateLoader()
    {
        var pluginLoader = new PluginLoader();
        pluginLoader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.FileSystem.FolderSourceNode).Assembly);
        pluginLoader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Logic.ExpressionFilterNode).Assembly);
        pluginLoader.ScanCurrentAppDomain();
        return pluginLoader;
    }

    [Fact]
    public void AddNode_ViewModelOnly_CompletesInstantly()
    {
        var pluginLoader = CreateLoader();
        var editorVm = new EditorViewModel(pluginLoader);

        var node = editorVm.AddNode("FolderSourceNode", new Point(150, 200));

        node.Should().NotBeNull();
        node!.Title.Should().NotBeNullOrWhiteSpace();
        node.Location.Should().Be(new Point(150, 200));
        node.InputPorts.Should().BeEmpty();
        node.OutputPorts.Should().HaveCount(1);
    }

    [Fact]
    public void Converters_UsedByNodeCard_ShouldWorkCorrectly()
    {
        var boolConv = new StringEqualsToBooleanConverter();
        boolConv.Convert("Running", typeof(bool), "Running", null!).Should().Be(true);
        boolConv.Convert("Idle", typeof(bool), "Running", null!).Should().Be(false);

        var msConv = new DurationMsToTextConverter();
        msConv.Convert(1500L, typeof(string), null!, null!).Should().NotBeNull();

        var bytesConv = new BytesToTextConverter();
        bytesConv.Convert(1048576L, typeof(string), null!, null!).Should().NotBeNull();
    }

    [Fact]
    public void WorkflowGraph_LoadAndConnectNodes_ShouldMaintainTopology()
    {
        var pluginLoader = CreateLoader();
        var editorVm = new EditorViewModel(pluginLoader);

        var node1 = editorVm.AddNode("FolderSourceNode", new Point(100, 100));
        var node2 = editorVm.AddNode("DestinationSinkNode", new Point(500, 100));
        node1.Should().NotBeNull();
        node2.Should().NotBeNull();

        node1!.OutputPorts.Should().NotBeEmpty();
        node2!.InputPorts.Should().NotBeEmpty();

        editorVm.CreateConnection(node1.OutputPorts[0], node2.InputPorts[0]);
        editorVm.Connections.Should().HaveCount(1);

        // Add annotation & group
        editorVm.AddAnnotation(new Point(100, 350), "Nota de Prueba", "Contenido");
        editorVm.AddGroup(new Point(50, 50), "Grupo de prueba", 800, 400);

        // Export and reload
        var exportedGraph = editorVm.ExportToGraphModel("Test Graph");
        editorVm.LoadFromGraphModel(exportedGraph);

        editorVm.Nodes.Should().HaveCount(2);
        editorVm.Connections.Should().HaveCount(1);
        editorVm.Annotations.Should().HaveCount(1);
        editorVm.Groups.Should().HaveCount(1);
    }

    [Fact]
    public void Node_UpdateWidth_ShouldClampAndNotify()
    {
        var pluginLoader = CreateLoader();
        var editorVm = new EditorViewModel(pluginLoader);

        var node = editorVm.AddNode("FolderSourceNode", new Point(100, 100));
        node.Should().NotBeNull();

        double initialWidth = node!.Width;
        initialWidth.Should().BeGreaterThan(0);

        // Update width within limits
        node.UpdateWidth(350);
        node.Width.Should().Be(350);

        // Test lower bound clamp (180)
        node.UpdateWidth(50);
        node.Width.Should().Be(180);

        // Test upper bound clamp (600)
        node.UpdateWidth(1000);
        node.Width.Should().Be(600);
    }

    [Fact]
    public void Ports_ConnectionState_ShouldUpdateCorrectlyOnConnectAndDisconnect()
    {
        var pluginLoader = CreateLoader();
        var editorVm = new EditorViewModel(pluginLoader);

        var node1 = editorVm.AddNode("FolderSourceNode", new Point(100, 100));
        var node2 = editorVm.AddNode("DestinationSinkNode", new Point(500, 100));

        var outPort = node1!.OutputPorts[0];
        var inPort = node2!.InputPorts[0];

        outPort.IsConnected.Should().BeFalse();
        inPort.IsConnected.Should().BeFalse();

        // Connect
        editorVm.CreateConnection(outPort, inPort);
        outPort.IsConnected.Should().BeTrue();
        inPort.IsConnected.Should().BeTrue();

        // Disconnect
        editorVm.DisconnectConnector(outPort);
        outPort.IsConnected.Should().BeFalse();
        inPort.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void NodifyConnectionCommands_ShouldHandleValueTuplesAndDirectPorts()
    {
        var pluginLoader = CreateLoader();
        var editorVm = new EditorViewModel(pluginLoader);

        var node1 = editorVm.AddNode("FolderSourceNode", new Point(100, 100));
        var node2 = editorVm.AddNode("DestinationSinkNode", new Point(500, 100));

        var outPort = node1!.OutputPorts[0];
        var inPort = node2!.InputPorts[0];

        // 1. Start Connection via direct port
        editorVm.StartConnection(outPort);
        editorVm.PendingConnection.Should().NotBeNull();
        editorVm.PendingConnection!.Source.Should().Be(outPort);

        // 2. Finish Connection via Nodify ValueTuple (Source, Target)
        editorVm.FinishConnection((outPort, inPort));
        editorVm.Connections.Should().HaveCount(1);
        editorVm.PendingConnection.Should().BeNull();
        outPort.IsConnected.Should().BeTrue();
        inPort.IsConnected.Should().BeTrue();

        // 3. Disconnect via ValueTuple
        editorVm.DisconnectConnector((outPort, inPort));
        editorVm.Connections.Should().BeEmpty();
        outPort.IsConnected.Should().BeFalse();
        inPort.IsConnected.Should().BeFalse();

        // 4. Start & Finish Connection via Tuple with only Target (using PendingConnection.Source)
        editorVm.StartConnection(outPort);
        editorVm.FinishConnection(inPort);
        editorVm.Connections.Should().HaveCount(1);
        outPort.IsConnected.Should().BeTrue();
        inPort.IsConnected.Should().BeTrue();
    }

    [Fact]
    public void PendingConnection_WhenStarted_ShouldInitializeTargetLocationToSourceAnchor()
    {
        var editorVm = new EditorViewModel(CreateLoader());
        var node1 = editorVm.AddNode("FolderSourceNode", new Point(200, 300));
        var outPort = node1!.OutputPorts[0];
        outPort.Anchor = new Point(350, 320);

        // Act - Start connection from outPort
        editorVm.StartConnection(outPort);

        // Assert
        editorVm.PendingConnection.Should().NotBeNull();
        editorVm.PendingConnection!.Source.Should().Be(outPort);
        editorVm.PendingConnection.TargetLocation.Should().Be(new Point(350, 320));
        editorVm.PendingConnection.IsVisible.Should().BeTrue();

        // Simulate moving cursor across canvas
        editorVm.PendingConnection.TargetLocation = new Point(480, 520);
        editorVm.PendingConnection.TargetLocation.Should().Be(new Point(480, 520));
    }

    [Fact]
    public void RepeatedConnectionDrag_ShouldUpdatePendingConnectionState_AndAllowSubsequentConnections()
    {
        var editorVm = new EditorViewModel(CreateLoader());
        var node1 = editorVm.AddNode("FolderSourceNode", new Point(100, 100))!;
        var node2 = editorVm.AddNode("DestinationSinkNode", new Point(500, 100))!;
        var outPort = node1.OutputPorts[0];
        var inPort = node2.InputPorts[0];
        outPort.Anchor = new Point(250, 150);
        inPort.Anchor = new Point(500, 150);

        // 1st Drag Attempt
        editorVm.StartConnection(outPort);
        editorVm.PendingConnection.Should().NotBeNull();
        editorVm.PendingConnection!.Source.Should().Be(outPort);
        editorVm.PendingConnection.TargetLocation.Should().Be(new Point(250, 150));
        editorVm.PendingConnection.TargetLocation = new Point(300, 200);

        // Finish 1st
        editorVm.FinishConnection((outPort, inPort));
        editorVm.PendingConnection.Should().BeNull();
        editorVm.Connections.Should().HaveCount(1);
        outPort.IsConnected.Should().BeTrue();
        inPort.IsConnected.Should().BeTrue();

        // 2nd Drag Attempt (dragging from output port to start new branch/connection)
        editorVm.StartConnection(outPort);
        editorVm.PendingConnection.Should().NotBeNull();
        editorVm.PendingConnection!.Source.Should().Be(outPort);
        editorVm.PendingConnection.TargetLocation.Should().Be(new Point(250, 150));

        // Move cursor in 2nd attempt
        editorVm.PendingConnection.TargetLocation = new Point(400, 300);
        editorVm.PendingConnection.TargetLocation.Should().Be(new Point(400, 300));

        // Cancel 2nd attempt (e.g. user drops on empty canvas)
        editorVm.CancelConnection();
        editorVm.PendingConnection.Should().BeNull();

        // 3rd Drag Attempt (start connection from an input port)
        editorVm.StartConnection(inPort);
        editorVm.PendingConnection.Should().NotBeNull();
        editorVm.PendingConnection!.Source.Should().Be(inPort);
        editorVm.PendingConnection.TargetLocation.Should().Be(new Point(500, 150));
    }

    [Fact]
    public void MovingNode_ShouldUpdatePortAnchor_AndAffectConnections()
    {
        var editorVm = new EditorViewModel(CreateLoader());
        var node1 = editorVm.AddNode("FolderSourceNode", new Point(100, 100))!;
        var node2 = editorVm.AddNode("DestinationSinkNode", new Point(500, 100))!;
        var outPort = node1.OutputPorts[0];
        var inPort = node2.InputPorts[0];
        outPort.Anchor = new Point(250, 150);
        inPort.Anchor = new Point(500, 150);

        editorVm.CreateConnection(outPort, inPort);
        editorVm.Connections.Should().HaveCount(1);

        var conn = editorVm.Connections[0];
        conn.Source.Anchor.Should().Be(new Point(250, 150));
        conn.Target.Anchor.Should().Be(new Point(500, 150));

        // Simulate moving node1
        node1.Location = new Point(200, 200);
        outPort.Anchor = new Point(350, 250);

        conn.Source.Anchor.Should().Be(new Point(350, 250));
        conn.Target.Anchor.Should().Be(new Point(500, 150));
    }

    [Fact]
    public void PendingConnectionViewModel_InitialState_MatchesExpectedProperties()
    {
        var pendingVm = new PendingConnectionViewModel();
        pendingVm.IsVisible.Should().BeTrue();
        pendingVm.Source.Should().BeNull();
        pendingVm.Target.Should().BeNull();
    }

    [Fact]
    public void PendingConnection_WhenGivenSource_SetsTargetLocationToSourceAnchor()
    {
        var editorVm = new EditorViewModel(CreateLoader());
        var node = editorVm.AddNode("FolderSourceNode", new Point(100, 100))!;
        var port = node.OutputPorts[0];
        port.Anchor = new Point(120, 240);

        var pendingVm = new PendingConnectionViewModel(port);

        pendingVm.Source.Should().Be(port);
        pendingVm.TargetLocation.Should().Be(new Point(120, 240));
        pendingVm.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void PendingConnection_TestSplineTemplate()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var editorVm = new EditorViewModel(CreateLoader());
            var node1 = editorVm.AddNode("FolderSourceNode", new Point(100, 100))!;
            var outPort = node1.OutputPorts[0];
            outPort.Anchor = new Point(250, 150);

            editorVm.StartConnection(outPort);
            editorVm.PendingConnection.Should().NotBeNull();
            editorVm.PendingConnection!.Source.Should().Be(outPort);

            var pc = new Nodify.Avalonia.Connections.PendingConnection
            {
                DataContext = editorVm.PendingConnection,
                Source = editorVm.PendingConnection.Source,
                SourceAnchor = outPort.Anchor,
                TargetAnchor = new Point(400, 300)
            };

            pc.SourceAnchor.Should().Be(new Point(250, 150));
            pc.TargetAnchor.Should().Be(new Point(400, 300));
        });
    }

    [Fact]
    public void FlowPendingConnection_WhenSourceAnchorAssigned_InitializesTargetAnchorToSourceAnchor()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var fpc = new FlowPendingConnection();
            fpc.TargetAnchor.Should().Be(default(Point));

            // Setting SourceAnchor should immediately initialize TargetAnchor to the same point,
            // preventing the wire from pointing to (0,0) before mouse movement.
            fpc.SourceAnchor = new Point(250, 180);
            fpc.TargetAnchor.Should().Be(new Point(250, 180));
        });
    }

    [Fact]
    public void FlowPendingConnection_SuccessiveDrags_FollowCursorCorrectly()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var editorVm = new EditorViewModel(CreateLoader());
            var node1 = editorVm.AddNode("FolderSourceNode", new Point(100, 100))!;
            var node2 = editorVm.AddNode("ExpressionFilterNode", new Point(500, 100))!;
            var outPort = node1.OutputPorts[0];
            var inPort = node2.InputPorts[0];
            outPort.Anchor = new Point(250, 150);
            inPort.Anchor = new Point(500, 150);

            // Drag 1
            editorVm.StartConnection(outPort);
            var fpc1 = new FlowPendingConnection
            {
                DataContext = editorVm.PendingConnection,
                Source = editorVm.PendingConnection!.Source,
                SourceAnchor = outPort.Anchor
            };
            fpc1.TargetAnchor.Should().Be(outPort.Anchor);

            // Simulate drag movement
            var dragArgs1 = new Nodify.Avalonia.Events.PendingConnectionEventArgs(outPort)
            {
                RoutedEvent = Nodify.Avalonia.Connections.Connector.PendingConnectionDragEvent,
                Anchor = new Point(300, 220),
                OffsetX = 50,
                OffsetY = 70
            };
            // Target Anchor updates
            fpc1.TargetAnchor = new Point(dragArgs1.Anchor.X + dragArgs1.OffsetX, dragArgs1.Anchor.Y + dragArgs1.OffsetY);
            fpc1.TargetAnchor.Should().Be(new Point(350, 290));

            // Drag 1 Completes
            editorVm.FinishConnection((outPort, inPort));
            editorVm.PendingConnection.Should().BeNull();

            // Drag 2 (Subsequent attempt)
            editorVm.StartConnection(outPort);
            editorVm.PendingConnection.Should().NotBeNull();
            var fpc2 = new FlowPendingConnection
            {
                DataContext = editorVm.PendingConnection,
                Source = editorVm.PendingConnection!.Source,
                SourceAnchor = outPort.Anchor
            };
            // Initial position MUST be source anchor, NOT (0,0)
            fpc2.TargetAnchor.Should().Be(outPort.Anchor);

            // Subsequent drag movement MUST update target anchor
            fpc2.TargetAnchor = new Point(420, 310);
            fpc2.TargetAnchor.Should().Be(new Point(420, 310));
            fpc2.TargetAnchor.Should().NotBe(default(Point));

            // Drag 2 Cancels
            editorVm.CancelConnection();
            editorVm.PendingConnection.Should().BeNull();
        });
    }

    [Fact]
    public void ContextMenu_OnNodeCard_ShouldHaveCommands_AndExecuteProperly()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var pluginLoader = CreateLoader();
            var editorVm = new EditorViewModel(pluginLoader);
            var node1 = editorVm.AddNode("FolderSourceNode", new Point(100, 100))!;
            var node2 = editorVm.AddNode("ExpressionFilterNode", new Point(400, 100))!;
            editorVm.CreateConnection(node1.OutputPorts[0], node2.InputPorts[0]);

            var editorView = new EditorView { DataContext = editorVm };
            var window = new Window { Content = editorView, Width = 1000, Height = 800 };
            window.Show();

            var nodeCard = editorView.GetVisualDescendants().OfType<NodeCardView>().FirstOrDefault();
            nodeCard.Should().NotBeNull();
            nodeCard!.ContextMenu.Should().NotBeNull();
            nodeCard.ContextMenu!.Items.Should().NotBeEmpty();

            // Test Rename command
            node1.StartRenamingCommand.Execute(null);
            node1.IsEditingTitle.Should().BeTrue();
            node1.CancelTitleRenameCommand.Execute(null);
            node1.IsEditingTitle.Should().BeFalse();

            // Test Copy and Cut commands
            node1.CopyCommand.Execute(null);
            node1.CutCommand.Execute(null);

            // Test Delete command
            node1.DeleteCommand.Execute(null);
            editorVm.Nodes.Should().NotContain(node1);
            editorVm.Connections.Should().BeEmpty("borrar el nodo debe limpiar sus conexiones asociadas");

            window.Close();
        });
    }

    /// <summary>
    /// El aviso del lienzo —lo que pegar o duplicar no pudo reconstruir— tiene que existir de verdad. Una
    /// ruta de binding mal escrita no rompe ninguna prueba de view model: el aviso se pone y no se ve nada.
    /// Este es el único sitio donde se puede comprobar, y por eso mira la superficie y no el modelo.
    /// </summary>
    [Fact]
    public void CanvasNoticeBanner_ShouldBeWiredToTheEditorNotice()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var editorVm = new EditorViewModel(CreateLoader());
            var editorView = new EditorView { DataContext = editorVm };
            var window = new Window { Content = editorView, Width = 1000, Height = 800 };
            window.Show();

            var banner = editorView.GetVisualDescendants().OfType<Border>()
                .FirstOrDefault(border => border.Name == "CanvasNoticeBanner");
            banner.Should().NotBeNull("el lienzo tiene que tener dónde contar lo que una acción dejó a medias");
            banner!.IsVisible.Should().BeFalse(
                "un lienzo sin nada que contar no lleva cartel: si sale aquí, la visibilidad no está atada al aviso "
                + "—una ruta de binding que no resuelve deja el valor por defecto, que es visible—");

            editorVm.CanvasNotice = "🔌 no se pudo reconstruir el cable";
            window.UpdateLayout();

            banner.IsVisible.Should().BeTrue("el aviso se ve en cuanto el editor tiene algo que contar");
            banner.Bounds.Width.Should().BeGreaterThan(0, "y ocupa sitio: un aviso invisible no avisa");

            var node = editorVm.AddNode("FolderSourceNode", new Point(100, 100))!;
            var fixWithProposal = AFixFor(node, proposedPort: "Alternates");
            var fixWithoutProposal = AFixFor(node, proposedPort: null);
            editorVm.CanvasNoticeFixes.Add(fixWithProposal);
            editorVm.CanvasNoticeFixes.Add(fixWithoutProposal);
            window.UpdateLayout();

            var buttons = banner.GetVisualDescendants().OfType<Button>().ToList();
            buttons.Should().Contain(button => ReferenceEquals(button.Command, editorVm.DismissCanvasNoticeCommand),
                "el botón del cartel descarta el aviso de este lienzo");
            buttons.Should().Contain(button => ReferenceEquals(button.Command, fixWithProposal.GoToNodeCommand),
                "cada cable perdido lleva al nodo cuyo puerto falta");
            buttons.Should().Contain(button => ReferenceEquals(button.Command, fixWithProposal.ReconnectCommand),
                "y ofrece la reconexión al puerto que más se le parece");
            buttons.Single(button => ReferenceEquals(button.Command, fixWithProposal.ReconnectCommand))
                .IsVisible.Should().BeTrue();

            // La fila sin propuesta no enseña el botón: un botón que no puede cumplir es un botón muerto.
            buttons.Single(button => ReferenceEquals(button.Command, fixWithoutProposal.ReconnectCommand))
                .IsVisible.Should().BeFalse("sin puerto al que reconectar, el botón no se muestra");
            buttons.Single(button => ReferenceEquals(button.Command, fixWithoutProposal.GoToNodeCommand))
                .IsVisible.Should().BeTrue("ir al nodo siempre se puede");

            editorVm.DismissCanvasNoticeCommand.Execute(null);
            window.UpdateLayout();

            banner.IsVisible.Should().BeFalse("descartarlo lo retira");
            editorVm.HasCanvasNoticeFixes.Should().BeFalse("y se lleva sus filas: el aviso y su arreglo van juntos");

            window.Close();
        });
    }

    /// <summary>
    /// El resumen de lo que quedó sin reconstruir también tiene que verse en la <b>barra de estado</b>: el aviso
    /// del lienzo cuenta lo mismo, pero sólo mientras ese lienzo se esté mirando, y la barra deja constancia
    /// desde cualquier sitio. Una ruta de binding mal escrita no rompe ninguna prueba de view model —el resumen
    /// se calcula y no se enseña—, así que hay que mirar la superficie.
    /// </summary>
    [Fact]
    public void StatusBarLossPill_ShouldBeWiredToWhatTheCanvasCouldNotRebuild()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var log = new LogViewModel(new InMemoryLogStore());
            var editorVm = new EditorViewModel(CreateLoader(), logViewModel: log);
            var controlBar = new ControlBarViewModel(
                editorVm,
                CreateLoader(),
                log,
                new NodeInspectorViewModel(editorVm, new NullFileDialogService(), log),
                new NullFileDialogService(),
                new InMemoryWorkflowStorageService());
            var statusBar = new StatusBarViewModel(editorVm, controlBar, new FrozenPerformanceMonitor(), log);

            var statusBarView = new StatusBarView { DataContext = statusBar };
            var window = new Window { Content = statusBarView, Width = 1400, Height = 60 };
            window.Show();

            var pill = statusBarView.GetVisualDescendants().OfType<Border>()
                .FirstOrDefault(border => border.Name == "UnrebuiltConnectionsPill");
            pill.Should().NotBeNull("la barra de estado tiene que contar lo que el grafo perdió");
            pill!.IsVisible.Should().BeFalse(
                "un grafo entero no lleva resumen: si sale aquí, la visibilidad no está atada —una ruta de binding "
                + "que no resuelve deja el valor por defecto, que es visible—");

            // Un texto que no puede estar escrito en el XAML: si la píldora enseñara una frase fija, la prueba
            // no lo notaría con un texto que se le parezca.
            statusBar.UnrebuiltConnectionsCount = 7;
            statusBar.UnrebuiltConnectionsText = "🔌 7 conexiones perdidas";
            statusBar.HasUnrebuiltConnections = true;
            window.UpdateLayout();

            pill.IsVisible.Should().BeTrue("el resumen se ve en cuanto al grafo le faltan cables");
            pill.Bounds.Width.Should().BeGreaterThan(0, "y ocupa sitio: un resumen invisible no resume");
            pill.GetVisualDescendants().OfType<TextBlock>()
                .Should().Contain(text => text.Text == "🔌 7 conexiones perdidas",
                    "el texto de la píldora es el del editor, no una frase escrita en el XAML");

            window.Close();
        });
    }

    /// <summary>Una fila del aviso, montada como la monta el lienzo, para poder mirar sus dos botones.</summary>
    private static DroppedConnectionFixViewModel AFixFor(NodeViewModel node, string? proposedPort) => new(
        node,
        "Alternate",
        "origen(Out) → Origen(Alternate)",
        proposedPort,
        _ => { },
        _ => { },
        FileFlow.Sdk.Localization.LocalizationManager.Instance)
    {
        CanReconnect = proposedPort != null
    };

    [Fact]
    public void ContextMenu_OnConnection_ShouldHaveDeleteCommand_AndExecuteProperly()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var pluginLoader = CreateLoader();
            var editorVm = new EditorViewModel(pluginLoader);
            var node1 = editorVm.AddNode("FolderSourceNode", new Point(100, 100))!;
            var node2 = editorVm.AddNode("ExpressionFilterNode", new Point(400, 100))!;
            editorVm.CreateConnection(node1.OutputPorts[0], node2.InputPorts[0]);
            var conn = editorVm.Connections[0];

            var editorView = new EditorView { DataContext = editorVm };
            var window = new Window { Content = editorView, Width = 1000, Height = 800 };
            window.Show();

            var connectionVisual = editorView.GetVisualDescendants().OfType<Nodify.Avalonia.Connections.Connection>().FirstOrDefault();
            connectionVisual.Should().NotBeNull();
            connectionVisual!.ContextMenu.Should().NotBeNull();

            // Execute DeleteCommand directly on connection VM
            conn.DeleteCommand.Execute(null);
            editorVm.Connections.Should().BeEmpty("ejecutar DeleteCommand en ConnectionViewModel debe eliminar la conexión del editor");

            window.Close();
        });
    }
}












