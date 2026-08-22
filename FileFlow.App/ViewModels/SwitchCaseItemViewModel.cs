using CommunityToolkit.Mvvm.ComponentModel;

namespace FileFlow.App.ViewModels;

public partial class SwitchCaseItemViewModel : ObservableObject
{
    public NodeViewModel NodeOwner { get; }
    public PortViewModel? Port { get; set; }

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _pattern = string.Empty;

    public SwitchCaseItemViewModel(NodeViewModel owner, string name, string pattern)
    {
        NodeOwner = owner;
        _name = name;
        _pattern = pattern;
    }

    partial void OnNameChanged(string? oldValue, string newValue)
    {
        if (oldValue != null && oldValue != newValue)
        {
            NodeOwner.OnSwitchCaseRenamed(oldValue, newValue, this);
        }
    }

    partial void OnPatternChanged(string value)
    {
        NodeOwner.SyncSwitchCasesToNodeInstance();
    }
}
