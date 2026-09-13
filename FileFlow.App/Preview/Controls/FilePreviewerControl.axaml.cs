using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FileFlow.App.Preview.Controls;

public partial class FilePreviewerControl : UserControl
{
    public FilePreviewerControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
