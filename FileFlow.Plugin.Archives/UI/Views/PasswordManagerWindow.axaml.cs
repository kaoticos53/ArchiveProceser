using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FileFlow.Sdk.Services;

namespace FileFlow.Plugin.Archives.UI.Views;

public partial class PasswordManagerWindow : Window
{
    private readonly IDialogService _dialogService;
    public string PasswordsText { get; private set; } = string.Empty;

    public PasswordManagerWindow() : this(string.Empty)
    {
    }

    public PasswordManagerWindow(string currentPasswords, IDialogService? dialogService = null)
    {
        _dialogService = dialogService ?? NullDialogService.Instance;
        InitializeComponent();
        if (!string.IsNullOrWhiteSpace(currentPasswords))
        {
            var lines = currentPasswords.Split([';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            TxtPasswordEditor.Text = string.Join(Environment.NewLine, lines);
        }
        UpdateCount();
    }

    private void TxtPasswordEditor_TextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateCount();
    }

    private void UpdateCount()
    {
        if (TxtPasswordEditor == null || TxtPasswordCount == null) return;
        var text = TxtPasswordEditor.Text ?? string.Empty;
        var lines = text.Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries);
        TxtPasswordCount.Text = $"{lines.Length} clave(s) cargada(s)";
    }

    private async void ImportFromTxt_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Importar lista de contraseñas",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Archivos de texto (*.txt)") { Patterns = ["*.txt"] },
                new FilePickerFileType("Todos los archivos (*.*)") { Patterns = ["*.*"] }
            ]
        });

        if (files.Count > 0)
        {
            try
            {
                await using var stream = await files[0].OpenReadAsync();
                using var reader = new StreamReader(stream);
                var content = await reader.ReadToEndAsync();

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
                string errorMsg = string.Format(FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("PasswordManager_MsgImportError", "Error al importar archivo: {0}"), ex.Message);
                string title = FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("Error", "Error");
                _dialogService.ShowError(errorMsg, title);
            }
        }
    }

    private async void ExportToTxt_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Exportar lista de contraseñas",
            SuggestedFileName = "passwords.txt",
            DefaultExtension = "txt",
            FileTypeChoices =
            [
                new FilePickerFileType("Archivos de texto (*.txt)") { Patterns = ["*.txt"] }
            ]
        });

        if (file != null)
        {
            try
            {
                await using var stream = await file.OpenWriteAsync();
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(TxtPasswordEditor.Text ?? string.Empty);

                string successMsg = string.Format(FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("PasswordManager_MsgExportSuccess", "Contraseñas exportadas con éxito a:\n{0}"), file.Path.LocalPath);
                string title = FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("Success", "Éxito");
                _dialogService.ShowInformation(successMsg, title);
            }
            catch (Exception ex)
            {
                string errorMsg = string.Format(FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("PasswordManager_MsgExportError", "Error al exportar archivo: {0}"), ex.Message);
                string title = FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("Error", "Error");
                _dialogService.ShowError(errorMsg, title);
            }
        }
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        var text = TxtPasswordEditor.Text ?? string.Empty;
        var lines = text.Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        PasswordsText = string.Join("; ", lines);
        Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
