using System;
using FileFlow.App.Services;
using Microsoft.Extensions.DependencyInjection;
using FileFlow.Plugin.Archives.UI.ViewModels;
using FileFlow.Sdk.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileFlow.App.Uno.Controls;

/// <summary>
/// El GESTOR DE CONTRASEÑAS del host Uno: la vista del <see cref="PasswordManagerViewModel"/> portable del
/// plugin de archivos.
///
/// <para><b>Qué es y qué no.</b> El view model —con la lista de claves, su recuento, lo que se guarda en el
/// parámetro del nodo y la lectura/escritura del .txt— vive en el plugin y lo comparten los dos hosts; lo que
/// cambia es quién lo pinta. El host original montaba la ventana del plugin (<c>PasswordManagerWindow</c>);
/// este host pinta esta vista sobre el MISMO view model, que escribe la MISMA lista en el MISMO parámetro del
/// nodo. No hay una segunda versión del gestor: hay dos vistas de él.</para>
///
/// <para><b>Lo que la vista sí decide</b>, porque es del host: dónde está el archivo —su selector— y el cierre
/// del modal, que lo posee <see cref="Platform.UnoWindowService"/>. Ni un cuadro de esta vista escribe en el
/// nodo: eso lo hace el view model.</para>
/// </summary>
public sealed partial class PasswordManagerBody : UserControl
{
    private readonly PasswordManagerViewModel _vm;
    private readonly IFileDialogService _fileDialog;

    public PasswordManagerBody(PasswordManagerViewModel viewModel)
    {
        _vm = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _fileDialog = App.Services.GetRequiredService<IFileDialogService>();

        InitializeComponent();
        DataContext = _vm;

        RefreshLocalization();
    }

    /// <summary>El view model portable en uso (la sonda lee de aquí la lista y su recuento).</summary>
    internal PasswordManagerViewModel Vm => _vm;

    /// <summary>El editor de la lista (donde la sonda escribe como lo haría el usuario).</summary>
    internal TextBox Editor => EditorBox;

    /// <summary>
    /// Importar un .txt: el archivo lo elige el HOST y su contenido lo añade el view model, que es quien sabe
    /// qué es una clave.
    /// </summary>
    private async void OnImportClicked(object sender, RoutedEventArgs e)
    {
        string title = LocalizationManager.Instance.GetString("PasswordManager_ImportTxt", "Importar (.txt)");
        string? path = await _fileDialog.ShowOpenFileDialogAsync(title, "Archivos de texto (*.txt)|*.txt", ".txt");
        if (!string.IsNullOrWhiteSpace(path))
        {
            await _vm.ImportFromAsync(path);
        }
    }

    /// <summary>Exportar la lista al .txt que elija el usuario, por el mismo selector del host.</summary>
    private async void OnExportClicked(object sender, RoutedEventArgs e)
    {
        string title = LocalizationManager.Instance.GetString("PasswordManager_ExportTxt", "Exportar (.txt)");
        string? path = await _fileDialog.ShowSaveFileDialogAsync(title, "Archivos de texto (*.txt)|*.txt", ".txt", "passwords.txt");
        if (!string.IsNullOrWhiteSpace(path))
        {
            await _vm.ExportToAsync(path);
        }
    }

    /// <summary>Los rótulos del host (se reescriben en caliente al cambiar de idioma).</summary>
    public void RefreshLocalization()
    {
        var loc = LocalizationManager.Instance;

        HeaderTitle.Text = loc.GetString("PasswordManager_HeaderTitle", "🔑 Gestor de Contraseñas para Descompresión");
        HeaderDesc.Text = loc.GetString("PasswordManager_HeaderSubtitle", "");
        ImportLabel.Text = loc.GetString("PasswordManager_ImportTxt", "📥 Importar (.txt)");
        ExportLabel.Text = loc.GetString("PasswordManager_ExportTxt", "📤 Exportar (.txt)");
    }
}
