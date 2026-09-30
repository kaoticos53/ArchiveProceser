using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.App.Core;
using FileFlow.App.ViewModels;
using FileFlow.Sdk;
using FileFlow.Sdk.Services;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// El BOTÓN DEL NODO que abre una ventana de la versión anterior (hito 270): la frontera del hito 268 se declara por
/// los diálogos de quien lo abrió, y quien los abre es <see cref="NodeViewModel.ExecuteCustomAction"/>.
///
/// <para><b>El defecto que guarda</b>: los siete nodos con ventana del toolkit declaran la frontera con
/// <c>(context as NodeCustomActionContext)?.Dialogs</c> y el contexto del botón del nodo se construía
/// <b>sin</b> el servicio de diálogos. La costura, el aviso y la traza existían; lo que faltaba era el
/// teléfono: el nodo llamaba a <c>UnavailableSurface.Declare(null, …)</c>, el aviso caía al nulo declarado del
/// Sdk y el usuario pulsaba un botón que <b>no hacía nada y no avisaba</b>. La guardia del 268 no lo veía
/// porque mide la costura con un doble puesto a mano; y el nodo tampoco, porque quien le pasa el contexto es
/// este view model.</para>
///
/// <para><b>Por qué se mide por comportamiento</b>: lo que hay que atar no es que el argumento esté escrito,
/// sino que el aviso <i>llegue</i> al servicio de diálogos del host. El nodo de prueba reproduce el contrato
/// de los siete de verdad (declarar la frontera con los diálogos del contexto) y la prueba lee lo que el host
/// recibió.</para>
/// </summary>
public class NodeActionFrontierWiringTests
{
    /// <summary>El nodo de prueba: declara su ventana como de la versión anterior, igual que los siete del producto.</summary>
    private sealed class UnavailableWindowProbeNode : IFlowNode, INodeCustomActionProvider
    {
        /// <summary>La última acción que se le pidió (la prueba comprueba que el botón llegó hasta aquí).</summary>
        public string? LastActionId { get; private set; }

        /// <summary>El contexto con el que se le pidió: de aquí sale el servicio que el botón le entregó.</summary>
        public NodeCustomActionContext? LastContext { get; private set; }

        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string Name => "Nodo con ventana de la versión anterior";

        public string Category => "Test";

        public string Description => "Nodo de prueba: su acción declara la frontera de las ventanas del toolkit";

        public IReadOnlyList<NodePort> Inputs => [];

        public IReadOnlyList<NodePort> Outputs => [];

        public Dictionary<string, object?> Parameters { get; } = [];

        public Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public void ExecuteCustomAction(string actionId, object? context = null)
        {
            LastActionId = actionId;
            LastContext = context as NodeCustomActionContext;

            // El contrato de los siete nodos con ventana del toolkit: sin toolkit la ventana no se construye y
            // la frontera se DECLARA por los diálogos de quien la abrió. Aquí se reproduce literal —el nodo de
            // prueba no compila la mitad del toolkit— para poder medir el camino entero desde el botón.
            UnavailableSurface.Declare(
                LastContext?.Dialogs,
                "Estudio de Renombrado",
                "Función no disponible",
                "«Estudio de Renombrado» no está disponible en este host.");
        }
    }

    [Fact]
    public void TheNodeActionButton_ShouldShowTheUnavailableSurfaceWarning_InTheHostDialogs()
    {
        var dialogs = new RecordingDialogService();
        IServiceProvider? previous = CoreDialogHost.Services;

        try
        {
            // El host ha levantado su contenedor (el arranque de los dos hosts hace esto): es la vía por la que
            // el núcleo portable resuelve el servicio de diálogos sin conocer la interfaz del host.
            CoreDialogHost.Services = new ServiceCollection()
                .AddSingleton<IDialogService>(dialogs)
                .BuildServiceProvider();

            using var node = new NodeViewModel(new UnavailableWindowProbeNode(), new Point(0, 0));

            node.ExecuteCustomAction("OpenRenameStudio");

            dialogs.WarningMessages.Should().ContainSingle(
                "el botón del nodo tiene que AVISAR de que esa ventana es de la versión anterior: un aviso que sólo sale "
                + "por la consola es, para el usuario, un botón que no hace nada")
                .Which.Should().Contain("Estudio de Renombrado",
                    "y el aviso nombra la ventana que falta: sin el nombre el usuario sabe que algo no pasó, "
                    + "pero no qué se perdió");
        }
        finally
        {
            CoreDialogHost.Services = previous;
        }
    }

