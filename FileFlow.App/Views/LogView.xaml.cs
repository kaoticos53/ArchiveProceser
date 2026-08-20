using System.Windows.Controls;

namespace FileFlow.App.Views;

public partial class LogView : UserControl
{
    public LogView()
    {
        InitializeComponent();
    }

    private void LogConsoleTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox && !textBox.IsFocused)
        {
            textBox.ScrollToEnd();
        }
    }
}
