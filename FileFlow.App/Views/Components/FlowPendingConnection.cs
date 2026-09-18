using System;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Nodify.Avalonia;
using Nodify.Avalonia.Connections;
using Nodify.Avalonia.Events;

namespace FileFlow.App.Views.Components;

/// <summary>
/// Control de conexión en curso con ciclo de vida robusto en Avalonia:
/// 1. Elimina sus manejadores enrutados de <see cref="NodifyEditor"/> al desconectarse del árbol visual,
///    evitando que instancias huérfanas de arrastres anteriores intercepten <see cref="Connector.PendingConnectionDragEvent"/>.
/// 2. Inicializa inmediatamente <see cref="PendingConnection.TargetAnchor"/> con la posición de <see cref="PendingConnection.SourceAnchor"/>
///    al cargarse o al recibir el puerto origen, eliminando el salto inicial a (0,0).
/// 3. Re-suscribe el arrastre con <c>handledEventsToo: true</c> para garantizar que la línea siga al cursor
///    en todos los arrastres sucesivos.
/// </summary>
public class FlowPendingConnection : PendingConnection
{
    private NodifyEditor? _subscribedEditor;

    protected override Type StyleKeyOverride => typeof(PendingConnection);

    static FlowPendingConnection()
    {
        SourceAnchorProperty.Changed.AddClassHandler<FlowPendingConnection>((x, e) => x.OnSourceAnchorChanged(e));
    }

    public FlowPendingConnection()
    {
    }

    private void OnSourceAnchorChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is Point p && p != default && TargetAnchor == default)
        {
            TargetAnchor = p;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _subscribedEditor = this.FindAncestorOfType<NodifyEditor>();

        if (TargetAnchor == default && SourceAnchor != default)
        {
            TargetAnchor = SourceAnchor;
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _subscribedEditor = this.FindAncestorOfType<NodifyEditor>();

        if (_subscribedEditor != null)
        {
            // Re-suscribir OnPendingConnectionDrag con handledEventsToo: true
            // para ser inmune a cualquier handler que haya podido marcar Handled el evento de arrastre.
            _subscribedEditor.RemoveHandler(Connector.PendingConnectionDragEvent, new PendingConnectionEventHandler(OnPendingConnectionDrag));
            _subscribedEditor.AddHandler(Connector.PendingConnectionDragEvent, new PendingConnectionEventHandler(OnPendingConnectionDrag), RoutingStrategies.Bubble, handledEventsToo: true);
        }

        if (TargetAnchor == default && SourceAnchor != default)
        {
            TargetAnchor = SourceAnchor;
        }
    }

    protected override void OnPendingConnectionDrag(object? sender, PendingConnectionEventArgs e)
    {
        if (!IsVisible)
        {
            return;
        }

        // Si el evento venía marcado como Handled por algún handler fantasma previo,
        // lo desmarcamos para que la clase base recalcule TargetAnchor y aplique snapping.
        var wasHandled = e.Handled;
        if (wasHandled)
        {
            e.Handled = false;
        }

        base.OnPendingConnectionDrag(sender ?? this, e);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        UnsubscribeFromEditor();
        IsVisible = false;
    }

    private void UnsubscribeFromEditor()
    {
        var editor = _subscribedEditor ?? this.FindAncestorOfType<NodifyEditor>();
        if (editor != null)
        {
            editor.RemoveHandler(Connector.PendingConnectionStartedEvent, new PendingConnectionEventHandler(OnPendingConnectionStarted));
            editor.RemoveHandler(Connector.PendingConnectionDragEvent, new PendingConnectionEventHandler(OnPendingConnectionDrag));
            editor.RemoveHandler(Connector.PendingConnectionCompletedEvent, new PendingConnectionEventHandler(OnPendingConnectionCompleted));

            var onKeyUpMethod = typeof(PendingConnection).GetMethod(
                "OnKeyUp",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                [typeof(object), typeof(KeyEventArgs)],
                null);
            if (onKeyUpMethod != null)
            {
                var onKeyUpDelegate = (EventHandler<KeyEventArgs>)Delegate.CreateDelegate(
                    typeof(EventHandler<KeyEventArgs>), this, onKeyUpMethod);
                editor.RemoveHandler(InputElement.KeyUpEvent, onKeyUpDelegate);
            }
        }
        _subscribedEditor = null;
    }
}
