using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FileFlow.Plugin.Archives.UI.ViewModels;

namespace FileFlow.Plugin.Archives.UI.Views;

/// <summary>
/// El GESTOR DE CONTRASEÑAS del escritorio: la VISTA de <see cref="PasswordManagerViewModel"/>, el view model
/// portable del plugin.
///
/// <para><b>Qué es y qué no.</b> El texto de la lista, el recuento de claves, lo que se guarda en el parámetro
/// del nodo y la lectura/escritura del .txt viven en el view model, que es el mismo que pinta el host Uno sobre
/// su propio cuerpo. Esta ventana sólo lo enseña y le pasa lo que el usuario escribe: antes tenía esa lógica en
/// su code-behind, y por eso ningún otro host podía ofrecer el gestor sin reescribirla.</para>
///
/// <para><b>Lo que la vista sí decide</b>, porque es del host: dónde está el archivo (su selector de archivos)
/// y cuándo se cierra la ventana —aceptar y cancelar son de la ventana que los pinta—.</para>
/// </summary>
public partial class PasswordManagerWindow : Window
{
    private readonly PasswordManagerViewModel _vm;

    public PasswordManagerWindow() : this(null)
    {
    }

    /// <summary>
    /// La ventana del gestor. Sin view model se construye uno vacío: es el camino del diseñador de XAML y de las
    /// capturas visuales, no el del producto —el producto entra por la superficie que declara el nodo y le pasa
    /// su view model con la lista del nodo y la vuelta al parámetro—.
    /// </summary>
    public PasswordManagerWindow(PasswordManagerViewModel? viewModel = null)
    {
        _vm = viewModel ?? new PasswordManagerViewModel();

        InitializeComponent();
        DataContext = _vm;
    }

    /// <summary>El view model portable en uso (lo leen las capturas y las pruebas del plugin).</summary>
    public PasswordManagerViewModel ViewModel => _vm;

    /// <summary>
    /// La lista en la forma en que la guarda el nodo. La lee el camino del toolkit —el host que monta ESTA
    /// ventana desde la acción del nodo—, no la ventana por su cuenta: el valor sale del view model.
    /// </summary>
    public string PasswordsText => _vm.PasswordsText;

    private async void ImportFromTxt_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("PasswordManager_ImportTxt", "Importar (.txt)"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Archivos de texto (*.txt)") { Patterns = ["*.txt"] },
                new FilePickerFileType("Todos los archivos (*.*)") { Patterns = ["*.*"] }
            ]
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            await _vm.ImportFromAsync(path);
        }
    }

    private async void ExportToTxt_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("PasswordManager_ExportTxt", "Exportar (.txt)"),
            SuggestedFileName = "passwords.txt",
            DefaultExtension = "txt",
            FileTypeChoices =
            [
                new FilePickerFileType("Archivos de texto (*.txt)") { Patterns = ["*.txt"] }
            ]
        });

        if (file?.TryGetLocalPath() is { } path)
        {
            await _vm.ExportToAsync(path);
        }
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        _vm.Save();
        Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
