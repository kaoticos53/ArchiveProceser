using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Views.Components;

public partial class AnnotationCardView : UserControl
{
    public AnnotationCardView()
    {
        InitializeComponent();
    }

    private void HeaderThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (DataContext is AnnotationViewModel vm)
        {
            vm.Location = new System.Windows.Point(vm.Location.X + e.HorizontalChange, vm.Location.Y + e.VerticalChange);
        }
    }

    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (DataContext is AnnotationViewModel vm)
        {
            double newWidth = vm.Width + e.HorizontalChange;
            double newHeight = vm.Height + e.VerticalChange;

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
