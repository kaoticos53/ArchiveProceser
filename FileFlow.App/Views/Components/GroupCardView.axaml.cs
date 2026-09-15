using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Views.Components;

public partial class GroupCardView : UserControl
{
    public GroupCardView()
    {
        InitializeComponent();
    }

    private void HeaderThumb_DragDelta(object? sender, VectorEventArgs e)
    {
        if (DataContext is GroupViewModel vm)
        {
            double deltaX = e.Vector.X;
            double deltaY = e.Vector.Y;

            if (vm.ParentEditor != null)
            {
                double groupLeft = vm.Location.X;
                double groupTop = vm.Location.Y;
                double groupRight = vm.Location.X + vm.Width;
                double groupBottom = vm.Location.Y + vm.Height;

                var nodesToMove = new List<NodeViewModel>();
                var currentContainedIds = new List<string>();

                foreach (var node in vm.ParentEditor.Nodes)
                {
                    double nodeWidth = node.Width > 0 ? node.Width : 260;
                    double nodeCenterX = node.Location.X + (nodeWidth / 2.0);
                    double nodeCenterY = node.Location.Y + 35;

                    bool isInside = nodeCenterX >= groupLeft && nodeCenterX <= groupRight &&
                                    nodeCenterY >= groupTop && nodeCenterY <= groupBottom;

                    if (isInside)
                    {
                        nodesToMove.Add(node);
                        currentContainedIds.Add(node.Id);
                    }
                }

                vm.NodeIds.Clear();
                foreach (var id in currentContainedIds)
                {
                    vm.NodeIds.Add(id);
                }

                foreach (var node in nodesToMove)
                {
                    node.Location = new Point(node.Location.X + deltaX, node.Location.Y + deltaY);
                }
            }

            vm.Location = new Point(vm.Location.X + deltaX, vm.Location.Y + deltaY);
        }
    }

    private void ResizeThumb_DragDelta(object? sender, VectorEventArgs e)
    {
        if (DataContext is GroupViewModel vm)
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
