using System;
using System.Globalization;
using System.Linq;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Sdk.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileFlow.App.Uno.Controls;

/// <summary>
/// El ESTUDIO DE TEMAS del host Uno: la vista del <see cref="ThemeCustomizerViewModel"/> portable, servida
/// por <see cref="Platform.UnoWindowService"/> cuando una orden pide <c>DialogKeys.ThemeCustomizer</c> —la
/// misma clave que el escritorio usa para abrir su <c>ThemeCustomizerWindow</c>—.
///
/// <para><b>El editor no está escrito a mano</b>: las secciones y sus filas vienen del catálogo del núcleo y
/// cada fila escribe sobre la propiedad real del tema. Aquí sólo vive el pegamento de la vista —qué control
/// edita cada tipo de fila y cómo se le devuelve el valor escrito al view model— y la tabla de lo que este
/// host NO dibuja, con su razón.</para>
/// </summary>
public sealed partial class ThemeCustomizerBody : UserControl
{
    private readonly ThemeCustomizerViewModel _vm;
    private bool _syncing;

    /// <summary>
    /// Las partes del estudio del escritorio que este host NO dibuja, con su razón. Es el otro lado del
    /// censo, igual que las tablas de entradas: lo que falta queda declarado —y la guardia lo compara con
    /// las órdenes del estudio— nunca fingido con un botón que no hace nada.
    /// </summary>
    internal static readonly (string Part, string Reason)[] DeclaredPendingParts =
    [
        // «Eliminar tema» vivió aquí hasta el hito 265, con su razón de entonces (preguntaba por el contrato
        // SÍNCRONO, que desde el hilo de UI devuelve «no»): su orden ya pregunta por el asíncrono, así que
        // está DIBUJADA y su fila se quitó —una parte dibujada y declarada pendiente sería una mentira—.
        ("ExportThemeAsyncCommand", "elige su fichero por el contrato SÍNCRONO (ServiceHolders.FileDialog), que en este host devuelve nulo"),
        ("ImportThemeAsyncCommand", "elige su fichero por el contrato SÍNCRONO (ServiceHolders.FileDialog), que en este host devuelve nulo"),
        ("LivePreviewResources", "la vista previa en vivo del escritorio: su diccionario de tokens no es observable para los recursos de WinUI sin una copia por superficie, así que aquí se aplica el tema y se ve en la aplicación"),
    ];

    public ThemeCustomizerBody(ThemeCustomizerViewModel viewModel)
    {
        _vm = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        InitializeComponent();
        DataContext = _vm;
        RefreshLocalization();
    }

    /// <summary>El estudio del núcleo con su catálogo y su editor ya generados.</summary>
    internal ThemeCustomizerViewModel Vm => _vm;

    /// <summary>Los temas del catálogo (lo que la lista está mostrando).</summary>
    internal int ThemeCount => _vm.AvailableThemes.Count;

    /// <summary>Las secciones del editor generadas por el catálogo del núcleo.</summary>
    internal int SectionCount => _vm.Sections.Count;

    /// <summary>Las filas de todas las secciones (el tamaño real del editor).</summary>
    internal int RowCount => _vm.Sections.Sum(section => section.Rows.Count);

    /// <summary>El nombre del tema en edición, tal como lo enseña la cabecera del editor.</summary>
    internal string EditingName => _vm.CurrentThemeName;

    /// <summary>El estado del estudio que enseña el pie (lo escribe el propio view model).</summary>
    internal string StatusLine => StatusText.Text;

    /// <summary>Escribe un ajuste de color por la MISMA vía que el usuario (el campo de la fila).</summary>
    internal bool WriteColorSetting(string property, string hex)
    {
        ThemeColorRowViewModel? row = FindRow<ThemeColorRowViewModel>(property);
        if (row is null)
        {
            return false;
        }

        row.SelectedColorHex = hex;
        return string.Equals(row.SelectedColorHex, hex, StringComparison.Ordinal);
    }

    /// <summary>El valor actual de un ajuste de color (lo que el tema tiene de verdad).</summary>
    internal string? ReadColorSetting(string property) => FindRow<ThemeColorRowViewModel>(property)?.SelectedColorHex;

    private ThemeSettingRowViewModel? FindRow(string property) =>
        _vm.Sections.SelectMany(section => section.Rows)
            .FirstOrDefault(row => string.Equals(row.Property, property, StringComparison.Ordinal));

