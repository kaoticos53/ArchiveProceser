using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FileFlow.App.Views;

public partial class NodeInspectorPanelView : UserControl
{
    public NodeInspectorPanelView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
