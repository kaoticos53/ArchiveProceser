using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.Sdk;

namespace FileFlow.App.ViewModels;

/// <summary>
/// ViewModel que envuelve una acción personalizada declarada por el plugin.
/// </summary>
public sealed partial class NodeActionViewModel : ObservableObject
{
    private readonly NodeViewModel _nodeOwner;

    public NodeActionDescriptor Descriptor { get; }
    public string ActionId => Descriptor.ActionId;
    public string Title => Descriptor.Title;
    public string Icon => Descriptor.Icon;
    public string? Tooltip => Descriptor.Tooltip;

    public NodeActionViewModel(NodeActionDescriptor descriptor, NodeViewModel nodeOwner)
    {
        Descriptor = descriptor;
        _nodeOwner = nodeOwner;
    }

    [RelayCommand]
    public void Execute()
    {
        _nodeOwner.ExecuteCustomAction(ActionId);
    }
}
