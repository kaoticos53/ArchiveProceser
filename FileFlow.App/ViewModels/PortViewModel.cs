using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using FileFlow.Sdk;

namespace FileFlow.App.ViewModels;

public partial class PortViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private PortDirection _direction;

    [ObservableProperty]
    private Type _dataType = typeof(FileItemContext);

    [ObservableProperty]
    private Point _anchor;

    public NodeViewModel NodeOwner { get; }

    public string PortColor => GetColorForDataType(DataType);

    public PortViewModel(NodeViewModel owner, string name, string displayName, PortDirection direction, Type dataType)
    {
        NodeOwner = owner;
        _name = name;
        _displayName = displayName;
        _direction = direction;
        _dataType = dataType;
    }

    public static string GetColorForDataType(Type type)
    {
        if (type == typeof(FileItemContext)) return "#10B981"; // Emerald Green for File Context
        if (type == typeof(string)) return "#06B6D4";          // Cyan for String/Path
        if (type == typeof(bool)) return "#F59E0B";            // Amber for Boolean
        if (type == typeof(int) || type == typeof(long) || type == typeof(double)) return "#818CF8"; // Indigo for Numbers
        return "#A855F7";                                      // Purple Default
    }
}
