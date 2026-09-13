using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Views.Components;

public partial class AnnotationCardView : UserControl
{
    public AnnotationCardView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void HeaderThumb_DragDelta(object? sender, VectorEventArgs e)
    {
        if (DataContext is AnnotationViewModel vm)
        {
            vm.Location = new Point(vm.Location.X + e.Vector.X, vm.Location.Y + e.Vector.Y);
        }
    }

    private void ResizeThumb_DragDelta(object? sender, VectorEventArgs e)
    {
        if (DataContext is AnnotationViewModel vm)
        {
            double newWidth = vm.Width + e.Vector.X;
            double newHeight = vm.Height + e.Vector.Y;

            if (newWidth >= MinWidth && newWidth <= MaxWidth)
            {
                vm.Width = newWidth;
            }

            if (newHeight >= MinHeight && newHeight <= MaxHeight)
            {
                vm.Height = newHeight;
            }
        }
    }
}
