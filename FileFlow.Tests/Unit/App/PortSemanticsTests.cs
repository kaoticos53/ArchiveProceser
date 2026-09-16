using FileFlow.Tests.TestHelpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using FileFlow.App.ViewModels;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Semántica de los sockets del lienzo.
///
/// La forma y el color de cada socket comunican el tipo de dato que transporta y su estado, y la tarjeta
/// los decide con clases de estilo alimentadas por estas propiedades. Si un flag y la forma declarada se
/// desincronizan, el socket se dibuja con la forma de un tipo y el color de otro (o con dos formas a la
/// vez), y eso no lo detecta ningún compilador: de ahí estas guardias.
/// </summary>
[Collection("VisualSnapshots")]
public class PortSemanticsTests : IDisposable
{
    public void Dispose() => AvaloniaTestHelper.SetCultureOnUI("es-ES");

    // ─────────────────────────────────────────────────────────────────────────────
    // Clasificación de tipos
    // ─────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(typeof(FileItemContext), PortTypeKind.Files)]
    [InlineData(typeof(string), PortTypeKind.Text)]
    [InlineData(typeof(bool), PortTypeKind.Boolean)]
    [InlineData(typeof(int), PortTypeKind.Number)]
    [InlineData(typeof(double), PortTypeKind.Number)]
    [InlineData(typeof(byte[]), PortTypeKind.Binary)]
    [InlineData(typeof(MemoryStream), PortTypeKind.Binary)]
    [InlineData(typeof(List<FileItemContext>), PortTypeKind.Collection)]
    [InlineData(typeof(object), PortTypeKind.Any)]
    public void GetTypeKind_ShouldClassifyTheDataTypeFamily(Type dataType, PortTypeKind expected)
    {
        PortViewModel.GetTypeKind(dataType).Should().Be(expected);
    }

    [Theory]
    [InlineData(typeof(string), PortSocketShape.Circle)]
    [InlineData(typeof(FileItemContext), PortSocketShape.Square)]
    [InlineData(typeof(List<FileItemContext>), PortSocketShape.Square)]
    [InlineData(typeof(bool), PortSocketShape.Triangle)]
    [InlineData(typeof(int), PortSocketShape.Diamond)]
    [InlineData(typeof(object), PortSocketShape.Square)]
    public void GetShapeForDataType_ShouldMapTypeToSocketShape(Type dataType, PortSocketShape expected)
    {
        PortViewModel.GetShapeForDataType(dataType).Should().Be(expected);
    }

    [Fact]
    public void EverySocket_ShouldExposeExactlyOneShapeFlag()
    {
        // La plantilla del socket declara las cuatro formas como clases independientes: si dos flags
        // estuvieran activos a la vez, ganaría la última regla de estilo del fichero y la forma dejaría de
        // significar el tipo.
        foreach (var dataType in DataTypesToAudit())
        {
            var port = BuildPort(dataType, PortDirection.Input);

            int active = new[] { port.IsCircleSocket, port.IsSquareSocket, port.IsTriangleSocket, port.IsDiamondSocket }
                .Count(flag => flag);

            active.Should().Be(1, $"'{dataType.Name}' debe tener una única forma de socket, no {active}");
        }
    }

    [Fact]
    public void EverySocket_ShouldExposeExactlyOneTypeClass()
    {
        // Igual que con las formas: el color del socket y del cable salen de siete clases de tipo.
        foreach (var dataType in DataTypesToAudit())
        {
            var port = BuildPort(dataType, PortDirection.Input);

            int active = new[]
            {
                port.IsFilesType, port.IsTextType, port.IsBooleanType, port.IsNumberType,
                port.IsBinaryType, port.IsCollectionType, port.IsAnyType
            }.Count(flag => flag);

            active.Should().Be(1, $"'{dataType.Name}' debe pertenecer a una sola familia de tipo, no a {active}");
        }
    }

