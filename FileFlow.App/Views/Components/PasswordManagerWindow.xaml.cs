using System.IO;
using System.Windows;

namespace FileFlow.App.Views.Components;

public partial class PasswordManagerWindow : Window
{
    public string PasswordsText { get; private set; } = string.Empty;

    public PasswordManagerWindow(string currentPasswords)
    {
        InitializeComponent();
        Services.WindowThemeHelper.ApplyThemeToWindow(this);
        if (!string.IsNullOrWhiteSpace(currentPasswords))
        {
            var lines = currentPasswords.Split([';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            TxtPasswordEditor.Text = string.Join(Environment.NewLine, lines);
        }
        UpdateCount();
    }

    private void TxtPasswordEditor_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdateCount();
    }

    private void UpdateCount()
    {
        var lines = TxtPasswordEditor.Text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        TxtPasswordCount.Text = $"{lines.Length} clave(s) cargada(s)";
    }

    private void ImportFromTxt_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Importar lista de contraseñas",
            Filter = "Archivos de texto (*.txt)|*.txt|Todos los archivos (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var content = File.ReadAllText(dialog.FileName);
                if (!string.IsNullOrWhiteSpace(TxtPasswordEditor.Text))
                {
                    TxtPasswordEditor.Text += Environment.NewLine + content;
                }
                else
                {
                    TxtPasswordEditor.Text = content;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al importar archivo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ExportToTxt_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Exportar lista de contraseñas",
            Filter = "Archivos de texto (*.txt)|*.txt",
            DefaultExt = ".txt",
            FileName = "passwords.txt"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                File.WriteAllText(dialog.FileName, TxtPasswordEditor.Text);
                MessageBox.Show($"Contraseñas exportadas con éxito a:\n{dialog.FileName}", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar archivo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var lines = TxtPasswordEditor.Text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        PasswordsText = string.Join("; ", lines);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
