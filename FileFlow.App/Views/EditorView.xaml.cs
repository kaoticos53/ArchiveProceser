using System.Windows;
using System.Windows.Controls;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Views;

public partial class EditorView : UserControl
{
    public EditorView()
    {
        InitializeComponent();
    }

    private void Editor_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("NodeTypeName") && DataContext is EditorViewModel vm)
        {
            string typeName = (string)e.Data.GetData("NodeTypeName");
            Point dropPoint = e.GetPosition(this);
            vm.AddNode(typeName, dropPoint);
        }
    }
}
