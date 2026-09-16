using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FileFlow.App.ViewModels;

public partial class PendingConnectionViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WireTypeClasses))]
    private PortViewModel? _source;

    [ObservableProperty]
    private PortViewModel? _target;

    [ObservableProperty]
    private Point _targetLocation;

    [ObservableProperty]
    private bool _isVisible = true;

    /// <summary>Color del cable en curso, por familia de tipo del puerto de origen (tokens del tema).</summary>
    public string WireTypeClasses => "pending " + (Source?.TypeKind switch
    {
        PortTypeKind.Files => "wireFiles",
        PortTypeKind.Text => "wireText",
        PortTypeKind.Boolean => "wireBoolean",
        PortTypeKind.Number => "wireNumber",
        PortTypeKind.Binary => "wireBinary",
        PortTypeKind.Collection => "wireCollection",
        _ => "wireAny"
    });

    public PendingConnectionViewModel(PortViewModel? source = null)
    {
        _source = source;
    }
}
