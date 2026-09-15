using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FileFlow.App.ViewModels;

public partial class PendingConnectionViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WireColor))]
    private PortViewModel? _source;

    [ObservableProperty]
    private PortViewModel? _target;

    [ObservableProperty]
    private Point _targetLocation;

    [ObservableProperty]
    private bool _isVisible = true;

    public string WireColor => Source?.PortColor ?? "#818CF8";

    public PendingConnectionViewModel(PortViewModel? source = null)
    {
        _source = source;
    }
}
