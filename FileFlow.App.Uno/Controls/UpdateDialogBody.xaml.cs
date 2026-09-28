using System;
using FileFlow.App.ViewModels;
using FileFlow.Sdk.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileFlow.App.Uno.Controls;

/// <summary>
/// El AVISO DE ACTUALIZACIÓN del host Uno: la vista del <see cref="UpdateDialogViewModel"/> portable,
/// servida por <see cref="Platform.UnoWindowService"/> cuando el núcleo pide <c>DialogKeys.UpdateDialog</c>
/// —la misma clave que el escritorio usa para abrir su <c>UpdateDialogWindow</c>—.
///
/// <para><b>La información es del view model</b>: versiones, título del release, formato del paquete,
/// novedades y el progreso de la descarga. La vista sólo pinta y las tres órdenes (omitir esta versión,
/// recordármelo luego, instalar y reiniciar) son sus comandos canónicos; el cierre lo pide el propio view
/// model por su evento <c>RequestClose</c>, que el servicio de ventanas escucha para retirar el modal.</para>
/// </summary>
public sealed partial class UpdateDialogBody : UserControl
{
    private readonly UpdateDialogViewModel _vm;

    public UpdateDialogBody(UpdateDialogViewModel viewModel)
    {
        _vm = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        InitializeComponent();
        DataContext = _vm;
        RefreshLocalization();
    }

    /// <summary>El view model portable en uso (la sonda lee de aquí la versión anunciada).</summary>
    internal UpdateDialogViewModel Vm => _vm;

    /// <summary>La versión nueva anunciada (el dato que el usuario viene a ver).</summary>
    internal string NewVersionText => NewVersionValue.Text;

    /// <summary>El título del release anunciado.</summary>
    internal string AnnouncedReleaseTitle => ReleaseTitleText.Text;

    /// <summary>El botón «recordármelo luego»: su orden retira el aviso desde dentro (el view model lo pide).</summary>
    internal Button Remind => RemindButton;

    /// <summary>El botón «omitir esta versión» (la otra orden que cierra sin instalar).</summary>
    internal Button Skip => SkipButton;

    /// <summary>Los rótulos del host (se reescriben en caliente al cambiar de idioma).</summary>
    public void RefreshLocalization()
    {
        var loc = LocalizationManager.Instance;

        HeaderTitle.Text = loc.GetString("Update_HeaderTitle", "Nueva versión disponible");
        GitHubLabel.Text = loc.GetString("Update_BtnOpenGitHub", "Ver en GitHub");
        CurrentVersionLabel.Text = loc.GetString("Update_CurrentVersionLabel", "Versión actual");
        NewVersionLabel.Text = loc.GetString("Update_NewVersionLabel", "Versión nueva");
        PackagingLabel.Text = loc.GetString("Update_PackagingFormatLabel", "Formato");
        ChangelogLabel.Text = loc.GetString("Update_ChangelogTitle", "Novedades de esta versión");
        SkipLabel.Text = loc.GetString("Update_BtnSkipVersion", "Omitir esta versión");
        RemindLabel.Text = loc.GetString("Update_BtnRemindLater", "Recordármelo luego");
        InstallLabel.Text = loc.GetString("Update_BtnInstallAndRestart", "Instalar y reiniciar");
    }

    private void OnOpenGitHubClicked(object sender, RoutedEventArgs e) => _vm.OpenGitHubCommand.Execute(null);

    private void OnSkipClicked(object sender, RoutedEventArgs e) => _vm.SkipVersionCommand.Execute(null);

    private void OnRemindClicked(object sender, RoutedEventArgs e) => _vm.RemindLaterCommand.Execute(null);

    private void OnInstallClicked(object sender, RoutedEventArgs e) =>
        _vm.InstallAndRestartCommand.Execute(null);
}
