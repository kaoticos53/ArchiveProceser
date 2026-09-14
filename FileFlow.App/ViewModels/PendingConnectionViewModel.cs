using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FileFlow.App.ViewModels;

public partial class PendingConnectionViewModel : ObservableObject
{
    [ObservableProperty]
    private PortViewModel? _source;

    [ObservableProperty]
    private Point _targetLocation;

    public PendingConnectionViewModel(PortViewModel source)
    {
        _source = source;
    }
}