    private T? FindRow<T>(string property) where T : ThemeSettingRowViewModel =>
        FindRow(property) as T;

    /// <summary>Los rótulos del host (se reescriben en caliente al cambiar de idioma).</summary>
    public void RefreshLocalization()
    {
        var loc = LocalizationManager.Instance;

        HeaderTitle.Text = loc.GetString("ThemeCustomizer_HeaderTitle", "Estudio de Personalización de Temas");
        HeaderSubtitle.Text = loc.GetString("ThemeCustomizer_HeaderSubtitle",
            "Personaliza paletas, tipografías, radios y efectos del tema.");
        CatalogLabel.Text = loc.GetString("ThemeCustomizer_AvailableThemes", "TEMAS DISPONIBLES");
        NameLabel.Text = loc.GetString("ThemeCustomizer_ThemeName", "Nombre del tema:");
        NewLabel.Text = loc.GetString("ThemeCustomizer_NewBtn", "Nuevo");
        DuplicateLabel.Text = loc.GetString("ThemeCustomizer_DuplicateBtn", "Duplicar");
        DeleteLabel.Text = loc.GetString("ThemeCustomizer_DeleteBtn", "Eliminar tema");
        ApplyLabel.Text = loc.GetString("ThemeCustomizer_TestInApp", "Probar en la app");
        SaveLabel.Text = loc.GetString("ThemeCustomizer_SaveAndApply", "Guardar y aplicar");
        CloseLabel.Text = loc.GetString("Common_Close", "Cerrar");

        ToolTipService.SetToolTip(ApplyButton, loc.GetString("ThemeCustomizer_TestInAppToolTip",
            "Aplicar este tema a la aplicación ahora mismo"));
    }

    private void OnNewClicked(object sender, RoutedEventArgs e) => _vm.NewCustomThemeCommand.Execute(null);

    private void OnDuplicateClicked(object sender, RoutedEventArgs e) => _vm.DuplicateThemeCommand.Execute(null);

    /// <summary>
    /// «Eliminar tema»: la orden DESTRUCTIVA del estudio, por su comando canónico.
    ///
    /// <para>La pregunta la hace el view model portable por el contrato <b>asíncrono</b> de diálogos, y sólo
    /// borra si el usuario dijo que sí: la vista no confirma ni borra por su cuenta. Es la razón por la que
    /// este botón se puede dibujar —el estudio lo declaraba pendiente cuando su confirmación era síncrona—.</para>
    /// </summary>
    private void OnDeleteClicked(object sender, RoutedEventArgs e) => _vm.DeleteThemeCommand.Execute(null);

    /// <summary>«Probar en la app»: aplica el tema en edición sin guardarlo (la mitad viva de Guardar y aplicar).</summary>
    private void OnApplyClicked(object sender, RoutedEventArgs e) => _vm.ApplyToApplicationCommand.Execute(null);

    /// <summary>
    /// «Guardar y aplicar»: la orden compuesta del propio view model (guarda y luego aplica) y, como en el
    /// code-behind del escritorio, el cierre de la superficie es del host.
    /// </summary>
    private void OnSaveAndApplyClicked(object sender, RoutedEventArgs e)
    {
        _vm.SaveAndApplyCommand.Execute(null);
        Platform.UnoWindowService.CloseActiveWindow();
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e) =>
        Platform.UnoWindowService.CloseActiveWindow();

    /// <summary>
    /// El campo de una fila de color escribe el valor en el tema EN CUANTO se teclea: es el mismo camino que
    /// el selector de color del escritorio (que escribe <c>SelectedColorHex</c>), sin esperar a que el campo
    /// pierda el foco.
    /// </summary>
    private void OnColorTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncing || sender is not TextBox box || box.DataContext is not ThemeColorRowViewModel row)
        {
            return;
        }

        _syncing = true;
        try
        {
            row.SelectedColorHex = box.Text;
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>
    /// El campo numérico escribe el valor en el tema; el rango y el paso los aplica el propio view model de
    /// la fila (que recorta al mínimo y al máximo del catálogo), no la vista.
    /// </summary>
    private void OnNumberTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncing || sender is not TextBox box || box.DataContext is not ThemeNumberRowViewModel row)
        {
            return;
        }

        if (!double.TryParse(box.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double parsed))
        {
            return;
        }

        _syncing = true;
        try
        {
            row.Value = (decimal)parsed;
        }
        finally
        {
            _syncing = false;
        }
    }
}
