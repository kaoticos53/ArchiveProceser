using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Views.Components;

public partial class GroupCardView : UserControl
{
    public GroupCardView()
    {
        InitializeComponent();
    }

    private void HeaderThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (DataContext is GroupViewModel vm)
        {
            double deltaX = e.HorizontalChange;
            double deltaY = e.VerticalChange;

            if (vm.ParentEditor != null)
            {
                // Límites estrictos del marco del grupo
                double groupLeft = vm.Location.X;
                double groupTop = vm.Location.Y;
                double groupRight = vm.Location.X + vm.Width;
                double groupBottom = vm.Location.Y + vm.Height;

                var nodesToMove = new List<NodeViewModel>();
                var currentContainedIds = new List<string>();

                foreach (var node in vm.ParentEditor.Nodes)
                {
                    // Punto de anclaje/centro del nodo
                    double nodeWidth = node.Width > 0 ? node.Width : 260;
                    double nodeCenterX = node.Location.X + (nodeWidth / 2.0);
                    double nodeCenterY = node.Location.Y + 35;

                    // El nodo se mueve si su centro está estrictamente dentro del marco del grupo
                    bool isInside = nodeCenterX >= groupLeft && nodeCenterX <= groupRight &&
                                    nodeCenterY >= groupTop && nodeCenterY <= groupBottom;

                    if (isInside)
                    {
                        nodesToMove.Add(node);
                        currentContainedIds.Add(node.Id);
                    }
                }

                // Sincronizar NodeIds dinámicamente con los nodos actualmente contenidos
                vm.NodeIds.Clear();
                foreach (var id in currentContainedIds)
                {
                    vm.NodeIds.Add(id);
                }

                // Mover los nodos que están dentro de forma coordinada
                foreach (var node in nodesToMove)
                {
                    node.Location = new System.Windows.Point(node.Location.X + deltaX, node.Location.Y + deltaY);
                }
            }

            // Mover el marco del grupo
            vm.Location = new System.Windows.Point(vm.Location.X + deltaX, vm.Location.Y + deltaY);
        }
    }

    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (DataContext is GroupViewModel vm)
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
