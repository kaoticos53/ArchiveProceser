using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FileFlow.App.Views;

public partial class ControlBarView : UserControl
{
    public ControlBarView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
