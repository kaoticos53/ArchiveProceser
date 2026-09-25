using System;
using Avalonia.Controls;
using Avalonia.Media;
using FileFlow.Sdk.Services;

namespace FileFlow.App.Services;

/// <summary>
/// Pinta un <see cref="PopupMenuDescriptor"/> con el <see cref="ContextMenu"/> nativo de Avalonia.
/// El ViewModel describió el menú con datos; este servicio lo traduce a controles del framework.
/// </summary>
public sealed class AvaloniaPopupMenuService : IPopupMenuService
{
    public void ShowMenu(PopupMenuDescriptor descriptor, object? anchor = null)
    {
        if (anchor is not Control control)
        {
            return;
        }

        var cm = new ContextMenu { MaxHeight = 500 };
        foreach (var item in descriptor.Items)
        {
            cm.Items.Add(ToMenuItem(item));
        }
        cm.Open(control);
    }

    private static Control ToMenuItem(PopupMenuItem item)
    {
        if (item.Items is { Count: > 0 })
        {
            var subMenu = new MenuItem { Header = item.Header, FontWeight = item.IsBold ? FontWeight.SemiBold : FontWeight.Normal };
            foreach (var child in item.Items)
            {
                subMenu.Items.Add(ToMenuItem(child));
            }
            return subMenu;
        }

        var mi = new MenuItem
        {
            Header = item.Header,
            FontWeight = item.IsBold ? FontWeight.Bold : FontWeight.Normal,
            Command = item.Command,
            CommandParameter = item.CommandParameter,
        };
        if (!string.IsNullOrEmpty(item.ToolTip))
        {
            ToolTip.SetTip(mi, item.ToolTip);
        }
        return mi;
    }
}
