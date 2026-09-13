using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FileFlow.Sdk.Services;

namespace FileFlow.Plugin.Archives.UI.Views;

public partial class PasswordManagerWindow : Window
{
    private readonly IDialogService _dialogService;
    public string PasswordsText { get; private set; } = string.Empty;

    private TextBox? TxtPasswordEditor => this.FindControl<TextBox>("TxtPasswordEditor");
    private TextBlock? TxtPasswordCount => this.FindControl<TextBlock>("TxtPasswordCount");

    public PasswordManagerWindow(string currentPasswords, IDialogService? dialogService = null)
    {
        _dialogService = dialogService ?? NullDialogService.Instance;
        if (!string.IsNullOrWhiteSpace(currentPasswords))
        {
            var lines = currentPasswords.Split([';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (TxtPasswordEditor != null)
            {
                TxtPasswordEditor.Text = string.Join(Environment.NewLine, lines);
            }
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
        var lines = (TxtPasswordEditor.Text ?? string.Empty).Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        TxtPasswordCount.Text = $"{lines.Length} clave(s) cargada(s)";
    }

    private async void ImportFromTxt_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider != null)
            {
                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Importar lista de contraseñas",
                    AllowMultiple = false
                });
                if (files != null && files.Count > 0)
                {
                    var content = await File.ReadAllTextAsync(files[0].Path.LocalPath);
                    if (TxtPasswordEditor != null)
                    {
                        if (!string.IsNullOrWhiteSpace(TxtPasswordEditor.Text))
                        {
                            TxtPasswordEditor.Text += Environment.NewLine + content;
                        }
                        else
                        {
                            TxtPasswordEditor.Text = content;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            string errorMsg = string.Format(FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("PasswordManager_MsgImportError", "Error al importar archivo: {0}"), ex.Message);
            string title = FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("Error", "Error");
            _dialogService.ShowError(errorMsg, title);
        }
    }

    private async void ExportToTxt_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider != null)
            {
                var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Exportar lista de contraseñas",
                    DefaultExtension = "txt",
                    SuggestedFileName = "passwords.txt"
                });
                if (file != null)
                {
                    await File.WriteAllTextAsync(file.Path.LocalPath, TxtPasswordEditor?.Text ?? string.Empty);
                    string successMsg = string.Format(FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("PasswordManager_MsgExportSuccess", "Contraseñas exportadas con éxito a:\n{0}"), file.Path.LocalPath);
                    string title = FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("Success", "Éxito");
                    _dialogService.ShowInformation(successMsg, title);
                }
            }
        }
        catch (Exception ex)
        {
            string errorMsg = string.Format(FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("PasswordManager_MsgExportError", "Error al exportar archivo: {0}"), ex.Message);
            string title = FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("Error", "Error");
            _dialogService.ShowError(errorMsg, title);
        }
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        var lines = (TxtPasswordEditor?.Text ?? string.Empty).Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        PasswordsText = string.Join("; ", lines);
        Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