    [Fact]
    public void TheNodeActionContext_ShouldCarryADialogService_EvenWithoutAHost()
    {
        // Sin contenedor (una prueba, un host sin ventana) el nodo NO puede recibir un nulo: la costura tiene
        // que poder llamar a un servicio y el nulo declarado del Sdk es el que no finge nada. Un `null` aquí es
        // exactamente el botón mudo que este tramo cierra.
        IServiceProvider? previous = CoreDialogHost.Services;

        try
        {
            CoreDialogHost.Services = null;
            var probe = new UnavailableWindowProbeNode();
            using var node = new NodeViewModel(probe, new Point(0, 0));

            node.ExecuteCustomAction("OpenRenameStudio");

            probe.LastActionId.Should().Be("OpenRenameStudio",
                "el botón del nodo llega a la acción que el nodo declara");
            probe.LastContext.Should().NotBeNull(
                "la acción del nodo se ejecuta con su contexto, no con un nulo suelto");
            probe.LastContext!.Dialogs.Should().NotBeNull(
                "el contexto lleva SIEMPRE un servicio de diálogos: sin él, la frontera de la versión anterior —y "
                + "cualquier aviso de la acción— se pierde en el nulo de la costura");
            probe.LastContext.Dialogs.Should().BeSameAs(NullDialogService.Instance,
                "sin host instalado, el servicio es el nulo declarado del Sdk, que no abre nada ni finge abrirlo");
        }
        finally
        {
            CoreDialogHost.Services = previous;
        }
    }

    [Fact]
    public void EveryNodeActionContext_ShouldCarryTheHostDialogService()
    {
        // El censo: la puerta del botón del nodo, la de la fila de parámetros y la de la superficie declarada
        // construyen el MISMO contexto. La que se olvide del servicio vuelve a tener un botón mudo, y sólo se
        // descubre pulsándolo —que es lo que pasó—: aquí se ve en el fuente.
        string code = SourceText.CodeWithoutComments("FileFlow.App.Core/ViewModels/NodeViewModel.cs")
                      + SourceText.CodeWithoutComments("FileFlow.App.Core/ViewModels/NodeParameterViewModel.cs")
                      + SourceText.CodeWithoutComments("FileFlow.App.Core/ViewModels/ControlBarViewModel.cs");

        var sites = Regex.Matches(code, @"new NodeCustomActionContext\s*\(")
            .Select(m => m.Index)
            .ToList();

        sites.Should().HaveCountGreaterThanOrEqualTo(4,
            "el censo mira de verdad las construcciones del contexto: con menos, estaría vigilando otra cosa");

        var blind = new List<string>();
        foreach (int start in sites)
        {
            int depth = 0;
            int end = start;
            for (int i = start; i < code.Length; i++)
            {
                if (code[i] == '(')
                {
                    depth++;
                }
                else if (code[i] == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        end = i;
                        break;
                    }
                }
            }

            // El nombre del servicio se escribe con minúscula en unos sitios (`_dialogService`) y con
            // mayúscula en otros (`CoreDialogHost.ResolveDialogService()`): el censo busca la palabra, no una
            // grafía, o el lint vigilaría el estilo y no el defecto.
            string arguments = code[start..end];
            if (!arguments.Contains("dialog", StringComparison.OrdinalIgnoreCase))
            {
                blind.Add(arguments.Replace('\n', ' ').Replace('\r', ' ').Trim());
            }
        }

        blind.Should().BeEmpty(
            "las órdenes del nodo avisan por los diálogos de quien las abrió (hito 268) y quien los abre es "
            + "quien construye el contexto: un contexto sin servicio deja la frontera en el nulo y el usuario "
            + "pulsa un botón que no hace nada");
    }
}
