using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FileFlow.App.Views.Components;

public partial class EditorZoomBarView : UserControl
{
    public EditorZoomBarView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
