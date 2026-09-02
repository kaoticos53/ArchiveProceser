using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FileFlow.App.ViewModels;

/// <summary>
/// Representa una nota adhesiva / anotación visual en el lienzo del editor.
/// </summary>
public partial class AnnotationViewModel : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public EditorViewModel? ParentEditor { get; set; }

    [ObservableProperty]
    private string _title = "Nota";

    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private Point _location = new(100, 100);

    [ObservableProperty]
    private double _width = 250;

    [ObservableProperty]
    private double _height = 180;

    [ObservableProperty]
    private string _color = "#FEF08A"; // Amarillo pastel por defecto

    [ObservableProperty]
    private bool _isSelected;

    public AnnotationViewModel()
    {
    }

    public AnnotationViewModel(string title, string content, Point location, double width = 250, double height = 180, string color = "#FEF08A")
    {
        _title = title;
        _content = content;
        _location = location;
        _width = width;
        _height = height;
        _color = color;
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
        ParentEditor?.DeleteAnnotation(this);
    }
}
