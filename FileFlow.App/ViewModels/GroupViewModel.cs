using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FileFlow.App.ViewModels;

/// <summary>
/// Representa un marco o grupo visual en el lienzo para organizar conjuntos de nodos.
/// </summary>
public partial class GroupViewModel : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public EditorViewModel? ParentEditor { get; set; }

    [ObservableProperty]
    private string _title = "Grupo de Nodos";

    [ObservableProperty]
    private Point _location = new(100, 100);

    [ObservableProperty]
    private double _width = 450;

    [ObservableProperty]
    private double _height = 320;

    [ObservableProperty]
    private string _color = "#3B82F6"; // Azul por defecto

    [ObservableProperty]
    private bool _isSelected;

    public ObservableCollection<string> NodeIds { get; } = [];

    public GroupViewModel()
    {
    }

    public GroupViewModel(string title, Point location, double width = 450, double height = 320, string color = "#3B82F6", IEnumerable<string>? nodeIds = null)
    {
        _title = title;
        _location = location;
        _width = width;
        _height = height;
        _color = color;

        if (nodeIds != null)
        {
            foreach (var id in nodeIds)
            {
                NodeIds.Add(id);
            }
        }
    }

    [RelayCommand]
    public void ChangeColor(string hexColor)
    {
        if (!string.IsNullOrWhiteSpace(hexColor))
        {
            Color = hexColor;
        }
    }

    [RelayCommand]
    public void Delete()
    {
        ParentEditor?.DeleteGroup(this);
    }
}