    [Fact]
    public void TypeKind_ShouldAgreeWithTheFlagsUsedByTheView()
    {
        foreach (var dataType in DataTypesToAudit())
        {
            var port = BuildPort(dataType, PortDirection.Output);

            bool matchesKind = port.TypeKind switch
            {
                PortTypeKind.Files => port.IsFilesType,
                PortTypeKind.Text => port.IsTextType,
                PortTypeKind.Boolean => port.IsBooleanType,
                PortTypeKind.Number => port.IsNumberType,
                PortTypeKind.Binary => port.IsBinaryType,
                PortTypeKind.Collection => port.IsCollectionType,
                _ => port.IsAnyType
            };

            matchesKind.Should().BeTrue($"el flag de clase debe corresponder al TypeKind {port.TypeKind}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Compatibilidad
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CanConnect_ShouldRequireOppositeDirectionsAndDifferentNodes()
    {
        var nodeA = BuildNode(typeof(string));
        var nodeB = BuildNode(typeof(string));
        var input = nodeA.InputPorts[0];
        var output = nodeB.OutputPorts[0];

        PortViewModel.CanConnect(output, input).Should().BeTrue("salida → entrada de otro nodo es válido");

        PortViewModel.CanConnect(input, input).Should().BeFalse("un puerto no puede conectarse consigo mismo");
        PortViewModel.CanConnect(output, output).Should().BeFalse("dos salidas no se pueden encadenar");
        PortViewModel.CanConnect(nodeA.InputPorts[0], nodeA.OutputPorts[0]).Should().BeFalse("un nodo no se conecta a sí mismo");
    }

    [Fact]
    public void AreTypesCompatible_ShouldTreatObjectAsWildcardAndRejectUnrelatedTypes()
    {
        var textOut = BuildNode(typeof(string)).OutputPorts[0];
        var textIn = BuildNode(typeof(string)).InputPorts[0];
        var numberIn = BuildNode(typeof(int)).InputPorts[0];
        var anyIn = BuildNode(typeof(object)).InputPorts[0];
        var fileIn = BuildNode(typeof(FileItemContext)).InputPorts[0];

        PortViewModel.AreTypesCompatible(textOut, textIn).Should().BeTrue("mismo tipo");
        PortViewModel.AreTypesCompatible(textOut, anyIn).Should().BeTrue("'object' acepta cualquier cosa");
        PortViewModel.AreTypesCompatible(textOut, numberIn).Should().BeFalse("texto no alimenta un puerto numérico");
        PortViewModel.AreTypesCompatible(textOut, fileIn).Should().BeFalse("texto no alimenta un puerto de contexto de archivo");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Resaltado de compatibilidad durante el arrastre
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ApplyDragHighlight_ShouldMarkSourceCompatibleAndDimmedPorts()
    {
        var sourceNode = BuildNode(typeof(FileItemContext));
        var source = sourceNode.OutputPorts[0];
        var compatible = BuildNode(typeof(FileItemContext)).InputPorts[0];

        // Segundo puerto del propio nodo origen: estructuralmente no es un destino válido.
        var otherInputOfSameNode = sourceNode.InputPorts[0];

        foreach (var port in new[] { source, compatible, otherInputOfSameNode })
        {
            port.IsDragActive = true;
            port.ApplyDragHighlight(source);
        }

        source.IsDragSource.Should().BeTrue();
        source.DragStateClasses.Should().Be("dragSource");

        compatible.IsConnectableDuringDrag.Should().BeTrue();
        compatible.IsTypeCompatibleDuringDrag.Should().BeTrue();
        compatible.IsTypeWarningDuringDrag.Should().BeFalse();
        compatible.DragStateClasses.Should().Be("compatible");
        compatible.IsDimmedDuringDrag.Should().BeFalse();

        // Destino estructuralmente posible pero perteneciente al mismo nodo que el origen: no se atenúa por
        // tipo, se descarta por estructura.
        otherInputOfSameNode.IsConnectableDuringDrag.Should().BeFalse();
        otherInputOfSameNode.IsDimmedDuringDrag.Should().BeTrue();
        otherInputOfSameNode.DragStateClasses.Should().Be("incompatible");
    }

    [Fact]
    public void ApplyDragHighlight_ShouldWarn_WhenTypesDifferButTheConnectionIsPossible()
    {
        var source = BuildNode(typeof(string)).OutputPorts[0];
        var numericTarget = BuildNode(typeof(int)).InputPorts[0];

        numericTarget.IsDragActive = true;
        numericTarget.ApplyDragHighlight(source);

        numericTarget.IsConnectableDuringDrag.Should().BeTrue("el motor puede validar/converir tipos en ejecución");
        numericTarget.IsTypeCompatibleDuringDrag.Should().BeFalse();
        numericTarget.IsTypeWarningDuringDrag.Should().BeTrue();
        numericTarget.DragStateClasses.Should().Be("compatibleWarning");
        numericTarget.IsDimmedDuringDrag.Should().BeFalse();
    }

    [Fact]
    public void ClearDragHighlight_ShouldReturnEveryPortToRest()
    {
        var source = BuildNode(typeof(string)).OutputPorts[0];
        var target = BuildNode(typeof(string)).InputPorts[0];

        target.IsDragActive = true;
        target.ApplyDragHighlight(source);
        target.ClearDragHighlight();

        target.IsDragActive.Should().BeFalse();
        target.IsDragSource.Should().BeFalse();
        target.IsConnectableDuringDrag.Should().BeFalse();
        target.IsTypeCompatibleDuringDrag.Should().BeFalse();
        target.IsTypeWarningDuringDrag.Should().BeFalse();
        target.IsDimmedDuringDrag.Should().BeFalse();
    }

    [Fact]
    public void DragHighlight_ShouldNotPersist_WhenTheCanvasIsNotDragging()
    {
        var source = BuildNode(typeof(string)).OutputPorts[0];
        var target = BuildNode(typeof(string)).InputPorts[0];

        target.ApplyDragHighlight(source);

        target.IsDimmedDuringDrag.Should().BeFalse("sin arrastre activo ningún puerto se atenúa");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Texto del socket (localizado)
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SocketToolTip_ShouldIncludeDirectionTypeAndStatus()
    {
        var port = BuildPort(typeof(FileItemContext), PortDirection.Input);

        port.SocketToolTip.Should().Contain("Entrada");
        port.SocketToolTip.Should().Contain(PortViewModel.GetTypeKindLabel(PortTypeKind.Files));
        port.SocketToolTip.Should().Contain("FileContext");
        port.SocketToolTip.Should().Contain(port.ConnectionStatusText);
    }

    [Fact]
    public void LanguageChange_ShouldRefreshTheVisiblePortTextsInPlace()
    {
        // El idioma se cambia en caliente: el texto del socket (tooltip y estado) no puede quedarse con el
        // idioma anterior, y no se puede recrear la tarjeta para conseguirlo.
        var port = BuildPort(typeof(bool), PortDirection.Output);
        var refreshed = new List<string?>();
        port.PropertyChanged += (_, e) => refreshed.Add(e.PropertyName);

        AvaloniaTestHelper.SetCultureOnUI("en-US");

        refreshed.Should().Contain(nameof(PortViewModel.SocketToolTip));
        refreshed.Should().Contain(nameof(PortViewModel.ConnectionStatusText));

        AvaloniaTestHelper.SetCultureOnUI("es-ES");
    }

    [Fact]
    public void UpdateConnectionState_ShouldComposeTheLocalizedStatus()
    {
        var port = BuildPort(typeof(string), PortDirection.Input);

        port.UpdateConnectionState(true, "Lector de CSV");
        port.IsConnected.Should().BeTrue();
        port.ConnectionStateClasses.Should().Be("connected");
        port.ConnectionStatusText.Should().Contain("Lector de CSV");
        port.ConnectionStatusIcon.Should().Be(Material.Icons.MaterialIconKind.CheckboxMarkedCircle);

        port.UpdateConnectionState(false);
        port.IsConnected.Should().BeFalse();
        port.ConnectionStateClasses.Should().Be("free");
        port.ConnectionStatusText.Should().NotContain("Lector de CSV");
        port.ConnectionStatusIcon.Should().Be(Material.Icons.MaterialIconKind.CircleOutline);
    }

    [Fact]
    public void TransmittedCountText_ShouldAgreeWithTheNumber()
    {
        var port = BuildPort(typeof(string), PortDirection.Input);

        port.TransmittedCount = 1;
        port.TransmittedCountText.Should().Contain("1");

        port.TransmittedCount = 7;
        port.TransmittedCountText.Should().Contain("7");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────────

    private static IEnumerable<Type> DataTypesToAudit() =>
    [
        typeof(FileItemContext), typeof(string), typeof(bool), typeof(int), typeof(double),
        typeof(byte[]), typeof(MemoryStream), typeof(List<FileItemContext>), typeof(object)
    ];

    private static PortViewModel BuildPort(Type dataType, PortDirection direction)
    {
        var node = BuildNode(dataType);
        return direction == PortDirection.Input ? node.InputPorts[0] : node.OutputPorts[0];
    }

    /// <summary>Nodo con un puerto de entrada y otro de salida del mismo tipo, como los nodos reales.</summary>
    private static NodeViewModel BuildNode(Type dataType)
    {
        var ports = new List<NodePort>
        {
            new("in", dataType, PortDirection.Input, "Entrada", "Descripción del puerto"),
            new("out", dataType, PortDirection.Output, "Salida", "Descripción del puerto")
        };

        return new NodeViewModel(new FakePortNode(ports), new Point(0, 0));
    }

    private sealed class FakePortNode : IFlowNode
    {
        private readonly IReadOnlyList<NodePort> _ports;

        public FakePortNode(IReadOnlyList<NodePort> ports) => _ports = ports;

        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name => "FakePortNode";
        public string Category => "Test";
        public string Description => "Nodo de prueba para puertos";
        public IReadOnlyList<NodePort> Inputs => _ports.Where(p => p.Direction == PortDirection.Input).ToList();
        public IReadOnlyList<NodePort> Outputs => _ports.Where(p => p.Direction == PortDirection.Output).ToList();
        public Dictionary<string, object?> Parameters { get; } = [];

        public Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
