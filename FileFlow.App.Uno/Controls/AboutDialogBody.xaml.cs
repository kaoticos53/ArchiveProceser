using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using Microsoft.UI.Xaml.Controls;

namespace FileFlow.App.Uno.Controls;

/// <summary>
/// La ventana «Acerca de» del host Uno como superficie propia: la sirve
/// <see cref="Platform.UnoWindowService"/> cuando una orden del menú pide
/// <c>DialogKeys.About</c>, que es exactamente lo que hace la versión anterior al abrir su
/// <c>AboutDialogWindow</c>.
///
/// <para>Los textos son claves del diccionario del host (<c>Uno_About_*</c>), copiadas del de la versión anterior;
/// la versión sale de la misma fuente que el pie del cajón, para que las dos digan lo mismo.</para>
/// </summary>
public sealed partial class AboutDialogBody : UserControl
{
    public AboutDialogBody()
    {
        InitializeComponent();
        RefreshLocalization();
    }

    /// <summary>La línea de versión que enseña la superficie (la lee la sonda).</summary>
    internal string VersionLine => VersionText.Text;

    /// <summary>El idioma vigente en los textos de la ventana (se reescribe al cambiar de idioma).</summary>
    public void RefreshLocalization()
    {
        var loc = LocalizationManager.Instance;

        VersionText.Text = AppVersionInfo.DisplayVersion + " · net10.0 · Uno Platform (WinUI 3)";
        SubtitleText.Text = loc.GetString("Uno_About_Subtitle", "Automatización Inteligente de Archivos y Motor DAG");
        DescriptionText.Text = loc.GetString("Uno_About_Description",
            "Plataforma de ingeniería de flujos de trabajo basada en grafos dirigidos acíclicos (DAG) con "
            + "arquitectura modular de plugins, procesamiento asíncrono de alto rendimiento, pipelines no "
            + "destructivos y desacoplamiento reactivo por capas.");
    }
}
